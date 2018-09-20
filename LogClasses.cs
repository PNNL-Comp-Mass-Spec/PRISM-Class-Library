using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using PRISM.Logging;

namespace PRISM
{

    /// <summary>
    /// The type of log message.
    /// </summary>
    [Obsolete("Use Logging.FileLogger, Logging.SQLServerDatabaseLogger, or Logging.ODBCDatabaseLogger")]
    public enum logMsgType
    {
        /// <summary>
        /// The message is informational.
        /// </summary>
        logNormal = 0,
        /// <summary>
        /// The message represents an error.
        /// </summary>
        logError = 1,
        /// <summary>
        /// The message represents a warning.
        /// </summary>
        logWarning = 2,
        /// <summary>
        /// The message is only for debugging purposes.
        /// </summary>
        logDebug = 3,
        /// <summary>
        /// The message does not apply
        /// </summary>
        logNA = 4,
        /// <summary>
        /// The message is an indicator of (in)correct operation.
        /// </summary>
        logHealth = 5
    }

    #region "Logger Interface"
    /// <summary>
    /// Defines the logging interface.
    /// </summary>
    [Obsolete("Use Logging.FileLogger, Logging.SQLServerDatabaseLogger, or Logging.ODBCDatabaseLogger")]
    public interface ILogger
    {

        /// <summary>
        /// Current log file path
        /// </summary>
        string CurrentLogFilePath { get; }

        /// <summary>
        /// Most recent log message
        /// </summary>
        string MostRecentLogMessage { get; }

        /// <summary>
        /// Most recent error
        /// </summary>
        string MostRecentErrorMessage { get; }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="messages">The messages to post.</param>
        void PostEntries(List<clsLogEntry> messages);

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="entryType">The ILogger error type.</param>
        /// <param name="localOnly">If true, only post the message to the local log file, not the database; only used by clsDBLogger</param>
        void PostEntry(string message, logMsgType entryType, bool localOnly);

        /// <summary>
        /// Posts an error to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="e">The exception associated with the error.</param>
        /// <param name="localOnly">
        /// Only used by clsDBLogger
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        void PostError(string message, Exception e, bool localOnly);
    }
    #endregion

    #region "Logger Aware Interface"
    /// <summary>
    /// Defines the logging aware interface.
    /// </summary>
    /// <remarks>
    /// This interface is used by any class that wants to optionally support
    /// logging to a logger that implements the ILogger interface.  The key
    /// here is the phrase optionally.  The class allows, but does not
    /// require the class user to supply an ILogger.  If the Logger is not
    /// specified, the class throws Exceptions and raises Events in the usual
    /// way.  If an ILogger is specified, the user has the option of just logging,
    /// or logging and throwing/raising Exceptions/Events in the usual way as well.
    /// </remarks>
    [Obsolete("Use Logging.FileLogger, Logging.SQLServerDatabaseLogger, or Logging.ODBCDatabaseLogger")]
    public interface ILoggerAware
    {
        /// <summary>
        /// Register an ILogger with a class to have it log any exception that might occur.
        /// </summary>
        /// <param name="logger">A logger object to be used when logging is desired.</param>
        void RegisterExceptionLogger(ILogger logger);

        /// <summary>
        /// Register an ILogger with a class to have it log any event that might occur.
        /// </summary>
        /// <param name="logger">A logger object to be used when logging is desired.</param>
        void RegisterEventLogger(ILogger logger);

        /// <summary>
        /// Set true and the class will raise events.  Set false and it will not.
        /// </summary>
        /// <remarks>A function like the one shown below can be placed in ILoggerAware class that will only raise the event in the
        /// event of one needing to be raised.
        /// </remarks>
        bool NotifyOnEvent { get; set; }

        /// <summary>
        /// Set true and the class will throw exceptions.  Set false and it will not
        /// </summary>
        /// <remarks>A function like this can be place in ILoggerAware class that will only throw an exception in the
        /// event of one needing to be thrown.
        /// </remarks>
        bool NotifyOnException { get; set; }

    }
    #endregion

