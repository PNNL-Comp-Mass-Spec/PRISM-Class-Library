using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

// ReSharper disable UnusedMember.Global

namespace PRISM.Logging
{
    /// <summary>
    /// Logs messages to a file
    /// </summary>
    /// <remarks>
    /// The filename is date-based, for example DataProcessor_2020-08-02.txt
    /// If you want year-month-day based names, update your class to inherit ProcessFilesBase or ProcessDirectoriesBase
    /// </remarks>
    public class FileLogger : BaseLogger
    {
        // Ignore Spelling: prepended, Wildcards, yyyy, yyyy-MM-dd

        /// <summary>
        /// Archived log files directory name
        /// </summary>
        public const string ARCHIVED_LOG_FILES_DIRECTORY_NAME = "Archived";

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
        /// <remarks>
        /// Prior to January 2022, this was month-day-year based; it is now year-month-day
        /// Note that abstract classes ProcessFilesBase and ProcessDirectoriesBase create year-month-day log files
        /// </remarks>
        public const string LOG_FILE_DATE_CODE = "yyyy-MM-dd";

        private const string LOG_FILE_MATCH_SPEC = "????-??-??";

        private const string LOG_FILE_DATE_REGEX = @"(?<Year>\d{4,4})-(?<Month>\d+)-(?<Day>\d+)";

        private const string LOG_FILE_MATCH_SPEC_LEGACY = "??-??-????";

        private const string LOG_FILE_DATE_REGEX_LEGACY = @"(?<Month>\d+)-(?<Day>\d+)-(?<Year>\d{4,4})";

        /// <summary>
        /// Default log file extension
        /// </summary>
        /// <remarks>Appended to the log file name if BaseLogFileName does not have an extension</remarks>
        public const string LOG_FILE_EXTENSION = ".txt";

        private const int OLD_LOG_FILE_AGE_THRESHOLD_DAYS = 32;

        /// <summary>
        /// Directories with old log files (typically named by year) will be zipped this many days after January 1
        /// </summary>
        public const int OLD_LOG_DIRECTORY_AGE_THRESHOLD_DAYS = 90;

        private static readonly ConcurrentQueue<LogMessage> mMessageQueue = new();

        private static readonly object mMessageQueueLock = new();

        // ReSharper disable once UnusedMember.Local
        private static readonly Timer mQueueLogger = new(LogMessagesCallback, null, 500, LOG_INTERVAL_MILLISECONDS);

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

        /// <summary>
        /// Messages will be written to the log file if they are this value or lower
        /// </summary>
        private LogLevels mLogThresholdLevel;

        /// <summary>
        /// When true, the actual log file name will have today's date appended to it, in the form yyyy-mm-dd.txt
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
        /// If AppendDateToBaseFileName is true, the actual log file name will have today's date appended to it, in the form yyyy-mm-dd.txt
        /// If AppendDateToBaseFileName is false, the actual log file name will be the base name plus .txt
        /// (unless the base name already has an extension, then the user-specified extension will be used)
        /// See also the comments for property AppendDateToBaseFileName
        /// </remarks>
        public static string BaseLogFileName { get; private set; } = string.Empty;

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

        // ReSharper disable once GrammarMistakeInComment

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
        public static string LogFileDateText { get; private set; } = string.Empty;

        /// <summary>
        /// Current log file path
        /// </summary>
        /// <remarks>Update using ChangeLogFileBaseName</remarks>
        public static string LogFilePath { get; private set; } = string.Empty;

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

        /// <summary>
        /// When true, ArchiveOldLogFilesNow will also zip subdirectories with old log files
        /// </summary>
        public static bool ZipOldLogDirectories { get; set; } = true;

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
        /// <remarks>If baseName is null or empty, the log file name will be named ExecutableName_log.txt</remarks>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <param name="appendDateToBaseName">
        /// When true, the actual log file name will have today's date appended to it, in the form yyyy-mm-dd.txt
        /// When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
        /// </param>
        /// <param name="maxRolledLogFiles">
        /// Maximum number of old log files to keep (Ignored if appendDateToBaseName is True)
        /// </param>
        public FileLogger(
            string baseName,
            bool appendDateToBaseName = true,
            int maxRolledLogFiles = DEFAULT_MAX_ROLLED_LOG_FILES) : this(baseName, LogLevels.INFO, appendDateToBaseName, maxRolledLogFiles)
        {
        }

