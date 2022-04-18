using System;
using System.Globalization;
using System.IO;
using JetBrains.Annotations;

// ReSharper disable UnusedMember.Global

namespace PRISM.Logging
{
    /// <summary>
    /// Class for handling logging via the FileLogger and DatabaseLogger
    /// </summary>
    /// <remarks>
    /// Call method CreateFileLogger to define the log file name
    /// Call method CreateDbLogger to define the database connection info
    /// Log files have date-based names, for example DataProcessor_01-02-2020.txt
    /// If you want year-month-day based names, update your class to inherit ProcessFilesBase or ProcessDirectoriesBase
    /// </remarks>
    public static class LogTools
    {
        // Ignore Spelling: yyyy

        /// <summary>
        /// Log types
        /// </summary>
        public enum LoggerTypes
        {
            /// <summary>
            /// Log to a log file
            /// </summary>
            LogFile,

            /// <summary>
            /// Log to the database and to the log file
            /// </summary>
            LogDb
        }

        /// <summary>
        /// File Logger
        /// </summary>
        private static readonly FileLogger mFileLogger = new();

        /// <summary>
        /// Database logger
        /// </summary>
        private static readonly DatabaseLogger mDbLogger = new SQLServerDatabaseLogger();

        /// <summary>
        /// File path for the current log file used by the FileLogger
        /// </summary>
        public static string CurrentLogFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(FileLogger.LogFilePath))
                {
                    return string.Empty;
                }

