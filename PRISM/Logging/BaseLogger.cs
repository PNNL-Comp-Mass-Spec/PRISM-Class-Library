using System;
using System.IO;

namespace PRISM.Logging
{
    /// <summary>
    /// Base class for FileLogger and DatabaseLogger
    /// </summary>
    public abstract class BaseLogger
    {
        /// <summary>
        /// Log levels
        /// </summary>
        public enum LogLevels
        {
            /// <summary>
            /// Disables all logging
            /// </summary>
            // ReSharper disable once UnusedMember.Global
            NOLOGGING = 0,

            /// <summary>
            /// Fatal error message
            /// </summary>
            FATAL = 1,

            /// <summary>
            /// Error message
            /// </summary>
            ERROR = 2,

            /// <summary>
            /// Warning message
            /// </summary>
            WARN = 3,

            /// <summary>
            /// Informational message
            /// </summary>
            INFO = 4,

            /// <summary>
            /// Debug message
            /// </summary>
            DEBUG = 5
        }

        /// <summary>
        /// Set to True if we cannot log to the official log file, we try to log to the local log file, and even that file cannot be written
        /// </summary>
        private static bool mLocalLogFileAccessError;

        /// <summary>
        /// Program name
        /// </summary>
        /// <remarks>Auto-determined using Assembly.GetEntryAssembly</remarks>
        private static string m_programName;

        /// <summary>
        /// Program version
        /// </summary>
        /// <remarks>Auto-determined using Assembly.GetEntryAssembly</remarks>
        private static string m_programVersion;

        #region "Properties"

        /// <summary>
        /// Gets the product version associated with this application
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static string ExecutableVersion => m_programVersion ??= FileProcessor.ProcessFilesOrDirectoriesBase.GetEntryOrExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// Gets the name of the executable file that started the application
        /// </summary>
        public static string ExecutableName => m_programName ??= Path.GetFileName(FileProcessor.ProcessFilesOrDirectoriesBase.GetEntryOrExecutingAssembly().Location);

        /// <summary>
        /// Most recent error message
        /// </summary>
        public static string MostRecentErrorMessage { get; protected set; } = string.Empty;

        /// <summary>
        /// Timestamp format, defaults to year-month-day time (24 hour clock)
        /// </summary>
        public static LogMessage.TimestampFormatMode TimestampFormat = Logging.LogMessage.TimestampFormatMode.YearMonthDay24hr;

        /// <summary>
        /// When true, show additional debug messages at the console
        /// </summary>
        public static bool TraceMode { get; set; }

        #endregion

        /// <summary>
        /// Compare message log level to the log threshold level
        /// </summary>
        /// <param name="messageLogLevel"></param>
        /// <param name="logThresholdLevel"></param>
        /// <returns>True if this message should be logged</returns>
        protected bool AllowLog(LogLevels messageLogLevel, LogLevels logThresholdLevel)
        {
            return messageLogLevel <= logThresholdLevel;
        }

        /// <summary>
        /// Log a local message regarding a message queue dequeue error
        /// </summary>
        /// <param name="failedDequeueEvents"></param>
        /// <param name="messageQueueCount"></param>
        protected static void LogDequeueError(int failedDequeueEvents, int messageQueueCount)
        {
            bool warnUser;

            if (failedDequeueEvents < 5)
            {
                warnUser = true;
            }
            else
            {
                var modDivisor = (int)(Math.Ceiling(Math.Log10(failedDequeueEvents)) * 10);
                warnUser = failedDequeueEvents % modDivisor == 0;
            }

            if (!warnUser)
                return;

            if (messageQueueCount == 1)
            {
                LogLocalMessage(LogLevels.WARN, "Unable to dequeue the next message to log to the database");
            }
            else
            {
                LogLocalMessage(LogLevels.WARN, string.Format("Unable to dequeue the next log message to log to the database; {0} pending messages", messageQueueCount));
            }
        }

        /// <summary>
        /// Log a message to the local, generic log file
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        /// <param name="localLogFilePath"></param>
        /// <remarks>Used to log errors and warnings when the standard log file (or database) cannot be written to</remarks>
        protected static void LogLocalMessage(LogLevels logLevel, string message, string localLogFilePath = "FileLoggerErrors.txt")
        {
            var logMessage = new LogMessage(logLevel, message);
            LogLocalMessage(logMessage, localLogFilePath);
        }

