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
        public const string LOG_FILE_DATE_CODE = "MM-dd-yyyy";

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

        private static readonly object mMessageQueueLock = new object();

        // ReSharper disable once UnusedMember.Local
        private static readonly Timer mQueueLogger = new Timer(LogMessagesCallback, null, 500, LOG_INTERVAL_MILLISECONDS);

        /// <summary>
        /// Tracks the number of successive dequeue failures
        /// </summary>
        private static int mFailedDequeueEvents;

        private static DateTime mLastCheckOldLogs = DateTime.UtcNow.AddDays(-2);

        /// <summary>
        /// When true, we need to rename existing log files because
        /// Only valid if AppendDateToBaseFileName is true
        /// </summary>
        /// <remarks>Log files are only renamed if a log message is actually logged</remarks>
        private static bool mNeedToRollLogFiles;

        #endregion

        #region "Member variables"

        /// <summary>
        /// Messages will be written to the log file if they are this value or lower
        /// </summary>
        private LogLevels mLogThresholdLevel;

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
        /// This is updated by ChangeLogFileBaseName or via the constructor
        /// </summary>
        /// <remarks>
        /// If AppendDateToBaseFileName is true, the actual log file name will have today's date appended to it, in the form mm-dd-yyyy.txt
        /// If AppendDateToBaseFileName is false, the actual log file name will be the base name plus .txt
        /// (unless the base name already has an extension, then the user-specified extension will be used)
        /// See also the comments for property AppendDateToBaseFileName
        /// </remarks>
        public static string BaseLogFileName { get; private set; } = "";

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
        /// Log file date
        /// </summary>
        public static DateTime LogFileDate { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Log file date (as a string)
        /// </summary>
        public static string LogFileDateText { get; private set; } = "";

        /// <summary>
        /// Current log file path
        /// </summary>
        /// <remarks>Update using ChangeLogFileBaseName</remarks>
        public static string LogFilePath { get; private set; } = "";

        /// <summary>
        /// Get or set the current log threshold level
        /// </summary>
        /// <remarks>
        /// If the LogLevel is DEBUG, all messages are logged
        /// If the LogLevel is INFO, all messages except DEBUG messages are logged
        /// If the LogLevel is ERROR, only FATAL and ERROR messages are logged
        /// </remarks>
        public LogLevels LogLevel
        {
            get => mLogThresholdLevel;
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
        /// Default constructor
        /// </summary>
        public FileLogger()
        {
            MaxRolledLogFiles = DEFAULT_MAX_ROLLED_LOG_FILES;
            LogLevel = LogLevels.INFO;
        }

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
            bool appendDateToBaseName = true,
            int maxRolledLogFiles = DEFAULT_MAX_ROLLED_LOG_FILES) : this(baseName, LogLevels.INFO, appendDateToBaseName, maxRolledLogFiles)
        {
        }

        /// <summary>
        /// Constructor that takes base log file name and log level
        /// </summary>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <param name="logLevel">Log threshold level</param>
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
            LogLevels logLevel,
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
        public static void ArchiveOldLogFilesNow()
        {
            ArchiveOldLogFilesNow(LogFilePath);
        }

        /// <summary>
        /// Look for log files over 32 days old that can be moved into a subdirectory
        /// </summary>
        /// <param name="logFilePath"></param>
        private static void ArchiveOldLogFilesNow(string logFilePath)
        {

            try
            {
                var currentLogFile = new FileInfo(logFilePath);
                var logDirectory = currentLogFile.Directory;
                if (logDirectory == null)
                {
                    WriteLog(LogLevels.WARN, "Error archiving old log files; cannot determine the parent directory of " + currentLogFile);
                    return;
                }

                mLastCheckOldLogs = DateTime.UtcNow;

                var archiveWarnings = ArchiveOldLogs(logDirectory, LOG_FILE_MATCH_SPEC, LOG_FILE_EXTENSION, LOG_FILE_DATE_REGEX);

                foreach (var warning in archiveWarnings)
                {
                    ConsoleMsgUtils.ShowWarning(warning);
                }
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error archiving old log files", ex);
            }
        }

        /// <summary>
        /// Look for log files over 32 days old that can be moved into a subdirectory
        /// </summary>
        /// <param name="logDirectory"></param>
        /// <param name="logFileMatchSpec">Wildcards to use to find date-based log files, for example ??-??-????</param>
        /// <param name="logFileExtension">Log file extension, for example .txt</param>
        /// <param name="logFileDateRegEx">
        /// RegEx pattern for extracting the log file date from the log file name
        /// The pattern must have named groups Year and Month
        /// The pattern can optionally have named group Day
        /// For an example, see constant LOG_FILE_DATE_REGEX
        /// </param>
        /// <returns>List of warning messages</returns>
        /// <remarks>
        /// If logFileMatchSpec is ??-??-???? and logFileExtension is .txt, will find files named *_??-??-????.txt
        /// </remarks>
        public static List<string> ArchiveOldLogs(
            DirectoryInfo logDirectory,
            string logFileMatchSpec,
            string logFileExtension,
            string logFileDateRegEx)
        {

            // Be careful when updating this method's arguments and how they're used,
            // since this method is called by the following classes
            //   PRISM.Logging.FileLogger
            //   PRISM.FileProcessor.ProcessFilesOrDirectoriesBase
            //   PRISM.clsFileLogger
            //   AnalysisManagerBase.clsMemoryUsageLogger

            var archiveWarnings = new List<string>();

            try
            {
                if (!logDirectory.Exists)
                    return archiveWarnings;

                var matchSpec = "*_" + logFileMatchSpec + logFileExtension;

                var logFiles = logDirectory.GetFiles(matchSpec);

                var dateMatcher = new Regex(logFileDateRegEx, RegexOptions.Compiled);

                foreach (var logFile in logFiles)
                {
                    var match = dateMatcher.Match(logFile.Name);

                    if (!match.Success)
                        continue;

                    var yearGroup = match.Groups["Year"];
                    var monthGroup = match.Groups["Month"];
                    var dayGroup = match.Groups["Day"];

                    DateTime logDate;
                    int logFileYear;
                    if (yearGroup.Success && monthGroup.Success)
                    {

                        logFileYear = int.Parse(match.Groups["Year"].Value);
                        var logFileMonth = int.Parse(match.Groups["Month"].Value);

                        if (dayGroup.Success)
                        {
                            var logFileDay = int.Parse(dayGroup.Value);
                            logDate = new DateTime(logFileYear, logFileMonth, logFileDay);
                        }
                        else
                        {
                            var daysInMonth = DateTime.DaysInMonth(logFileYear, logFileMonth);
                            logDate = new DateTime(logFileYear, logFileMonth, daysInMonth);
                        }
                    }
                    else
                    {
                        logDate = logFile.LastWriteTime;
                        logFileYear = logDate.Year;
                    }

                    if (DateTime.Now.Subtract(logDate).TotalDays <= OLD_LOG_FILE_AGE_THRESHOLD_DAYS)
                        continue;

                    var targetDirectory = new DirectoryInfo(Path.Combine(logDirectory.FullName, logFileYear.ToString()));
                    if (!targetDirectory.Exists)
                        targetDirectory.Create();

                    var targetFile = new FileInfo(Path.Combine(targetDirectory.FullName, logFile.Name));

                    try
                    {
                        if (targetFile.Exists)
                        {
                            // A file with the same name exists in the target directory
                            // If the source and target files are the same size and have the same SHA1 hash, delete the source file
                            // Otherwise, rename the file in the target directory, then move the source file

                            if (logFile.Length == targetFile.Length)
                            {
                                var logFileHash = HashUtilities.ComputeFileHashSha1(logFile.FullName);
                                var targetFileHash = HashUtilities.ComputeFileHashSha1(targetFile.FullName);
                                if (logFileHash.Equals(targetFileHash))
                                {
                                    ConsoleMsgUtils.ShowDebug("Identical old log file already exists in the target directory; deleting " + logFile.FullName);
                                    logFile.Delete();
                                    continue;
                                }
                            }

                            ConsoleMsgUtils.ShowDebug("Backing up identically named old log file: " + targetFile.FullName);
                            FileTools.BackupFileBeforeCopy(targetFile.FullName);

                            targetFile.Refresh();
                            if (targetFile.Exists)
                            {
                                ConsoleMsgUtils.ShowDebug("Backup/rename failed; cannot archive old log file " + logFile.FullName);
                                continue;
                            }
                        }

                        logFile.MoveTo(targetFile.FullName);
                    }
                    catch (Exception ex2)
                    {
                        archiveWarnings.Add("Error moving old log file to " + targetFile.FullName + ": " + ex2.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                archiveWarnings.Add("Error archiving old log files: " + ex.Message);
            }

            return archiveWarnings;
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
        /// When false, if baseName is a relative file path, the log file will be created in a subdirectory relative to the working directory
        /// </param>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        public static void ChangeLogFileBaseName(string baseName, bool appendDateToBaseName, bool relativeToEntryAssembly = true)
        {
            ShowStackTraceOnEnter("ChangeLogFileBaseName");

            if (string.IsNullOrWhiteSpace(baseName) && !string.IsNullOrWhiteSpace(BaseLogFileName))
            {
                ShowTrace("Leaving base log file name unchanged since new base name is empty");
                return;
            }

            if (!mMessageQueue.IsEmpty)
            {
                ShowTrace("Flushing pending messages prior to updating log file base name");
                FlushPendingMessages();
            }
            else
            {
                ShowTrace("Updating log file base name");
            }

            AppendDateToBaseFileName = appendDateToBaseName;

            if (Path.IsPathRooted(baseName))
            {
                ShowTrace("New log file base name is a rooted path; will use as-is: " + baseName);
                BaseLogFileName = baseName;
            }
            else if (relativeToEntryAssembly || string.IsNullOrWhiteSpace(baseName))
            {
                var appDirectoryPath = FileProcessor.ProcessFilesOrDirectoriesBase.GetAppDirectoryPath();
                string logFilePath;
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    logFilePath = Path.Combine(appDirectoryPath, DefaultLogFileName);
                    ShowTrace("New log file base name is empty; will use the default path, " + logFilePath);
                }
                else
                {
                    logFilePath = Path.Combine(appDirectoryPath, baseName);
                    ShowTrace("New log file will use a relative path: " + logFilePath);
                }
                BaseLogFileName = logFilePath;
            }
            else
            {
                ShowTrace("relativeToEntryAssembly is false; new log file path will be " + baseName);
                BaseLogFileName = baseName;
            }

            ChangeLogFileName();
        }

        /// <summary>
        /// Changes the base log file name
        /// </summary>
        private static void ChangeLogFileName()
        {
            LogFileDate = DateTime.Now.Date;
            LogFileDateText = LogFileDate.ToString(LOG_FILE_DATE_CODE);

            if (string.IsNullOrWhiteSpace(BaseLogFileName))
                BaseLogFileName = DefaultLogFileName;

            string newLogFilePath;
            if (AppendDateToBaseFileName)
            {
                if (Path.HasExtension(BaseLogFileName))
                {
                    var currentExtension = Path.GetExtension(BaseLogFileName);
                    newLogFilePath = Path.ChangeExtension(BaseLogFileName, null) + "_" + LogFileDateText + currentExtension;
                }
                else
                    newLogFilePath = BaseLogFileName + "_" + LogFileDateText + LOG_FILE_EXTENSION;
            }
            else
            {
                if (Path.HasExtension(BaseLogFileName))
                    newLogFilePath = string.Copy(BaseLogFileName);
                else
                    newLogFilePath = BaseLogFileName + LOG_FILE_EXTENSION;
            }

            LogFilePath = newLogFilePath;
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
            // Maximum time, in seconds, to continue to call StartLogQueuedMessages while the message queue is not empty
            const int MAX_TIME_SECONDS = 5;

            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow.Subtract(startTime).TotalSeconds < MAX_TIME_SECONDS)
            {
                StartLogQueuedMessages();

                if (mMessageQueue.IsEmpty)
                    break;

                ProgRunner.SleepMilliseconds(10);
            }

        }

        /// <summary>
        /// Callback invoked by the mQueueLogger timer
        /// </summary>
        /// <param name="state"></param>
        private static void LogMessagesCallback(object state)
        {
            ShowTrace("FileLogger.mQueueLogger callback raised");
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
                        var testFileDate = logMessage.MessageDateLocal.ToString(LOG_FILE_DATE_CODE);
                        if (!string.Equals(testFileDate, LogFileDateText))
                        {
                            ShowTrace(string.Format("Updating log file date from {0} to {1}", LogFileDateText, testFileDate));

                            LogFileDateText = testFileDate;
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
                        if (string.IsNullOrWhiteSpace(LogFilePath))
                        {
                            LogFilePath = DefaultLogFileName;
                        }

                        var logFile = new FileInfo(LogFilePath);
                        if (logFile.Directory == null)
                        {
                            // Create the log file in the current directory
                            LogFilePath = logFile.Name;
                            logFile = new FileInfo(LogFilePath);

                        }
                        else if (!logFile.Directory.Exists)
                        {
                            logFile.Directory.Create();
                        }

                        if (mNeedToRollLogFiles)
                        {
                            mNeedToRollLogFiles = false;
                            RollLogFiles(LogFileDate, LogFilePath);
                        }

                        ShowTrace(string.Format("Opening log file: {0}", LogFilePath));
                        writer = new StreamWriter(new FileStream(logFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                    }
                    writer.WriteLine(logMessage.GetFormattedMessage(TimestampFormat));

                    if (logMessage.MessageException != null)
                    {
                        writer.WriteLine(StackTraceFormatter.GetExceptionStackTraceMultiLine(logMessage.MessageException));
                    }

                    messagesWritten++;

                }

                if (DateTime.UtcNow.Subtract(mLastCheckOldLogs).TotalHours >= 24)
                {
                    mLastCheckOldLogs = DateTime.UtcNow;
                    ArchiveOldLogFilesNow();
                }

            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error writing queued log messages to disk: " + ex.Message, ex, false, false);
            }
            finally
            {
                writer?.Close();
                ShowTrace(string.Format("FileLogger writer closed; wrote {0} messages", messagesWritten));
            }
        }

        /// <summary>
        /// Reset the base log file name to an empty string and reset the cached log file dates
        /// </summary>
        /// <remarks>This method is only intended to be used by unit tests</remarks>
        public static void ResetLogFileName()
        {
            BaseLogFileName = string.Empty;
            LogFileDate = DateTime.MinValue;
            LogFileDateText = string.Empty;
            LogFilePath = string.Empty;
            mLastCheckOldLogs = DateTime.UtcNow.AddDays(-2);
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

                ShowTrace(string.Format("Renaming {0} old log file{1} in {2}", pendingRenames.Count, pluralSuffix, currentLogFile.DirectoryName));

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
        /// Update the log threshold level
        /// </summary>
        /// <param name="logLevel">Log threshold level</param>
        private void SetLogLevel(LogLevels logLevel)
        {
            mLogThresholdLevel = logLevel;
            IsDebugEnabled = mLogThresholdLevel >= LogLevels.DEBUG;
            IsErrorEnabled = mLogThresholdLevel >= LogLevels.ERROR;
            IsFatalEnabled = mLogThresholdLevel >= LogLevels.FATAL;
            IsInfoEnabled = mLogThresholdLevel >= LogLevels.INFO;
            IsWarnEnabled = mLogThresholdLevel >= LogLevels.WARN;
        }

        /// <summary>
        /// Show a stack trace when entering a method
        /// </summary>
        /// <param name="callingMethod"></param>
        private static void ShowStackTraceOnEnter(string callingMethod)
        {
            if (TraceMode)
            {
                ShowTrace(callingMethod + " " + StackTraceFormatter.GetCurrentStackTraceMultiLine());
            }
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
        /// Log a debug message
        /// (provided the log threshold is LogLevels.DEBUG; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Debug(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.DEBUG, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.DEBUG, message, ex);
        }

        /// <summary>
        /// Log an error message
        /// (provided the log threshold is LogLevels.ERROR or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Error(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.ERROR, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.ERROR, message, ex);
        }

        /// <summary>
        /// Log a fatal error message
        /// (provided the log threshold is LogLevels.FATAL or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Fatal(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.FATAL, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.FATAL, message, ex);
        }

        /// <summary>
        /// Log an informational message
        /// (provided the log threshold is LogLevels.INFO or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Info(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.INFO, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.INFO, message, ex);
        }

        /// <summary>
        /// Log a warning message
        /// (provided the log threshold is LogLevels.WARN or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Warn(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.WARN, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.WARN, message, ex);
        }

        /// <summary>
        /// Log a message (regardless of the log threshold level)
        /// </summary>
        /// <param name="logLevel">Log level of the message</param>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public static void WriteLog(LogLevels logLevel, string message, Exception ex = null)
        {
            var logMessage = new LogMessage(logLevel, message, ex);
            WriteLog(logMessage);
        }

        /// <summary>
        /// Log a message (regardless of the log threshold level)
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
            ShowTrace("Disposing FileLogger");
            FlushPendingMessages();
        }
    }
}
