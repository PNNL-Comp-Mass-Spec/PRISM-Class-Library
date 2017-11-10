using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace PRISM.FileProcessor
{
    /// <summary>
    /// Base class for both ProcessFilesBase and ProcessFoldersBase
    /// </summary>
    public abstract class ProcessFilesOrFoldersBase : clsEventNotifier
    {
        #region "Constants and Enums"

        /// <summary>
        /// Message type enums
        /// </summary>
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
            /// Warninig message
            /// </summary>
            Warning = 2,

            /// <summary>
            /// Debugging message
            /// </summary>
            Debug = -1
        }

        /// <summary>
        /// Log levels, specifying the severity of messages to be logged
        /// </summary>
        public enum LogLevel
        {
            /// <summary>
            ///  All messages
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
        /// True if the log file should include the current date
        /// </summary>
        protected bool mLogFileUsesDateStamp = true;

        /// <summary>
        /// Log file path
        /// </summary>
        /// <remarks>Leave blank to auto-define</remarks>
        protected string mLogFilePath;

        /// <summary>
        /// Log file writer
        /// </summary>
        protected StreamWriter mLogFile;

        /// <summary>
        /// Output folder path
        /// </summary>
        /// <remarks>This variable is updated when CleanupFilePaths() is called</remarks>
        protected string mOutputFolderPath;

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
        /// Keys in this dictionary are the log type and message (separated by an underscore), values are the most recent time the string was logged
        /// </summary>
        /// <remarks></remarks>
        private readonly Dictionary<string, DateTime> mLogDataCache;

        private const int MAX_LOGDATA_CACHE_SIZE = 100000;

        #endregion

        #region "Interface Functions"

        /// <summary>
        /// True if processing should be aborted
        /// </summary>
        public bool AbortProcessing { get; set; }

        /// <summary>
        /// Version of the executing assembly
        /// </summary>
        public string FileVersion => GetVersionForExecutingAssembly();

        /// <summary>
        /// File date (aka program date)
        /// </summary>
        public string FileDate => mFileDate;

        /// <summary>
        /// Log file path
        /// </summary>
        /// <remarks>Leave blank to auto-define</remarks>
        public string LogFilePath
        {
            get => mLogFilePath;
            set => mLogFilePath = value ?? string.Empty;
        }

        /// <summary>
        /// Log folder path (ignored if LogFilePath is rooted)
        /// </summary>
        /// <remarks>
        /// If blank, mOutputFolderPath will be used; if mOutputFolderPath is also blank, the log is created in the same folder as the executing assembly
        /// </remarks>
        public string LogFolderPath { get; set; }

        /// <summary>
        /// True to log messages to a file
        /// </summary>
        public bool LogMessagesToFile { get; set; }

        /// <summary>
        /// Description of the current task
        /// </summary>
        public virtual string ProgressStepDescription { get; private set; }

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
        /// When true, if an error occurs a message will be logged, then the event will be re-thrown
        /// </summary>
        public bool ReThrowEvents { get; set; } = false;

        /// <summary>
        /// Minimum severity of messages to log
        /// </summary>
        public LogLevel LoggingLevel { get; set; } = LogLevel.Normal;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        protected ProcessFilesOrFoldersBase()
        {
            ProgressStepDescription = string.Empty;

            mOutputFolderPath = string.Empty;
            LogFolderPath = string.Empty;
            mLogFilePath = string.Empty;

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
        /// Cleanup paths
        /// </summary>
        /// <param name="inputFileOrFolderPath"></param>
        /// <param name="outputFolderPath"></param>
        protected abstract void CleanupPaths(ref string inputFileOrFolderPath, ref string outputFolderPath);

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
        /// Verifies that the specified .XML settings file exists in the user's local settings folder
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
        /// Verifies that the specified .XML settings file exists in the user's local settings folder
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
                    var masterSettingsFile = new FileInfo(Path.Combine(GetAppFolderPath(), Path.GetFileName(settingsFilePathLocal)));

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
            clsProgRunner.GarbageCollectNow();
        }

        /// <summary>
        /// Returns the full path to the folder into which this application should read/write settings file information
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName</remarks>
        public static string GetAppDataFolderPath(string appName)
        {
            string appDataFolder;

            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = string.Empty;
            }

            try
            {
                appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
                if (!Directory.Exists(appDataFolder))
                {
                    Directory.CreateDirectory(appDataFolder);
                }
            }
            catch (Exception)
            {
                // Error creating the folder, revert to using the system Temp folder
                appDataFolder = Path.GetTempPath();
            }

            return appDataFolder;
        }

        /// <summary>
        /// Returns the full path to the folder that contains the currently executing .Exe or .Dll
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string GetAppFolderPath()
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
            return Path.Combine(GetAppDataFolderPath(applicationName), settingsFileName);
        }

        /// <summary>
        /// Handle exceptions, rethrowing it if ReThrowEvents is true
        /// </summary>
        /// <param name="baseMessage"></param>
        /// <param name="ex"></param>
        protected void HandleException(string baseMessage, Exception ex)
        {
            if (string.IsNullOrWhiteSpace(baseMessage))
            {
                baseMessage = "Error";
            }

            LogMessage(baseMessage + ": " + ex.Message, eMessageTypeConstants.ErrorMsg);

            if (ReThrowEvents)
            {
                throw new Exception(baseMessage, ex);
            }
        }

        /// <summary>
        /// Sets the log file path (<see cref="mLogFilePath"/>),
        /// according to data in <see cref="mLogFilePath"/>,
        /// <see cref="mLogFileUsesDateStamp"/>, and <see cref="LogFolderPath"/>
        /// </summary>
        protected void ConfigureLogFilePath()
        {
            if (string.IsNullOrWhiteSpace(mLogFilePath))
            {
                // Auto-name the log file
                mLogFilePath = Path.GetFileNameWithoutExtension(GetAppPath());
                mLogFilePath += "_log";

                if (mLogFileUsesDateStamp)
                {
                    mLogFilePath += "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                }
                else
                {
                    mLogFilePath += ".txt";
                }
            }

            try
            {
                if (LogFolderPath == null)
                    LogFolderPath = string.Empty;

                if (string.IsNullOrWhiteSpace(LogFolderPath))
                {
                    // Log folder is undefined; use mOutputFolderPath if it is defined
                    if (!string.IsNullOrWhiteSpace(mOutputFolderPath))
                    {
                        LogFolderPath = string.Copy(mOutputFolderPath);
                    }
                }

                if (LogFolderPath.Length > 0)
                {
                    // Create the log folder if it doesn't exist
                    if (!Directory.Exists(LogFolderPath))
                    {
                        Directory.CreateDirectory(LogFolderPath);
                    }
                }
            }
            catch (Exception)
            {
                LogFolderPath = string.Empty;
            }

            if (!Path.IsPathRooted(mLogFilePath) && LogFolderPath.Length > 0 && !mLogFilePath.StartsWith(LogFolderPath))
            {
                mLogFilePath = Path.Combine(LogFolderPath, mLogFilePath);
            }
        }

        private void InitializeLogFile(int duplicateHoldoffHours)
        {
            try
            {
                ConfigureLogFilePath();

                var openingExistingFile = File.Exists(mLogFilePath);

                if (openingExistingFile & mLogDataCache.Count == 0)
                {
                    UpdateLogDataCache(mLogFilePath, DateTime.UtcNow.AddHours(-duplicateHoldoffHours));
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
                // Note: do not exit this function if an exception occurs
            }
        }

        /// <summary>
        /// Log a message then raise a Status, Warning, or Error event
        /// </summary>
        /// <param name="message"></param>
        /// <param name="eMessageType"></param>
        /// <param name="duplicateHoldoffHours"></param>
        /// <remarks>
        /// Note that CleanupPaths() will update mOutputFolderPath, which is used here if mLogFolderPath is blank
        /// Thus, be sure to call CleanupPaths (or update mLogFolderPath) before the first call to LogMessage
        /// </remarks>
        protected void LogMessage(string message, eMessageTypeConstants eMessageType = eMessageTypeConstants.Normal, int duplicateHoldoffHours = 0)
        {

            if (mLogFile == null && LogMessagesToFile)
            {
                InitializeLogFile(duplicateHoldoffHours);
            }

            if (mLogFile != null)
            {
                WriteToLogFile(message, eMessageType, duplicateHoldoffHours);
            }

            RaiseMessageEvent(message, eMessageType);
        }

        private void RaiseMessageEvent(string message, eMessageTypeConstants eMessageType)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (string.Equals(message, mLastMessage) && DateTime.UtcNow.Subtract(mLastReportTime).TotalSeconds < 0.5)
            {
                // Duplicate message; do not raise any events
            }
            else
            {
                mLastReportTime = DateTime.UtcNow;
                mLastMessage = string.Copy(message);

                switch (eMessageType)
                {
                    case eMessageTypeConstants.Normal:
                        OnStatusEvent(message);
                        break;
                    case eMessageTypeConstants.Warning:
                        OnWarningEvent(message);

                        break;
                    case eMessageTypeConstants.ErrorMsg:
                        OnErrorEvent(message);

                        break;
                    default:
                        OnStatusEvent(message);
                        break;
                }
            }
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
        /// Show an error message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="duplicateHoldoffHours"></param>
        protected void ShowErrorMessage(string message, int duplicateHoldoffHours)
        {
            ShowErrorMessage(message, allowLogToFile: true, duplicateHoldoffHours: duplicateHoldoffHours);
        }

        /// <summary>
        /// Show an error message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allowLogToFile"></param>
        /// <param name="duplicateHoldoffHours"></param>
        protected void ShowErrorMessage(string message, bool allowLogToFile = true, int duplicateHoldoffHours = 0)
        {
            if (allowLogToFile)
            {
                // Note that LogMessage will call RaiseMessageEvent
                LogMessage(message, eMessageTypeConstants.ErrorMsg, duplicateHoldoffHours);
            }
            else
            {
                RaiseMessageEvent(message, eMessageTypeConstants.ErrorMsg);
            }
        }

        /// <summary>
        /// Show a status message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="duplicateHoldoffHours"></param>
        protected void ShowMessage(string message, int duplicateHoldoffHours)
        {
            ShowMessage(message, allowLogToFile: true, duplicateHoldoffHours: duplicateHoldoffHours);
        }

        /// <summary>
        /// Show a status message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allowLogToFile"></param>
        /// <param name="duplicateHoldoffHours"></param>
        /// <param name="eMessageType"></param>
        protected void ShowMessage(
            string message,
            bool allowLogToFile = true,
            int duplicateHoldoffHours = 0,
            eMessageTypeConstants eMessageType = eMessageTypeConstants.Normal)
        {
            if (allowLogToFile)
            {
                // Note that LogMessage will call RaiseMessageEvent
                LogMessage(message, eMessageType, duplicateHoldoffHours);
            }
            else
            {
                RaiseMessageEvent(message, eMessageType);
            }
        }

        /// <summary>
        /// Show a warning
        /// </summary>
        /// <param name="message"></param>
        /// <param name="duplicateHoldoffHours"></param>
        protected void ShowWarning(string message, int duplicateHoldoffHours = 0)
        {
            ShowMessage(message, allowLogToFile: true, duplicateHoldoffHours: duplicateHoldoffHours, eMessageType: eMessageTypeConstants.Warning);
        }

        /// <summary>
        /// Show a warning
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allowLogToFile"></param>
        protected void ShowWarning(string message, bool allowLogToFile)
        {
            ShowMessage(message, allowLogToFile, duplicateHoldoffHours: 0, eMessageType: eMessageTypeConstants.Warning);
        }

        private void TrimLogDataCache()
        {
            if (mLogDataCache.Count < MAX_LOGDATA_CACHE_SIZE)
                return;

            try
            {
                // Remove entries from mLogDataCache so that the list count is 80% of MAX_LOGDATA_CACHE_SIZE

                // First construct a list of dates that we can sort to determine the datetime threshold for removal
                var lstDates = (from entry in mLogDataCache select entry.Value).ToList();

                // Sort by date
                lstDates.Sort();

                var thresholdIndex = Convert.ToInt32(Math.Floor(mLogDataCache.Count - MAX_LOGDATA_CACHE_SIZE * 0.8));
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

        private void UpdateLogDataCache(string logFilePath, DateTime dateThresholdToStore)
        {
            var reParseLine = new Regex(@"^([^\t]+)\t([^\t]+)\t(.+)", RegexOptions.Compiled);

            try
            {
                mLogDataCache.Clear();

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

                        if (!DateTime.TryParse(reMatch.Groups[1].Value, out var logTime))
                            continue;

                        var universalTime = logTime.ToUniversalTime();
                        if (universalTime < dateThresholdToStore)
                            continue;

                        var key = reMatch.Groups[2].Value + "_" + reMatch.Groups[3].Value;

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

                if (mLogDataCache.Count > MAX_LOGDATA_CACHE_SIZE)
                {
                    TrimLogDataCache();
                }
            }
            catch (Exception ex)
            {
                if (DateTime.UtcNow.Subtract(mLastErrorShown).TotalSeconds > 10)
                {
                    mLastErrorShown = DateTime.UtcNow;
                    Console.WriteLine("Error caching the log file: " + ex.Message);
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
                    LogMessage(ProgressStepDescription.Replace(Environment.NewLine, "; "));
                }
                else
                {
                    LogMessage(ProgressStepDescription + " (" + mProgressPercentComplete.ToString("0.0") +
                               "% complete)".Replace(Environment.NewLine, "; "));
                }
            }

            OnProgressUpdate(ProgressStepDescription, ProgressPercentComplete);
        }

        private LogLevel ConvertMessageTypeToLogLevel(eMessageTypeConstants messageType)
        {
            switch (messageType)
            {
                case eMessageTypeConstants.Debug:
                    return LogLevel.Debug;
                case eMessageTypeConstants.Normal:
                    return LogLevel.Normal;
                case eMessageTypeConstants.Warning:
                    return LogLevel.Warning;
                case eMessageTypeConstants.ErrorMsg:
                    return LogLevel.Error;
                default:
                    return LogLevel.Normal;
            }
        }

        private void WriteToLogFile(string message, eMessageTypeConstants eMessageType, int duplicateHoldoffHours)
        {
            var level = ConvertMessageTypeToLogLevel(eMessageType);
            if (level < LoggingLevel)
            {
                return;
            }

            string messageType;

            switch (eMessageType)
            {
                case eMessageTypeConstants.Normal:
                    messageType = "Normal";
                    break;
                case eMessageTypeConstants.ErrorMsg:
                    messageType = "Error";
                    break;
                case eMessageTypeConstants.Warning:
                    messageType = "Warning";
                    break;
                case eMessageTypeConstants.Debug:
                    messageType = "Debug";
                    break;
                default:
                    messageType = "Unknown";
                    break;
            }

            var writeToLog = true;

            var logKey = messageType + "_" + message;
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
                               messageType + "\t" +
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

                    if (mLogDataCache.Count > MAX_LOGDATA_CACHE_SIZE)
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
}
