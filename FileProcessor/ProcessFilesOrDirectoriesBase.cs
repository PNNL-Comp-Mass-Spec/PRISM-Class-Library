using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace PRISM.FileProcessor
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Base class for both ProcessFilesBase and ProcessDirectoriesBase
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public abstract class ProcessFilesOrDirectoriesBase : EventNotifier
    {
        #region "Constants and Enums"

        private const string LOG_FILE_EXTENSION = ".txt";

        private const string LOG_FILE_SUFFIX = "_log";

        private const string LOG_FILE_TIMESTAMP_FORMAT = "yyyy-MM-dd";

        private const string LOG_FILE_MATCH_SPEC = "????-??-??";

        private const string LOG_FILE_DATE_REGEX = @"(?<Year>\d{4,4})-(?<Month>\d+)-(?<Day>\d+)";

        private const int MAX_LOG_DATA_CACHE_SIZE = 100000;

        /// <summary>
        /// Message type enums
        /// </summary>
        protected enum MessageTypeConstants
        {
            /// <summary>
            /// Normal message
            /// </summary>
            Normal = 0,

            /// <summary>
            /// Error message
            /// </summary>
            ErrorMsg = 1,

            /// <summary>
            /// Warning message
            /// </summary>
            Warning = 2,

            /// <summary>
            /// Debugging message
            /// </summary>
            Debug = -1,

            /// <summary>
            /// Message that should not be output
            /// </summary>
            Suppress = -100
        }

        /// <summary>
        /// Message type enums
        /// </summary>
        [Obsolete("Use MessageTypeConstants")]
        protected enum eMessageTypeConstants
        {
            /// <summary>
            /// Normal message
            /// </summary>
            Normal = 0,

            /// <summary>
            /// Error message
            /// </summary>
            ErrorMsg = 1,

            /// <summary>
            /// Warning message
            /// </summary>
            Warning = 2,

            /// <summary>
            /// Debugging message
            /// </summary>
            Debug = -1,

            /// <summary>
            /// Message that should not be output
            /// </summary>
            Suppress = -100
        }

        /// <summary>
        /// Log levels, specifying the severity of messages to be logged
        /// </summary>
        /// <remarks>
        /// Logging methods in this class support duplicateHoldoffHours
        /// In contrast, Logging.FileLogger does not support skipping duplicate log messages
        /// </remarks>
        public enum LogLevel
        {
            /// <summary>
            /// Output suppressed messages with everything else
            /// </summary>
            Suppress = -100,

            /// <summary>
            /// All messages
            /// </summary>
            Debug = -1,

            /// <summary>
            /// All normal, warning, and error messages
            /// </summary>
            Normal = 0,

            /// <summary>
            /// Warning and error messages
            /// </summary>
            Warning = 2,

            /// <summary>
            /// Error messages only
            /// </summary>
            Error = 3,
        }

        #endregion

        #region "Classwide Variables"

        /// <summary>
        /// File date (program date)
        /// </summary>
        protected string mFileDate;

        /// <summary>
        /// Base path for the log file
        /// </summary>
        /// <remarks>Only used if the log file path is auto-defined</remarks>
        private string mLogFileBasePath = string.Empty;

        /// <summary>
        /// Log file path (relative or absolute path)
        /// </summary>
        /// <remarks>Leave blank to auto-define</remarks>
        protected string mLogFilePath = string.Empty;

        /// <summary>
        /// Time when the a new log file should be created (12 am tomorrow)
        /// </summary>
        /// <remarks>Ony used if mLogFileUsesDateStamp is true</remarks>
        protected DateTime mLogFileRolloverTime = DateTime.Now;

        /// <summary>
        /// True if the auto-defined log file should have the current date appended to the name
        /// </summary>
        /// <remarks>Only used if LogFilePath is initially blank</remarks>
        protected bool mLogFileUsesDateStamp = true;

        /// <summary>
        /// Log file writer
        /// </summary>
        protected StreamWriter mLogFile;

        /// <summary>
        /// Output directory path
        /// </summary>
        /// <remarks>This variable is updated when CleanupFilePaths() is called</remarks>
        protected string mOutputDirectoryPath = string.Empty;

        private static DateTime mLastCheckOldLogs = DateTime.UtcNow.AddDays(-2);

        private string mLastMessage = "";

        private DateTime mLastReportTime = DateTime.UtcNow;

        private DateTime mLastErrorShown = DateTime.MinValue;

        /// <summary>
        /// Progress was reset
        /// </summary>
        public event ProgressResetEventHandler ProgressReset;


        /// <summary>
        /// Delegate to indicate that progress was reset
        /// </summary>
        public delegate void ProgressResetEventHandler();

        /// <summary>
        /// Processing is complete
        /// </summary>
        public event ProgressCompleteEventHandler ProgressComplete;

        /// <summary>
        /// Delegate to indicate that processing is complete
        /// </summary>
        public delegate void ProgressCompleteEventHandler();

        /// <summary>
        /// Percent complete, value between 0 and 100, but can contain decimal percentage values
        /// </summary>
        protected float mProgressPercentComplete;

        /// <summary>
        /// Keys in this dictionary are the log type and message (separated by an underscore)
        /// Values are the most recent time the message was logged
        /// </summary>
        /// <remarks></remarks>
        private readonly Dictionary<string, DateTime> mLogDataCache;

        private MessageTypeConstants mProgressMessageType = MessageTypeConstants.Normal;

        #endregion

        #region "Interface Functions"

        /// <summary>
        /// True if processing should be aborted
        /// </summary>
        public bool AbortProcessing { get; set; }

        /// <summary>
        /// When true, auto-move old log files to a subdirectory based on the log file date
        /// </summary>
        /// <remarks>Only valid if the log file name was auto-defined (meaning LogFilePath was initially blank)</remarks>
        public bool ArchiveOldLogFiles { get; set; } = true;

        /// <summary>
        /// Version of the executing assembly
        /// </summary>
        public string FileVersion => GetVersionForExecutingAssembly();

        /// <summary>
        /// File date (aka program date)
        /// </summary>
        public string FileDate => mFileDate;

        /// <summary>
        /// This option applies when processing files or directories matched with a wildcard
        /// </summary>
        public bool IgnoreErrorsWhenUsingWildcardMatching { get; set; }

        /// <summary>
        /// Log file path (relative or absolute path)
        /// </summary>
        /// <remarks>Leave blank to auto-define</remarks>
        public string LogFilePath
        {
            get => mLogFilePath;
            set => mLogFilePath = value ?? string.Empty;
        }

        /// <summary>
        /// Log directory path (ignored if LogFilePath is rooted)
        /// </summary>
        /// <remarks>
        /// If blank, mOutputDirectoryPath will be used
        /// If mOutputDirectoryPath is also blank, the log file is created in the same directory as the executing assembly
        /// </remarks>
        public string LogDirectoryPath { get; set; } = string.Empty;

        /// <summary>
        /// Log directory path (ignored if LogFilePath is rooted)
        /// </summary>
        [Obsolete("Use LogDirectoryPath")]
        public string LogFolderPath
        {
            get => LogDirectoryPath;
            set => LogDirectoryPath = value;
        }

        /// <summary>
        /// True to log messages to a file
        /// </summary>
        public bool LogMessagesToFile { get; set; }

        /// <summary>
        /// Description of the current task
        /// </summary>
        public virtual string ProgressStepDescription { get; private set; } = string.Empty;

        /// <summary>
        /// Percent complete, value between 0 and 100, but can contain decimal percentage values
        /// </summary>
        public float ProgressPercentComplete => Convert.ToSingle(Math.Round(mProgressPercentComplete, 2));

        /// <summary>
        /// Deprecated property (previously, when true, events would be raised but when false, exceptions would be thrown)
        /// </summary>
        [Obsolete("Deprecated")]
        public bool ShowMessages { get; set; } = true;

        /// <summary>
        /// When true, if an error occurs, a message will be logged, then the event will be re-thrown
        /// </summary>
        public bool ReThrowEvents { get; set; } = false;

        /// <summary>
        /// Minimum severity of messages to log
        /// </summary>
        public LogLevel LoggingLevel { get; set; } = LogLevel.Normal;

        /// <summary>
        /// The severity of progress output; normally only used to suppress progress output in logs by setting this to <see cref="LogLevel.Suppress"/>
        /// </summary>
        public LogLevel ProgressOutputLevel
        {
            get => ConvertMessageTypeToLogLevel(mProgressMessageType);
            set => mProgressMessageType = ConvertLogLevelToMessageType(value);
        }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        protected ProcessFilesOrDirectoriesBase()
        {
            // Keys in this dictionary are the log type and message (separated by an underscore)
            // Values are the most recent time the message was logged
            mLogDataCache = new Dictionary<string, DateTime>();
        }

        /// <summary>
        /// Abort processing as soon as possible
        /// </summary>
        public virtual void AbortProcessingNow()
        {
            AbortProcessing = true;
        }

        /// <summary>
        /// Look for log files over 32 days old that can be moved into a subdirectory
        /// </summary>
        /// <remarks>Only valid if the log file name was auto-defined</remarks>
        public void ArchiveOldLogFilesNow()
        {

            if (string.IsNullOrWhiteSpace(mLogFileBasePath))
                return;

            try
            {
                var baseLogFile = new FileInfo(mLogFileBasePath);
                var logDirectory = baseLogFile.Directory;
                if (logDirectory == null)
                {
                    ShowWarning("Error archiving old log files; cannot determine the parent directory of " + mLogFileBasePath);
                    return;
                }

                mLastCheckOldLogs = DateTime.UtcNow;

                var archiveWarnings = Logging.FileLogger.ArchiveOldLogs(logDirectory, LOG_FILE_MATCH_SPEC, LOG_FILE_EXTENSION, LOG_FILE_DATE_REGEX);

                foreach (var warning in archiveWarnings)
                {
                    ShowWarning(warning, false);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error archiving old log files: " + ex.Message, false);
            }
        }

        /// <summary>
        /// Cleanup paths
        /// </summary>
        /// <param name="inputFileOrDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        protected abstract void CleanupPaths(ref string inputFileOrDirectoryPath, ref string outputDirectoryPath);

        /// <summary>
        /// Close the log file
        /// </summary>
        public void CloseLogFileNow()
        {
            if (mLogFile != null)
            {
                mLogFile.Close();
                mLogFile = null;

                GarbageCollectNow();
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Sets the log file path (<see cref="mLogFilePath"/>),
        /// according to data in <see cref="mLogFilePath"/>,
        /// <see cref="mLogFileUsesDateStamp"/>, and <see cref="LogDirectoryPath"/>
        /// </summary>
        /// <param name="logFileBaseName">Base name for the log file (ignored if mLogFilePath is defined)</param>
        /// <remarks>
        /// If mLogFilePath is empty and logFileBaseName is empty, will use the name of the entry or executing assembly
        /// This method is private; Use protected method UpdateAutoDefinedLogFilePath to update the auto-defined log file path
        /// </remarks>
        private void ConfigureLogFilePath(string logFileBaseName = "")
        {

            try
            {
                if (LogDirectoryPath == null)
                {
                    LogDirectoryPath = string.Empty;
                }

                if (string.IsNullOrWhiteSpace(LogDirectoryPath))
                {
                    // Log directory is undefined; use mOutputDirectoryPath if it is defined
                    if (!string.IsNullOrWhiteSpace(mOutputDirectoryPath))
                    {
                        LogDirectoryPath = string.Copy(mOutputDirectoryPath);
                    }
                }

                if (LogDirectoryPath.Length > 0)
                {
                    // Create the log directory if it doesn't exist
                    if (!Directory.Exists(LogDirectoryPath))
                    {
                        Directory.CreateDirectory(LogDirectoryPath);
                    }
                }
            }
            catch (Exception)
            {
                LogDirectoryPath = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(mLogFilePath))
            {
                if (string.IsNullOrWhiteSpace(logFileBaseName))
                {
                    // Auto-name the log file
                    logFileBaseName = Path.GetFileNameWithoutExtension(GetAppPath());

                    if (string.IsNullOrWhiteSpace(logFileBaseName))
                    {
                        logFileBaseName = GetEntryOrExecutingAssembly().GetName().Name;
                    }
                }

                if (LogDirectoryPath.Length > 0)
                {
                    mLogFileBasePath = Path.Combine(LogDirectoryPath, logFileBaseName) + LOG_FILE_SUFFIX;
                }
                else
                {
                    mLogFileBasePath = logFileBaseName + LOG_FILE_SUFFIX;
                }

                if (mLogFileUsesDateStamp)
                {
                    // Append the current date to the name
                    var currentLocalTime = DateTime.Now;
                    mLogFilePath = GetDateBasedLogFilePath(currentLocalTime);
                    mLogFileRolloverTime = new DateTime(currentLocalTime.Year, currentLocalTime.Month, currentLocalTime.Day).AddDays(1);
                }
                else
                {
                    mLogFilePath = mLogFileBasePath + LOG_FILE_EXTENSION;
                }
            }
            else
            {
                if (!Path.IsPathRooted(mLogFilePath) && LogDirectoryPath.Length > 0 && !mLogFilePath.StartsWith(LogDirectoryPath))
                {
                    mLogFilePath = Path.Combine(LogDirectoryPath, mLogFilePath);
                }
            }

        }

        /// <summary>
        /// Verifies that the specified .XML settings file exists in the user's local settings directory
        /// </summary>
        /// <param name="applicationName">Application name</param>
        /// <param name="settingsFileName">Settings file name</param>
        /// <returns>True if the file already exists or was created, false if an error</returns>
        /// <remarks>Will return True if the master settings file does not exist</remarks>
        public static bool CreateSettingsFileIfMissing(string applicationName, string settingsFileName)
        {
            var settingsFilePathLocal = GetSettingsFilePathLocal(applicationName, settingsFileName);

            return CreateSettingsFileIfMissing(settingsFilePathLocal);
        }

        /// <summary>
        /// Verifies that the specified .XML settings file exists in the user's local settings directory
        /// </summary>
        /// <param name="settingsFilePathLocal">Full path to the local settings file, for example C:\Users\username\AppData\Roaming\AppName\SettingsFileName.xml</param>
        /// <returns>True if the file already exists or was created, false if an error</returns>
        /// <remarks>Will return True if the master settings file does not exist</remarks>
        public static bool CreateSettingsFileIfMissing(string settingsFilePathLocal)
        {
            if (string.IsNullOrWhiteSpace(settingsFilePathLocal))
                throw new ArgumentException("settingsFilePathLocal cannot be empty", nameof(settingsFilePathLocal));

            try
            {
                if (!File.Exists(settingsFilePathLocal))
                {
                    var masterSettingsFile = new FileInfo(Path.Combine(GetAppDirectoryPath(), Path.GetFileName(settingsFilePathLocal)));

                    if (masterSettingsFile.Exists)
                    {
                        masterSettingsFile.CopyTo(settingsFilePathLocal);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors, but return false
                return false;
            }

            return true;
        }

        /// <summary>
        /// Perform garbage collection
        /// </summary>
        /// <remarks></remarks>
        public static void GarbageCollectNow()
        {
            const int maxWaitTimeMSec = 1000;
            GarbageCollectNow(maxWaitTimeMSec);
        }

        /// <summary>
        /// Perform garbage collection
        /// </summary>
        /// <param name="maxWaitTimeMSec"></param>
        /// <remarks></remarks>
        public static void GarbageCollectNow(int maxWaitTimeMSec)
        {
            const int THREAD_SLEEP_TIME_MSEC = 100;

            if (maxWaitTimeMSec < 100)
                maxWaitTimeMSec = 100;
            if (maxWaitTimeMSec > 5000)
                maxWaitTimeMSec = 5000;

            Thread.Sleep(100);

            try
            {
                var gcThread = new Thread(GarbageCollectWaitForGC);
                gcThread.Start();

                var totalThreadWaitTimeMsec = 0;
                while (gcThread.IsAlive && totalThreadWaitTimeMsec < maxWaitTimeMSec)
                {
                    Thread.Sleep(THREAD_SLEEP_TIME_MSEC);
                    totalThreadWaitTimeMsec += THREAD_SLEEP_TIME_MSEC;
                }
                if (gcThread.IsAlive)
                    gcThread.Abort();
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Force the garbage collector to run, waiting up to 1 second for it to finish
        /// </summary>
        protected static void GarbageCollectWaitForGC()
        {
            ProgRunner.GarbageCollectNow();
        }

        /// <summary>
        /// Returns the full path to the directory into which this application should read/write settings file information
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName</remarks>
        public static string GetAppDataDirectoryPath(string appName)
        {
            string appDataDirectory;

            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = string.Empty;
            }

            try
            {
                appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
                if (!Directory.Exists(appDataDirectory))
                {
                    Directory.CreateDirectory(appDataDirectory);
                }
            }
            catch (Exception)
            {
                // Error creating the directory, revert to using the system Temp directory
                appDataDirectory = Path.GetTempPath();
            }

            return appDataDirectory;
        }

        /// <summary>
        /// Returns the full path to the directory that contains the currently executing .Exe or .Dll
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string GetAppDirectoryPath()
        {
            // Could use Application.StartupPath, but .GetExecutingAssembly is better
            return Path.GetDirectoryName(GetAppPath());
        }

        /// <summary>
        /// Returns the full path to the executing .Exe or .Dll
        /// </summary>
        /// <returns>File path</returns>
        /// <remarks></remarks>
        public static string GetAppPath()
        {
            return GetEntryOrExecutingAssembly().Location;
        }

        /// <summary>
        /// Returns the entry assembly, if it is unavailable, returns the executing assembly
        /// </summary>
        /// <returns></returns>
        public static Assembly GetEntryOrExecutingAssembly()
        {
            return Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        }

        /// <summary>
        /// Returns the .NET assembly version followed by the program date
        /// </summary>
        /// <param name="programDate"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string GetAppVersion(string programDate)
        {
            return GetEntryOrExecutingAssembly().GetName().Version + " (" + programDate + ")";
        }

        /// <summary>
        /// Append the timestamp for the given date to mLogFileBasePath
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private string GetDateBasedLogFilePath(DateTime date)
        {
            if (string.IsNullOrWhiteSpace(mLogFileBasePath))
            {
                mLogFileBasePath = GetEntryOrExecutingAssembly().GetName().Name + LOG_FILE_SUFFIX;
            }

            return mLogFileBasePath + "_" + date.ToString(LOG_FILE_TIMESTAMP_FORMAT) + LOG_FILE_EXTENSION;
        }

        /// <summary>
        /// Get the current error message
        /// </summary>
        /// <returns></returns>
        public abstract string GetErrorMessage();

        /// <summary>
        /// Gets the version for the entry assembly, if available
        /// </summary>
        /// <returns></returns>
        private string GetVersionForExecutingAssembly()
        {
            string version;

            try
            {
                version = GetEntryOrExecutingAssembly().GetName().Version.ToString();
            }
            catch (Exception)
            {
                version = "??.??.??.??";
            }

            return version;
        }

        /// <summary>
        /// Returns the full path to this application's local settings file
        /// </summary>
        /// <param name="applicationName"></param>
        /// <param name="settingsFileName"></param>
        /// <returns></returns>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName\SettingsFileName.xml</remarks>
        public static string GetSettingsFilePathLocal(string applicationName, string settingsFileName)
        {
            return Path.Combine(GetAppDataDirectoryPath(applicationName), settingsFileName);
        }

        /// <summary>
        /// Log an error message with the exception message
        /// Rethrow the exception if ReThrowEvents is true
        /// </summary>
        /// <param name="baseMessage">Base error message</param>
        /// <param name="ex">Exception</param>
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </param>
        protected void HandleException(string baseMessage, Exception ex, int duplicateHoldoffHours = 0, int emptyLinesBeforeMessage = 0)
        {
            if (string.IsNullOrWhiteSpace(baseMessage))
            {
                baseMessage = "Error";
            }

            string formattedError;
            if (ex == null || baseMessage.Contains(ex.Message))
            {
                formattedError = baseMessage;
            }
            else
            {
                formattedError = baseMessage + ": " + ex.Message;
            }

            LogMessage(formattedError, MessageTypeConstants.ErrorMsg, duplicateHoldoffHours, emptyLinesBeforeMessage, ex);

            if (ReThrowEvents)
            {
                throw new Exception(formattedError, ex);
            }
        }

        /// <summary>
        /// Initialize the log file
        /// </summary>
        private void InitializeLogFile()
        {
            try
            {
                ConfigureLogFilePath();

                var openingExistingFile = File.Exists(mLogFilePath);

                if (mLogDataCache.Count == 0)
                {
                    UpdateLogDataCache();
                }

                mLogFile = new StreamWriter(new FileStream(mLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };

                if (!openingExistingFile)
                {
                    mLogFile.WriteLine("Date\tType\tMessage");
                }
            }
            catch (Exception ex)
            {
                // Error creating the log file; set mLogMessagesToFile to false so we don't repeatedly try to create it
                LogMessagesToFile = false;
                HandleException("Error opening log file", ex);
            }
        }

        /// <summary>
        /// Log a message then raise a Status, Warning, or Error event
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="messageType">Message type</param>
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </param>
        /// <param name="ex">If logging an exception, the exception object</param>
        /// <remarks>
        /// Note that CleanupPaths() will update mOutputDirectoryPath, which is used here if mLogDirectoryPath is blank
        /// Thus, be sure to call CleanupPaths (or update mLogDirectoryPath) before the first call to LogMessage
        /// </remarks>
        protected void LogMessage(
            string message,
            MessageTypeConstants messageType = MessageTypeConstants.Normal,
            int duplicateHoldoffHours = 0,
            int emptyLinesBeforeMessage = 0,
            Exception ex = null)
        {

            if (mLogFile == null && LogMessagesToFile)
            {
                InitializeLogFile();
            }
            else if (mLogFile != null && mLogFileUsesDateStamp && DateTime.Now >= mLogFileRolloverTime)
            {
                CloseLogFileNow();
                mLogFilePath = string.Empty;
                InitializeLogFile();
                ConsoleMsgUtils.ShowDebug("Logging to " + LogFilePath);
            }

            if (mLogFile != null)
            {
                WriteToLogFile(message, messageType, duplicateHoldoffHours);

                if (ArchiveOldLogFiles && DateTime.UtcNow.Subtract(mLastCheckOldLogs).TotalHours >= 24)
                {
                    mLastCheckOldLogs = DateTime.UtcNow;
                    ArchiveOldLogFilesNow();
                }

            }

            RaiseMessageEvent(message, messageType, emptyLinesBeforeMessage, ex);
        }

        private void RaiseMessageEvent(string message, MessageTypeConstants messageType, int emptyLinesBeforeMessage, Exception ex = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (string.Equals(message, mLastMessage) && DateTime.UtcNow.Subtract(mLastReportTime).TotalSeconds < 0.5)
            {
                // Duplicate message; do not raise any events
            }
            else
            {
                mLastReportTime = DateTime.UtcNow;
                mLastMessage = string.Copy(message);

                switch (messageType)
                {
                    case MessageTypeConstants.Normal:
                        EmptyLinesBeforeStatusMessages = emptyLinesBeforeMessage;
                        OnStatusEvent(message);
                        break;

                    case MessageTypeConstants.Warning:
                        EmptyLinesBeforeWarningMessages = emptyLinesBeforeMessage;
                        OnWarningEvent(message);
                        break;

                    case MessageTypeConstants.ErrorMsg:
                        EmptyLinesBeforeErrorMessages = emptyLinesBeforeMessage;
                        OnErrorEvent(message, ex);
                        break;

                    case MessageTypeConstants.Debug:
                        EmptyLinesBeforeDebugMessages = emptyLinesBeforeMessage;
                        OnDebugEvent(message);
                        break;

                    default:
                        throw new Exception("Unrecognized message type: " + messageType);
                }
            }
        }

        /// <summary>
        /// Reset the base log file name to an empty string and reset the cached log file dates
        /// </summary>
        protected void ResetLogFileName()
        {
            mLogFileBasePath = string.Empty;
            mLogFilePath = string.Empty;
            mLastCheckOldLogs = DateTime.UtcNow.AddDays(-2);
            mLogDataCache.Clear();
        }

        /// <summary>
        /// Reset progress
        /// </summary>
        protected void ResetProgress()
        {
            mProgressPercentComplete = 0;
            ProgressReset?.Invoke();
        }

        /// <summary>
        /// Reset progress, updating the current processing step
        /// </summary>
        /// <param name="progressStepDescription"></param>
        protected void ResetProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, 0);
            ProgressReset?.Invoke();
        }

        /// <summary>
        /// Show a debug message, optionally logging the message to the log file
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="allowLogToFile"></param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the debug event has no listeners
        /// </param>
        protected void ShowDebug(string message, bool allowLogToFile, int emptyLinesBeforeMessage = 1)
        {
            const int duplicateHoldoffHours = 0;
            ShowMessage(message, allowLogToFile, duplicateHoldoffHours, MessageTypeConstants.Debug, emptyLinesBeforeMessage);
        }

        /// <summary>
        /// Show an error message and write it to the log file
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="duplicateHoldoffHours"></param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the error event has no listeners
        /// </param>
        protected void ShowErrorMessage(string message, int duplicateHoldoffHours, int emptyLinesBeforeMessage = 1)
        {
            const bool allowLogToFile = true;
            ShowErrorMessage(message, allowLogToFile, duplicateHoldoffHours, emptyLinesBeforeMessage);
        }

        /// <summary>
        /// Show an error message, optionally logging the message to the log file
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="allowLogToFile">When true, allow the message to be logged</param>
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </param>
        protected void ShowErrorMessage(
            string message,
            bool allowLogToFile = true,
            int duplicateHoldoffHours = 0,
            int emptyLinesBeforeMessage = 1)
        {
            if (allowLogToFile)
            {
                // Note that LogMessage will call RaiseMessageEvent
                LogMessage(message, MessageTypeConstants.ErrorMsg, duplicateHoldoffHours, emptyLinesBeforeMessage);
            }
            else
            {
                RaiseMessageEvent(message, MessageTypeConstants.ErrorMsg, emptyLinesBeforeMessage);
            }
        }

        /// <summary>
        /// Show a status message and write it to the log file
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="duplicateHoldoffHours"></param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the message event has no listeners
        /// </param>
        protected void ShowMessage(string message, int duplicateHoldoffHours, int emptyLinesBeforeMessage = 0)
        {
            const bool allowLogToFile = true;
            ShowMessage(message, allowLogToFile, duplicateHoldoffHours, MessageTypeConstants.Normal, emptyLinesBeforeMessage);
        }

        /// <summary>
        /// Show a status message, optionally logging the message to the log file
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="allowLogToFile">When true, write to the log file (if the message severity is >= LoggingLevel)</param>
        /// <param name="duplicateHoldoffHours"></param>
        /// <param name="messageType"></param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        protected void ShowMessage(
            string message,
            bool allowLogToFile = true,
            int duplicateHoldoffHours = 0,
            MessageTypeConstants messageType = MessageTypeConstants.Normal,
            int emptyLinesBeforeMessage = 0)
        {
            if (allowLogToFile)
            {
                // Note that LogMessage will call RaiseMessageEvent
                LogMessage(message, messageType, duplicateHoldoffHours, emptyLinesBeforeMessage);
            }
            else
            {
                RaiseMessageEvent(message, messageType, emptyLinesBeforeMessage);
            }
        }

        /// <summary>
        /// Show a warning
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="duplicateHoldoffHours"></param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the warning event has no listeners
        /// </param>
        protected void ShowWarning(string message, int duplicateHoldoffHours = 0, int emptyLinesBeforeMessage = 1)
        {
            const bool allowLogToFile = true;
            ShowMessage(message, allowLogToFile, duplicateHoldoffHours, MessageTypeConstants.Warning, emptyLinesBeforeMessage);
        }

        /// <summary>
        /// Show a warning
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="allowLogToFile"></param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the warning event has no listeners
        /// </param>
        protected void ShowWarning(string message, bool allowLogToFile, int emptyLinesBeforeMessage = 1)
        {
            const int duplicateHoldoffHours = 0;
            ShowMessage(message, allowLogToFile, duplicateHoldoffHours, MessageTypeConstants.Warning, emptyLinesBeforeMessage);
        }

        private void TrimLogDataCache()
        {
            if (mLogDataCache.Count < MAX_LOG_DATA_CACHE_SIZE)
                return;

            try
            {
                // Remove entries from mLogDataCache so that the list count is 90% of MAX_LOG_DATA_CACHE_SIZE

                // First construct a list of dates that we can sort to determine the datetime threshold for removal
                var lstDates = (from entry in mLogDataCache select entry.Value).ToList();

                // Sort by date
                lstDates.Sort();

                var thresholdIndex = Convert.ToInt32(Math.Floor(mLogDataCache.Count - MAX_LOG_DATA_CACHE_SIZE * 0.9));
                if (thresholdIndex < 0)
                    thresholdIndex = 0;

                var threshold = lstDates[thresholdIndex];

                // Construct a list of keys to be removed
                var lstKeys = (from entry in mLogDataCache where entry.Value <= threshold select entry.Key).ToList();

                // Remove each of the keys
                foreach (var key in lstKeys)
                {
                    mLogDataCache.Remove(key);
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Auto-define the log file path using the given log directory path and base log file name
        /// </summary>
        /// <param name="logDirectoryPath">
        /// Log directory path; if blank, mOutputDirectoryPath will be used
        /// If mOutputDirectoryPath is also blank, the log file is created in the same directory as the executing assembly
        /// </param>
        /// <param name="logFileBaseName">
        /// Base name for the log file
        /// If blank, will use the name of the entry or executing assembly (without any extension)
        /// </param>
        /// <param name="archiveOldLogFiles">When true, archive old log files immediately</param>
        protected void UpdateAutoDefinedLogFilePath(string logDirectoryPath, string logFileBaseName, bool archiveOldLogFiles = true)
        {
            CloseLogFileNow();
            ResetLogFileName();
            LogDirectoryPath = logDirectoryPath;

            ConfigureLogFilePath(logFileBaseName);
            ConsoleMsgUtils.ShowDebug("Logging to " + LogFilePath);

            if (!archiveOldLogFiles)
                return;

            Console.WriteLine();
            ArchiveOldLogFilesNow();
        }

        private void UpdateLogDataCache()
        {
            const int CACHE_LENGTH_HOURS = 48;

            try
            {
                if (mLogFileUsesDateStamp && !string.IsNullOrWhiteSpace(mLogFileBasePath))
                {
                    // Read the log file from the previous day
                    var previousLogFile = GetDateBasedLogFilePath(DateTime.Now.AddDays(-1));
                    if (!string.IsNullOrWhiteSpace(previousLogFile) && File.Exists(previousLogFile))
                    {
                        UpdateLogDataCache(previousLogFile, DateTime.UtcNow.AddHours(-CACHE_LENGTH_HOURS));
                    }
                }

                if (File.Exists(mLogFilePath))
                {
                    UpdateLogDataCache(mLogFilePath, DateTime.UtcNow.AddHours(-CACHE_LENGTH_HOURS));
                }
            }
            catch (Exception ex)
            {
                ShowWarning("Error caching log messages: " + ex.Message, false);
            }

        }

        private void UpdateLogDataCache(string logFilePath, DateTime dateThresholdToStoreUTC)
        {
            var reParseLine = new Regex(@"^(?<Date>[^\t]+)\t(?<Type>[^\t]+)\t(?<Message>.+)", RegexOptions.Compiled);

            try
            {
                using (var srLogFile = new StreamReader(new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!srLogFile.EndOfStream)
                    {
                        var lineIn = srLogFile.ReadLine();
                        if (string.IsNullOrWhiteSpace(lineIn))
                            continue;

                        var reMatch = reParseLine.Match(lineIn);

                        if (!reMatch.Success)
                            continue;

                        if (!DateTime.TryParse(reMatch.Groups["Date"].Value, out var logTime))
                            continue;

                        var universalTime = logTime.ToUniversalTime();
                        if (universalTime < dateThresholdToStoreUTC)
                            continue;

                        var key = reMatch.Groups["Type"].Value + "_" + reMatch.Groups["Message"].Value;

                        try
                        {
                            if (mLogDataCache.ContainsKey(key))
                            {
                                mLogDataCache[key] = universalTime;
                            }
                            else
                            {
                                mLogDataCache.Add(key, universalTime);
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore errors here
                        }
                    }
                }

                if (mLogDataCache.Count > MAX_LOG_DATA_CACHE_SIZE)
                {
                    TrimLogDataCache();
                }
            }
            catch (Exception ex)
            {
                if (DateTime.UtcNow.Subtract(mLastErrorShown).TotalSeconds > 10)
                {
                    mLastErrorShown = DateTime.UtcNow;
                    ConsoleMsgUtils.ShowWarning("Error caching the log file: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Update the current progress description
        /// </summary>
        /// <param name="progressStepDescription"></param>
        protected void UpdateProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, mProgressPercentComplete);
        }

        /// <summary>
        /// Update progress percent complete
        /// </summary>
        /// <param name="percentComplete"></param>
        protected void UpdateProgress(float percentComplete)
        {
            UpdateProgress(ProgressStepDescription, percentComplete);
        }

        /// <summary>
        /// Update progress description and percent complete
        /// </summary>
        /// <param name="progressStepDescription"></param>
        /// <param name="percentComplete"></param>
        protected void UpdateProgress(string progressStepDescription, float percentComplete)
        {
            var descriptionChanged = !string.Equals(progressStepDescription, ProgressStepDescription);

            ProgressStepDescription = string.Copy(progressStepDescription);
            if (percentComplete < 0)
            {
                percentComplete = 0;
            }
            else if (percentComplete > 100)
            {
                percentComplete = 100;
            }
            mProgressPercentComplete = percentComplete;

            if (descriptionChanged)
            {
                if (mProgressPercentComplete < float.Epsilon)
                {
                    LogMessage(ProgressStepDescription.Replace(Environment.NewLine, "; "), mProgressMessageType);
                }
                else
                {
                    LogMessage(ProgressStepDescription + " (" + mProgressPercentComplete.ToString("0.0") +
                               "% complete)".Replace(Environment.NewLine, "; "), mProgressMessageType);
                }
            }

            OnProgressUpdate(ProgressStepDescription, ProgressPercentComplete);
        }

        private static MessageTypeConstants ConvertLogLevelToMessageType(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return MessageTypeConstants.Debug;
                case LogLevel.Normal:
                    return MessageTypeConstants.Normal;
                case LogLevel.Warning:
                    return MessageTypeConstants.Warning;
                case LogLevel.Error:
                    return MessageTypeConstants.ErrorMsg;
                default:
                    return MessageTypeConstants.Normal;
            }
        }

        private static LogLevel ConvertMessageTypeToLogLevel(MessageTypeConstants messageType)
        {
            switch (messageType)
            {
                case MessageTypeConstants.Debug:
                    return LogLevel.Debug;
                case MessageTypeConstants.Normal:
                    return LogLevel.Normal;
                case MessageTypeConstants.Warning:
                    return LogLevel.Warning;
                case MessageTypeConstants.ErrorMsg:
                    return LogLevel.Error;
                default:
                    return LogLevel.Normal;
            }
        }

        private void WriteToLogFile(string message, MessageTypeConstants messageType, int duplicateHoldoffHours)
        {
            var level = ConvertMessageTypeToLogLevel(messageType);
            if (level < LoggingLevel)
            {
                return;
            }

            string messageTypeName;

            switch (messageType)
            {
                case MessageTypeConstants.Normal:
                    messageTypeName = "Normal";
                    break;
                case MessageTypeConstants.ErrorMsg:
                    messageTypeName = "Error";
                    break;
                case MessageTypeConstants.Warning:
                    messageTypeName = "Warning";
                    break;
                case MessageTypeConstants.Debug:
                    messageTypeName = "Debug";
                    break;
                default:
                    messageTypeName = "Unknown";
                    break;
            }

            var writeToLog = true;

            var logKey = messageTypeName + "_" + message;
            bool messageCached;

            if (mLogDataCache.TryGetValue(logKey, out var lastLogTime))
            {
                messageCached = true;
            }
            else
            {
                messageCached = false;
                lastLogTime = DateTime.UtcNow.AddHours(-(duplicateHoldoffHours + 1));
            }

            if (duplicateHoldoffHours > 0 && DateTime.UtcNow.Subtract(lastLogTime).TotalHours < duplicateHoldoffHours)
            {
                writeToLog = false;
            }

            if (!writeToLog)
                return;

            mLogFile.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + "\t" +
                               messageTypeName + "\t" +
                               message);

            if (messageCached)
            {
                mLogDataCache[logKey] = DateTime.UtcNow;
            }
            else
            {
                try
                {
                    mLogDataCache.Add(logKey, DateTime.UtcNow);

                    if (mLogDataCache.Count > MAX_LOG_DATA_CACHE_SIZE)
                    {
                        TrimLogDataCache();
                    }
                }
                catch (Exception)
                {
                    // Ignore errors here
                }
            }
        }

        /// <summary>
        /// Raise event ProgressComplete
        /// </summary>
        protected void OperationComplete()
        {
            ProgressComplete?.Invoke();
        }
    }
#pragma warning restore IDE1006 // Naming Styles

}
