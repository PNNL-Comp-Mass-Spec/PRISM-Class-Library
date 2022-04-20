using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace PRISM.Logging
{
    /// <summary>
    /// Logs messages to a SQL Server database by calling a stored procedure
    /// Connect using System.Data.SqlClient
    /// </summary>
    /// <remarks>Can only log to a single database at a time</remarks>
    public sealed class SQLServerDatabaseLogger : DatabaseLogger
    {
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
        public SQLServerDatabaseLogger(LogLevels logLevel = LogLevels.INFO) : this(string.Empty, string.Empty, logLevel)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="moduleName">
        /// Program name to pass to the postedByParamName field when contacting the database
        /// (will be auto-defined later if blank)
        /// </param>
        /// <param name="connectionString">SQL Server style connection string</param>
        /// <param name="logLevel">Log threshold level</param>
        /// <param name="storedProcedure">Stored procedure to call</param>
        /// <param name="logTypeParamName">LogType parameter name (string representation of logLevel</param>
        /// <param name="messageParamName">Message parameter name</param>
        /// <param name="postedByParamName">Log source parameter name</param>
        /// <param name="logTypeParamSize">LogType parameter size</param>
        /// <param name="messageParamSize">Message parameter size</param>
        /// <param name="postedByParamSize">Log source parameter size</param>
        public SQLServerDatabaseLogger(
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
        /// <param name="connectionString">SQL Server style connection string</param>
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


            LogTypeParamSize = logTypeParamSize;
            MessageParamSize = messageParamSize;
            PostedByParamSize = postedByParamSize;
        }

        /// <summary>
        /// Callback invoked by the mQueueLogger timer
        /// </summary>
        /// <param name="state"></param>
        private static void LogMessagesCallback(object state)
        {
            ShowTrace("SQLServerDatabaseLogger.mQueueLogger callback raised");
            StartLogQueuedMessages();
        }

        private static void LogQueuedMessages()
        {
            try
            {
                if (mMessageQueue.IsEmpty || !HasConnectionInfo)
                    return;

                ShowTrace(string.Format("SQLServerDatabaseLogger connecting to {0}", ConnectionString));
                var messagesWritten = 0;

                using (var sqlConnection = new SqlConnection(ConnectionString))
                {
                    sqlConnection.Open();

                    var spCmd = new SqlCommand(LoggingProcedure.ProcedureName)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;

                    var logTypeParam = spCmd.Parameters.Add(new SqlParameter(LoggingProcedure.LogTypeParamName, SqlDbType.VarChar, LoggingProcedure.LogTypeParamSize));
                    var logMessageParam = spCmd.Parameters.Add(new SqlParameter(LoggingProcedure.MessageParamName, SqlDbType.VarChar, LoggingProcedure.MessageParamSize));
                    spCmd.Parameters.Add(new SqlParameter(LoggingProcedure.LogSourceParamName, SqlDbType.VarChar, LoggingProcedure.LogSourceParamSize)).Value = ModuleName;

                    spCmd.Connection = sqlConnection;
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

                        if (logMessageParam == null)
                            continue;

                        logTypeParam.Value = LogLevelToString(logMessage.LogLevel);
                        logMessageParam.Value = logMessage.Message;

                        var retryCount = 2;

                        while (retryCount > 0)
                        {
                            var returnValue = 0;

                            try
                            {
                                spCmd.ExecuteNonQuery();
                                returnValue = Convert.ToInt32(spCmd.Parameters["@Return"].Value);

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

                ShowTrace(string.Format("SQLServerDatabaseLogger connection closed; wrote {0} messages", messagesWritten));
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowErrorCustom(
                    "Error writing queued log messages to the database using SQL Server: " + ex.Message,
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
        ~SQLServerDatabaseLogger()
        {
            ShowTrace("Disposing SQLServerDatabaseLogger");
            StartLogQueuedMessages();
        }
    }
}
