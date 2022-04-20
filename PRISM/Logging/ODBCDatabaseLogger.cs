using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Threading;

namespace PRISM.Logging
{
    /// <summary>
    /// Logs messages to a database by calling a stored procedure
    /// Connect using an ODBC driver
    /// </summary>
    /// <remarks>Connect using an ODBC driver</remarks>
    // ReSharper disable once UnusedMember.Global
    public sealed class ODBCDatabaseLogger : DatabaseLogger
    {
        // Ignore Spelling: Pwd, uid

        private static readonly ConcurrentQueue<LogMessage> mMessageQueue = new();

        private static readonly object mMessageQueueLock = new();

        // ReSharper disable once UnusedMember.Local
        private static readonly Timer mQueueLogger = new(LogMessagesCallback, null, 500, LOG_INTERVAL_MILLISECONDS);

        /// <summary>
        /// Tracks the number of successive dequeue failures
        /// </summary>
        private static int mFailedDequeueEvents;

        /// <summary>
        /// Module name
        /// </summary>
        private static string mModuleName;

        /// <summary>
        /// ODBC style connection string
        /// </summary>
        public static string ConnectionString { get; private set; }

        /// <summary>
        /// Program name to pass to the PostedBy field when contacting the database
        /// </summary>
        /// <remarks>Will be auto-defined in LogQueuedMessages if blank</remarks>
        public static string ModuleName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(mModuleName))
                {
                    mModuleName = GetDefaultModuleName();
                }
                return mModuleName;
            }
            set => mModuleName = value;
        }

        /// <summary>
        /// Constructor when the connection info is unknown
        /// </summary>
        /// <remarks>No database logging will occur until ChangeConnectionInfo is called (to define the connection string)</remarks>
        /// <param name="logLevel">Log threshold level</param>
        public ODBCDatabaseLogger(LogLevels logLevel = LogLevels.INFO) : this(string.Empty, string.Empty, logLevel)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="moduleName">
        /// Program name to pass to the postedByParamName field when contacting the database
        /// (will be auto-defined later if blank)
        /// </param>
        /// <param name="connectionString">ODBC-style connection string</param>
        /// <param name="logLevel">Log threshold level</param>
        /// <param name="storedProcedure">Stored procedure to call</param>
        /// <param name="logTypeParamName">LogType parameter name (string representation of logLevel</param>
        /// <param name="messageParamName">Message parameter name</param>
        /// <param name="postedByParamName">Log source parameter name</param>
        /// <param name="logTypeParamSize">LogType parameter size</param>
        /// <param name="messageParamSize">Message parameter size</param>
        /// <param name="postedByParamSize">Log source parameter size</param>
        public ODBCDatabaseLogger(
            string moduleName,
            string connectionString,
            LogLevels logLevel = LogLevels.INFO,
            string storedProcedure = "post_log_entry",
            string logTypeParamName = "type",
            string messageParamName = "message",
            string postedByParamName = "postedBy",
            int logTypeParamSize = 128,
            int messageParamSize = 4096,
            int postedByParamSize = 128)
        {
            ChangeConnectionInfo(
                moduleName, connectionString, storedProcedure,
                logTypeParamName, messageParamName, postedByParamName,
                logTypeParamSize, messageParamSize, postedByParamSize);

            LogLevel = logLevel;
        }

        /// <summary>
        /// Update the database connection info
        /// </summary>
        /// <param name="moduleName">
        /// Program name to pass to the postedByParamName field when contacting the database
        /// (will be auto-defined later if blank)
        /// </param>
        /// <param name="connectionString">ODBC-style connection string</param>
        /// <param name="storedProcedure">Stored procedure to call</param>
        /// <param name="logTypeParamName">LogType parameter name</param>
        /// <param name="messageParamName">Message parameter name</param>
        /// <param name="postedByParamName">Log source parameter name</param>
        /// <param name="logTypeParamSize">LogType parameter size</param>
        /// <param name="messageParamSize">Message parameter size</param>
        /// <param name="postedByParamSize">Log source parameter size</param>
        public override void ChangeConnectionInfo(
            string moduleName,
            string connectionString,
            string storedProcedure,
            string logTypeParamName,
            string messageParamName,
            string postedByParamName,
            int logTypeParamSize = 128,
            int messageParamSize = 4096,
            int postedByParamSize = 128)
        {
            ModuleName = moduleName;

            ConnectionString = connectionString;

            LoggingProcedure.UpdateProcedureInfo(
                storedProcedure,
                logTypeParamName, messageParamName, postedByParamName,
                logTypeParamSize, messageParamSize, postedByParamSize);
        }

        /// <summary>
        /// Convert a .NET framework SQL Server connection string to an ODBC-style connection string
        /// </summary>
        /// <param name="sqlServerConnectionString">SQL Server connection string</param>
        /// <param name="odbcDriverName">
        /// Typical values are:
        ///   "SQL Server Native Client 11.0" for SQL Server 2012
        ///   "SQL Server Native Client 10.0" for SQL Server 2008
        ///   "SQL Native Client"             for SQL Server 2005
        ///   "SQL Server"                    for SQL Server 2000
        /// </param>
        // ReSharper disable once UnusedMember.Global
        public static string ConvertSqlServerConnectionStringToODBC(string sqlServerConnectionString, string odbcDriverName = "SQL Server Native Client 11.0")
        {
            // Example connection strings:
            // https://www.connectionstrings.com/sql-server/

            // Integrated authentication:
            // Convert from one of these forms:
            //   Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;
            //   Data Source=myServerAddress;Initial Catalog=myDataBase;Integrated Security=SSPI
            // To ODBC style:
            //   Driver={SQL Server Native Client 11.0};Server=myServerAddress;Database=myDataBase;Trusted_Connection=yes;

            // Standard security:
            // Convert from one of these forms:
            //   Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;
            //   Data Source=myServerAddress;Initial Catalog=myDataBase;User ID=myDomain\myUsername;Password=myPassword
            // To ODBC style:
            //   Driver={SQL Server Native Client 11.0};Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;

            var settingsList = sqlServerConnectionString.Split(';');
            var keyParts = new[] { '=' };

            var sqlServerSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var setting in settingsList)
            {
                var settingParts = setting.Split(keyParts, 2);
                if (settingParts.Length < 2)
                    continue;

                var settingKey = settingParts[0].Trim();
                var settingValue = settingParts[1].Trim();

                if (string.IsNullOrWhiteSpace(settingKey))
                    continue;

                if (sqlServerSettings.ContainsKey(settingKey))
                    continue;

                sqlServerSettings.Add(settingKey, settingValue);
            }

            var userPassword = string.Empty;

            var namedUser = sqlServerSettings.TryGetValue("User Id", out var userId) &&
                            sqlServerSettings.TryGetValue("Password", out userPassword);

            var odbcConnectionParts = new List<string> {
                "Driver={" + odbcDriverName + "}"
            };

            if (sqlServerSettings.TryGetValue("Server", out var serverName))
            {
                if (sqlServerSettings.TryGetValue("Database", out var databaseName))
                {
                    odbcConnectionParts.Add("Server=" + serverName);
                    odbcConnectionParts.Add("Database=" + databaseName);
                }
                else
                {
                    ConsoleMsgUtils.ShowError(
                        "SQL Server connection string had 'Server' but did not have 'Database': " + sqlServerConnectionString);
                    return string.Empty;
                }
            }

            if (sqlServerSettings.TryGetValue("Data Source", out var dataSource))
            {
                if (sqlServerSettings.TryGetValue("Initial Catalog", out var databaseName))
                {
                    odbcConnectionParts.Add("Server=" + dataSource);
                    odbcConnectionParts.Add("Database=" + databaseName);
                }
                else
                {
                    ConsoleMsgUtils.ShowError(
                        "SQL Server connection string had 'Data Source' but did not have 'Initial catalog': " + sqlServerConnectionString);
                    return string.Empty;
                }
            }

            if (odbcConnectionParts.Count <= 1)
            {
                ConsoleMsgUtils.ShowError(
                    "SQL Server connection string did not have 'Server' or 'Data Source': " + sqlServerConnectionString);
                return string.Empty;
            }

            if (namedUser)
            {
                odbcConnectionParts.Add(string.Format("Uid={0};Pwd={1}", userId, userPassword));
            }
            else
            {
                odbcConnectionParts.Add("Trusted_Connection=yes");
            }

            var odbcConnectionString = string.Join(";", odbcConnectionParts) + ";";

            return odbcConnectionString;
        }

        /// <summary>
        /// Callback invoked by the mQueueLogger timer
        /// </summary>
        /// <param name="state"></param>
        private static void LogMessagesCallback(object state)
        {
            ShowTrace("ODBCDatabaseLogger.mQueueLogger callback raised");
            StartLogQueuedMessages();
        }

        private static void LogQueuedMessages()
        {
            try
            {
                if (mMessageQueue.IsEmpty)
                    return;

                ShowTrace(string.Format("ODBCDatabaseLogger connecting to {0}", ConnectionString));
                var messagesWritten = 0;

                using (var odbcConnection = new OdbcConnection(ConnectionString))
                {
                    odbcConnection.Open();

                    // Set up the command object prior to SP execution
                    // The syntax for calling procedure post_log_entry is
                    // {call post_log_entry (?,?,?)}

                    var spCmdText = "{call " + LoggingProcedure.ProcedureName + " (?,?,?)}";

                    var spCmd = new OdbcCommand(spCmdText)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    // Not including this parameter because it doesn't get populated when we use ExecuteNonQuery
                    // spCmd.Parameters.Add(new OdbcParameter("@Return", OdbcType.Int)).Direction = ParameterDirection.ReturnValue;

                    var logTypeParam = spCmd.Parameters.Add(new OdbcParameter("@" + LoggingProcedure.LogTypeParamName, OdbcType.VarChar, LoggingProcedure.LogTypeParamSize));
                    var logMessageParam = spCmd.Parameters.Add(new OdbcParameter("@" + LoggingProcedure.MessageParamName, OdbcType.VarChar, LoggingProcedure.MessageParamSize));
                    spCmd.Parameters.Add(new OdbcParameter("@" + LoggingProcedure.LogSourceParamName, OdbcType.VarChar, LoggingProcedure.LogSourceParamSize)).Value = ModuleName;

                    spCmd.Connection = odbcConnection;
                    spCmd.CommandTimeout = TIMEOUT_SECONDS;

                    while (!mMessageQueue.IsEmpty)
                    {
                        if (!mMessageQueue.TryDequeue(out var logMessage))
                        {
                            mFailedDequeueEvents++;
                            LogDequeueError(mFailedDequeueEvents, mMessageQueue.Count);
                            return;
                        }

                        mFailedDequeueEvents = 0;

                        if (logMessage.LogLevel is LogLevels.ERROR or LogLevels.FATAL)
                        {
                            MostRecentErrorMessage = logMessage.Message;
                        }

                        if (string.IsNullOrWhiteSpace(ConnectionString) || string.IsNullOrWhiteSpace(StoredProcedureName) || logMessageParam == null)
                            continue;

                        logTypeParam.Value = LogLevelToString(logMessage.LogLevel);
                        logMessageParam.Value = logMessage.Message;

                        var retryCount = 2;

                        while (retryCount > 0)
                        {
                            const int returnValue = 0;

                            try
                            {
                                spCmd.ExecuteNonQuery();
                                // returnValue = Convert.ToInt32(spCmd.Parameters["@Return"].Value);

                                messagesWritten++;
                                break;
                            }
                            catch (Exception ex)
                            {
                                --retryCount;
                                var errorMessage = "Exception calling stored procedure " +
                                                   spCmd.CommandText + ": " + ex.Message +
                                                   "; resultCode = " + returnValue + "; Retry count = " + retryCount + "; " +
                                                   StackTraceFormatter.GetExceptionStackTrace(ex);

                                if (retryCount == 0)
                                    FileLogger.WriteLog(LogLevels.ERROR, errorMessage);
                                else
                                    FileLogger.WriteLog(LogLevels.WARN, errorMessage);

                                if (!ex.Message.StartsWith("Could not find stored procedure " + spCmd.CommandText))
                                {
                                    // Try again
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                ShowTrace(string.Format("ODBCDatabaseLogger connection closed; wrote {0} messages", messagesWritten));
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowErrorCustom(
                    "Error writing queued log messages to the database using ODBC: " + ex.Message,
                    ex, false, false);
            }
        }

        /// <summary>
        /// Disable database logging
        /// </summary>
        public override void RemoveConnectionInfo()
        {
            ChangeConnectionInfo(ModuleName, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }

        /// <summary>
        /// Check for queued messages
        /// If found, try to log them, wrapping then attempt with Monitor.TryEnter and Monitor.Exit
        /// </summary>
        private static void StartLogQueuedMessages()
        {
            if (mMessageQueue.IsEmpty)
                return;

            lock (mMessageQueueLock)
            {
                LogQueuedMessages();
            }
        }

        /// <summary>
        /// Log a message (regardless of this.LogLevel)
        /// </summary>
        /// <param name="logMessage"></param>
        public override void WriteLog(LogMessage logMessage)
        {
            mMessageQueue.Enqueue(logMessage);

            if (EchoMessagesToFileLogger)
                FileLogger.WriteLog(logMessage);
        }

        /// <summary>
        /// Class is disposing; write out any queued messages
        /// </summary>
        ~ODBCDatabaseLogger()
        {
            ShowTrace("Disposing ODBCDatabaseLogger");
            StartLogQueuedMessages();
        }
    }
}