                return FileLogger.LogFilePath;
            }
        }

        /// <summary>
        /// Tells calling program file debug status
        /// </summary>
        /// <returns>True if debug level enabled for file logger; false otherwise</returns>
        public static bool FileLogDebugEnabled => mFileLogger.IsDebugEnabled;

        /// <summary>
        /// When true, never try to log to a database
        /// </summary>
        public static bool OfflineMode { get; set; }

        /// <summary>
        /// Most recent error message
        /// </summary>
        public static string MostRecentErrorMessage => BaseLogger.MostRecentErrorMessage;

        /// <summary>
        /// Working directory path
        /// </summary>
        public static string WorkDirPath { get; set; }

        /// <summary>
        /// Update the log file's base name (or relative path)
        /// However, if appendDateToBaseName is false, baseName is the full path to the log file
        /// </summary>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <param name="appendDateToBaseName">
        /// When true, the actual log file name will have today's date appended to it, in the form yyyy-mm-dd.txt
        /// When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
        /// </param>
        public static void ChangeLogFileBaseName(string baseName, bool appendDateToBaseName)
        {
            FileLogger.ChangeLogFileBaseName(baseName, appendDateToBaseName);
        }

        /// <summary>
        /// Configures the file logger
        /// </summary>
        /// <param name="logFileNameBase">Base name for log file</param>
        /// <param name="traceMode">When true, show additional debug messages at the console</param>
        public static void CreateFileLogger(string logFileNameBase, bool traceMode)
        {
            CreateFileLogger(logFileNameBase, BaseLogger.LogLevels.INFO, traceMode);
        }

        /// <summary>
        /// Configures the file logger
        /// </summary>
        /// <param name="logFileNameBase">Base name for log file</param>
        /// <param name="logLevel">Log threshold level</param>
        /// <param name="traceMode">When true, show additional debug messages at the console</param>
        public static void CreateFileLogger(
            string logFileNameBase,
            BaseLogger.LogLevels logLevel = BaseLogger.LogLevels.INFO,
            bool traceMode = false)
        {
            if (traceMode && !BaseLogger.TraceMode)
                BaseLogger.TraceMode = true;

            BaseLogger.TimestampFormat = Logging.LogMessage.TimestampFormatMode.YearMonthDay24hr;
            mFileLogger.LogLevel = logLevel;

            FileLogger.ChangeLogFileBaseName(logFileNameBase, appendDateToBaseName: true);
        }

        /// <summary>
        /// Configures the database logger
        /// </summary>
        /// <param name="connectionString">System.Data.SqlClient style connection string</param>
        /// <param name="moduleName">Module name used by logger</param>
        /// <param name="traceMode">When true, show additional debug messages at the console</param>
        public static void CreateDbLogger(string connectionString, string moduleName, bool traceMode)
        {
            CreateDbLogger(connectionString, moduleName, BaseLogger.LogLevels.INFO, traceMode);
        }

        /// <summary>
        /// Configures the database logger
        /// </summary>
        /// <param name="connectionString">System.Data.SqlClient style connection string</param>
        /// <param name="moduleName">Module name used by logger</param>
        /// <param name="logLevel">Log threshold level</param>
        /// <param name="traceMode">When true, show additional debug messages at the console</param>
        public static void CreateDbLogger(
            string connectionString,
            string moduleName,
            BaseLogger.LogLevels logLevel = BaseLogger.LogLevels.INFO,
            bool traceMode = false)
        {
            if (traceMode && !BaseLogger.TraceMode)
                BaseLogger.TraceMode = true;

            mDbLogger.EchoMessagesToFileLogger = true;
            mDbLogger.LogLevel = logLevel;

            mDbLogger.ChangeConnectionInfo(moduleName, connectionString, "post_log_entry", "type", "message", "postedBy");
        }

        /// <summary>
        /// Notify the user at console that an error occurred while writing to a log file or posting a log message to the database
        /// </summary>
        /// <param name="logMessage"></param>
        /// <param name="ex"></param>
        public static void ErrorWritingToLog(string logMessage, Exception ex)
        {
            ConsoleMsgUtils.ShowError("Error logging errors; log message: " + logMessage, ex);
        }

        /// <summary>
        /// Immediately write out any queued messages (using the current thread)
        /// </summary>
        public static void FlushPendingMessages()
        {
            FileLogger.FlushPendingMessages();
        }

        /// <summary>
        /// Show a status message at the console and optionally include in the log file, tagging it as a debug message
        /// </summary>
        /// <remarks>The message is shown in dark gray in the console</remarks>
        /// <param name="statusMessage">Status message</param>
        /// <param name="writeToLog">True to write to the log file; false to only display at console</param>
        public static void LogDebug(string statusMessage, bool writeToLog = true)
        {
            ConsoleMsgUtils.ShowDebug(statusMessage);

            if (!writeToLog)
                return;

            try
            {
                WriteLog(LoggerTypes.LogFile, BaseLogger.LogLevels.DEBUG, statusMessage);
            }
            catch (Exception ex)
            {
                ErrorWritingToLog(statusMessage, ex);
            }
        }

        /// <summary>
        /// Show a status message at the console and in the log file, tagging it as a debug message
        /// </summary>
        /// <remarks>The message is shown in dark gray in the console</remarks>
        /// <param name="format">Status message format string</param>
        /// <param name="args">string format arguments</param>
        [StringFormatMethod("format")]
        public static void LogDebug(string format, params object[] args)
        {
            LogDebug(string.Format(format, args));
        }

        /// <summary>
        /// Log an error message and exception
        /// </summary>
        /// <remarks>The error is shown in red in the console.  The exception stack trace is shown in cyan</remarks>
        /// <param name="errorMessage">Error message (do not include ex.message)</param>
        /// <param name="ex">Exception to log (allowed to be null)</param>
        /// <param name="logToDatabase">When true, log to the database (and to the file)</param>
        public static void LogError(string errorMessage, Exception ex = null, bool logToDatabase = false)
        {
            var formattedError = ConsoleMsgUtils.ShowError(errorMessage, ex);

            try
            {
                var logType = logToDatabase ? LoggerTypes.LogDb : LoggerTypes.LogFile;
                WriteLog(logType, BaseLogger.LogLevels.ERROR, formattedError, ex);
            }
            catch (Exception ex2)
            {
                ErrorWritingToLog(formattedError, ex2);
            }
        }

        /// <summary>
        /// Log an error message and exception
        /// </summary>
        /// <remarks>The error is shown in red in the console.</remarks>
        /// <param name="format">Error message format string</param>
        /// <param name="args">string format arguments</param>
        [StringFormatMethod("format")]
        public static void LogError(string format, params object[] args)
        {
            LogError(string.Format(format, args));
        }

        /// <summary>
        /// Log an error message and exception
        /// </summary>
        /// <remarks>The error is shown in red in the console.  The exception stack trace is shown in cyan</remarks>
        /// <param name="ex">Exception to log</param>
        /// <param name="format">Error message format string (do not include ex.message)</param>
        /// <param name="args">string format arguments</param>
        [StringFormatMethod("format")]
        public static void LogError(Exception ex, string format, params object[] args)
        {
            LogError(string.Format(format, args), ex);
        }

        /// <summary>
        /// Log a fatal error message and exception
        /// </summary>
        /// <remarks>The error is shown in red in the console.  The exception stack trace is shown in cyan</remarks>
        /// <param name="errorMessage">Error message (do not include ex.message)</param>
        /// <param name="ex">Exception to log (allowed to be null)</param>
        /// <param name="logToDatabase">When true, log to the database (and to the file)</param>
        public static void LogFatalError(string errorMessage, Exception ex = null, bool logToDatabase = false)
        {
            var formattedError = ConsoleMsgUtils.ShowError(errorMessage, ex);

            try
            {
                var logType = logToDatabase ? LoggerTypes.LogDb : LoggerTypes.LogFile;
                WriteLog(logType, BaseLogger.LogLevels.FATAL, formattedError, ex);
            }
            catch (Exception ex2)
            {
                ErrorWritingToLog(formattedError, ex2);
            }
        }

        /// <summary>
        /// Log a fatal error message and exception
        /// </summary>
        /// <remarks>The error is shown in red in the console.</remarks>
        /// <param name="format">Error message format string</param>
        /// <param name="args">string format arguments</param>
        [StringFormatMethod("format")]
        public static void LogFatalError(string format, params object[] args)
        {
            LogFatalError(string.Format(format, args));
        }

        /// <summary>
        /// Log a fatal error message and exception
        /// </summary>
        /// <remarks>The error is shown in red in the console.  The exception stack trace is shown in cyan</remarks>
        /// <param name="ex">Exception to log</param>
        /// <param name="format">Error message format string (do not include ex.message)</param>
        /// <param name="args">string format arguments</param>
        [StringFormatMethod("format")]
        public static void LogFatalError(Exception ex, string format, params object[] args)
        {
            LogFatalError(string.Format(format, args), ex);
        }

        /// <summary>
        /// Show a status message at the console and optionally include in the log file
        /// </summary>
        /// <param name="statusMessage">Status message</param>
        /// <param name="isError">True if this is an error</param>
        /// <param name="writeToLog">True to write to the log file; false to only display at console</param>
        public static void LogMessage(string statusMessage, bool isError = false, bool writeToLog = true)
        {
            if (isError)
            {
                ConsoleMsgUtils.ShowErrorCustom(statusMessage, false);
            }
            else
            {
                Console.WriteLine(statusMessage);
            }

            if (!writeToLog)
                return;

            try
            {
                if (isError)
                {
                    WriteLog(LoggerTypes.LogFile, BaseLogger.LogLevels.ERROR, statusMessage);
                }
                else
                {
                    WriteLog(LoggerTypes.LogFile, BaseLogger.LogLevels.INFO, statusMessage);
                }
            }
            catch (Exception ex)
            {
                ErrorWritingToLog(statusMessage, ex);
            }
        }

        /// <summary>
        /// Show a status message at the console and optionally include in the log file
        /// </summary>
        /// <param name="format">Status message format string</param>
        /// <param name="args">string format arguments</param>
        [StringFormatMethod("format")]
        public static void LogMessage(string format, params object[] args)
        {
            LogMessage(string.Format(format, args));
        }

        /// <summary>
        /// Display a warning message at the console and write to the log file
        /// </summary>
        /// <param name="warningMessage">Warning message</param>
        /// <param name="logToDatabase">When true, log to the database (and to the file)</param>
        public static void LogWarning(string warningMessage, bool logToDatabase = false)
        {
            ConsoleMsgUtils.ShowWarning(warningMessage);

            try
            {
                var logType = logToDatabase ? LoggerTypes.LogDb : LoggerTypes.LogFile;
                WriteLog(logType, BaseLogger.LogLevels.WARN, warningMessage);
            }
            catch (Exception ex)
            {
                ErrorWritingToLog(warningMessage, ex);
            }
        }

        /// <summary>
        /// Display a warning message at the console and write to the log file
        /// </summary>
        /// <param name="format">Warning message format string</param>
        /// <param name="args">string format arguments</param>
        [StringFormatMethod("format")]
        public static void LogWarning(string format, params object[] args)
        {
            LogWarning(string.Format(format, args));
        }

        /// <summary>
        /// Remove the default database logger that was created when the program first started
        /// </summary>
        public static void RemoveDefaultDbLogger()
        {
            mDbLogger.RemoveConnectionInfo();
        }

        /// <summary>
        /// Sets the file logging log threshold via an integer
        /// </summary>
        /// <param name="logLevel">Integer corresponding to log threshold level (1-5, 5 being most verbose)</param>
        public static void SetFileLogLevel(int logLevel)
        {
            var logLevelEnumType = typeof(BaseLogger.LogLevels);

            // Verify input level is a valid log level
            if (!Enum.IsDefined(logLevelEnumType, logLevel))
            {
                LogError("Invalid value specified for level: " + logLevel);
                return;
            }

            // Convert input integer into the associated enum
            var logLevelEnum = (BaseLogger.LogLevels)Enum.Parse(logLevelEnumType, logLevel.ToString(CultureInfo.InvariantCulture));

            SetFileLogLevel(logLevelEnum);
        }

        /// <summary>
        /// Sets the file logging log threshold via an enum
        /// </summary>
        /// <param name="logLevel">LogLevels value defining log threshold level (Debug is most verbose)</param>
        public static void SetFileLogLevel(BaseLogger.LogLevels logLevel)
        {
            mFileLogger.LogLevel = logLevel;
        }

        /// <summary>
        /// Write a message to the logging system
        /// </summary>
        /// <param name="loggerType">Type of logger to use</param>
        /// <param name="logLevel">Level of log reporting</param>
        /// <param name="message">Message to be logged</param>
        public static void WriteLog(LoggerTypes loggerType, BaseLogger.LogLevels logLevel, string message)
        {
            WriteLogWork(loggerType, logLevel, message, null);
        }

        /// <summary>
        /// Write a message and exception to the logging system
        /// </summary>
        /// <param name="loggerType">Type of logger to use</param>
        /// <param name="logLevel">Level of log reporting</param>
        /// <param name="message">Message to be logged</param>
        /// <param name="ex">Exception to be logged</param>
        public static void WriteLog(LoggerTypes loggerType, BaseLogger.LogLevels logLevel, string message, Exception ex)
        {
            WriteLogWork(loggerType, logLevel, message, ex);
        }

        /// <summary>
        /// Write a message and possibly an exception to the logging system
        /// </summary>
        /// <remarks>Log message will not be written if logLevel is LogLevel or higher)</remarks>
        /// <param name="loggerType">Type of logger to use</param>
        /// <param name="logLevel">Level of log reporting</param>
        /// <param name="message">Message to be logged</param>
        /// <param name="ex">Exception to be logged; null if no exception</param>
        private static void WriteLogWork(LoggerTypes loggerType, BaseLogger.LogLevels logLevel, string message, Exception ex)
        {
            if (OfflineMode && loggerType == LoggerTypes.LogDb)
                loggerType = LoggerTypes.LogFile;

            BaseLogger myLogger;

            // Establish which logger will be used
            switch (loggerType)
            {
                case LoggerTypes.LogDb:
                    // Note that the Database logger will (by default) also echo messages to the file logger
                    myLogger = mDbLogger;
                    message = System.Net.Dns.GetHostName() + ": " + message;
                    break;

                case LoggerTypes.LogFile:
                    myLogger = mFileLogger;

                    if (!string.IsNullOrWhiteSpace(FileLogger.LogFilePath) &&
                        !FileLogger.LogFilePath.Contains(Path.DirectorySeparatorChar.ToString()))
                    {
                        var logFileName = Path.GetFileName(FileLogger.LogFilePath);
                        string workDirLogPath;
                        if (string.IsNullOrEmpty(WorkDirPath))
                            workDirLogPath = Path.Combine(".", logFileName);
                        else
                            workDirLogPath = Path.Combine(WorkDirPath, logFileName);

                        ChangeLogFileBaseName(workDirLogPath, FileLogger.AppendDateToBaseFileName);
                    }

                    break;

                default:
                    throw new Exception("Invalid logger type specified");
            }

            MessageLogged?.Invoke(message, logLevel);

            // Send the log message
            myLogger?.LogMessage(logLevel, message, ex);
        }

        /// <summary>
        /// Delegate for event MessageLogged
        /// </summary>
        public delegate void MessageLoggedEventHandler(string message, BaseLogger.LogLevels logLevel);

        /// <summary>
        /// This event is raised when a message is logged
        /// </summary>
        public static event MessageLoggedEventHandler MessageLogged;
    }
}
