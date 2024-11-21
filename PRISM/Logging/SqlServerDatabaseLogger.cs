using System;

namespace PRISM.Logging
{
    /// <summary>
    /// Logs messages to a SQL Server database by calling a stored procedure
    /// Connect using Microsoft.Data.SqlClient
    /// </summary>
    /// <remarks>Can only log to a single database at a time</remarks>
    [Obsolete("Use PRISMDatabaseUtils.Logging.SQLServerDatabaseLogger instead (drop-in replacement); this class is only stubs now")]
    public sealed class SQLServerDatabaseLogger : DatabaseLogger
    {
        private const string DEFAULT_STORED_PROCEDURE_NAME = "post_log_entry";
        private const string DEFAULT_PARAM_NAME_LOG_TYPE = "type";
        private const string DEFAULT_PARAM_NAME_MESSAGE = "message";
        private const string DEFAULT_PARAM_NAME_POSTED_BY = "postedBy";

        /// <summary>
        /// Program name to pass to the PostedBy field when contacting the database
        /// </summary>
        /// <remarks>Will be auto-defined in LogQueuedMessages if blank</remarks>
        public static string ModuleName { get; set; }

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
        }

        /// <summary>
        /// Log a message (regardless of this.LogLevel)
        /// </summary>
        /// <param name="logMessage">Log message</param>
        public override void WriteLog(LogMessage logMessage)
        {
        }

        /// <summary>
        /// Class is disposing; write out any queued messages
        /// </summary>
        ~SQLServerDatabaseLogger()
        {
        }
    }
}
