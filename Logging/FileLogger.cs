using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace PRISM.Logging
{
    /// <summary>
    /// Logs messages to a file
    /// </summary>
    public class FileLogger : BaseLogger
    {
        #region "Constants"

        /// <summary>
        /// Default number of old log files to keep when AppendDateToBaseFileName is false
        /// </summary>
        private const int DEFAULT_MAX_ROLLED_LOG_FILES = 5;

        /// <summary>
        /// Interval, in milliseconds, between flushing log messages to disk
        /// </summary>
        private const int LOG_INTERVAL_MILLISECONDS = 500;

        /// <summary>
        /// Date format for log file names
        /// </summary>
        public const string LOG_FILE_DATECODE = "MM-dd-yyyy";

        private const string LOG_FILE_MATCH_SPEC = "??-??-????";

        private const string LOG_FILE_DATE_REGEX = @"(?<Month>\d+)-(?<Day>\d+)-(?<Year>\d{4,4})";

        /// <summary>
        /// Default log file extension
        /// </summary>
        /// <remarks>Appended to the log file name if BaseLogFileName does not have an extension</remarks>
        public const string LOG_FILE_EXTENSION = ".txt";

        private const int OLD_LOG_FILE_AGE_THRESHOLD_DAYS = 32;

        #endregion

        #region "Static variables"

        private static readonly ConcurrentQueue<LogMessage> mMessageQueue = new ConcurrentQueue<LogMessage>();

        private static Object mMessageQueueLock = new Object();

        // ReSharper disable once UnusedMember.Local
        private static readonly Timer mQueueLogger = new Timer(LogMessagesCallback, null, 500, LOG_INTERVAL_MILLISECONDS);

        /// <summary>
        /// Tracks the number of successive dequeue failures
        /// </summary>
        private static int mFailedDequeueEvents;

        /// <summary>
        /// Base log file name (or relative path)
        /// </summary>
        /// <remarks>This is updated by ChangeLogFileBaseName or via the constructor</remarks>
        private static string mBaseLogFileName = "";

        /// <summary>
        /// Log file date
        /// </summary>
        private static DateTime mLogFileDate = DateTime.MinValue;

        /// <summary>
        /// Log file date (as a string)
        /// </summary>
        private static string mLogFileDateText = "";

        /// <summary>
        /// Relative file path to the current log file
        /// </summary>
        /// <remarks>update this using method ChangeLogFileName</remarks>
        private static string mLogFilePath = "";

        private static DateTime mLastCheckOldLogs = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// When true, we need to rename existing log files because
        /// Only valid if AppendDateToBaseFileName is true
        /// </summary>
        /// <remarks>Log files are only renamed if a log message is actually logged</remarks>
        private static bool mNeedToRollLogFiles;

        #endregion

        #region "Member variables"

        private LogLevels mLogLevel;

        #endregion

        #region "Properties"

        /// <summary>
        /// When true, the actual log file name will have today's date appended to it, in the form mm-dd-yyyy.txt
        /// When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
        /// If a file exists with that name, but was last modified before today, it will be renamed to BaseName.txt.1
        /// </summary>
        /// <remarks>
        /// Other, existing log files will also be renamed, keeping up to MaxRolledLogFiles old log files
        /// </remarks>
        public static bool AppendDateToBaseFileName { get; private set; } = true;

        /// <summary>
        /// Base log file name
        /// </summary>
        /// <remarks>
        /// If AppendDateToBaseFileName is true, the actual log file name will have today's date appended to it, in the form mm-dd-yyyy.txt
        /// If AppendDateToBaseFileName is false, the actual log file name will be the base name plus .txt
        /// (unless the base name already has an extension, then the user-specified extension will be used)
        /// See also the comments for property AppendDateToBaseFileName
        /// </remarks>
        public static string BaseLogFileName => mBaseLogFileName;

        /// <summary>
        /// Default log file name
        /// </summary>
        /// <remarks>Used when BaseLogFileName is empty</remarks>
        private static string DefaultLogFileName => Path.GetFileNameWithoutExtension(ExecutableName) + "_log.txt";

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
        /// Current log file path
        /// </summary>
        public static string LogFilePath => mLogFilePath;

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
        /// Maximum number of old log files to keep
        /// Ignored if AppendDateToBaseFileName is True
        /// </summary>
        /// <remarks>Defaults to 5; minimum value is 1</remarks>
        public static int MaxRolledLogFiles { get; set; } = DEFAULT_MAX_ROLLED_LOG_FILES;

        #endregion

        /// <summary>
        /// Constructor that takes base log file name and appendDateToBaseName
        /// </summary>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <param name="appendDateToBaseName">
        /// When true, the actual log file name will have today's date appended to it, in the form mm-dd-yyyy.txt
        /// When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
        /// </param>
        /// <param name="maxRolledLogFiles">
        /// Maximum number of old log files to keep (Ignored if appendDateToBaseName is True)
        /// </param>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        public FileLogger(
            string baseName,
            bool appendDateToBaseName,
            int maxRolledLogFiles = DEFAULT_MAX_ROLLED_LOG_FILES) : this(baseName, LogLevels.INFO, appendDateToBaseName, maxRolledLogFiles)
        {
        }

        /// <summary>
        /// Constructor with default values for all parameters
        /// </summary>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <param name="logLevel">Log level</param>
        /// <param name="appendDateToBaseName">
        /// When true, the actual log file name will have today's date appended to it, in the form mm-dd-yyyy.txt
        /// When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
        /// </param>
        /// <param name="maxRolledLogFiles">
        /// Maximum number of old log files to keep (Ignored if appendDateToBaseName is True)
        /// </param>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        public FileLogger(
            string baseName = "",
            LogLevels logLevel = LogLevels.INFO,
            bool appendDateToBaseName = true,
            int maxRolledLogFiles = DEFAULT_MAX_ROLLED_LOG_FILES)
        {
            MaxRolledLogFiles = maxRolledLogFiles;

            ChangeLogFileBaseName(baseName, appendDateToBaseName);

            LogLevel = logLevel;
        }

        /// <summary>
        /// Look for log files over 32 days old that can be moved into a subdirectory
        /// </summary>
        /// <param name="logFilePath"></param>
        private static void ArchiveOldLogs(string logFilePath)
        {
            var targetPath = "??";

            try
            {
                var currentLogFile = new FileInfo(logFilePath);

                var matchSpec = "*_" + LOG_FILE_MATCH_SPEC + LOG_FILE_EXTENSION;

                var logDirectory = currentLogFile.Directory;
                if (logDirectory == null)
                {

                    WriteLog(LogLevels.WARN, "Error archiving old log files; cannot determine the parent directory of " + currentLogFile);
                    return;
                }

                mLastCheckOldLogs = DateTime.UtcNow;

                var logFiles = logDirectory.GetFiles(matchSpec);

                var matcher = new Regex(LOG_FILE_DATE_REGEX, RegexOptions.Compiled);

                foreach (var logFile in logFiles)
                {
                    var match = matcher.Match(logFile.Name);

                    if (!match.Success)
                        continue;

                    var logFileYear = int.Parse(match.Groups["Year"].Value);
                    var logFileMonth = int.Parse(match.Groups["Month"].Value);
                    var logFileDay = int.Parse(match.Groups["Day"].Value);

                    var logDate = new DateTime(logFileYear, logFileMonth, logFileDay);

                    if (DateTime.Now.Subtract(logDate).TotalDays <= OLD_LOG_FILE_AGE_THRESHOLD_DAYS)
                        continue;

                    var targetDirectory = new DirectoryInfo(Path.Combine(logDirectory.FullName, logFileYear.ToString()));
                    if (!targetDirectory.Exists)
                        targetDirectory.Create();

                    targetPath = Path.Combine(targetDirectory.FullName, logFile.Name);

                    logFile.MoveTo(targetPath);
                }
            }
            catch (Exception ex)
            {
                WriteLog(LogLevels.ERROR, "Error moving old log file to " + targetPath, ex);
            }
        }

        /// <summary>
        /// Update the log file's base name (or relative path)
        /// </summary>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <remarks>
        /// If AppendDateToBaseFileName is true, will append today's date to the base name
        /// If baseName is a relative file path (aka is not rooted), the entry assembly's path will be prepended to baseName
        /// If baseName is null or empty, the log file name will be named DefaultLogFileName
        /// </remarks>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        public static void ChangeLogFileBaseName(string baseName)
        {
            ChangeLogFileBaseName(baseName, AppendDateToBaseFileName);
        }

        /// <summary>
        /// Update the log file's base name (or relative path)
        /// However, if appendDateToBaseName is false, baseName is the full path to the log file
        /// </summary>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <param name="appendDateToBaseName">
        /// When true, the actual log file name will have today's date appended to it, in the form mm-dd-yyyy.txt
        /// When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
        /// </param>
        /// <param name="relativeToEntryAssembly">
        /// When true, if baseName is a relative file path (aka is not rooted), the entry assembly's path will be prepended to baseName
        /// When false, if baseName is a relative file path, the log file will be created in a subfolder relative to the working directory
        /// </param>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        public static void ChangeLogFileBaseName(string baseName, bool appendDateToBaseName, bool relativeToEntryAssembly = true)
        {
            if (!mMessageQueue.IsEmpty)
                FlushPendingMessages();

            AppendDateToBaseFileName = appendDateToBaseName;

            if (relativeToEntryAssembly && (string.IsNullOrWhiteSpace(baseName) || !Path.IsPathRooted(baseName)))
            {
                var appFolderPath = FileProcessor.ProcessFilesOrFoldersBase.GetAppFolderPath();

                if (string.IsNullOrWhiteSpace(baseName))
                    mBaseLogFileName = Path.Combine(appFolderPath, DefaultLogFileName);
                else
                    mBaseLogFileName = Path.Combine(appFolderPath, baseName);
            }
            else
            {
                mBaseLogFileName = baseName;
            }

            ChangeLogFileName();
        }

        /// <summary>
        /// Changes the base log file name
        /// </summary>
        private static void ChangeLogFileName()
        {
            mLogFileDate = DateTime.Now.Date;
            mLogFileDateText = mLogFileDate.ToString(LOG_FILE_DATECODE);

            if (string.IsNullOrWhiteSpace(mBaseLogFileName))
                mBaseLogFileName = DefaultLogFileName;

            string newLogFilePath;
            if (AppendDateToBaseFileName)
            {
                if (Path.HasExtension(mBaseLogFileName))
                {
                    var currentExtension = Path.GetExtension(mBaseLogFileName);
                    newLogFilePath = Path.ChangeExtension(mBaseLogFileName, null) + "_" + mLogFileDateText + currentExtension;
                }
                else
                    newLogFilePath = mBaseLogFileName + "_" + mLogFileDateText + LOG_FILE_EXTENSION;
            }
            else
            {
                if (Path.HasExtension(mBaseLogFileName))
                    newLogFilePath = string.Copy(mBaseLogFileName);
                else
                    newLogFilePath = mBaseLogFileName + LOG_FILE_EXTENSION;
            }

            mLogFilePath = newLogFilePath;
            mNeedToRollLogFiles = !AppendDateToBaseFileName;
        }

        /// <summary>
        /// Immediately write out any queued messages (using the current thread)
        /// </summary>
        /// <remarks>
        /// There is no need to call this method if you create an instance of this class.
        /// On the other hand, if you only call static methods in this class, call this method
        /// before ending the program to assure that all messages have been logged.
        /// </remarks>
        public static void FlushPendingMessages()
        {
            StartLogQueuedMessages();
        }

        /// <summary>
        /// Callback invoked by the mQueueLogger timer
        /// </summary>
        /// <param name="state"></param>
        private static void LogMessagesCallback(object state)
        {
            ShowTraceMessage("FileLogger.mQueueLogger callback raised");
            StartLogQueuedMessages();
        }

        private static void LogQueuedMessages()
        {
            StreamWriter writer = null;
            var messagesWritten = 0;

            try
            {

                while (!mMessageQueue.IsEmpty)
                {
                    if (!mMessageQueue.TryDequeue(out var logMessage))
                    {
                        mFailedDequeueEvents += 1;
                        LogDequeueError(mFailedDequeueEvents, mMessageQueue.Count);
                        return;
                    }

                    mFailedDequeueEvents = 0;

                    try
                    {
                        // Check to determine if a new file should be started
                        var testFileDate = logMessage.MessageDateLocal.ToString(LOG_FILE_DATECODE);
                        if (!string.Equals(testFileDate, mLogFileDateText))
                        {
                            mLogFileDateText = testFileDate;
                            ChangeLogFileName();

                            writer?.Close();
                            writer = null;
                        }
                    }
                    catch (Exception ex2)
                    {
                        ConsoleMsgUtils.ShowError("Error defining the new log file name: " + ex2.Message, ex2, false, false);
                    }

                    if (logMessage.LogLevel == LogLevels.ERROR || logMessage.LogLevel == LogLevels.FATAL)
                    {
                        MostRecentErrorMessage = logMessage.Message;
                    }

                    if (writer == null)
                    {
                        if (string.IsNullOrWhiteSpace(mLogFilePath))
                        {
                            mLogFilePath = DefaultLogFileName;
                        }

                        var logFile = new FileInfo(mLogFilePath);
                        if (logFile.Directory == null)
                        {
                            // Create the log file in the current directory
                            mLogFilePath = logFile.Name;
                            logFile = new FileInfo(mLogFilePath);

                        }
                        else if (!logFile.Directory.Exists)
                        {
                            logFile.Directory.Create();
                        }

                        if (mNeedToRollLogFiles)
                        {
                            mNeedToRollLogFiles = false;
                            RollLogFiles(mLogFileDate, mLogFilePath);
                        }

                        ShowTraceMessage(string.Format("Opening log file: {0}", mLogFilePath));
                        writer = new StreamWriter(new FileStream(logFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                    }
                    writer.WriteLine(logMessage.GetFormattedMessage(TimestampFormat));

                    if (logMessage.MessageException != null)
                    {
                        writer.WriteLine(clsStackTraceFormatter.GetExceptionStackTraceMultiLine(logMessage.MessageException));
                    }

                    messagesWritten++;

                }

                if (DateTime.UtcNow.Subtract(mLastCheckOldLogs).TotalHours > 24)
                {
                    mLastCheckOldLogs = DateTime.UtcNow;

                    ArchiveOldLogs(mLogFilePath);
                }

            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error writing queued log messages to disk: " + ex.Message, ex, false, false);
            }
            finally
            {
                writer?.Close();
                ShowTraceMessage(string.Format("FileLogger writer closed; wrote {0} messages", messagesWritten));
            }
        }

        /// <summary>
        /// Rename existing log files if required
        /// </summary>
        /// <param name="currentDate">Current date (local time)</param>
        /// <param name="currentLogFilePath">Current log file name (for today)</param>
        private static void RollLogFiles(DateTime currentDate, string currentLogFilePath)
        {
            try
            {
                var currentLogFile = new FileInfo(currentLogFilePath);
                if (!currentLogFile.Exists)
                {
                    // Nothing to do
                    return;
                }

                if (currentLogFile.LastWriteTime >= currentDate.Date)
                {
                    // The log file is from today (or from the future)
                    // Nothing to do
                    return;
                }

                var pendingRenames = new Dictionary<int, KeyValuePair<FileInfo, string>>();

                var filePathToCheck = currentLogFile.FullName;
                var nextFileSuffix = 1;

                var oldVersionsToKeep = Math.Max(1, MaxRolledLogFiles);

                while (nextFileSuffix <= oldVersionsToKeep && File.Exists(filePathToCheck))
                {
                    // Append .1 or .2 or .3 etc.
                    var nextLogFilePath = currentLogFile.FullName + "." + nextFileSuffix;
                    pendingRenames.Add(nextFileSuffix, new KeyValuePair<FileInfo, string>(new FileInfo(filePathToCheck), nextLogFilePath));

                    nextFileSuffix++;
                    filePathToCheck = string.Copy(nextLogFilePath);
                }

                if (pendingRenames.Count == 0)
                    return;

                var pluralSuffix = pendingRenames.Count == 1 ? "" : "s";

                ShowTraceMessage(string.Format("Renaming {0} old log file{1} in {2}", pendingRenames.Count, pluralSuffix, currentLogFile.DirectoryName));

                foreach (var item in (from key in pendingRenames.Keys orderby key select key).Reverse())
                {
                    var logFile = pendingRenames[item].Key;

                    try
                    {
                        if (item > oldVersionsToKeep)
                        {
                            logFile.Delete();
                        }
                        else
                        {
                            var newPath = pendingRenames[item].Value;
                            if (File.Exists(newPath))
                            {
                                ConsoleMsgUtils.ShowError(
                                    "Existing old log file will be overwritten (this likely indicates a code logic error): " + newPath);
                                File.Delete(newPath);
                            }

                            logFile.MoveTo(newPath);
                        }
                    }
                    catch (Exception ex2)
                    {
                        if (item >= oldVersionsToKeep)
                            ConsoleMsgUtils.ShowError(
                                string.Format("Error deleting old log file {0}: {1}", logFile.FullName, ex2.Message), ex2, false, false);
                        else
                            ConsoleMsgUtils.ShowError(
                                string.Format("Error renaming old log file {0}: {1}", logFile.FullName, ex2.Message), ex2, false, false);
                    }
                }

            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error rolling (renaming) old log files: " + ex.Message, ex, false, false);
            }
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
        }

        #endregion

        /// <summary>
        /// Class is disposing; write out any queued messages
        /// </summary>
        ~FileLogger()
        {
            ShowTraceMessage("Disposing FileLogger");
            FlushPendingMessages();
        }
    }
}