        /// <summary>
        /// Constructor that takes base log file name and log level
        /// </summary>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        /// <param name="baseName">Base log file name (or relative path)</param>
        /// <param name="logLevel">Log threshold level</param>
        /// <param name="appendDateToBaseName">
        /// When true, the actual log file name will have today's date appended to it, in the form yyyy-mm-dd.txt
        /// When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
        /// </param>
        /// <param name="maxRolledLogFiles">
        /// Maximum number of old log files to keep (Ignored if appendDateToBaseName is True)
        /// </param>
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
        /// <remarks>
        /// LogQueuedMessages calls this method every 24 hours
        /// </remarks>
        public static void ArchiveOldLogFilesNow()
        {
            ArchiveOldLogFilesNow(LogFilePath);
        }

        /// <summary>
        /// Look for log files over 32 days old that can be moved into a subdirectory
        /// </summary>
        /// <param name="logFilePath">Log file path</param>
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

                var archiveWarnings = new List<string>();

                var legacyFileWarnings = ArchiveOldLogs(logDirectory, LOG_FILE_MATCH_SPEC_LEGACY, LOG_FILE_EXTENSION, LOG_FILE_DATE_REGEX_LEGACY, false);
                archiveWarnings.AddRange(legacyFileWarnings);

                var warnings = ArchiveOldLogs(logDirectory, LOG_FILE_MATCH_SPEC, LOG_FILE_EXTENSION, LOG_FILE_DATE_REGEX, ZipOldLogDirectories);
                archiveWarnings.AddRange(warnings);

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
        /// <remarks>
        /// If logFileMatchSpec is ????-??-?? and logFileExtension is .txt, will find files named *_????-??-??.txt
        /// </remarks>
        /// <param name="logDirectory">Path to the directory with log files</param>
        /// <param name="logFileMatchSpec">Wildcards to use to find date-based log files, for example ????-??-??</param>
        /// <param name="logFileExtension">Log file extension, for example .txt</param>
        /// <param name="logFileDateRegEx">
        /// RegEx pattern for extracting the log file date from the log file name
        /// The pattern must have named groups Year and Month
        /// The pattern can optionally have named group Day
        /// For an example, see constant LOG_FILE_DATE_REGEX
        /// </param>
        /// <returns>List of warning messages</returns>
        public static IEnumerable<string> ArchiveOldLogs(
            DirectoryInfo logDirectory,
            string logFileMatchSpec,
            string logFileExtension,
            string logFileDateRegEx)
        {
            return ArchiveOldLogs(logDirectory, logFileMatchSpec, logFileExtension, logFileDateRegEx, ZipOldLogDirectories);
        }

        /// <summary>
        /// Look for log files over 32 days old that can be moved into a subdirectory
        /// </summary>
        /// <remarks>
        /// If logFileMatchSpec is ????-??-?? and logFileExtension is .txt, will find files named *_????-??-??.txt
        /// </remarks>
        /// <param name="logDirectory">Path to the directory with log files</param>
        /// <param name="logFileMatchSpec">Wildcards to use to find date-based log files, for example ????-??-??</param>
        /// <param name="logFileExtension">Log file extension, for example .txt</param>
        /// <param name="logFileDateRegEx">
        /// RegEx pattern for extracting the log file date from the log file name
        /// The pattern must have named groups Year and Month
        /// The pattern can optionally have named group Day
        /// For an example, see constant LOG_FILE_DATE_REGEX
        /// </param>
        /// <param name="zipOldDirectories">When true, zip old directories</param>
        /// <returns>List of warning messages</returns>
        private static IEnumerable<string> ArchiveOldLogs(
            DirectoryInfo logDirectory,
            string logFileMatchSpec,
            string logFileExtension,
            string logFileDateRegEx,
            bool zipOldDirectories)
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
                            // If the source and target files are the same size and have the same SHA-1 hash, delete the source file
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

                if (zipOldDirectories)
                {
                    var zipWarnings = ZipOldLogSubdirectories(logDirectory);
                    archiveWarnings.AddRange(zipWarnings);
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
        /// <remarks>
        /// If AppendDateToBaseFileName is true, will append today's date to the base name
        /// If baseName is a relative file path (aka is not rooted), the entry assembly's path will be prepended to baseName
        /// If baseName is null or empty, the log file name will be named DefaultLogFileName
        /// </remarks>
        /// <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
        /// <param name="baseName">Base log file name (or relative path)</param>
        public static void ChangeLogFileBaseName(string baseName)
        {
            ChangeLogFileBaseName(baseName, AppendDateToBaseFileName);
        }

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
        /// <param name="relativeToEntryAssembly">
        /// When true, if baseName is a relative file path (aka is not rooted), the entry assembly's path will be prepended to baseName
        /// When false, if baseName is a relative file path, the log file will be created in a subdirectory relative to the working directory
        /// </param>
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
                var appDirectoryPath = AppUtils.GetAppDirectoryPath();
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
                {
                    newLogFilePath = BaseLogFileName + "_" + LogFileDateText + LOG_FILE_EXTENSION;
                }
            }
            else
            {
                if (Path.HasExtension(BaseLogFileName))
                    newLogFilePath = BaseLogFileName;
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
        /// <para>
        /// There is no need to call this method if you create an instance of this class
        /// </para>
        /// <para>
        /// On the other hand, if you only call static methods in this class, call this method
        /// before ending the program to assure that all messages have been logged
        /// </para>
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

                AppUtils.SleepMilliseconds(10);
            }
        }