        /// <summary>
        /// Log a message to the local, generic log file
        /// </summary>
        /// <param name="logMessage"></param>
        /// <param name="localLogFilePath"></param>
        /// <remarks>Used to log errors and warnings when the standard log file (or database) cannot be written to</remarks>
        protected static void LogLocalMessage(LogMessage logMessage, string localLogFilePath = "FileLoggerErrors.txt")
        {
            switch (logMessage.LogLevel)
            {
                case LogLevels.DEBUG:
                    ConsoleMsgUtils.ShowDebug(logMessage.Message);
                    break;
                case LogLevels.WARN:
                    ConsoleMsgUtils.ShowWarning(logMessage.Message);
                    break;
                case LogLevels.ERROR:
                case LogLevels.FATAL:
                    ConsoleMsgUtils.ShowError(logMessage.Message);
                    break;
                default:
                    Console.WriteLine(logMessage.Message);
                    break;
            }

            if (string.IsNullOrWhiteSpace(localLogFilePath))
                localLogFilePath = "FileLoggerErrors.txt";

            try
            {
                var localLogFile = new FileInfo(localLogFilePath);
                localLogFilePath = localLogFile.FullName;

                using var localLogWriter = new StreamWriter(new FileStream(localLogFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                localLogWriter.WriteLine(logMessage.GetFormattedMessage(TimestampFormat));
            }
            catch (Exception ex)
            {
                if (!mLocalLogFileAccessError)
                {
                    mLocalLogFileAccessError = true;
                    ConsoleMsgUtils.ShowError("Error writing to the local log file: " + localLogFilePath);
                }

                ConsoleMsgUtils.ShowErrorCustom(
                    string.Format("Error writing '{0}' to the local log file: {1}", logMessage.GetFormattedMessage(TimestampFormat), ex),
                    false,
                    false);
            }
        }

        /// <summary>
        /// Log a message (provided logLevel is the log threshold value or lower)
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public void LogMessage(LogLevels logLevel, string message, Exception ex = null)
        {
            // Send the log message
            switch (logLevel)
            {
                case LogLevels.DEBUG:
                    Debug(message, ex);
                    break;
                case LogLevels.ERROR:
                    Error(message, ex);
                    break;
                case LogLevels.FATAL:
                    Fatal(message, ex);
                    break;
                case LogLevels.INFO:
                    Info(message, ex);
                    break;
                case LogLevels.WARN:
                    Warn(message, ex);
                    break;
                default:
                    throw new Exception("Invalid log level specified");
            }
        }

        /// <summary>
        /// Show a trace message at the console if TraceMode is true
        /// </summary>
        /// <param name="message"></param>
        protected static void ShowTrace(string message)
        {
            if (TraceMode)
            {
                ShowTraceMessage(message, true);
            }
        }

        /// <summary>
        /// Show a trace message at the console, optionally including date
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="includeDate">When true, include the date in the prefix; when false, only prefix with time</param>
        /// <param name="indentChars">Characters to use to indent the message</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        /// <remarks>Not dependent on TraceMode</remarks>
        public static void ShowTraceMessage(string message, bool includeDate, string indentChars = "  ", int emptyLinesBeforeMessage = 1)
        {
            var timeStamp = string.Format(includeDate ? "{0:yyyy-MM-dd hh:mm:ss.fff tt}" : "{0:hh:mm:ss.fff tt}", DateTime.Now);

            ConsoleMsgUtils.ShowDebugCustom(string.Format("{0}: {1}", timeStamp, message), indentChars, emptyLinesBeforeMessage);
        }

        #region "Methods to be defined in derived classes"

        /// <summary>
        /// Log a debug message (provided the log threshold is LogLevels.DEBUG)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public abstract void Debug(string message, Exception ex = null);

        /// <summary>
        /// Log an error message (provided the log threshold is LogLevels.ERROR or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public abstract void Error(string message, Exception ex = null);

        /// <summary>
        /// Log a fatal error message (provided the log threshold is LogLevels.FATAL or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public abstract void Fatal(string message, Exception ex = null);

        /// <summary>
        /// Log an informational message (provided the log threshold is LogLevels.INFO or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public abstract void Info(string message, Exception ex = null);

        /// <summary>
        /// Log a warning message (provided the log threshold is LogLevels.WARN or higher)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public abstract void Warn(string message, Exception ex = null);

        #endregion
    }
}
