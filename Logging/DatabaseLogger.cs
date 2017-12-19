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
    /// </summary>
    public class DatabaseLogger : BaseLogger
    {
        #region "Constants"

        /// <summary>
        /// Interval, in milliseconds, between flushing log messages to the database
        /// </summary>
        private const int LOG_INTERVAL_MILLISECONDS = 1000;

        private const int TIMEOUT_SECONDS = 5;

        #endregion

        #region "Static variables"

        private static readonly ConcurrentQueue<LogMessage> mMessageQueue = new ConcurrentQueue<LogMessage>();

        private static bool mQueueLoggerInitialized;

        private static readonly List<string> mMessageQueueEntryFlag = new List<string>();

        private static readonly Timer mQueueLogger = new Timer(LogQueuedMessages, null, 0, 0);

        /// <summary>
        /// Tracks the number of successive dequeue failures
        /// </summary>
        private static int mFailedDequeueEvents;

        #endregion

        #region "Member variables"

        private LogLevels mLogLevel;

        #endregion

        #region "Properties"

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.DEBUG or higher)
        /// </summary>
        public bool IsDebugEnabled { get; private set; }

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.ERROR or higher)
        /// </summary>
        public bool IsErrorEnabled { get; private set; }

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.FATAL or higher)
        /// </summary>
        public bool IsFatalEnabled { get; private set; }

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.INFO or higher)
        /// </summary>
        public bool IsInfoEnabled { get; private set; }

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.WARN or higher)
        /// </summary>
        public bool IsWarnEnabled { get; private set; }

        /// <summary>
        /// Get or set the current log level
        /// </summary>
        /// <remarks>
        /// If the LogLevel is DEBUG, all messages are logged
        /// If the LogLevel is INFO, all messages except DEBUG messages are logged
        /// If the LogLevel is ERROR, only FATAL and ERROR messages are logged
        /// </remarks>
        public LogLevels LogLevel
        {
            get => mLogLevel;
            set => SetLogLevel(value);
        }

        /// <summary>
        /// ODBC style connection string
        /// </summary>
        public static string ConnectionString { get; private set; }

        /// <summary>
        /// Program name to pass to the PostedBy field when contacting the database
        /// </summary>
        public static string ModuleName { get; set; } = "DatabaseLogger";

        /// <summary>
        /// Stored procedure where log messages will be posted
        /// </summary>
        public static string StoredProcedureName { get; private set; }

        private static OdbcParameter LogTypeParam { get; set; }

        private static OdbcParameter MessageParam { get; set; }

        private static OdbcParameter PostedByParam { get; set; }

        /// <summary>
        /// When true, also send any messages to the file logger
        /// </summary>
        public static bool EchoMessagesToFileLogger { get; set; } = true;

        #endregion

        /// <summary>
        /// Constructor when the connection info is unknown
        /// </summary>
        /// <param name="logLevel"></param>
        /// <remarks>No database logging will occur until ChangeConnectionInfo is called</remarks>
        public DatabaseLogger(LogLevels logLevel = LogLevels.INFO) : this("", "", "", "", "", "")
        {
            LogLevel = logLevel;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="moduleName">Program name to pass to the postedByParamName field when contacting the database</param>
        /// <param name="connectionString">ODBC-style connection string</param>
        /// <param name="storedProcedure">Stored procedure to call</param>
        /// <param name="logTypeParamName">LogType parameter name (string representation of logLevel</param>
        /// <param name="messageParamName">Message parameter name</param>
        /// <param name="postedByParamName">Log source parameter name</param>
        /// <param name="logTypeParamSize">LogType parameter size</param>
        /// <param name="messageParamSize">Message parameter size</param>
        /// <param name="postedByParamSize">Log source parameter size</param>
        /// <param name="logLevel">Log level</param>
        public DatabaseLogger(
            string moduleName,
            string connectionString,
            string storedProcedure,
            string logTypeParamName,
            string messageParamName,
            string postedByParamName,
            int logTypeParamSize = 128,
            int messageParamSize = 4000,
            int postedByParamSize = 128,
            LogLevels logLevel = LogLevels.INFO)
        {
            ChangeConnectionInfo(
                moduleName, connectionString, storedProcedure,
                logTypeParamName, messageParamName, postedByParamName,
                logTypeParamSize, messageParamSize, postedByParamSize);

            LogLevel = logLevel;

            if (!mQueueLoggerInitialized)
            {
                mQueueLoggerInitialized = true;
                mQueueLogger.Change(100, LOG_INTERVAL_MILLISECONDS);
            }
        }


        /// <summary>
        /// Update the database connection info
        /// </summary>
        /// <param name="moduleName">Program name to be sent to the PostedBy field when contacting the database</param>
        /// <param name="connectionString">ODBC-style connection string</param>
        /// <param name="storedProcedure">Stored procedure to call</param>
        /// <param name="logTypeParamName">LogType parameter name</param>
        /// <param name="messageParamName">Message parameter name</param>
        /// <param name="postedByParamName">Log source parameter name</param>
        /// <param name="logTypeParamSize">LogType parameter size</param>
        /// <param name="messageParamSize">Message parameter size</param>
        /// <param name="postedByParamSize">Log source parameter size</param>
        /// <remarks>Will append today's date to the base name</remarks>
        public void ChangeConnectionInfo(
            string moduleName,
            string connectionString,
            string storedProcedure,
            string logTypeParamName,
            string messageParamName,
            string postedByParamName,
            int logTypeParamSize = 128,
            int messageParamSize = 4000,
            int postedByParamSize = 128)
        {
            ModuleName = moduleName;

            ConnectionString = connectionString;
            StoredProcedureName = storedProcedure;
            LogTypeParam = new OdbcParameter(logTypeParamName, OdbcType.VarChar, logTypeParamSize);
            MessageParam = new OdbcParameter(messageParamName, OdbcType.VarChar, messageParamSize);
            PostedByParam = new OdbcParameter(postedByParamName, OdbcType.VarChar, postedByParamSize);
        }

        private static void LogQueuedMessages(object state)
        {
            if (mMessageQueue.IsEmpty)
                return;

            if (Monitor.TryEnter(mMessageQueueEntryFlag))
            {
                try
                {
                    LogQueuedMessages();
                }
                finally
                {
                    Monitor.Exit(mMessageQueueEntryFlag);
                }
            }

        }

        private static void LogQueuedMessages()
        {
            try
            {
                // Set up the command object prior to SP execution
                var spCmd = new OdbcCommand(StoredProcedureName) { CommandType = CommandType.StoredProcedure };

                spCmd.Parameters.Add(new OdbcParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;
                var logTypeParam = spCmd.Parameters.Add(LogTypeParam);
                var logMessageParam = spCmd.Parameters.Add(MessageParam);
                spCmd.Parameters.Add(PostedByParam).Value = ModuleName;

                using (var sqlConnection = new OdbcConnection(ConnectionString))
                {
                    sqlConnection.Open();
                    spCmd.Connection = sqlConnection;
                    spCmd.CommandTimeout = TIMEOUT_SECONDS;

                    while (!mMessageQueue.IsEmpty)
                    {
                        if (!mMessageQueue.TryDequeue(out var logMessage))
                        {
                            mFailedDequeueEvents += 1;
                            LogDequeueError(mFailedDequeueEvents, mMessageQueue.Count);
                            return;
                        }

                        mFailedDequeueEvents = 0;

                        if (logMessage.LogLevel <= LogLevels.ERROR)
                        {
                            MostRecentErrorMessage = logMessage.Message;
                        }

                        if (string.IsNullOrWhiteSpace(ConnectionString) || string.IsNullOrWhiteSpace(StoredProcedureName) || MessageParam == null)
                            continue;

                        logTypeParam.Value = logMessage.LogLevel.ToString();
                        logMessageParam.Value = logMessage.Message;

                        var retryCount = 2;

                        while (retryCount > 0)
                        {
                            var returnValue = 0;

                            try
                            {
                                spCmd.ExecuteNonQuery();
                                returnValue = Convert.ToInt32(spCmd.Parameters["@Return"].Value);
                            }
                            catch (Exception ex)
                            {
                                --retryCount;
                                var errorMessage = "Exception calling stored procedure " +
                                    spCmd.CommandText + ": " + ex.Message +
                                    "; resultCode = " + returnValue + "; Retry count = " + retryCount + "; " +
                                    PRISM.Utilities.GetExceptionStackTrace(ex);

                                if (retryCount ==0)
                                    FileLogger.WriteLog(LogLevels.ERROR, errorMessage);
                                else
                                    FileLogger.WriteLog(LogLevels.WARN, errorMessage);

                                if (!ex.Message.StartsWith("Could not find stored procedure " + spCmd.CommandText))
                                {
                                    // Try again
                                }
                                else
                                    break;
                            }

                        }


                    }
                }

            }
            catch (Exception ex)
            {
                PRISM.ConsoleMsgUtils.ShowError("Error writing queued log messages to the database: " + ex.Message, ex, false, false);
            }

        }

        /// <summary>
        /// Disable database logging
        /// </summary>
        public void RemoveConnectionInfo()
        {
            ChangeConnectionInfo(ModuleName, "", "", "", "", "");
        }

        /// <summary>
        /// Update the Log Level (called by property LogLevel)
        /// </summary>
        /// <param name="logLevel"></param>
        private void SetLogLevel(LogLevels logLevel)
        {
            mLogLevel = logLevel;
            IsDebugEnabled = mLogLevel >= LogLevels.DEBUG;
            IsErrorEnabled = mLogLevel >= LogLevels.ERROR;
            IsFatalEnabled = mLogLevel >= LogLevels.FATAL;
            IsInfoEnabled = mLogLevel >= LogLevels.INFO;
            IsWarnEnabled = mLogLevel >= LogLevels.WARN;
        }

        #region "Message logging methods"

        /// <summary>
        /// Log a debug message (provided LogLevel is LogLevels.DEBUG)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Debug(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.DEBUG, mLogLevel))
                return;

            WriteLog(LogLevels.DEBUG, message, ex);
        }

        /// <summary>
        /// Log an error message (provided LogLevel is LogLevels.ERROR or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Error(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.ERROR, mLogLevel))
                return;

            WriteLog(LogLevels.ERROR, message, ex);
        }

        /// <summary>
        /// Log a fatal error message (provided LogLevel is LogLevels.FATAL or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Fatal(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.FATAL, mLogLevel))
                return;

            WriteLog(LogLevels.FATAL, message, ex);
        }

        /// <summary>
        /// Log an informational message (provided LogLevel is LogLevels.INFO or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Info(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.INFO, mLogLevel))
                return;

            WriteLog(LogLevels.INFO, message, ex);
        }

        /// <summary>
        /// Log a warning message (provided LogLevel is LogLevels.WARN or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Warn(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.WARN, mLogLevel))
                return;

            WriteLog(LogLevels.WARN, message, ex);
        }

        /// <summary>
        /// Log a message (regardless of base.LogLevel)
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public static void WriteLog(LogLevels logLevel, string message, Exception ex = null)
        {
            var logMessage = new LogMessage(logLevel, message, ex);
            WriteLog(logMessage);
        }

        /// <summary>
        /// Log a message (regardless of base.LogLevel)
        /// </summary>
        /// <param name="logMessage"></param>
        public static void WriteLog(LogMessage logMessage)
        {
            mMessageQueue.Enqueue(logMessage);

            if (EchoMessagesToFileLogger)
                FileLogger.WriteLog(logMessage);
        }

        #endregion
    }
}
