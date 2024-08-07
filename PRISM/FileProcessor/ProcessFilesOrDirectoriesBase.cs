﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using PRISM.Logging;

// ReSharper disable UnusedMember.Global

namespace PRISM.FileProcessor
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Base class for both ProcessFilesBase and ProcessDirectoriesBase
    /// </summary>
    public abstract class ProcessFilesOrDirectoriesBase : EventNotifier
    {
        // Ignore Spelling: app, holdoff, username, yyyy-MM-dd, hh:mm:ss tt

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
            /// Debugging message
            /// </summary>
            Debug = -1,

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
            /// Debugging message
            /// </summary>
            Debug = -1,

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
        /// <remarks>Only used if mLogFileUsesDateStamp is true</remarks>
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

        private string mLastMessage = string.Empty;

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
        private readonly Dictionary<string, DateTime> mLogDataCache;

        private MessageTypeConstants mProgressMessageType = MessageTypeConstants.Normal;

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
        /// <remarks>Leave blank to auto-define using the executable name and today's date</remarks>
        public string LogFilePath
        {
            get => mLogFilePath;
            set
            {
                mLogFilePath = value ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(mLogFilePath))
                {
                    mLogFileUsesDateStamp = false;
                }
            }
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

        /// <summary>
        /// Constructor
        /// </summary>
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

                var archiveWarnings = FileLogger.ArchiveOldLogs(logDirectory, LOG_FILE_MATCH_SPEC, LOG_FILE_EXTENSION, LOG_FILE_DATE_REGEX);

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
        /// <param name="inputFileOrDirectoryPath">Input file or directory path</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
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

                AppUtils.GarbageCollectNow();
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Computes the incremental progress that has been made beyond currentTaskProgressAtStart,
        /// based on the number of items processed and the next overall progress level
        /// </summary>
        /// <param name="currentTaskProgressAtStart">Progress at the start of the current task (value between 0 and 100)</param>
        /// <param name="currentTaskProgressAtEnd">Progress at the start of the current task (value between 0 and 100)</param>
        /// <param name="subTaskProgress">Progress of the current task (value between 0 and 100)</param>
        /// <returns>Overall progress (value between 0 and 100)</returns>
        public static float ComputeIncrementalProgress(float currentTaskProgressAtStart, float currentTaskProgressAtEnd, float subTaskProgress)
        {
            if (subTaskProgress < 0)
            {
                return currentTaskProgressAtStart;
            }

            if (subTaskProgress >= 100)
            {
                return currentTaskProgressAtEnd;
            }

            return (float)(currentTaskProgressAtStart + subTaskProgress / 100.0 * (currentTaskProgressAtEnd - currentTaskProgressAtStart));
        }

        /// <summary>
        /// Computes the incremental progress that has been made beyond currentTaskProgressAtStart,
        /// based on the number of items processed and the next overall progress level
        /// </summary>
        /// <param name="currentTaskProgressAtStart">Progress at the start of the current task (value between 0 and 100)</param>
        /// <param name="currentTaskProgressAtEnd">Progress at the start of the current task (value between 0 and 100)</param>
        /// <param name="currentTaskItemsProcessed">Number of items processed so far during this task</param>
        /// <param name="currentTaskTotalItems">Total number of items to process during this task</param>
        /// <returns>Overall progress (value between 0 and 100)</returns>
        public static float ComputeIncrementalProgress(
            float currentTaskProgressAtStart, float currentTaskProgressAtEnd,
            int currentTaskItemsProcessed, int currentTaskTotalItems)
        {
            if (currentTaskTotalItems < 1)
            {
                return currentTaskProgressAtStart;
            }

            if (currentTaskItemsProcessed > currentTaskTotalItems)
            {
                return currentTaskProgressAtEnd;
            }

            return currentTaskProgressAtStart +
                currentTaskItemsProcessed / (float)currentTaskTotalItems * (currentTaskProgressAtEnd - currentTaskProgressAtStart);
        }

        /// <summary>
        /// Sets the log file path (<see cref="mLogFilePath"/>),
        /// according to data in <see cref="mLogFilePath"/>,
        /// <see cref="mLogFileUsesDateStamp"/>, and <see cref="LogDirectoryPath"/>
        /// </summary>
        /// <remarks>
        /// If mLogFilePath is empty and logFileBaseName is empty, will use the name of the entry or executing assembly
        /// This method is private; Use protected method UpdateAutoDefinedLogFilePath to update the auto-defined log file path
        /// </remarks>
        /// <param name="logFileBaseName">Base name for the log file (ignored if mLogFilePath is defined)</param>
        private void ConfigureLogFilePath(string logFileBaseName = "")
        {
            try
            {
                LogDirectoryPath ??= string.Empty;

                if (string.IsNullOrWhiteSpace(LogDirectoryPath))
                {
                    // Log directory is undefined; use mOutputDirectoryPath if it is defined
                    // LogDirectoryPath will get updated below if mLogFilePath is defined and Rooted
                    if (!string.IsNullOrWhiteSpace(mOutputDirectoryPath))
                    {
                        LogDirectoryPath = mOutputDirectoryPath;
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
                    logFileBaseName = Path.GetFileNameWithoutExtension(AppUtils.GetAppPath());

                    if (string.IsNullOrWhiteSpace(logFileBaseName))
                    {
                        logFileBaseName = AppUtils.GetEntryOrExecutingAssembly().GetName().Name;
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

                var logFile = new FileInfo(mLogFilePath);

                if (logFile.Directory == null)
                    return;

                if (!string.Equals(LogDirectoryPath, logFile.DirectoryName))
                {
                    LogDirectoryPath = logFile.DirectoryName;
                }

                try
                {
                    if (!logFile.Directory.Exists)
                    {
                        // Create the log directory if it doesn't exist
                        logFile.Directory.Create();
                    }
                }
                catch (Exception)
                {
                    // Ignore errors here
                }
            }
        }

        /// <summary>
        /// Verifies that the specified .XML settings file exists in the user's local settings directory
        /// </summary>
        /// <remarks>Will return True if the master settings file does not exist</remarks>
        /// <param name="applicationName">Application name</param>
        /// <param name="settingsFileName">Settings file name</param>
        /// <returns>True if the file already exists or was created, false if an error</returns>
        public static bool CreateSettingsFileIfMissing(string applicationName, string settingsFileName)
        {
            var settingsFilePathLocal = GetSettingsFilePathLocal(applicationName, settingsFileName);

            return CreateSettingsFileIfMissing(settingsFilePathLocal);
        }

        /// <summary>
        /// Verifies that the specified .XML settings file exists in the user's local settings directory
        /// </summary>
        /// <remarks>Will return True if the master settings file does not exist</remarks>
        /// <param name="settingsFilePathLocal">Full path to the local settings file, for example C:\Users\username\AppData\Roaming\AppName\SettingsFileName.xml</param>
        /// <returns>True if the file already exists or was created, false if an error</returns>
        public static bool CreateSettingsFileIfMissing(string settingsFilePathLocal)
        {
            if (string.IsNullOrWhiteSpace(settingsFilePathLocal))
                throw new ArgumentException("settingsFilePathLocal cannot be empty", nameof(settingsFilePathLocal));

            try
            {
                if (!File.Exists(settingsFilePathLocal))
                {
                    var masterSettingsFile = new FileInfo(Path.Combine(AppUtils.GetAppDirectoryPath(), Path.GetFileName(settingsFilePathLocal)));

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
        [Obsolete("Moved to static class PRISM.AppUtils")]
        public static void GarbageCollectNow()
        {
            AppUtils.GarbageCollectNow();
        }

        /// <summary>
        /// Perform garbage collection
        /// </summary>
        /// <param name="maxWaitTimeMSec">Maximum wait time, in seconds</param>
        [Obsolete("Moved to static class PRISM.AppUtils")]
        public static void GarbageCollectNow(int maxWaitTimeMSec)
        {
            AppUtils.GarbageCollectNow(maxWaitTimeMSec);
        }

        /// <summary>
        /// Returns the full path to the directory into which this application should read/write settings file information
        /// </summary>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName</remarks>
        /// <param name="appName">Application name</param>
        [Obsolete("Moved to static class PRISM.AppUtils")]
        public static string GetAppDataDirectoryPath(string appName)
        {
            return AppUtils.GetAppDataDirectoryPath(appName);
        }

        /// <summary>
        /// Returns the full path to the directory that contains the currently executing .Exe or .Dll
        /// </summary>
        [Obsolete("Moved to static class PRISM.AppUtils")]
        public static string GetAppDirectoryPath()
        {
            return AppUtils.GetAppDirectoryPath();
        }

        /// <summary>
        /// Returns the full path to the executing .Exe or .Dll
        /// </summary>
        /// <returns>File path</returns>
        [Obsolete("Moved to static class PRISM.AppUtils")]
        public static string GetAppPath()
        {
            return AppUtils.GetAppPath();
        }

        /// <summary>
        /// Returns the .NET assembly version followed by the program date
        /// </summary>
        /// <param name="programDate">Program date</param>
        [Obsolete("Moved to static class PRISM.AppUtils")]
        public static string GetAppVersion(string programDate)
        {
            return AppUtils.GetAppVersion(programDate);
        }

        /// <summary>
        /// Append the timestamp for the given date to mLogFileBasePath
        /// </summary>
        /// <param name="date">Timestamp</param>
        private string GetDateBasedLogFilePath(DateTime date)
        {
            if (string.IsNullOrWhiteSpace(mLogFileBasePath))
            {
                mLogFileBasePath = AppUtils.GetEntryOrExecutingAssembly().GetName().Name + LOG_FILE_SUFFIX;
            }

            return mLogFileBasePath + "_" + date.ToString(LOG_FILE_TIMESTAMP_FORMAT) + LOG_FILE_EXTENSION;
        }

        /// <summary>
        /// Returns the entry assembly, if it is unavailable, returns the executing assembly
        /// </summary>
        [Obsolete("Moved to static class PRISM.AppUtils")]
        public static Assembly GetEntryOrExecutingAssembly()
        {
            return AppUtils.GetEntryOrExecutingAssembly();
        }

        /// <summary>
        /// Get the current error message
        /// </summary>
        public abstract string GetErrorMessage();

        /// <summary>
        /// Gets the version for the entry assembly, if available
        /// </summary>
        private static string GetVersionForExecutingAssembly()
        {
            string version;

            try
            {
                version = AppUtils.GetEntryOrExecutingAssembly().GetName().Version.ToString();
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
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName\SettingsFileName.xml</remarks>
        /// <param name="applicationName">Application name</param>
        /// <param name="settingsFileName">Settings file name</param>
        public static string GetSettingsFilePathLocal(string applicationName, string settingsFileName)
        {
            return Path.Combine(AppUtils.GetAppDataDirectoryPath(applicationName), settingsFileName);
        }

        /// <summary>
        /// Log an error message with the exception message
        /// Re-throw the exception if ReThrowEvents is true
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
        /// <remarks>
        /// Note that CleanupPaths() will update mOutputDirectoryPath, which is used here if mLogDirectoryPath is blank
        /// Thus, be sure to call CleanupPaths (or update mLogDirectoryPath) before the first call to LogMessage
        /// </remarks>
        /// <param name="message">Message</param>
        /// <param name="messageType">Message type</param>
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
        /// <param name="emptyLinesBeforeMessage">
        /// Number of empty lines to write to the console before displaying a message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </param>
        /// <param name="ex">If logging an exception, the exception object</param>
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
                mLastMessage = message;

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
        /// <param name="progressStepDescription">Progress step description</param>
        protected void ResetProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, 0);
            ProgressReset?.Invoke();
        }

        /// <summary>
        /// Show a debug message, optionally logging the message to the log file
        /// </summary>
        /// <param name="message">Message to show</param>
        /// <param name="allowLogToFile">When true, write to the log file</param>
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
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
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
        /// <param name="allowLogToFile">When true, write to the log file</param>
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
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
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
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
        /// <param name="messageType">Message type</param>
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
        /// <param name="duplicateHoldoffHours">Do not log the message if it was previously logged within this many hours</param>
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
        /// <param name="allowLogToFile">When true, write to the log file</param>
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

                // First construct a list of dates that we can sort to determine the date/time threshold for removal
                var dates = (from entry in mLogDataCache select entry.Value).ToList();

                // Sort by date
                dates.Sort();

                var thresholdIndex = Convert.ToInt32(Math.Floor(mLogDataCache.Count - MAX_LOG_DATA_CACHE_SIZE * 0.9));

                if (thresholdIndex < 0)
                    thresholdIndex = 0;

                var threshold = dates[thresholdIndex];

                // Construct a list of keys to be removed
                var keys = (from entry in mLogDataCache where entry.Value <= threshold select entry.Key).ToList();

                // Remove each of the keys
                foreach (var key in keys)
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
                using (var logFileReader = new StreamReader(new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!logFileReader.EndOfStream)
                    {
                        var lineIn = logFileReader.ReadLine();

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
                            // Add/update the time associated with the key
                            mLogDataCache[key] = universalTime;
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
        /// <param name="progressStepDescription">Progress step description</param>
        protected void UpdateProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, mProgressPercentComplete);
        }

        /// <summary>
        /// Update progress percent complete
        /// </summary>
        /// <param name="percentComplete">Percent complete</param>
        protected void UpdateProgress(float percentComplete)
        {
            UpdateProgress(ProgressStepDescription, percentComplete);
        }

        /// <summary>
        /// Update progress description and percent complete
        /// </summary>
        /// <param name="progressStepDescription">Progress step description</param>
        /// <param name="percentComplete">Percent complete</param>
        protected void UpdateProgress(string progressStepDescription, float percentComplete)
        {
            var descriptionChanged = !string.Equals(progressStepDescription, ProgressStepDescription);

            ProgressStepDescription = progressStepDescription;

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
            return level switch
            {
                LogLevel.Debug => MessageTypeConstants.Debug,
                LogLevel.Normal => MessageTypeConstants.Normal,
                LogLevel.Warning => MessageTypeConstants.Warning,
                LogLevel.Error => MessageTypeConstants.ErrorMsg,
                _ => MessageTypeConstants.Normal,
            };
        }

        private static LogLevel ConvertMessageTypeToLogLevel(MessageTypeConstants messageType)
        {
            return messageType switch
            {
                MessageTypeConstants.Debug => LogLevel.Debug,
                MessageTypeConstants.Normal => LogLevel.Normal,
                MessageTypeConstants.Warning => LogLevel.Warning,
                MessageTypeConstants.ErrorMsg => LogLevel.Error,
                _ => LogLevel.Normal,
            };
        }

        private void WriteToLogFile(string message, MessageTypeConstants messageType, int duplicateHoldoffHours)
        {
            var level = ConvertMessageTypeToLogLevel(messageType);

            if (level < LoggingLevel)
            {
                return;
            }

            var messageTypeName = messageType switch
            {
                MessageTypeConstants.Normal => "Normal",
                MessageTypeConstants.ErrorMsg => "Error",
                MessageTypeConstants.Warning => "Warning",
                MessageTypeConstants.Debug => "Debug",
                _ => "Unknown",
            };
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