        /// <summary>
        /// Callback invoked by the mQueueLogger timer
        /// </summary>
        /// <param name="state">State</param>
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
                        mFailedDequeueEvents++;
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
                        ConsoleMsgUtils.ShowErrorCustom(
                            "Error defining the new log file name: " + ex2.Message,
                            ex2, false, false);
                    }

                    if (logMessage.LogLevel is LogLevels.ERROR or LogLevels.FATAL)
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
                ConsoleMsgUtils.ShowErrorCustom("Error writing queued log messages to disk: " + ex.Message, ex, false, false);
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
                    filePathToCheck = nextLogFilePath;
                }

                if (pendingRenames.Count == 0)
                    return;

                var pluralSuffix = pendingRenames.Count == 1 ? string.Empty : "s";

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
                        {
                            ConsoleMsgUtils.ShowErrorCustom(
                                string.Format("Error deleting old log file {0}: {1}", logFile.FullName, ex2.Message), ex2,
                                false,
                                false);
                        }
                        else
                        {
                            ConsoleMsgUtils.ShowErrorCustom(
                                string.Format("Error renaming old log file {0}: {1}", logFile.FullName, ex2.Message), ex2,
                                false,
                                false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowErrorCustom("Error rolling (renaming) old log files: " + ex.Message, ex, false, false);
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
        /// <param name="callingMethod">Calling method</param>
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

        /// <summary>
        /// Zip subdirectories with old log files
        /// </summary>
        /// <param name="logDirectory">Path to the directory with log files</param>
        /// <returns>List of warning messages</returns>
        private static IEnumerable<string> ZipOldLogSubdirectories(DirectoryInfo logDirectory)
        {
            var yearMatcher = new Regex(@"^\d{4,}$", RegexOptions.Compiled);

            var zipWarnings = new List<string>();

            try
            {
                var subDirectories = logDirectory.GetDirectories();

                var dateThresholdForZippingPreviousYearFiles = new DateTime(DateTime.Now.Year, 1, 1).AddDays(OLD_LOG_DIRECTORY_AGE_THRESHOLD_DAYS);

                var zipFilesToArchive = new Dictionary<string, FileInfo>();

                foreach (var subDir in subDirectories)
                {
                    if (!yearMatcher.IsMatch(subDir.Name))
                        continue;

                    if (!int.TryParse(subDir.Name, out var subDirYear))
                        continue;

                    if (subDirYear == DateTime.Now.Year ||
                        subDirYear == DateTime.Now.Year - 1 && DateTime.Now < dateThresholdForZippingPreviousYearFiles)
                    {
                        continue;
                    }

                    // The directory is old enough; zip the files

                    var zipFileName = subDir.Name + ".zip";
                    var zipFile = new FileInfo(Path.Combine(logDirectory.FullName, zipFileName));
                    var archivedZipFile = new FileInfo(Path.Combine(logDirectory.FullName, ARCHIVED_LOG_FILES_DIRECTORY_NAME, zipFileName));

                    if (zipFile.Exists)
                    {
                        zipWarnings.Add(string.Format(
                            "Not compressing old log directory {0} since the Zip file already exists at {1}",
                            subDir.Name, zipFile.FullName));

                        continue;
                    }

                    if (archivedZipFile.Exists)
                    {
                        zipWarnings.Add(string.Format(
                            "Not compressing old log directory {0} since the Zip file has already been archived to {1}",
                            subDir.Name, archivedZipFile.FullName));

                        continue;
                    }
                    try
                    {
                        ZipFile.CreateFromDirectory(subDir.FullName, zipFile.FullName);
                    }
                    catch (Exception ex2)
                    {
                        zipWarnings.Add(string.Format("Error creating zip file {0}: {1}", zipFile.FullName, ex2.Message));
                        continue;
                    }

                    // Verify that the zip file was created and has the expected number of files
                    zipFile.Refresh();

                    if (!zipFile.Exists)
                    {
                        zipWarnings.Add("Expected .zip file not found: " + zipFile.FullName);
                        continue;
                    }

                    using (var archive = ZipFile.OpenRead(zipFile.FullName))
                    {
                        var expectedFileCount = subDir.GetFiles().Length;
                        var fileCountInZip = archive.Entries.Count;

                        if (fileCountInZip < expectedFileCount)
                        {
                            zipWarnings.Add(string.Format(
                                "Zip file {0} has {1} files, but the subdirectory has {2} files: {3}",
                                zipFile.Name, fileCountInZip, expectedFileCount, subDir.FullName));

                            continue;
                        }

                        WriteLog(LogLevels.INFO, string.Format(
                            "Compressed {0} files in {1} to create {2}",
                            fileCountInZip, subDir.FullName, zipFile.FullName));
                    }

                    bool removeOldLogSubdirectory;

                    try
                    {
                        // Delete the files and the subdirectory
                        // While doing this, determine the date of the newest file
                        var newestLastWriteTime = DateTime.MinValue;

                        foreach (var oldLogFile in subDir.GetFiles())
                        {
                            if (oldLogFile.LastWriteTime > newestLastWriteTime)
                                newestLastWriteTime = oldLogFile.LastWriteTime;

                            oldLogFile.Delete();
                        }

                        if (newestLastWriteTime > DateTime.MinValue)
                        {
                            // Update the date of the zip file to newestLastWriteTime
                            zipFile.Refresh();
                            zipFile.LastWriteTime = newestLastWriteTime;
                        }

                        removeOldLogSubdirectory = true;
                    }
                    catch (Exception ex2)
                    {
                        zipWarnings.Add("Error deleting old log files after successfully creating the zip file: " + ex2.Message);
                        removeOldLogSubdirectory = false;
                    }

                    try
                    {
                        if (removeOldLogSubdirectory && subDir.GetFiles("*", SearchOption.AllDirectories).Length == 0)
                        {
                            subDir.Delete();
                        }
                    }
                    catch (Exception ex2)
                    {
                        zipWarnings.Add(string.Format("Error removing empty subdirectory {0}: {1}", subDir.FullName, ex2.Message));
                    }

                    zipFilesToArchive.Add(zipFile.FullName, zipFile);
                }

                try
                {
                    // Move the zip file (plus any other year-named .zip files) into a subdirectory named Archived
                    var archiveDirectory = new DirectoryInfo(Path.Combine(logDirectory.FullName, ARCHIVED_LOG_FILES_DIRECTORY_NAME));

                    if (!archiveDirectory.Exists)
                    {
                        archiveDirectory.Create();
                    }

                    // Look for additional zipped log files
                    foreach (var zipFile in logDirectory.GetFiles("*.zip", SearchOption.TopDirectoryOnly))
                    {
                        if (!yearMatcher.IsMatch(Path.GetFileNameWithoutExtension(zipFile.Name)))
                            continue;

                        if (zipFilesToArchive.Keys.Contains(zipFile.FullName))
                            continue;

                        zipFilesToArchive.Add(zipFile.FullName, zipFile);
                    }

                    foreach (var zipFile in zipFilesToArchive.Values)
                    {
                        var targetFile = new FileInfo(Path.Combine(archiveDirectory.FullName, zipFile.Name));

                        if (targetFile.Exists)
                        {
                            zipWarnings.Add(string.Format(
                                "Not archiving Zip file {0} since an existing Zip file was found in directory {1}",
                                zipFile.Name, archiveDirectory.FullName));

                            continue;
                        }

                        zipFile.Refresh();

                        if (!zipFile.Exists)
                        {
                            zipWarnings.Add(string.Format("Not archiving Zip file {0} since it no longer exists", zipFile.Name));
                            continue;
                        }

                        zipFile.MoveTo(targetFile.FullName);
                    }
                }
                catch (Exception ex2)
                {
                    zipWarnings.Add("Error moving zipped log files into the archive directory: " + ex2.Message);
                }
            }
            catch (Exception ex)
            {
                zipWarnings.Add("Error zipping subdirectory with old log files: " + ex.Message);
            }

            return zipWarnings;
        }

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

        // ReSharper disable once GrammarMistakeInComment

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

        // ReSharper disable once GrammarMistakeInComment

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

        // ReSharper disable once GrammarMistakeInComment

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
        /// <param name="message">Message</param>
        /// <param name="ex">Exception</param>
        public static void WriteLog(LogLevels logLevel, string message, Exception ex = null)
        {
            var logMessage = new LogMessage(logLevel, message, ex);
            WriteLog(logMessage);
        }

        /// <summary>
        /// Log a message (regardless of the log threshold level)
        /// </summary>
        /// <param name="logMessage">Message</param>
        public static void WriteLog(LogMessage logMessage)
        {
            mMessageQueue.Enqueue(logMessage);
        }

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