    /// <summary>
    /// Utility functions
    /// </summary>
    [Obsolete("Use clsStackTraceFormatter")]
    public class Utilities
    {

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a compact description of the current stack
        /// </summary>
        /// <param name="ex"></param>
        /// <returns>
        /// String of the form:
        /// Stack trace: clsCodeTest.Test-:-clsCodeTest.TestException-:-clsCodeTest.InnerTestException in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTrace(Exception ex)
        {
            return clsStackTraceFormatter.GetExceptionStackTrace(ex);
        }

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a cleaned up description of the current stack,
        /// with one line for each function in the call tree
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>
        /// Stack trace:
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTraceMultiLine(Exception ex)
        {
            return clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex);
        }
    }

    #region "File Logger Class"
    /// <summary>
    /// Provides logging to a local file.
    /// </summary>
    /// <remarks>
    /// The actual log file name changes daily and is of the form "filePath_mm-dd-yyyy.txt".
    /// </remarks>
    [Obsolete("Use Logging.FileLogger")]
    public class clsFileLogger : ILogger
    {
        /// <summary>
        /// Default filename timestamp format string
        /// </summary>
        public const string FILENAME_DATE_STAMP = "MM-dd-yyyy";

        private const string LOG_FILE_MATCH_SPEC = "??-??-????";

        private const string LOG_FILE_DATE_REGEX = @"(?<Month>\d+)-(?<Day>\d+)-(?<Year>\d{4,4})";

        private const string LOG_FILE_EXTENSION = ".txt";

        private const string DATE_TIME_FORMAT = "yyyy-MM-dd hh:mm:ss tt";

        /// <summary>
        /// Program name
        /// </summary>
        /// <remarks>Auto-determined using Assembly.GetEntryAssembly</remarks>
        private string m_programName;

        /// <summary>
        /// Program version
        /// </summary>
        /// <remarks>Auto-determined using Assembly.GetEntryAssembly</remarks>
        private string m_programVersion;

        private DateTime m_LastCheckOldLogs = DateTime.UtcNow.AddDays(-2);

        /// <summary>
        /// Initializes a new instance of the clsFileLogger class.
        /// </summary>
        /// <remarks>This constructor (without a file path) is required because clsDBLogger inherits clsFileLogger</remarks>
        public clsFileLogger()
        {
        }

        /// <summary>
        /// Initializes a new instance of the clsFileLogger class which logs to the specified file.
        /// </summary>
        /// <param name="logFileBaseName">The name of the file to use for the log (either a filename like UpdateManager, or a relative path, like Logs\UpdateManager</param>
        /// <remarks>
        /// The actual log file name changes daily and is of the form "FilePath_mm-dd-yyyy.txt"
        /// logFileBaseName is allowed to be blank (e.g. if using clsDBLogger and there is no need for a local log file)
        /// </remarks>
        public clsFileLogger(string logFileBaseName)
        {
            LogFileBaseName = string.IsNullOrWhiteSpace(logFileBaseName) ? string.Empty : logFileBaseName;

            UpdateCurrentLogFilePath();
        }

        /// <summary>
        /// When true, auto-archive old log files daily
        /// </summary>
        public bool ArchiveOldLogFiles { get; set; } = true;

        /// <summary>
        /// Path to the current log file (readonly)
        /// </summary>
        public string CurrentLogFilePath { get; private set; } = "";

        /// <summary>
        /// Gets the product version associated with this application.
        /// </summary>
        public string ExecutableVersion
        {
            get
            {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (m_programVersion == null)
                {
                    m_programVersion = FileProcessor.ProcessFilesOrFoldersBase.GetEntryOrExecutingAssembly().GetName().Version.ToString();
                }
                return m_programVersion;
            }
        }

        /// <summary>
        /// Gets the name of the executable file that started the application.
        /// </summary>
        public string ExecutableName
        {
            get
            {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (m_programName == null)
                {
                    m_programName = Path.GetFileName(FileProcessor.ProcessFilesOrFoldersBase.GetEntryOrExecutingAssembly().Location);
                }
                return m_programName;
            }
        }

        /// <summary>
        /// The base name of the the log file, e.g. UpdateManager or Logs\UpdateManager
        /// </summary>
        /// <remarks>
        /// The actual log file name changes daily and is of the form "FilePath_mm-dd-yyyy.txt"
        /// This property is readonly; define when instantiating the logger using the constructor
        /// </remarks>
        public string LogFileBaseName { get; private set; }

        /// <summary>
        /// The base name of the the log file, e.g. UpdateManager or Logs\UpdateManager
        /// </summary>
        /// <remarks>The actual log file name changes daily and is of the form "FilePath_mm-dd-yyyy.txt"</remarks>
        [Obsolete("Use LogFileBaseName instead")]
        public string LogFilePath
        {
            get => LogFileBaseName;
            set => LogFileBaseName = value;
        }

        /// <summary>
        /// Most recent log message
        /// </summary>
        public string MostRecentLogMessage { get; private set; } = "";

        /// <summary>
        /// Most recent error message
        /// </summary>
        public string MostRecentErrorMessage { get; private set; } = "";

        /// <summary>
        /// Move log files more than 32 days old into a year-based directory
        /// </summary>
        private void ArchiveOldLogs()
        {

            try
            {
                var currentLogFile = new FileInfo(CurrentLogFilePath);

                var logDirectory = currentLogFile.Directory;
                if (logDirectory == null)
                    return;

                var archiveWarnings = FileLogger.ArchiveOldLogs(logDirectory, LOG_FILE_MATCH_SPEC, LOG_FILE_EXTENSION, LOG_FILE_DATE_REGEX);

                foreach (var warning in archiveWarnings)
                {
                    ConsoleMsgUtils.ShowWarning(warning);
                }

            }
            catch (Exception ex)
            {
                PostError("Error archiving old log files", ex, true);
            }
        }

        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="messages">List of messages to post.</param>
        private void LogToFile(IEnumerable<clsLogEntry> messages)
        {
            // Don't log to file if no file name given
            // This will be the case if logging to clsDBLogger and a filepath was not provided when the class was instantiated
            if (string.IsNullOrEmpty(LogFileBaseName))
            {
                return;
            }

            // Make sure the log file name is up-to-date
            UpdateCurrentLogFilePath();

            try
            {
                using (var writer = new StreamWriter(new FileStream(CurrentLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {

                    foreach (var item in messages)
                    {

                        var formattedLogMessage = string.Format("{0}, {1}, {2}",
                            DateTime.Now.ToString(DATE_TIME_FORMAT), item.Message, TypeToString(item.EntryType));

                        writer.WriteLine(formattedLogMessage);

                        if (item.EntryType == logMsgType.logError)
                        {
                            MostRecentErrorMessage = formattedLogMessage;
                        }
                        else
                        {
                            MostRecentLogMessage = formattedLogMessage;
                        }
                    }
                }

            }
            catch (Exception)
            {
                // Ignore errors here
            }

            if (!ArchiveOldLogFiles || DateTime.UtcNow.Subtract(m_LastCheckOldLogs).TotalHours < 24)
                return;

            m_LastCheckOldLogs = DateTime.UtcNow;
            ArchiveOldLogs();
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="messages">The messages to post.</param>
        public virtual void PostEntries(List<clsLogEntry> messages)
        {
            LogToFile(messages);
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="entryType">The ILogger error type.</param>
        /// <param name="localOnly">
        /// Only used by clsDBLogger
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        public virtual void PostEntry(string message, logMsgType entryType, bool localOnly)
        {
            var messages = new List<clsLogEntry>{
                new clsLogEntry(message, entryType, localOnly)
            };

            LogToFile(messages);
        }

        /// <summary>
        /// Posts an error to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="ex">The exception associated with the error.</param>
        /// <param name="localOnly">
        /// Only used by clsDBLogger
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        public virtual void PostError(string message, Exception ex, bool localOnly)
        {
            if (ex == null)
            {
                PostEntry(message, logMsgType.logError, localOnly);
                return;
            }

            var messages = new List<clsLogEntry>{
                new clsLogEntry(message + ": " + ex.Message, logMsgType.logError, localOnly)
            };

            LogToFile(messages);
        }

        /// <summary>
        /// Converts enumerated error type to string for logging output.
        /// </summary>
        /// <param name="MyErrType">The ILogger error type.</param>
        protected string TypeToString(logMsgType MyErrType)
        {
            string functionReturnValue;
            switch (MyErrType)
            {
                case logMsgType.logNormal:
                    functionReturnValue = "Normal";
                    break;
                case logMsgType.logError:
                    functionReturnValue = "Error";
                    break;
                case logMsgType.logWarning:
                    functionReturnValue = "Warning";
                    break;
                case logMsgType.logDebug:
                    functionReturnValue = "Debug";
                    break;
                case logMsgType.logNA:
                    functionReturnValue = "na";
                    break;
                case logMsgType.logHealth:
                    functionReturnValue = "Health";
                    break;
                default:
                    functionReturnValue = "??";
                    break;
            }
            return functionReturnValue;
        }

        private void UpdateCurrentLogFilePath()
        {
            if (string.IsNullOrWhiteSpace(LogFileBaseName))
                CurrentLogFilePath = string.Empty;
            else
                // Define log file name by appending the current date to m_logFileBaseName
                CurrentLogFilePath = LogFileBaseName + "_" + DateTime.Now.ToString(FILENAME_DATE_STAMP) + LOG_FILE_EXTENSION;
        }

    }
    #endregion

    #region "Database Logger Class"
    /// <summary>
    /// Provides logging to a database and local file.
    /// </summary>
    /// <remarks>The module name identifies the logging process; if not defined, will use MachineName:UserName</remarks>
    [Obsolete("Use Logging.SQLServerDatabaseLogger or Logging.ODBCDatabaseLogger")]
    public class clsDBLogger : clsFileLogger
    {

        // connection string
        private string m_connection_str;

        /// <summary>
        /// List of database errors
        /// </summary>
        private readonly List<string> m_error_list = new List<string>();

        /// <summary>
        /// Module name
        /// </summary>
        protected string m_moduleName;

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class.
        /// </summary>
        public clsDBLogger()
        {
        }

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class which logs to the specified database
        /// </summary>
        /// <param name="connectionStr">The connection string used to access the database.</param>
        /// <remarks>Only logs to a local file if a file name is defined using LogFilePath</remarks>
        public clsDBLogger(string connectionStr)
        {
            m_connection_str = connectionStr;
        }

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class which logs to the specified database and file.
        /// </summary>
        /// <param name="connectionStr">The connection string used to access the database.</param>
        /// <param name="filePath">The name of the file to use for the log.</param>
        public clsDBLogger(string connectionStr, string filePath) : base(filePath)
        {
            m_connection_str = connectionStr;
        }

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class which logs to the specified database and file.
        /// </summary>
        /// <param name="modName">The string used to identify the posting process.</param>
        /// <param name="connectionStr">The connection string used to access the database.</param>
        /// <param name="filePath">The name of the file to use for the log.</param>
        /// <remarks>The module name identifies the logging process; if not defined, will use MachineName:UserName</remarks>
        public clsDBLogger(string modName, string connectionStr, string filePath) : base(filePath)
        {
            m_connection_str = connectionStr;
            m_moduleName = modName;
        }

        /// <summary>
        /// The connection string used to access the database.
        /// </summary>
        public string ConnectionString
        {
            get => m_connection_str;
            set => m_connection_str = value;
        }

        /// <summary>
        /// List of any database errors that occurred while posting the log entry to the database
        /// </summary>
        public IReadOnlyList<string> DBErrors => m_error_list;

        /// <summary>
        /// The module name identifies the logging process.
        /// </summary>
        public string MachineName => DatabaseLogger.MachineName;

        /// <summary>
        /// Construct the string MachineName:UserName.
        /// </summary>
        private string ConstructModuleName()
        {
            var retVal = MachineName + ":" + DatabaseLogger.UserName;
            return retVal;
        }

        /// <summary>
        /// The module name identifies the logging process.
        /// </summary>
        /// <remarks>If the module name is not specified, it is filled in as
        /// MachineName:UserName.</remarks>
        public string ModuleName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(m_moduleName))
                {
                    m_moduleName = ConstructModuleName();
                }
                return m_moduleName;
            }
            set => m_moduleName = value;
        }

        /// <summary>
        /// Writes a message to the log table.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="entryType">The ILogger error type.</param>
        protected virtual void LogToDB(string message, logMsgType entryType)
        {
            PostLogEntry(TypeToString(entryType), message);
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="messages">The messages to post.</param>
        public override void PostEntries(List<clsLogEntry> messages)
        {
            if(!string.IsNullOrWhiteSpace(LogFileBaseName))
            {
                base.PostEntries(messages);
            }

            foreach (var item in messages)
            {
                if (!item.LocalOnly)
                {
                    LogToDB(item.Message, item.EntryType);
                }
            }
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="entryType">The ILogger error type.</param>
        /// <param name="localOnly">
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        public override void PostEntry(string message, logMsgType entryType, bool localOnly)
        {
            if (!string.IsNullOrWhiteSpace(LogFileBaseName))
            {
                base.PostEntry(message, entryType, localOnly);
            }

            if (!localOnly)
            {
                LogToDB(message, entryType);
            }
        }

        /// <summary>
        /// Posts an error to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="ex">The exception associated with the error.</param>
        /// <param name="localOnly">
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        public override void PostError(string message, Exception ex, bool localOnly)
        {
            if (!string.IsNullOrWhiteSpace(LogFileBaseName))
            {
                base.PostError(message, ex, localOnly);
            }
            if (!localOnly)
            {
                LogToDB(message + ": " + ex.Message, logMsgType.logError);
            }
        }

        /// <summary>
        /// Writes a message to the log table via the stored procedure.
        /// </summary>
        /// <param name="type">The ILogger error type.</param>
        /// <param name="message">The message to post.</param>
        private void PostLogEntry(string type, string message)
        {
            try
            {
                m_error_list.Clear();

                // Create the database connection
                var cnStr = m_connection_str;
                using (var dbCn = new SqlConnection(cnStr))
                {
                    dbCn.InfoMessage += OnInfoMessage;
                    dbCn.Open();

                    // Create the command object
                    var sc = new SqlCommand("PostLogEntry", dbCn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    sc.Parameters.Add("@Return", SqlDbType.Int).Direction = ParameterDirection.ReturnValue;
                    sc.Parameters.Add("@type", SqlDbType.VarChar, 50).Value = type;
                    sc.Parameters.Add("@message", SqlDbType.VarChar, 500).Value = message;
                    sc.Parameters.Add("@postedBy", SqlDbType.VarChar, 50).Value = ModuleName;

                    // Execute the stored procedure
                    sc.ExecuteNonQuery();

                }

            }
            catch (Exception ex)
            {
                PostError("Failed to post log entry in database.", ex, true);
            }
        }

        /// <summary>
        /// Event handler for InfoMessage event.
        /// </summary>
        /// <remarks>Errors and warnings sent from the SQL Server database engine are caught here.</remarks>
        private void OnInfoMessage(object sender, SqlInfoMessageEventArgs args)
        {
            foreach (SqlError err in args.Errors)
            {
                var s = "Message: " + err.Message +
                        ", Source: " + err.Source +
                        ", Class: " + err.Class +
                        ", State: " + err.State +
                        ", Number: " + err.Number +
                        ", LineNumber: " + err.LineNumber +
                        ", Procedure:" + err.Procedure +
                        ", Server: " + err.Server;
                m_error_list.Add(s);
            }
        }

    }
    #endregion

    #region "Log Entry Class"

    /// <summary>
    /// A class to hold a log entry
    /// </summary>
    [Obsolete("Use Logging.FileLogger, Logging.SQLServerDatabaseLogger, or Logging.ODBCDatabaseLogger")]
    public class clsLogEntry
    {
        /// <summary>
        /// Log message
        /// </summary>
        public string Message;

        /// <summary>
        /// Log message type
        /// </summary>
        public logMsgType EntryType;

        /// <summary>
        /// Only used by clsDBLogger
        /// When true, log to the local file but not to the database
        /// When false, log to the local log file and the database
        /// </summary>
        public bool LocalOnly;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="entryType">Message type</param>
        /// <param name="localOnly">
        /// Only used by clsDBLogger
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        public clsLogEntry(string message, logMsgType entryType, bool localOnly = true)
        {
            Message = message;
            EntryType = entryType;
            LocalOnly = localOnly;
        }
    }
    #endregion

    #region "Queue Logger Class"
    /// <summary>
    /// Wraps a queuing mechanism around any object that implements ILogger interface.
    /// </summary>
    /// <remarks>The posting member functions of this class put the log entry
    /// onto the end of an internal queue and return very quickly to the caller.
    /// A separate thread within the class is used to perform the actual output of
    /// the log entries using the logging object that is specified
    /// in the constructor for this class.
    /// </remarks>
    [Obsolete("Use Logging.FileLogger, Logging.SQLServerDatabaseLogger, or Logging.ODBCDatabaseLogger")]
    public class clsQueLogger : ILogger
    {
        /// <summary>
        /// queue to hold entries to be output
        /// </summary>
        protected ConcurrentQueue<clsLogEntry> m_queue;

        /// <summary>
        /// Internal thread for outputting entries from queue
        /// </summary>
        protected Timer m_ThreadTimer;

        /// <summary>
        /// logger object to use for outputting entries from queue
        /// </summary>
        protected ILogger m_logger;

        /// <summary>
        /// Path to the current log file
        /// </summary>
        public string CurrentLogFilePath
        {
            get
            {
                if (m_logger == null)
                {
                    return string.Empty;
                }

                return m_logger.CurrentLogFilePath;
            }
        }

        /// <summary>
        /// Most recent log message
        /// </summary>
        public string MostRecentLogMessage
        {
            get
            {
                if (m_logger == null)
                {
                    return string.Empty;
                }

                return m_logger.MostRecentLogMessage;
            }
        }

        /// <summary>
        /// Most recent error message
        /// </summary>
        public string MostRecentErrorMessage
        {
            get
            {
                if (m_logger == null)
                {
                    return string.Empty;
                }

                return m_logger.MostRecentErrorMessage;
            }
        }

        /// <summary>
        /// Constructor: Initializes a new instance of the clsQueLogger class which logs to the ILogger.
        /// </summary>
        /// <param name="logger">The target logger object.</param>
        public clsQueLogger(ILogger logger)
        {
            // Remember my logging object
            m_logger = logger;

            // Create a thread safe queue for log entries
            m_queue = new ConcurrentQueue<clsLogEntry>();

            // Log every 1 second
            m_ThreadTimer = new Timer(LogFromQueue, this, 0, 1000);
        }

        /// <summary>
        /// Pull all entries from the queue and output them to the log streams.
        /// </summary>
        protected void LogFromQueue(object o)
        {
            if (m_queue.Count == 0)
                return;

            var messages = new List<clsLogEntry>();

            while (true)
            {
                if (m_queue.TryDequeue(out var le))
                {
                    messages.Add(le);
                }

                if (m_queue.Count == 0)
                    break;
            }

            m_logger.PostEntries(messages);

        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="messages">The messages to post.</param>
        public virtual void PostEntries(List<clsLogEntry> messages)
        {
            foreach (var item in messages)
            {
                m_queue.Enqueue(item);
            }
        }

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="entryType">The ILogger error type.</param>
        /// <param name="localOnly">
        /// Only used by clsDBLogger
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        public void PostEntry(string message, logMsgType entryType, bool localOnly)
        {
            var le = new clsLogEntry(message, entryType, localOnly);
            m_queue.Enqueue(le);
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="e">The exception associated with the error.</param>
        /// <param name="localOnly">
        /// Only used by clsDBLogger
        /// When true, only post the message to the local log file
        /// When false, log to the local log file and the database
        /// </param>
        public void PostError(string message, Exception e, bool localOnly)
        {
            var le = new clsLogEntry(message + ": " + e.Message, logMsgType.logError, localOnly);
            m_queue.Enqueue(le);
        }
    }

    #endregion

}
