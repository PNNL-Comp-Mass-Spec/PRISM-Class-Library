﻿using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;
using Microsoft.Data.SqlClient;
using PRISM;
using PRISM.Logging;

namespace PRISMDatabaseUtils.Logging
{
    /// <summary>
    /// Logs messages to a SQL Server database by calling a stored procedure
    /// Connect using Microsoft.Data.SqlClient
    /// </summary>
    /// <remarks>Can only log to a single database at a time</remarks>
    public sealed class SQLServerDatabaseLogger : DatabaseLogger
    {
        private const string DEFAULT_STORED_PROCEDURE_NAME = "post_log_entry";
        private const string DEFAULT_PARAM_NAME_LOG_TYPE = "type";
        private const string DEFAULT_PARAM_NAME_MESSAGE = "message";
        private const string DEFAULT_PARAM_NAME_POSTED_BY = "postedBy";

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

        // ReSharper disable once UnusedMember.Global

        /// <summary>
        /// Stored procedure used to store log messages
        /// </summary>
        public static string StoredProcedureName => LoggingProcedure?.ProcedureName;

        /// <summary>
        /// Constructor when the connection info is unknown
        /// </summary>
        /// <remarks>
        /// No database logging will occur until ChangeConnectionInfo is called (to define the connection string and stored procedure)
        /// </remarks>
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
        /// <param name="storedProcedure">Stored procedure used to store the log message</param>
        /// <param name="logTypeParamName">LogType parameter name (string representation of logLevel)</param>
        /// <param name="messageParamName">Message parameter name</param>
        /// <param name="postedByParamName">Log source parameter name</param>
        /// <param name="logTypeParamSize">LogType parameter size</param>
        /// <param name="messageParamSize">Message parameter size</param>
        /// <param name="postedByParamSize">Log source parameter size</param>
        public SQLServerDatabaseLogger(
            string moduleName,
            string connectionString,
            LogLevels logLevel = LogLevels.INFO,
            string storedProcedure = DEFAULT_STORED_PROCEDURE_NAME,
            string logTypeParamName = DEFAULT_PARAM_NAME_LOG_TYPE,
            string messageParamName = DEFAULT_PARAM_NAME_MESSAGE,
            string postedByParamName = DEFAULT_PARAM_NAME_POSTED_BY,
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
        public override void ChangeConnectionInfo(
            string moduleName,
            string connectionString)
        {
            ChangeConnectionInfo(
                moduleName, connectionString,
                DEFAULT_STORED_PROCEDURE_NAME,
                DEFAULT_PARAM_NAME_LOG_TYPE,
                DEFAULT_PARAM_NAME_MESSAGE,
                DEFAULT_PARAM_NAME_POSTED_BY);
        }

        /// <summary>
        /// Update the database connection info
        /// </summary>
        /// <param name="moduleName">
        /// Program name to pass to the postedByParamName field when contacting the database
        /// (will be auto-defined later if blank)
        /// </param>
        /// <param name="connectionString">SQL Server style connection string</param>
        /// <param name="storedProcedure">Stored procedure used to store the log message</param>
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
        /// Immediately write out any queued messages (using the current thread)
        /// </summary>
        /// <remarks>
        /// <para>
        /// There is no need to call this method if you create an instance of this class
        /// </para>
        /// <para>
        /// On the other hand, if you only call static methods in this class, call this method
        /// before ending the program to assure that all messages have been logged
        /// </para>
        /// </remarks>
        public override void FlushPendingMessages()
        {
            // Maximum time, in seconds, to continue to call StartLogQueuedMessages while the message queue is not empty
            const int MAX_TIME_SECONDS = 5;

            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow.Subtract(startTime).TotalSeconds < MAX_TIME_SECONDS)
            {
                StartLogQueuedMessages();

                if (mMessageQueue.IsEmpty)
                    break;

                AppUtils.SleepMilliseconds(10);
            }
        }

        /// <summary>
        /// Callback invoked by the mQueueLogger timer
        /// </summary>
        /// <param name="state">State</param>
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

                    var fatalError = false;

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

                                ConsoleMsgUtils.ShowWarning(errorMessage);

                                if (retryCount == 0)
                                    FileLogger.WriteLog(LogLevels.ERROR, errorMessage);
                                else
                                    FileLogger.WriteLog(LogLevels.WARN, errorMessage);

                                if (ex.Message.StartsWith("Could not find stored procedure"))
                                {
                                    fatalError = true;
                                    break;
                                }
                            }
                        }

                        if (fatalError)
                            break;
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
        /// <param name="logMessage">Log message</param>
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
