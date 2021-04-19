using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Tools to manipulate paths and directories.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class FileTools : EventNotifier
    {
        // Ignore Spelling: yyyy-MM-dd, hh:mm:ss, hh:mm:ss.fff tt, picfs, mbBacklog

        #region "Events"

        /// <summary>
        /// File copy starting event
        /// </summary>
        public event CopyingFileEventHandler CopyingFile;

        /// <summary>
        /// Event is raised before copying begins.
        /// </summary>
        /// <param name="filename">The file's full path.</param>
        public delegate void CopyingFileEventHandler(string filename);

        /// <summary>
        /// Event is raised before copying begins (when resuming a file copy)
        /// </summary>
        public event ResumingFileCopyEventHandler ResumingFileCopy;

        /// <summary>
        /// Event is raised before copying begins (when resuming a file copy)
        /// </summary>
        /// <param name="filename">The file's full path.</param>
        public delegate void ResumingFileCopyEventHandler(string filename);

        /// <summary>
        /// Event is raised before copying begins
        /// </summary>
        public event FileCopyProgressEventHandler FileCopyProgress;

        /// <summary>
        /// Event is raised before copying begins
        /// </summary>
        /// <param name="filename">The file name (not full path)</param>
        /// <param name="percentComplete">Percent complete (value between 0 and 100)</param>
        public delegate void FileCopyProgressEventHandler(string filename, float percentComplete);

        /// <summary>
        /// Event raised while waiting for the lock queue
        /// </summary>
        public event WaitingForLockQueueEventHandler WaitingForLockQueue;

        /// <summary>
        /// Reports the source and target paths, plus the source and target backlog
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="backlogSourceMB">Source computer backlog, in MB</param>
        /// <param name="backlogTargetMB">Target computer backlog, in MB</param>
        public delegate void WaitingForLockQueueEventHandler(string sourceFilePath, string targetFilePath, int backlogSourceMB, int backlogTargetMB);

        /// <summary>
        /// Event raised after waiting for 5 minutes
        /// </summary>
        public event WaitingForLockQueueNotifyLockFilePathsEventHandler WaitingForLockQueueNotifyLockFilePaths;

        /// <summary>
        /// Information about lock files associated with the current wait
        /// </summary>
        /// <param name="sourceLockFilePath">Source lock file path</param>
        /// <param name="targetLockFilePath">Target lock file path</param>
        /// <param name="adminBypassMessage">Message that describes deleting the lock files to abort the wait</param>
        public delegate void WaitingForLockQueueNotifyLockFilePathsEventHandler(string sourceLockFilePath, string targetLockFilePath, string adminBypassMessage);

        /// <summary>
        /// Event is raised if we wait to long for our turn in the lock file queue
        /// </summary>
        public event LockQueueTimedOutEventHandler LockQueueTimedOut;

        /// <summary>
        /// Event is raised if we wait to long for our turn in the lock file queue
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="waitTimeMinutes"></param>
        public delegate void LockQueueTimedOutEventHandler(string sourceFilePath, string targetFilePath, double waitTimeMinutes);

        /// <summary>
        /// Event is raised when we are done waiting for our turn in the lock file queue
        /// </summary>
        public event LockQueueWaitCompleteEventHandler LockQueueWaitComplete;

        /// <summary>
        /// Event is raised when we are done waiting for our turn in the lock file queue
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="waitTimeMinutes"></param>
        public delegate void LockQueueWaitCompleteEventHandler(string sourceFilePath, string targetFilePath, double waitTimeMinutes);

        #endregion

        #region "Constants and class members"

        private const int MAX_LOCKFILE_WAIT_TIME_MINUTES = 180;

        /// <summary>
        /// Minimum source file size (in MB) for the lock queue to be used
        /// </summary>
        public const int LOCKFILE_MINIMUM_SOURCE_FILE_SIZE_MB = 20;

        private const int LOCKFILE_TRANSFER_THRESHOLD_MB = 1000;

        private const string LOCKFILE_EXTENSION = ".lock";

        private const int DEFAULT_VERSION_COUNT_TO_KEEP = 9;

        /// <summary>
        /// Standard date/time formatting
        /// </summary>
        public const string DATE_TIME_FORMAT = "yyyy-MM-dd hh:mm:ss tt";

        private int mChunkSizeMB = DEFAULT_CHUNK_SIZE_MB;

        private int mFlushThresholdMB = DEFAULT_FLUSH_THRESHOLD_MB;

        private DateTime mLastGC = DateTime.UtcNow;

        private readonly Regex mInvalidDosChars;

        private readonly Regex mParseLockFileName;

        #endregion

        #region "Public constants"

        /// <summary>
        /// Used by CopyFileWithResume and CopyDirectoryWithResume when copying a file byte-by-byte and supporting resuming the copy if interrupted
        /// </summary>
        public const int DEFAULT_CHUNK_SIZE_MB = 1;

        /// <summary>
        /// Used by CopyFileWithResume; defines how often the data is flushed out to disk; must be larger than the ChunkSize
        /// </summary>
        public const int DEFAULT_FLUSH_THRESHOLD_MB = 25;

        #endregion

        #region "Enums"

        /// <summary>
        /// File overwrite options
        /// </summary>
        public enum FileOverwriteMode
        {
            /// <summary>
            /// Do not overwrite
            /// </summary>
            /// <remarks>An exception will be thrown if you try to overwrite an existing file</remarks>
            DoNotOverwrite = 0,
            /// <summary>
            /// Always overwrite
            /// </summary>
            AlwaysOverwrite = 1,
            /// <summary>
            /// OverWrite if source date newer (or if same date but length differs)
            /// </summary>
            OverwriteIfSourceNewer = 2,
            /// <summary>
            /// OverWrite if any difference in size or date; note that newer files in target directory will get overwritten since their date doesn't match
            /// </summary>
            OverWriteIfDateOrLengthDiffer = 3
        }

        /// <summary>
        /// Copy status
        /// </summary>
        public enum CopyStatus
        {
            /// <summary>
            /// Not copying a file
            /// </summary>
            Idle = 0,
            /// <summary>
            /// File is being copied via .NET and cannot be resumed
            /// </summary>
            NormalCopy = 1,
            /// <summary>
            /// File is being copied in chunks and can be resumed
            /// </summary>
            BufferedCopy = 2,
            /// <summary>
            /// Resuming copying a file in chunks
            /// </summary>
            BufferedCopyResume = 3
        }
        #endregion

        #region "Properties"

        /// <summary>
        /// Copy chunk size, in MB
        /// </summary>
        /// <remarks>Used by CopyFileWithResume</remarks>
        public int CopyChunkSizeMB
        {
            get => mChunkSizeMB;
            set
            {
                if (value < 1)
                    value = 1;
                mChunkSizeMB = value;
            }
        }

        /// <summary>
        /// Copy flush threshold, in MB
        /// Cached data is written to disk when this threshold is reached
        /// </summary>
        /// <remarks>Used by CopyFileWithResume</remarks>
        public int CopyFlushThresholdMB
        {
            get => mFlushThresholdMB;
            set
            {
                if (value < 1)
                    value = 1;
                mFlushThresholdMB = value;
            }
        }

        /// <summary>
        /// Current copy status
        /// </summary>
        public CopyStatus CurrentCopyStatus { get; set; } = CopyStatus.Idle;

        /// <summary>
        /// Current source file path
        /// </summary>
        public string CurrentSourceFile { get; set; } = string.Empty;

        /// <summary>
        /// Debug level controls the level of sending messages by raising StatusEvent events
        /// </summary>
        /// <remarks>1 results in fewer messages; 2 for additional messages, 3 for all messages</remarks>
        public int DebugLevel { get; set; }

        /// <summary>
        /// Manager name (used when creating lock files)
        /// </summary>
        public string ManagerName { get; set; }

        #endregion

        #region "Constructors"

        /// <summary>
        /// Constructor
        /// </summary>
        public FileTools() : this("Unknown-Manager", 1)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="managerName">Manager name</param>
        /// <param name="debugLevel">1 results in fewer messages; 2 for additional messages, 3 for all messages</param>
        public FileTools(string managerName, int debugLevel)
        {
            ManagerName = managerName;
            DebugLevel = debugLevel;

            mInvalidDosChars = new Regex(@"[\\/:*?""<>| ]", RegexOptions.Compiled);

            mParseLockFileName = new Regex(@"^(?<QueueTime>\d+)_(?<FileSizeMB>\d+)_", RegexOptions.Compiled);
        }
        #endregion

        #region "CheckTerminator Methods"

        /// <summary>
        /// Modifies input directory path string depending on optional settings.
        /// Overload for all parameters specified
        /// </summary>
        /// <param name="directoryPath">The input directory path.</param>
        /// <param name="addTerm">Specifies whether the directory path string ends with the specified directory separation character.</param>
        /// <param name="termChar">The specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string directoryPath, bool addTerm, char termChar)
        {
            return CheckTerminatorEX(directoryPath, addTerm, termChar);
        }

        /// <summary>
        /// Adds or removes the DOS path separation character from the end of the directory path.
        /// </summary>
        /// <param name="directoryPath">The input directory path.</param>
        /// <param name="addTerm">Specifies whether the directory path string ends with the specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string directoryPath, bool addTerm)
        {
            return CheckTerminatorEX(directoryPath, addTerm, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Assures the directory path ends with the specified path separation character.
        /// </summary>
        /// <param name="directoryPath">The input directory path.</param>
        /// <param name="termChar">The specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string directoryPath, string termChar)
        {
            if (!string.IsNullOrWhiteSpace(termChar) && termChar.Length > 0)
                return CheckTerminatorEX(directoryPath, addTerm: true, termChar: termChar[0]);
            else
                return CheckTerminatorEX(directoryPath, addTerm: true, termChar: Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Assures the directory path ends with the DOS path separation character.
        /// Overload for using all defaults (add DOS terminator char)
        /// </summary>
        /// <param name="directoryPath">The input directory path.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string directoryPath)
        {
            return CheckTerminatorEX(directoryPath, addTerm: true, termChar: Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Modifies input directory path string depending on addTerm
        /// </summary>
        /// <param name="directoryPath">The input directory path.</param>
        /// <param name="addTerm">Specifies whether the directory path should end with the specified directory separation character</param>
        /// <param name="termChar">The specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        /// <remarks>addTerm=True forces the path to end with specified termChar while addTerm=False will remove termChar from the end if present</remarks>
        private static string CheckTerminatorEX(string directoryPath, bool addTerm, char termChar)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return directoryPath;
            }

            if (addTerm)
            {
                if (directoryPath.EndsWith(termChar.ToString()))
                {
                    return directoryPath;
                }
                return directoryPath + termChar;
            }

            if (directoryPath.EndsWith(termChar.ToString()))
            {
                return directoryPath.TrimEnd(termChar);
            }

            return directoryPath;
        }

        #endregion

        #region "CopyFile Method"

        /// <summary>
        /// Copies a source file to the destination file. Does not allow overwriting.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        public void CopyFile(string sourcePath, string destPath)
        {
            // Overload with overWrite set to default (false)
            const bool backupDestFileBeforeCopy = false;
            CopyFileEx(sourcePath, destPath, overWrite: false, backupDestFileBeforeCopy: backupDestFileBeforeCopy);
        }

        /// <summary>
        /// Copies a source file to the destination file
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        /// <param name="overWrite">True to overwrite</param>
        public void CopyFile(string sourcePath, string destPath, bool overWrite)
        {
            const bool backupDestFileBeforeCopy = false;
            CopyFile(sourcePath, destPath, overWrite, backupDestFileBeforeCopy);
        }

        /// <summary>
        /// Copies a source file to the destination file
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        /// <param name="overWrite">True to overwrite</param>
        /// <param name="backupDestFileBeforeCopy">True to backup the destination file before copying</param>
        public void CopyFile(string sourcePath, string destPath, bool overWrite, bool backupDestFileBeforeCopy)
        {
            CopyFile(sourcePath, destPath, overWrite, backupDestFileBeforeCopy, DEFAULT_VERSION_COUNT_TO_KEEP);
        }

        /// <summary>
        /// Copies a source file to the destination file. Allows overwriting.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        /// <param name="overWrite">True if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="backupDestFileBeforeCopy">True to backup the destination file before copying</param>
        /// <param name="versionCountToKeep">Number of backup copies to keep</param>
        public void CopyFile(string sourcePath, string destPath, bool overWrite, bool backupDestFileBeforeCopy, int versionCountToKeep)
        {
            CopyFileEx(sourcePath, destPath, overWrite, backupDestFileBeforeCopy, versionCountToKeep);
        }

        /// <summary>
        /// Copies a source file to the destination file. Allows overwriting.
        /// </summary>
        /// <remarks>
        /// This method is unique in that it allows you to specify a destination path where
        /// some of the directories do not already exist.  It will create them if they don't.
        /// The last parameter specifies whether a file already present in the
        /// destination directory will be overwritten
        /// - Note: requires Imports System.IO
        /// - Usage: CopyFile("C:\Misc\Bob.txt", "D:\MiscBackup\Bob.txt")
        /// </remarks>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destPath">The destination file path.</param>
        /// <param name="overWrite">True if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="backupDestFileBeforeCopy">True to backup the destination file before copying</param>
        /// <param name="versionCountToKeep">Number of backup copies to keep</param>
        private void CopyFileEx(string sourcePath, string destPath, bool overWrite,
            bool backupDestFileBeforeCopy, int versionCountToKeep = DEFAULT_VERSION_COUNT_TO_KEEP)
        {
            var directoryPath = Path.GetDirectoryName(destPath);

            if (directoryPath == null)
            {
                throw new DirectoryNotFoundException("Unable to determine the parent directory for " + destPath);
            }

            CreateDirectoryIfNotExists(directoryPath);

            if (backupDestFileBeforeCopy)
            {
                BackupFileBeforeCopy(destPath, versionCountToKeep);
            }

            if (DebugLevel >= 3)
            {
                OnStatusEvent("Copying file with CopyFileEx", sourcePath + " to " + destPath);
            }

            UpdateCurrentStatus(CopyStatus.NormalCopy, sourcePath);

            CopyFileNative(sourcePath, destPath, overWrite);

            UpdateCurrentStatusIdle();
        }

        /// <summary>
        /// Try to copy a file using File.Copy
        /// If the copy fails due to the path length being 260 characters or longer,
        /// and if we're running Windows, use CopyFileW in kernel32.dll instead
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        /// <param name="overwrite"></param>
        private void CopyFileNative(string sourcePath, string destPath, bool overwrite)
        {
            try
            {
                File.Copy(sourcePath, destPath, overwrite);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception)
            {
                if (!SystemInfo.IsLinux &&
                    (sourcePath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD ||
                     destPath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD))
                {
                    NativeIOFileTools.Copy(sourcePath, destPath, overwrite);
                }
                else
                {
                    throw;
                }
            }
        }

        #endregion

        #region "Lock File Copying Methods"

        /// <summary>
        /// Copy the source file to the target path; do not overwrite existing files
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="overWrite">True to overwrite existing files</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>If the file exists, will not copy the file but will still return true</remarks>
        public bool CopyFileUsingLocks(string sourceFilePath, string targetFilePath, bool overWrite)
        {
            return CopyFileUsingLocks(new FileInfo(sourceFilePath), targetFilePath, ManagerName, overWrite);
        }

        /// <summary>
        /// Copy the source file to the target path
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="managerName">Manager name (included in the lock file name)</param>
        /// <param name="overWrite">True to overWrite existing files</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>If the file exists yet overWrite is false, will not copy the file but will still return true</remarks>
        public bool CopyFileUsingLocks(string sourceFilePath, string targetFilePath, string managerName = "", bool overWrite = false)
        {
            if (string.IsNullOrWhiteSpace(managerName))
                managerName = ManagerName;

            return CopyFileUsingLocks(new FileInfo(sourceFilePath), targetFilePath, managerName, overWrite);
        }

        /// <summary>
        /// Copy the source file to the target path
        /// </summary>
        /// <param name="sourceFile">Source file object</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="overWrite">True to overWrite existing files</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>If the file exists yet overWrite is false, will not copy the file but will still return true</remarks>
        public bool CopyFileUsingLocks(FileInfo sourceFile, string targetFilePath, bool overWrite)
        {
            return CopyFileUsingLocks(sourceFile, targetFilePath, ManagerName, overWrite);
        }

        /// <summary>
        /// Copy the source file to the target path
        /// </summary>
        /// <param name="sourceFile">Source file object</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="managerName">Manager name (included in the lock file name)</param>
        /// <param name="overWrite">True to overWrite existing files</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>If the file exists yet overWrite is false, will not copy the file but will still return true</remarks>
        public bool CopyFileUsingLocks(FileInfo sourceFile, string targetFilePath, string managerName = "", bool overWrite = false)
        {
            var useLockFile = false;

            if (!overWrite && File.Exists(targetFilePath))
            {
                return true;
            }

            var targetFile = new FileInfo(targetFilePath);

            var lockDirectoryPathSource = GetLockDirectory(sourceFile);
            var lockDirectoryPathTarget = GetLockDirectory(targetFile);

            if (!string.IsNullOrEmpty(lockDirectoryPathSource) || !string.IsNullOrEmpty(lockDirectoryPathTarget))
            {
                useLockFile = true;
            }

            if (useLockFile)
            {
                var success = CopyFileUsingLocks(
                    lockDirectoryPathSource, lockDirectoryPathTarget,
                    sourceFile, targetFilePath,
                    managerName, overWrite);
                return success;
            }

            var expectedSourceLockDirectory = GetLockDirectoryPath(sourceFile);
            var expectedTargetLockDirectory = GetLockDirectoryPath(targetFile);

            if (string.IsNullOrEmpty(expectedSourceLockDirectory) && string.IsNullOrEmpty(expectedTargetLockDirectory))
            {
                // File is being copied locally; we don't use lock directories
                // Do not raise this as a DebugEvent
            }
            else
            {
                if (string.IsNullOrEmpty(expectedSourceLockDirectory))
                {
                    // Source file is local; lock directory would not be used
                    expectedSourceLockDirectory = "Source file is local";
                }

                if (string.IsNullOrEmpty(expectedTargetLockDirectory))
                {
                    // Target file is local; lock directory would not be used
                    expectedTargetLockDirectory = "Target file is local";
                }

                if (DebugLevel >= 2)
                {
                    OnStatusEvent("Lock file directory not found on the source or target",
                                  expectedSourceLockDirectory + " and " + expectedTargetLockDirectory);
                }
            }

            CopyFileEx(sourceFile.FullName, targetFilePath, overWrite, backupDestFileBeforeCopy: false);

            return true;
        }

        /// <summary>
        /// Given a file path, return the lock file directory if it exists
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns>Lock directory path if it exists</returns>
        /// <remarks>Lock directories are only returned for remote shares (shares that start with \\)</remarks>
        [Obsolete("Use GetLockDirectory")]
        public string GetLockFolder(FileInfo dataFile)
        {
            return GetLockDirectory(dataFile);
        }

        /// <summary>
        /// Given a file path, return the lock file directory if it exists
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns>Lock directory path if it exists</returns>
        /// <remarks>Lock directories are only returned for remote shares (shares that start with \\)</remarks>
        public string GetLockDirectory(FileInfo dataFile)
        {
            var lockDirectoryPath = GetLockDirectoryPath(dataFile);

            if (!string.IsNullOrEmpty(lockDirectoryPath) && Directory.Exists(lockDirectoryPath))
            {
                return lockDirectoryPath;
            }

            return string.Empty;
        }

        /// <summary>
        /// Given a file path, return the lock file directory path (does not verify that it exists)
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns>Lock directory path</returns>
        /// <remarks>Lock directories are only returned for remote shares (shares that start with \\)</remarks>
        private string GetLockDirectoryPath(FileInfo dataFile)
        {
            if (Path.IsPathRooted(dataFile.FullName))
            {
                var directory = dataFile.Directory;
                if (directory?.Root.FullName.StartsWith(@"\\") == true)
                {
                    return Path.Combine(GetServerShareBase(directory.Root.FullName), "DMS_LockFiles");
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Copy the source file to the target path
        /// </summary>
        /// <param name="lockDirectoryPathSource">Path to the lock directory for the source file; can be an empty string</param>
        /// <param name="lockDirectoryPathTarget">Path to the lock directory for the target file; can be an empty string</param>
        /// <param name="sourceFile">Source file object</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="managerName">Manager name (included in the lock file name)</param>
        /// <param name="overWrite">True to overWrite existing files</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>If the file exists yet overWrite is false, will not copy the file but will still return true</remarks>
        public bool CopyFileUsingLocks(
            string lockDirectoryPathSource, string lockDirectoryPathTarget,
            FileInfo sourceFile, string targetFilePath, string managerName, bool overWrite)
        {
            if (!overWrite && File.Exists(targetFilePath))
            {
                if (DebugLevel >= 2)
                {
                    OnStatusEvent("Skipping file since target exists", targetFilePath);
                }
                return true;
            }

            // Examine the size of the source file
            // If less than LOCKFILE_MINIMUM_SOURCE_FILE_SIZE_MB then
            // copy the file normally
            var sourceFileSizeMB = Convert.ToInt32(sourceFile.Length / 1024.0 / 1024.0);
            if (sourceFileSizeMB < LOCKFILE_MINIMUM_SOURCE_FILE_SIZE_MB ||
                string.IsNullOrWhiteSpace(lockDirectoryPathSource) && string.IsNullOrWhiteSpace(lockDirectoryPathTarget))
            {
                const bool backupDestFileBeforeCopy = false;
                if (DebugLevel >= 2)
                {
                    var debugMsg = string.Format(
                        "File to copy is {0:F2} MB, which is less than {1} MB; will use CopyFileEx for {2}",
                        sourceFile.Length / 1024.0 / 1024.0, LOCKFILE_MINIMUM_SOURCE_FILE_SIZE_MB, sourceFile.Name);

                    OnStatusEvent(debugMsg, sourceFile.FullName);
                }

                CopyFileEx(sourceFile.FullName, targetFilePath, overWrite, backupDestFileBeforeCopy);
                return true;
            }

            var lockFilePathSource = string.Empty;
            var lockFilePathTarget = string.Empty;

            try
            {
                // Create a new lock file on the source and/or target server
                // This file indicates an intent to copy a file

                DirectoryInfo lockDirectorySource = null;
                DirectoryInfo lockDirectoryTarget = null;
                var lockFileTimestamp = GetLockFileTimeStamp();

                if (!string.IsNullOrWhiteSpace(lockDirectoryPathSource))
                {
                    lockDirectorySource = new DirectoryInfo(lockDirectoryPathSource);
                    lockFilePathSource = CreateLockFile(lockDirectorySource, lockFileTimestamp, sourceFile, targetFilePath, managerName);
                }

                if (!string.IsNullOrWhiteSpace(lockDirectoryPathTarget))
                {
                    lockDirectoryTarget = new DirectoryInfo(lockDirectoryPathTarget);
                    lockFilePathTarget = CreateLockFile(lockDirectoryTarget, lockFileTimestamp, sourceFile, targetFilePath, managerName);
                }

                WaitForLockFileQueue(lockFileTimestamp, lockDirectorySource, lockDirectoryTarget,
                                     sourceFile, targetFilePath, MAX_LOCKFILE_WAIT_TIME_MINUTES,
                                     lockFilePathSource, lockFilePathTarget);

                if (DebugLevel >= 1)
                {
                    OnStatusEvent("Copying " + sourceFile.Name + " using Locks", sourceFile.FullName + " to " + targetFilePath);
                }

                // Perform the copy
                const bool backupDestFileBeforeCopy = false;
                CopyFileEx(sourceFile.FullName, targetFilePath, overWrite, backupDestFileBeforeCopy);

                // Delete the lock file(s)
                DeleteFileIgnoreErrors(lockFilePathSource);
                DeleteFileIgnoreErrors(lockFilePathTarget);
            }
            catch (Exception)
            {
                // Error occurred
                // Delete the lock file then throw the exception
                DeleteFileIgnoreErrors(lockFilePathSource);
                DeleteFileIgnoreErrors(lockFilePathTarget);

                throw;
            }

            return true;
        }

        /// <summary>
        /// Create a lock file in the specified lock directory
        /// </summary>
        /// <param name="lockDirectory"></param>
        /// <param name="lockFileTimestamp"></param>
        /// <param name="sourceFile"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="managerName"></param>
        /// <returns>Full path to the lock file; empty string if an error or if lockDirectory is null</returns>
        public string CreateLockFile(DirectoryInfo lockDirectory, long lockFileTimestamp, FileInfo sourceFile, string targetFilePath, string managerName)
        {
            if (lockDirectory == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(managerName))
            {
                managerName = "UnknownManager";
            }

            // Define the lock file name
            var lockFileName = GenerateLockFileName(lockFileTimestamp, sourceFile, managerName);
            var lockFilePath = Path.Combine(lockDirectory.FullName, lockFileName);
            while (File.Exists(lockFilePath))
            {
                // File already exists for this manager; append a dash to the path
                lockFileName = Path.GetFileNameWithoutExtension(lockFileName) + "-" + Path.GetExtension(lockFileName);
                lockFilePath = Path.Combine(lockDirectory.FullName, lockFileName);
            }

            try
            {
                // Create the lock file
                using (var writer = new StreamWriter(new FileStream(lockFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.WriteLine("Date: " + DateTime.Now.ToString(DATE_TIME_FORMAT));
                    writer.WriteLine("Source: " + sourceFile.FullName);
                    writer.WriteLine("Target: " + targetFilePath);
                    writer.WriteLine("Size_Bytes: " + sourceFile.Length);
                    writer.WriteLine("Manager: " + managerName);
                }

                OnStatusEvent("Created lock file in " + lockDirectory.FullName, lockFilePath);
            }
            catch (Exception ex)
            {
                // Error creating the lock file
                // Return an empty string
                OnWarningEvent("Error creating lock file in " + lockDirectory.FullName + ": " + ex.Message);
                return string.Empty;
            }

            return lockFilePath;
        }

        /// <summary>
        /// Attempts to create a directory only if it doesn't exist. Parent Directory must exist
        /// </summary>
        /// <param name="directoryPath"></param>
        public void CreateDirectoryIfNotExists(string directoryPath)
        {
            // Possible future change: add another version that handles nested, non-existing directories
            if (directoryPath.Length < NativeIODirectoryTools.DIRECTORY_PATH_LENGTH_THRESHOLD)
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            else
            {
                if (!NativeIODirectoryTools.Exists(directoryPath))
                {
                    NativeIODirectoryTools.CreateDirectory(directoryPath);
                }
            }
        }

        /// <summary>
        /// Deletes the specified directory and all subdirectories
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns>True if success, false if an error</returns>
        public bool DeleteDirectory(string directoryPath)
        {
            return DeleteDirectory(directoryPath, ignoreErrors: false);
        }

        /// <summary>
        /// Deletes the specified directory and all subdirectories
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="ignoreErrors"></param>
        /// <returns>True if success, false if an error</returns>
        public bool DeleteDirectory(string directoryPath, bool ignoreErrors)
        {
            var targetDirectory = new DirectoryInfo(directoryPath);

            try
            {
                if (targetDirectory.Exists)
                    targetDirectory.Delete(true);
            }
            catch (Exception)
            {
                // Problems deleting one or more of the files
                if (!ignoreErrors)
                    throw;

                // Collect garbage, then delete the files one-by-one
                ProgRunner.GarbageCollectNow();

                return DeleteDirectoryFiles(directoryPath, deleteDirectoryIfEmpty: true);
            }

            return true;
        }

        /// <summary>
        /// Deletes the specified directory and all subdirectories; does not delete the target directory
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Deletes each file individually.  Deletion errors are reported but are not treated as a fatal error</remarks>
        public bool DeleteDirectoryFiles(string directoryPath)
        {
            return DeleteDirectoryFiles(directoryPath, deleteDirectoryIfEmpty: false);
        }

        /// <summary>
        /// Deletes the specified directory and all subdirectories
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="deleteDirectoryIfEmpty">Set to True to delete the directory, if it is empty</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Deletes each file individually.  Deletion errors are reported but are not treated as a fatal error</remarks>
        public bool DeleteDirectoryFiles(string directoryPath, bool deleteDirectoryIfEmpty)
        {
            var targetDirectory = new DirectoryInfo(directoryPath);
            if (!targetDirectory.Exists)
                return true;

            var errorCount = 0;

            foreach (var targetFile in targetDirectory.GetFiles("*", SearchOption.AllDirectories))
            {
                if (!DeleteFileIgnoreErrors(targetFile.FullName))
                {
                    errorCount++;
                }
            }

            if (errorCount == 0 && deleteDirectoryIfEmpty)
            {
                try
                {
                    targetDirectory.Delete(true);
                }
                catch (Exception ex)
                {
                    OnWarningEvent("Error removing empty directory", "Unable to delete directory " + targetDirectory.FullName + ": " + ex.Message);
                    errorCount++;
                }
            }

            return errorCount == 0;
        }

        /// <summary>
        /// Delete the specified file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>True if successfully deleted (or if the file doesn't exist); false if an error</returns>
        /// <remarks>
        /// If the initial attempt fails, checks the ReadOnly bit and tries again.
        /// If not ReadOnly, performs a garbage collection (minimum 500 msec between GC calls).</remarks>
        private bool DeleteFileIgnoreErrors(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            var targetFile = new FileInfo(filePath);

            try
            {
                DeleteFileNative(targetFile);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore
            }

            try
            {
                // The file might be ReadOnly; check for this then re-try the delete
                if (targetFile.IsReadOnly)
                {
                    targetFile.IsReadOnly = false;
                }
                else
                {
                    if (DateTime.UtcNow.Subtract(mLastGC).TotalMilliseconds >= 500)
                    {
                        mLastGC = DateTime.UtcNow;
                        ProgRunner.GarbageCollectNow();
                    }
                }

                DeleteFileNative(targetFile);
            }
            catch (Exception ex)
            {
                // Ignore errors here
                OnWarningEvent("Error deleting file " + targetFile.Name, "Unable to delete file " + targetFile.FullName + ": " + ex.Message);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Try to delete a file using File.Delete
        /// If the delete fails due to the path length being 260 characters or longer,
        /// and if we're running Windows, use DeleteFileW in kernel32.dll instead
        /// </summary>
        /// <param name="targetFile"></param>
        // ReSharper disable once SuggestBaseTypeForParameter
        private void DeleteFileNative(FileInfo targetFile)
        {
            try
            {
                if (targetFile.Exists)
                {
                    targetFile.Delete();
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception)
            {
                if (!SystemInfo.IsLinux && targetFile.FullName.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD)
                {
                    NativeIOFileTools.Delete(targetFile.FullName);
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Finds lock files with a timestamp less than
        /// </summary>
        /// <param name="lockDirectory"></param>
        /// <param name="lockFileTimestamp"></param>
        private List<int> FindLockFiles(DirectoryInfo lockDirectory, long lockFileTimestamp)
        {
            var lockFiles = new List<int>();

            if (lockDirectory == null)
            {
                return lockFiles;
            }

            lockDirectory.Refresh();

            foreach (var lockFile in lockDirectory.GetFiles("*" + LOCKFILE_EXTENSION))
            {
                var reMatch = mParseLockFileName.Match(lockFile.Name);

                if (!reMatch.Success)
                    continue;

                if (!long.TryParse(reMatch.Groups["QueueTime"].Value, out var queueTimeMSec))
                    continue;

                if (!int.TryParse(reMatch.Groups["FileSizeMB"].Value, out var fileSizeMB))
                    continue;

                if (queueTimeMSec >= lockFileTimestamp)
                    continue;

                // Lock file lockFile was created prior to the current one
                // Make sure it's less than 1 hour old
                if (Math.Abs((lockFileTimestamp - queueTimeMSec) / 1000.0 / 60.0) < MAX_LOCKFILE_WAIT_TIME_MINUTES)
                {
                    lockFiles.Add(fileSizeMB);
                }
            }

            return lockFiles;
        }

        /// <summary>
        /// Generate the lock file name, which starts with a msec-based timestamp,
        /// then has the source file size (in MB),
        /// then has information on the machine creating the file
        /// </summary>
        /// <param name="lockFileTimestamp"></param>
        /// <param name="sourceFile"></param>
        /// <param name="managerName"></param>
        private string GenerateLockFileName(long lockFileTimestamp, FileInfo sourceFile, string managerName)
        {
            if (string.IsNullOrWhiteSpace(managerName))
            {
                managerName = "UnknownManager";
            }

            var hostName = Dns.GetHostName();

            if (hostName.Contains("."))
            {
                hostName = hostName.Substring(0, hostName.IndexOf('.'));
            }

            foreach (var invalidChar in Path.GetInvalidPathChars())
            {
                if (hostName.Contains(invalidChar))
                    hostName = hostName.Replace(invalidChar, '_');
            }

            var lockFileName = lockFileTimestamp + "_" +
                               (sourceFile.Length / 1024.0 / 1024.0).ToString("0000") + "_" +
                               hostName + "_" +
                               managerName + LOCKFILE_EXTENSION;

            // Replace any invalid characters (including spaces) with an underscore
            return mInvalidDosChars.Replace(lockFileName, "_");
        }

        /// <summary>
        /// Get the time stamp to be used when naming a lock file
        /// </summary>
        public long GetLockFileTimeStamp()
        {
            return (long)Math.Round(DateTime.UtcNow.Subtract(new DateTime(2010, 1, 1)).TotalMilliseconds, 0);
        }

        /// <summary>
        /// Returns the first portion of a network share path, for example \\MyServer is returned for \\MyServer\Share\Filename.txt
        /// </summary>
        /// <param name="serverSharePath"></param>
        /// <remarks>Treats \\picfs as a special share since DMS-related files are at \\picfs\projects\DMS</remarks>
        public string GetServerShareBase(string serverSharePath)
        {
            if (!serverSharePath.StartsWith(@"\\"))
                return string.Empty;

            var slashIndex = serverSharePath.IndexOf('\\', 2);
            if (slashIndex <= 0)
                return serverSharePath;

            var serverShareBase = serverSharePath.Substring(0, slashIndex);
            if (string.Equals(serverShareBase, @"\\picfs", StringComparison.OrdinalIgnoreCase))
            {
                serverShareBase = @"\\picfs\projects\DMS";
            }
            return serverShareBase;
        }

        #endregion

        #region "CopyDirectory Method"

        /// <summary>
        /// Copies a source directory to the destination directory. Does not allow overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        public void CopyDirectory(string sourcePath, string destPath)
        {
            CopyDirectory(sourcePath, destPath, overWrite: false);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Does not allow overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="managerName"></param>
        public void CopyDirectory(string sourcePath, string destPath, string managerName)
        {
            CopyDirectory(sourcePath, destPath, overWrite: false, managerName: managerName);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Does not allow overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        public void CopyDirectory(string sourcePath, string destPath, List<string> fileNamesToSkip)
        {
            CopyDirectory(sourcePath, destPath, overWrite: false, fileNamesToSkip: fileNamesToSkip);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        public void CopyDirectory(string sourcePath, string destPath, bool overWrite)
        {
            const bool readOnly = false;
            CopyDirectory(sourcePath, destPath, overWrite, readOnly);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="managerName"></param>
        public void CopyDirectory(string sourcePath, string destPath, bool overWrite, string managerName)
        {
            const bool readOnly = false;
            CopyDirectory(sourcePath, destPath, overWrite, readOnly, new List<string>(), managerName);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        public void CopyDirectory(string sourcePath, string destPath, bool overWrite, List<string> fileNamesToSkip)
        {
            const bool readOnly = false;
            CopyDirectory(sourcePath, destPath, overWrite, readOnly, fileNamesToSkip);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="readOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        public void CopyDirectory(string sourcePath, string destPath, bool overWrite, bool readOnly)
        {
            const bool setAttribute = true;
            CopyDirectoryEx(sourcePath, destPath, overWrite, setAttribute, readOnly, new List<string>(), ManagerName);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="readOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        public void CopyDirectory(string sourcePath, string destPath, bool overWrite, bool readOnly, List<string> fileNamesToSkip)
        {
            const bool setAttribute = true;
            CopyDirectoryEx(sourcePath, destPath, overWrite, setAttribute, readOnly, fileNamesToSkip, ManagerName);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="readOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <param name="managerName"></param>
        public void CopyDirectory(string sourcePath, string destPath, bool overWrite, bool readOnly, List<string> fileNamesToSkip, string managerName)
        {
            const bool setAttribute = true;
            CopyDirectoryEx(sourcePath, destPath, overWrite, setAttribute, readOnly, fileNamesToSkip, managerName);
        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <remarks>Usage: CopyDirectory("C:\Misc", "D:\MiscBackup")</remarks>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="setAttribute">true if the read-only attribute of the destination file is to be modified, false otherwise.</param>
        /// <param name="readOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <param name="managerName">Name of the calling program; used when calling CopyFileUsingLocks</param>
        private void CopyDirectoryEx(string sourcePath, string destPath, bool overWrite, bool setAttribute, bool readOnly,
            IReadOnlyCollection<string> fileNamesToSkip, string managerName)
        {
            // Paths > 248 characters are okay for DirectoryInfo with .NET >= 4.6.2
            var sourceDir = new DirectoryInfo(sourcePath);
            var destDir = new DirectoryInfo(destPath);

            // The source directory must exist, otherwise throw an exception
            if (!sourceDir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist: " + sourceDir.FullName);
            }

            // Verify the parent directory of the destination directory
            if (destDir.Parent?.Exists == false)
            {
                throw new DirectoryNotFoundException("Destination directory does not exist: " + destDir.Parent.FullName);
            }

            if (!destDir.Exists)
            {
                if (destPath.Length < NativeIODirectoryTools.DIRECTORY_PATH_LENGTH_THRESHOLD)
                {
                    // Issue: Throws an exception if the directory path > 248 characters
                    destDir.Create();
                }
                else
                {
                    NativeIODirectoryTools.CreateDirectory(destPath);
                }
            }

            // Copy the values from fileNamesToSkip to sortedFileNames so that we can perform case-insensitive searching
            var sortedFileNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (fileNamesToSkip != null)
            {
                foreach (var fileName in fileNamesToSkip)
                {
                    sortedFileNames.Add(fileName);
                }
            }

            // Copy all the files of the current directory
            foreach (var childFile in sourceDir.GetFiles())
            {
                // Look for both the file name and the full path in sortedFileNames
                // If either matches, do not copy the file
                bool copyFile;
                if (sortedFileNames.Contains(childFile.Name))
                {
                    copyFile = false;
                }
                else if (sortedFileNames.Contains(childFile.FullName))
                {
                    copyFile = false;
                }
                else
                {
                    copyFile = true;
                }

                if (!copyFile)
                    continue;

                var targetFilePath = Path.Combine(destDir.FullName, childFile.Name);

                if (overWrite)
                {
                    UpdateCurrentStatus(CopyStatus.NormalCopy, childFile.FullName);
                    CopyFileUsingLocks(childFile, targetFilePath, managerName, overWrite: true);
                }
                else
                {
                    // If overWrite = false, copy the file only if it does not exist
                    // this is done to avoid an IOException if a file already exists
                    // this way the other files can be copied anyway...
                    if (!File.Exists(targetFilePath))
                    {
                        UpdateCurrentStatus(CopyStatus.NormalCopy, childFile.FullName);
                        CopyFileUsingLocks(childFile, targetFilePath, managerName, overWrite: false);
                    }
                }

                if (setAttribute)
                {
                    UpdateReadonlyAttribute(childFile, targetFilePath, readOnly);
                }

                UpdateCurrentStatusIdle();
            }

            // Copy all the sub-directories by recursively calling this same routine
            foreach (var subDirectory in sourceDir.GetDirectories())
            {
                if (subDirectory.FullName.Equals(destDir.FullName))
                {
                    // Skip this subdirectory since it is our destination directory
                    continue;
                }

                CopyDirectoryEx(subDirectory.FullName, Path.Combine(destDir.FullName, subDirectory.Name),
                                overWrite, setAttribute, readOnly, fileNamesToSkip, managerName);
            }
        }

        /// <summary>
        /// Copies the file attributes from a source file to a target file, explicitly updating the read-only bit based on readOnly
        /// </summary>
        /// <param name="sourceFile">Source FileInfo</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="readOnly">True to force the ReadOnly bit on, False to force it off</param>
        private void UpdateReadonlyAttribute(FileSystemInfo sourceFile, string targetFilePath, bool readOnly)
        {
            // Get the file attributes from the source file
            var fa = sourceFile.Attributes;
            FileAttributes faNew;

            // Change the read-only attribute to the desired value
            if (readOnly)
            {
                faNew = fa | FileAttributes.ReadOnly;
            }
            else
            {
                faNew = fa & ~FileAttributes.ReadOnly;
            }

            if (fa != faNew)
            {
                // Set the attributes of the destination file
                File.SetAttributes(targetFilePath, fa);
            }
        }

        #endregion

        #region "CopyDirectoryWithResume Method"

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// Overwrites existing files if they differ in modification time or size.
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceDirectoryPath">The source directory path.</param>
        /// <param name="targetDirectoryPath">The destination directory path.</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(string sourceDirectoryPath, string targetDirectoryPath)
        {
            const bool recurse = false;
            const FileOverwriteMode fileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer;
            var fileNamesToSkip = new List<string>();

            return CopyDirectoryWithResume(sourceDirectoryPath, targetDirectoryPath, recurse, fileOverwriteMode, fileNamesToSkip);
        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// Overwrites existing files if they differ in modification time or size.
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceDirectoryPath">The source directory path.</param>
        /// <param name="targetDirectoryPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="ignoreFileLocks">When true, copy the file even if another program has it open for writing</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(string sourceDirectoryPath, string targetDirectoryPath, bool recurse, bool ignoreFileLocks = false)
        {
            const FileOverwriteMode fileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer;
            var fileNamesToSkip = new List<string>();

            return CopyDirectoryWithResume(sourceDirectoryPath, targetDirectoryPath, recurse, fileOverwriteMode, fileNamesToSkip, ignoreFileLocks);
        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceDirectoryPath">The source directory path.</param>
        /// <param name="targetDirectoryPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <param name="ignoreFileLocks">When true, copy the file even if another program has it open for writing</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceDirectoryPath, string targetDirectoryPath,
            bool recurse, FileOverwriteMode fileOverwriteMode, List<string> fileNamesToSkip,
            bool ignoreFileLocks = false)
        {
            const bool setAttribute = false;
            const bool readOnly = false;

            return CopyDirectoryWithResume(sourceDirectoryPath, targetDirectoryPath, recurse, fileOverwriteMode, setAttribute, readOnly,
                fileNamesToSkip, out _, out _, out _, ignoreFileLocks);
        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceDirectoryPath">The source directory path.</param>
        /// <param name="targetDirectoryPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="fileCountSkipped">Number of files skipped (output)</param>
        /// <param name="fileCountResumed">Number of files resumed (output)</param>
        /// <param name="fileCountNewlyCopied">Number of files newly copied (output)</param>
        /// <param name="ignoreFileLocks">When true, copy the file even if another program has it open for writing</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceDirectoryPath, string targetDirectoryPath,
            bool recurse, FileOverwriteMode fileOverwriteMode,
            out int fileCountSkipped, out int fileCountResumed, out int fileCountNewlyCopied,
            bool ignoreFileLocks = false)
        {
            const bool setAttribute = false;
            const bool readOnly = false;
            var fileNamesToSkip = new List<string>();

            return CopyDirectoryWithResume(sourceDirectoryPath, targetDirectoryPath, recurse, fileOverwriteMode, setAttribute, readOnly,
                fileNamesToSkip, out fileCountSkipped, out fileCountResumed, out fileCountNewlyCopied, ignoreFileLocks);
        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceDirectoryPath">The source directory path.</param>
        /// <param name="targetDirectoryPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <param name="fileCountSkipped">Number of files skipped (output)</param>
        /// <param name="fileCountResumed">Number of files resumed (output)</param>
        /// <param name="fileCountNewlyCopied">Number of files newly copied (output)</param>
        /// <param name="ignoreFileLocks">When true, copy the file even if another program has it open for writing</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceDirectoryPath, string targetDirectoryPath,
            bool recurse, FileOverwriteMode fileOverwriteMode, List<string> fileNamesToSkip,
            out int fileCountSkipped, out int fileCountResumed, out int fileCountNewlyCopied,
            bool ignoreFileLocks = false)
        {
            const bool setAttribute = false;
            const bool readOnly = false;

            return CopyDirectoryWithResume(sourceDirectoryPath, targetDirectoryPath, recurse, fileOverwriteMode, setAttribute, readOnly,
                fileNamesToSkip, out fileCountSkipped, out fileCountResumed, out fileCountNewlyCopied, ignoreFileLocks);
        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceDirectoryPath">The source directory path.</param>
        /// <param name="targetDirectoryPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="setAttribute">True if the read-only attribute of the destination file is to be modified, false otherwise.</param>
        /// <param name="readOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <param name="fileCountSkipped">Number of files skipped (output)</param>
        /// <param name="fileCountResumed">Number of files resumed (output)</param>
        /// <param name="fileCountNewlyCopied">Number of files newly copied (output)</param>
        /// <param name="ignoreFileLocks">When true, copy the file even if another program has it open for writing</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceDirectoryPath, string targetDirectoryPath,
            bool recurse, FileOverwriteMode fileOverwriteMode,
            bool setAttribute, bool readOnly, List<string> fileNamesToSkip,
            out int fileCountSkipped, out int fileCountResumed, out int fileCountNewlyCopied,
            bool ignoreFileLocks = false)
        {
            var success = true;

            fileCountSkipped = 0;
            fileCountResumed = 0;
            fileCountNewlyCopied = 0;

            var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);
            var targetDirectory = new DirectoryInfo(targetDirectoryPath);

            // The source directory must exist, otherwise throw an exception
            if (!sourceDirectory.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist: " + sourceDirectory.FullName);
            }

            if (targetDirectory.Parent == null)
            {
                throw new DirectoryNotFoundException("Unable to determine the parent directory of " + targetDirectory.FullName);
            }
            // Verify the parent directory of the destination directory
            if (!targetDirectory.Parent.Exists)
            {
                throw new DirectoryNotFoundException("Destination directory does not exist: " + targetDirectory.Parent.FullName);
            }

            if (sourceDirectory.FullName == targetDirectory.FullName)
            {
                throw new IOException("Source and target directories cannot be the same: " + targetDirectory.FullName);
            }

            try
            {
                // Create the target directory if necessary
                if (!targetDirectory.Exists)
                {
                    // TODO: Potential issues with path length > 248 characters
                    targetDirectory.Create();
                }

                // Copy the values from fileNamesToSkip to sortedFileNames so that we can perform case-insensitive searching
                var sortedFileNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                if (fileNamesToSkip != null)
                {
                    foreach (var item in fileNamesToSkip)
                    {
                        sortedFileNames.Add(item);
                    }
                }

                // Copy all the files of the current directory

                foreach (var sourceFile in sourceDirectory.GetFiles())
                {
                    // Look for both the file name and the full path in sortedFileNames
                    // If either matches, do not copy the file
                    bool copyFile;
                    if (sortedFileNames.Contains(sourceFile.Name))
                    {
                        copyFile = false;
                    }
                    else if (sortedFileNames.Contains(sourceFile.FullName))
                    {
                        copyFile = false;
                    }
                    else
                    {
                        copyFile = true;
                    }

                    if (copyFile)
                    {
                        // Does file already exist?
                        var existingFile = new FileInfo(Path.Combine(targetDirectory.FullName, sourceFile.Name));

                        if (existingFile.Exists)
                        {
                            switch (fileOverwriteMode)
                            {
                                case FileOverwriteMode.AlwaysOverwrite:
                                    copyFile = true;
                                    break;

                                case FileOverwriteMode.DoNotOverwrite:
                                    copyFile = false;
                                    break;

                                case FileOverwriteMode.OverwriteIfSourceNewer:
                                    if (sourceFile.LastWriteTimeUtc < existingFile.LastWriteTimeUtc ||
                                        NearlyEqualFileTimes(sourceFile.LastWriteTimeUtc, existingFile.LastWriteTimeUtc) &&
                                        existingFile.Length == sourceFile.Length)
                                    {
                                        copyFile = false;
                                    }
                                    break;

                                case FileOverwriteMode.OverWriteIfDateOrLengthDiffer:
                                    // File exists; if size and last modified time are the same then don't copy
                                    if (NearlyEqualFileTimes(sourceFile.LastWriteTimeUtc, existingFile.LastWriteTimeUtc) &&
                                        existingFile.Length == sourceFile.Length)
                                    {
                                        copyFile = false;
                                    }
                                    break;

                                default:
                                    // Unknown mode; assume DoNotOverwrite
                                    copyFile = false;
                                    break;
                            }
                        }
                    }

                    if (!copyFile)
                    {
                        fileCountSkipped++;
                    }
                    else
                    {
                        var targetFilePath = Path.Combine(targetDirectory.FullName, sourceFile.Name);
                        bool copyResumed;

                        try
                        {
                            success = CopyFileWithResume(sourceFile, targetFilePath, out copyResumed, ignoreFileLocks);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            throw;
                        }
                        catch (Exception)
                        {
                            if (sourceFile.FullName.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD ||
                                targetFilePath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD)
                            {
                                // The source or target path is too long
                                // Try a normal file copy instead
                                CopyFile(sourceFile.FullName, targetFilePath, true);
                                copyResumed = false;
                            }
                            else
                            {
                                throw;
                            }
                        }

                        if (!success)
                            break;

                        if (copyResumed)
                        {
                            fileCountResumed++;
                        }
                        else
                        {
                            fileCountNewlyCopied++;
                        }

                        if (setAttribute)
                        {
                            UpdateReadonlyAttribute(sourceFile, targetFilePath, readOnly);
                        }
                    }
                }

                if (success && recurse)
                {
                    // Process each subdirectory
                    foreach (var subDirectory in sourceDirectory.GetDirectories())
                    {
                        var subDirTargetDirPath = Path.Combine(targetDirectoryPath, subDirectory.Name);

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        success = CopyDirectoryWithResume(
                            subDirectory.FullName, subDirTargetDirPath,
                            recurse, fileOverwriteMode, setAttribute, readOnly, fileNamesToSkip,
                            out fileCountSkipped, out fileCountResumed, out fileCountNewlyCopied);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException("Access denied copying directory with resume: " + ex.Message, ex);
            }
            catch (Exception ex)
            {
                throw new IOException("Exception copying directory with resume: " + ex.Message, ex);
            }

            return success;
        }

        /// <summary>
        /// Copy a file using chunks, thus allowing for resuming
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="copyResumed"></param>
        /// <param name="ignoreFileLocks">When true, copy the file even if another program has it open for writing</param>
        /// <returns>True if success; false if an error</returns>
        public bool CopyFileWithResume(string sourceFilePath, string targetFilePath, out bool copyResumed, bool ignoreFileLocks = false)
        {
            var sourceFile = new FileInfo(sourceFilePath);
            return CopyFileWithResume(sourceFile, targetFilePath, out copyResumed, ignoreFileLocks);
        }

        /// <summary>
        /// Copy sourceFile to targetDirectory
        /// Copies the file using chunks, thus allowing for resuming
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="copyResumed">Output parameter; true if copying was resumed</param>
        /// <param name="ignoreFileLocks">When true, copy the file even if another program has it open for writing</param>
        /// <returns>True if success; false if an error</returns>
        public bool CopyFileWithResume(FileInfo sourceFile, string targetFilePath, out bool copyResumed, bool ignoreFileLocks = false)
        {
            const string FILE_PART_TAG = ".#FilePart#";
            const string FILE_PART_INFO_TAG = ".#FilePartInfo#";

            long fileOffsetStart = 0;

            FileStream filePartWriter = null;

            try
            {
                if (mChunkSizeMB < 1)
                    mChunkSizeMB = 1;

                var chunkSizeBytes = mChunkSizeMB * 1024 * 1024;

                if (mFlushThresholdMB < mChunkSizeMB)
                {
                    mFlushThresholdMB = mChunkSizeMB;
                }
                var flushThresholdBytes = mFlushThresholdMB * 1024 * 1024;

                var resumeCopy = false;

                if (sourceFile.Length <= chunkSizeBytes)
                {
                    // Simply copy the file

                    UpdateCurrentStatus(CopyStatus.NormalCopy, sourceFile.FullName);
                    CopyFile(sourceFile.FullName, targetFilePath, true);

                    UpdateCurrentStatusIdle();
                    copyResumed = false;
                    return true;
                }

                // Delete the target file if it already exists
                var targetFile = new FileInfo(targetFilePath);
                if (targetFile.Exists)
                {
                    DeleteFileNative(targetFile);
                    ProgRunner.SleepMilliseconds(25);
                }

                // Check for a #FilePart# file
                var filePart = new FileInfo(targetFilePath + FILE_PART_TAG);

                var filePartInfo = new FileInfo(targetFilePath + FILE_PART_INFO_TAG);

                var sourceFileLastWriteTimeUTC = sourceFile.LastWriteTimeUtc;
                var sourceFileLastWriteTime = sourceFileLastWriteTimeUTC.ToString("yyyy-MM-dd hh:mm:ss.fff tt");

                if (filePart.Exists)
                {
                    // Possibly resume copying
                    // First inspect the FilePartInfo file

                    if (filePartInfo.Exists)
                    {
                        // Open the file and read the file length and file modification time
                        // If they match sourceFile then set resumeCopy to true and update fileOffsetStart

                        using var infoFileReader = new StreamReader(new FileStream(filePartInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                        var sourceLines = new List<string>();

                        while (!infoFileReader.EndOfStream)
                        {
                            sourceLines.Add(infoFileReader.ReadLine());
                        }

                        if (sourceLines.Count >= 3)
                        {
                            // The first line contains the source file path
                            // The second contains the file length, in bytes
                            // The third contains the file modification time (UTC)

                            if (sourceLines[0] == sourceFile.FullName && sourceLines[1] == sourceFile.Length.ToString())
                            {
                                // Name and size are the same
                                // See if the timestamps agree within 2 seconds (need to allow for this in case we're comparing NTFS and FAT32)

                                if (DateTime.TryParse(sourceLines[2], out var cachedLastWriteTimeUTC))
                                {
                                    if (NearlyEqualFileTimes(sourceFileLastWriteTimeUTC, cachedLastWriteTimeUTC))
                                    {
                                        // Source file is unchanged; safe to resume

                                        fileOffsetStart = filePart.Length;
                                        resumeCopy = true;
                                    }
                                }
                            }
                        }
                    }
                }

                if (resumeCopy)
                {
                    UpdateCurrentStatus(CopyStatus.BufferedCopyResume, sourceFile.FullName);
                    filePartWriter = new FileStream(filePart.FullName, FileMode.Append, FileAccess.Write, FileShare.Read);
                    copyResumed = true;
                }
                else
                {
                    UpdateCurrentStatus(CopyStatus.BufferedCopy, sourceFile.FullName);

                    // Delete FilePart file in the target directory if it already exists
                    if (filePart.Exists)
                    {
                        filePart.Delete();
                        ProgRunner.SleepMilliseconds(25);
                    }

                    // Create the FILE_PART_INFO_TAG file
                    using (var infoWriter = new StreamWriter(new FileStream(filePartInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {
                        // The first line contains the source file path
                        // The second contains the file length, in bytes
                        // The third contains the file modification time (UTC)
                        infoWriter.WriteLine(sourceFile.FullName);
                        infoWriter.WriteLine(sourceFile.Length);
                        infoWriter.WriteLine(sourceFileLastWriteTime);
                    }

                    // Open the FilePart file
                    filePartWriter = new FileStream(filePart.FullName, FileMode.Create, FileAccess.Write, FileShare.Read);
                    copyResumed = false;
                }

                var fileShareMode = ignoreFileLocks ? FileShare.ReadWrite : FileShare.Read;

                // Now copy the file, appending data to filePartWriter
                // Open the source and seek to fileOffsetStart if > 0
                using (var reader = new FileStream(sourceFile.FullName, FileMode.Open, FileAccess.Read, fileShareMode))
                {
                    if (fileOffsetStart > 0)
                    {
                        reader.Seek(fileOffsetStart, SeekOrigin.Begin);
                    }

                    int bytesRead;

                    var bytesWritten = fileOffsetStart;
                    float totalBytes = reader.Length;

                    var buffer = new byte[chunkSizeBytes + 1];
                    long bytesSinceLastFlush = 0;

                    do
                    {
                        // Read data in 1MB chunks and append to filePartWriter
                        bytesRead = reader.Read(buffer, 0, chunkSizeBytes);
                        filePartWriter.Write(buffer, 0, bytesRead);
                        bytesWritten += bytesRead;

                        // Flush out the data periodically
                        bytesSinceLastFlush += bytesRead;
                        if (bytesSinceLastFlush >= flushThresholdBytes)
                        {
                            filePartWriter.Flush();
                            bytesSinceLastFlush = 0;

                            // Value between 0 and 100
                            var progress = bytesWritten / totalBytes * 100;
                            FileCopyProgress?.Invoke(sourceFile.Name, progress);
                        }

                        if (bytesRead < chunkSizeBytes)
                        {
                            break;
                        }

                    } while (bytesRead > 0);

                    FileCopyProgress?.Invoke(sourceFile.Name, 100);
                }

                filePartWriter.Flush();
                filePartWriter.Dispose();

                UpdateCurrentStatusIdle();

                // Copy is complete
                // Update last write time UTC to match source UTC
                filePart.Refresh();
                filePart.LastWriteTimeUtc = sourceFileLastWriteTimeUTC;

                // Rename filePart to targetFilePath
                filePart.MoveTo(targetFilePath);

                // Delete filePartInfo
                filePartInfo.Delete();
            }
            catch (Exception ex)
            {
                filePartWriter?.Flush();
                ProgRunner.GarbageCollectNow();

                throw new IOException("Exception copying file with resume: " + ex.Message, ex);
            }

            return true;
        }

        /// <summary>
        /// Compares two timestamps (typically the LastWriteTime for a file)
        /// If they agree within 2 seconds, returns True, otherwise false
        /// </summary>
        /// <param name="time1">First file time</param>
        /// <param name="time2">Second file time</param>
        /// <returns>True if the times agree within 2 seconds</returns>
        public static bool NearlyEqualFileTimes(DateTime time1, DateTime time2)
        {
            return Math.Abs(time1.Subtract(time2).TotalSeconds) <= 2.05;
        }

        private void OnStatusEvent(string message, string detailedMessage)
        {
            OnStatusEvent(message);
            if (DebugLevel >= 2)
            {
                OnDebugEvent("  " + detailedMessage);
            }
        }

        private void OnWarningEvent(string message, string detailedMessage)
        {
            OnWarningEvent(message);
            OnStatusEvent("  " + detailedMessage);
        }

        private void UpdateCurrentStatusIdle()
        {
            UpdateCurrentStatus(CopyStatus.Idle, string.Empty);
        }

        private void UpdateCurrentStatus(CopyStatus eStatus, string sourceFilePath)
        {
            CurrentCopyStatus = eStatus;

            if (eStatus == CopyStatus.Idle)
            {
                CurrentSourceFile = string.Empty;
            }
            else
            {
                CurrentSourceFile = sourceFilePath;

                if (eStatus == CopyStatus.BufferedCopyResume)
                {
                    ResumingFileCopy?.Invoke(sourceFilePath);
                }
                else if (eStatus == CopyStatus.NormalCopy)
                {
                    CopyingFile?.Invoke(sourceFilePath);
                }
            }
        }

        #endregion

        #region "GetDirectorySize Method"
        /// <summary>
        /// Get the directory size.
        /// </summary>
        /// <param name="directoryPath">The path to the directory.</param>
        /// <returns>The directory size.</returns>
        public long GetDirectorySize(string directoryPath)
        {
            return GetDirectorySize(directoryPath, out _, out _);
        }

        /// <summary>
        /// Get the directory size, file count, and directory count for the entire directory tree.
        /// </summary>
        /// <param name="directoryPath">The path to the directory.</param>
        /// <param name="fileCount">The number of files in the entire directory tree.</param>
        /// <param name="subDirectoryCount">The number of directories in the entire directory tree.</param>
        /// <returns>The directory size.</returns>
        public long GetDirectorySize(string directoryPath, out long fileCount, out long subDirectoryCount)
        {
            long runningFileCount = 0;
            long runningSubDirCount = 0;
            var directorySize = GetDirectorySizeEx(directoryPath, ref runningFileCount, ref runningSubDirCount);

            fileCount = runningFileCount;
            subDirectoryCount = runningSubDirCount;
            return directorySize;
        }

        /// <summary>
        /// Get the directory size, file count, and directory count for the entire directory tree.
        /// </summary>
        /// <param name="directoryPath">The path to the directory.</param>
        /// <param name="fileCount">The number of files in the entire directory tree.</param>
        /// <param name="subDirectoryCount">The number of directories in the entire directory tree.</param>
        /// <returns>The directory size.</returns>
        private long GetDirectorySizeEx(string directoryPath, ref long fileCount, ref long subDirectoryCount)
        {
            long directorySize = 0;
            var directory = new DirectoryInfo(directoryPath);

            // Add the size of each file
            foreach (var childFile in directory.GetFiles())
            {
                directorySize += childFile.Length;
                fileCount++;
            }

            // Add the size of each sub-directory, that is retrieved by recursively
            // calling this same routine
            foreach (var subDir in directory.GetDirectories())
            {
                directorySize += GetDirectorySizeEx(subDir.FullName, ref fileCount, ref subDirectoryCount);
                subDirectoryCount++;
            }

            return directorySize;
        }
        #endregion

        #region "MoveDirectory Method"

        /// <summary>
        /// Move a directory
        /// </summary>
        /// <param name="sourceDirectoryPath"></param>
        /// <param name="targetDirectoryPath"></param>
        /// <param name="overwriteFiles"></param>
        public bool MoveDirectory(string sourceDirectoryPath, string targetDirectoryPath, bool overwriteFiles)
        {
            return MoveDirectory(sourceDirectoryPath, targetDirectoryPath, overwriteFiles, ManagerName);
        }

        /// <summary>
        /// Move a directory
        /// </summary>
        /// <param name="sourceDirectoryPath"></param>
        /// <param name="targetDirectoryPath"></param>
        /// <param name="overwriteFiles"></param>
        /// <param name="managerName"></param>
        public bool MoveDirectory(string sourceDirectoryPath, string targetDirectoryPath, bool overwriteFiles, string managerName)
        {
            bool success;

            var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);

            // Recursively call this method for each subdirectory
            foreach (var subDirectory in sourceDirectory.GetDirectories())
            {
                success = MoveDirectory(subDirectory.FullName, Path.Combine(targetDirectoryPath, subDirectory.Name), overwriteFiles, managerName);
                if (!success)
                {
                    throw new Exception("Error moving directory " + subDirectory.FullName + " to " + targetDirectoryPath + "; MoveDirectory returned False");
                }
            }

            foreach (var sourceFile in sourceDirectory.GetFiles())
            {
                success = CopyFileUsingLocks(sourceFile.FullName, Path.Combine(targetDirectoryPath, sourceFile.Name), managerName, overwriteFiles);
                if (!success)
                {
                    throw new Exception("Error copying file " + sourceFile.FullName + " to " + targetDirectoryPath + "; CopyFileUsingLocks returned False");
                }

                // Delete the source file
                DeleteFileIgnoreErrors(sourceFile.FullName);
            }

            sourceDirectory.Refresh();
            if (sourceDirectory.GetFileSystemInfos("*", SearchOption.AllDirectories).Length == 0)
            {
                // This directory is now empty; delete it
                try
                {
                    sourceDirectory.Delete(true);
                }
                catch (Exception)
                {
                    // Ignore errors here
                }
            }

            return true;
        }

        #endregion

        #region "Utility Methods"

        /// <summary>
        /// Renames targetFilePath to have _Old1 before the file extension
        /// Also looks for and renames other backed up versions of the file (those with _Old2, _Old3, etc.)
        /// Use this method to backup old versions of a file before copying a new version to a target directory
        /// Keeps up to 9 old versions of a file
        /// </summary>
        /// <param name="targetFilePath">Full path to the file to backup</param>
        /// <returns>True if the file was successfully renamed (also returns True if the target file does not exist)</returns>
        public static bool BackupFileBeforeCopy(string targetFilePath)
        {
            return BackupFileBeforeCopy(targetFilePath, DEFAULT_VERSION_COUNT_TO_KEEP);
        }

        /// <summary>
        /// Renames targetFilePath to have _Old1 before the file extension
        /// Also looks for and renames other backed up versions of the file (those with _Old2, _Old3, etc.)
        /// Use this method to backup old versions of a file before copying a new version to a target directory
        /// </summary>
        /// <param name="targetFilePath">Full path to the file to backup</param>
        /// <param name="versionCountToKeep">Maximum backup copies of the file to keep</param>
        /// <returns>True if the file was successfully renamed (also returns True if the target file does not exist)</returns>
        public static bool BackupFileBeforeCopy(string targetFilePath, int versionCountToKeep)
        {
            var targetFile = new FileInfo(targetFilePath);

            if (!targetFile.Exists)
            {
                // Target file does not exist; nothing to backup
                return true;
            }

            if (versionCountToKeep == 0)
                versionCountToKeep = 2;
            if (versionCountToKeep < 1)
                versionCountToKeep = 1;

            var baseName = Path.GetFileNameWithoutExtension(targetFile.Name);
            var extension = Path.GetExtension(targetFile.Name);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".bak";
            }

            if (targetFile.Directory == null)
                return true;

            var targetDirectoryPath = targetFile.Directory.FullName;

            // Backup any existing copies of targetFilePath

            for (var revision = versionCountToKeep - 1; revision >= 0; revision += -1)
            {
                var baseNameCurrent = baseName;
                if (revision > 0)
                {
                    baseNameCurrent += "_Old" + revision;
                }
                baseNameCurrent += extension;

                var fileToRename = new FileInfo(Path.Combine(targetDirectoryPath, baseNameCurrent));
                var newFilePath = Path.Combine(targetDirectoryPath, baseName + "_Old" + (revision + 1) + extension);

                // Confirm that newFilePath doesn't exist; delete it if it does
                if (File.Exists(newFilePath))
                {
                    File.Delete(newFilePath);
                }

                // Rename the current file to newFilePath
                if (fileToRename.Exists)
                {
                    fileToRename.MoveTo(newFilePath);
                }
            }

            return true;
        }

        /// <summary>
        /// Convert a size, bytes, to a string representation
        /// For example, 165342 will return 161.5 KB
        /// </summary>
        /// <param name="bytes"></param>
        public static string BytesToHumanReadable(long bytes)
        {
            if (bytes < 2048)
                return string.Format("{0:F1} bytes", bytes);

            var scaledBytes = bytes / 1024.0;
            if (scaledBytes < 1000)
                return string.Format("{0:F1} KB", scaledBytes);

            scaledBytes /= 1024.0;
            if (scaledBytes < 1000)
                return string.Format("{0:F1} MB", scaledBytes);

            scaledBytes /= 1024.0;
            if (scaledBytes < 1000)
                return string.Format("{0:F1} GB", scaledBytes);

            scaledBytes /= 1024.0;
            return string.Format("{0:F1} TB", scaledBytes);
        }

        /// <summary>
        /// Shorten pathToCompact to a maximum length of maxLength
        /// Examples:
        /// C:\...\B..\Finance..
        /// C:\...\W..\Business\Finances.doc
        /// C:\My Doc..\Word\Business\Finances.doc
        /// </summary>
        /// <param name="pathToCompact"></param>
        /// <param name="maxLength">Maximum length of the shortened path</param>
        /// <returns>Shortened path</returns>
        public static string CompactPathString(string pathToCompact, int maxLength = 40)
        {
            // The following is example output
            // Note that when drive letters or subdirectories are present, a minimum length is imposed
            // For "C:\My Documents\Readme.txt"
            //   Minimum string returned=  C:\M..\Rea..
            //   Length for 20 characters= C:\My D..\Readme.txt
            //   Length for 25 characters= C:\My Doc..\Readme.txt

            // For "C:\My Documents\Word\Business\Finances.doc"
            //   Minimum string returned=  C:\...\B..\Fin..
            //   Length for 20 characters= C:\...\B..\Finance..
            //   Length for 25 characters= C:\...\Bus..\Finances.doc
            //   Length for 32 characters= C:\...\W..\Business\Finances.doc
            //   Length for 40 characters= C:\My Doc..\Word\Business\Finances.doc

            var pathSepChars = new char[2];
            pathSepChars[0] = '\\';
            pathSepChars[1] = '/';

            var pathSepCharPreferred = '\\';

            // 0-based array
            var pathParts = new string[5];

            int pathPartCount;

            string shortenedPath;

            int charIndex;
            int shortLength;
            int leadingCharsLength;

            if (maxLength < 3)
                maxLength = 3;

            for (pathPartCount = 0; pathPartCount < pathParts.Length; pathPartCount++)
            {
                pathParts[pathPartCount] = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(pathToCompact))
            {
                return string.Empty;
            }

            var firstPathSepChar = pathToCompact.IndexOfAny(pathSepChars);
            if (firstPathSepChar >= 0)
            {
                pathSepCharPreferred = pathToCompact[firstPathSepChar];
            }

            pathToCompact = pathToCompact.Trim();
            if (pathToCompact.Length <= maxLength)
            {
                return pathToCompact;
            }

            pathPartCount = 1;
            var leadingChars = string.Empty;

            if (pathToCompact.StartsWith(@"\\"))
            {
                leadingChars = @"\\";
                charIndex = pathToCompact.IndexOfAny(pathSepChars, 2);

                if (charIndex > 0)
                {
                    leadingChars = @"\\" + pathToCompact.Substring(2, charIndex - 1);
                    pathParts[0] = pathToCompact.Substring(charIndex + 1);
                }
                else
                {
                    pathParts[0] = pathToCompact.Substring(2);
                }
            }
            else if (pathToCompact.StartsWith(@"\") || pathToCompact.StartsWith("/"))
            {
                leadingChars = pathToCompact.Substring(0, 1);
                pathParts[0] = pathToCompact.Substring(1);
            }
            else if (pathToCompact.StartsWith(@".\") || pathToCompact.StartsWith("./"))
            {
                leadingChars = pathToCompact.Substring(0, 2);
                pathParts[0] = pathToCompact.Substring(2);
            }
            else if (pathToCompact.StartsWith(@"..\") ||
                     pathToCompact.Substring(1, 2) == @":\" ||
                     pathToCompact.StartsWith("../") ||
                     pathToCompact.Substring(1, 2) == ":/")
            {
                leadingChars = pathToCompact.Substring(0, 3);
                pathParts[0] = pathToCompact.Substring(3);
            }
            else
            {
                pathParts[0] = pathToCompact;
            }

            // Examine pathParts[0] to see if there are 1, 2, or more subdirectories
            var loopCount = 0;
            do
            {
                charIndex = pathParts[pathPartCount - 1].IndexOfAny(pathSepChars);
                if (charIndex >= 0)
                {
                    pathParts[pathPartCount] = pathParts[pathPartCount - 1].Substring(charIndex + 1);
                    pathParts[pathPartCount - 1] = pathParts[pathPartCount - 1].Substring(0, charIndex + 1);
                    pathPartCount++;
                }
                else
                {
                    break;
                }
                loopCount++;
            } while (loopCount < 3);

            if (pathPartCount == 1)
            {
                // No \ or / found, we're forced to shorten the filename (though if a UNC, can shorten part of the UNC)

                if (leadingChars.StartsWith(@"\\"))
                {
                    leadingCharsLength = leadingChars.Length;
                    if (leadingCharsLength > 5)
                    {
                        // Can shorten the server name as needed
                        shortLength = maxLength - pathParts[0].Length - 3;
                        if (shortLength < leadingCharsLength)
                        {
                            if (shortLength < 3)
                                shortLength = 3;
                            leadingChars = leadingChars.Substring(0, shortLength) + @"..\";
                        }
                    }
                }

                shortLength = maxLength - leadingChars.Length - 2;
                if (shortLength < 3)
                    shortLength = 3;
                if (shortLength < pathParts[0].Length - 2)
                {
                    if (shortLength < 4)
                    {
                        shortenedPath = leadingChars + pathParts[0].Substring(0, shortLength) + "..";
                    }
                    else
                    {
                        // Shorten by removing the middle portion of the filename
                        var leftLength = Convert.ToInt32(Math.Ceiling(shortLength / 2.0));
                        var rightLength = shortLength - leftLength;
                        shortenedPath = leadingChars + pathParts[0].Substring(0, leftLength) + ".." + pathParts[0].Substring(pathParts[0].Length - rightLength);
                    }
                }
                else
                {
                    shortenedPath = leadingChars + pathParts[0];
                }
            }
            else
            {
                // Found one (or more) subdirectories

                // First check if pathParts[1] = "...\" or ".../"
                short multiPathCorrection;
                if (pathParts[0] == @"...\" || pathParts[0] == ".../")
                {
                    multiPathCorrection = 4;
                    pathParts[0] = pathParts[1];
                    pathParts[1] = pathParts[2];
                    pathParts[2] = pathParts[3];
                    pathParts[3] = string.Empty;
                    pathPartCount = 3;
                }
                else
                {
                    multiPathCorrection = 0;
                }

                // Shorten the first to as little as possible
                // If not short enough, replace the first with ... and call this method again
                shortLength = maxLength - leadingChars.Length - pathParts[3].Length - pathParts[2].Length - pathParts[1].Length - 3 - multiPathCorrection;
                if (shortLength < 1 && pathParts[2].Length > 0)
                {
                    // Not short enough, but other subdirectories are present
                    // Thus, can call this method recursively
                    shortenedPath = leadingChars + "..." + pathSepCharPreferred + pathParts[1] + pathParts[2] + pathParts[3];
                    shortenedPath = CompactPathString(shortenedPath, maxLength);
                }
                else
                {
                    if (leadingChars.StartsWith(@"\\"))
                    {
                        leadingCharsLength = leadingChars.Length;
                        if (leadingCharsLength > 5)
                        {
                            // Can shorten the server name as needed
                            shortLength = maxLength - pathParts[3].Length - pathParts[2].Length - pathParts[1].Length - 7 - multiPathCorrection;
                            if (shortLength < leadingCharsLength - 3)
                            {
                                if (shortLength < 3)
                                    shortLength = 3;
                                leadingChars = leadingChars.Substring(0, shortLength) + @"..\";
                            }

                            // Recompute shortLength
                            shortLength = maxLength - leadingChars.Length - pathParts[3].Length - pathParts[2].Length - pathParts[1].Length - 3 - multiPathCorrection;
                        }
                    }

                    if (multiPathCorrection > 0)
                    {
                        leadingChars = leadingChars + "..." + pathSepCharPreferred;
                    }

                    if (shortLength < 1)
                        shortLength = 1;
                    pathParts[0] = pathParts[0].Substring(0, shortLength) + ".." + pathSepCharPreferred;
                    shortenedPath = leadingChars + pathParts[0] + pathParts[1] + pathParts[2] + pathParts[3];

                    // See if still too long
                    // If it is, will need to shorten the filename too
                    var overLength = shortenedPath.Length - maxLength;
                    if (overLength > 0)
                    {
                        // Need to shorten filename too
                        // Determine which index the filename is in
                        int fileNameIndex;
                        for (fileNameIndex = pathPartCount - 1; fileNameIndex >= 0; fileNameIndex += -1)
                        {
                            if (pathParts[fileNameIndex].Length > 0)
                                break;
                        }

                        shortLength = pathParts[fileNameIndex].Length - overLength - 2;
                        if (shortLength < 4)
                        {
                            pathParts[fileNameIndex] = pathParts[fileNameIndex].Substring(0, 3) + "..";
                        }
                        else
                        {
                            // Shorten by removing the middle portion of the filename
                            var leftLength = Convert.ToInt32(Math.Ceiling(shortLength / 2.0));
                            var rightLength = shortLength - leftLength;
                            pathParts[fileNameIndex] = pathParts[fileNameIndex].Substring(0, leftLength) + ".." +
                                pathParts[fileNameIndex].Substring(pathParts[fileNameIndex].Length - rightLength);
                        }

                        shortenedPath = leadingChars + pathParts[0] + pathParts[1] + pathParts[2] + pathParts[3];
                    }
                }
            }

            return shortenedPath;
        }

        /// <summary>
        /// Delete the file, retrying up to 3 times
        /// </summary>
        /// <param name="fileToDelete">File to delete</param>
        /// <param name="errorMessage">Output message: error message if unable to delete the file</param>
        public bool DeleteFileWithRetry(FileInfo fileToDelete, out string errorMessage)
        {
            return DeleteFileWithRetry(fileToDelete, 3, out errorMessage);
        }

        /// <summary>
        /// Delete the file, retrying up to retryCount times
        /// </summary>
        /// <param name="fileToDelete">File to delete</param>
        /// <param name="retryCount">Maximum number of times to retry the deletion, waiting 500 msec, then 750 msec between deletion attempts</param>
        /// <param name="errorMessage">Output message: error message if unable to delete the file</param>
        public bool DeleteFileWithRetry(FileInfo fileToDelete, int retryCount, out string errorMessage)
        {
            var fileDeleted = false;
            var sleepTimeMsec = 500;

            var retriesRemaining = retryCount - 1;
            if (retriesRemaining < 0)
                retriesRemaining = 0;

            errorMessage = string.Empty;

            while (!fileDeleted && retriesRemaining >= 0)
            {
                retriesRemaining--;

                try
                {
                    fileToDelete.Delete();
                    fileDeleted = true;
                }
                catch (Exception ex)
                {
                    if (IsVimSwapFile(fileToDelete.Name))
                    {
                        // Ignore this error
                        errorMessage = string.Empty;
                        return true;
                    }

                    // Make sure the ReadOnly bit is not set
                    if (fileToDelete.IsReadOnly)
                    {
                        var attributes = fileToDelete.Attributes;
                        fileToDelete.Attributes = attributes & ~FileAttributes.ReadOnly;

                        try
                        {
                            // Retry the delete
                            fileToDelete.Delete();
                            fileDeleted = true;
                        }
                        catch (Exception ex2)
                        {
                            errorMessage = "Error deleting file " + fileToDelete.FullName + ": " + ex2.Message;
                        }
                    }
                    else
                    {
                        errorMessage = "Error deleting file " + fileToDelete.FullName + ": " + ex.Message;
                    }
                }

                if (!fileDeleted)
                {
                    // Sleep for 0.5 second (or longer) then try again
                    ProgRunner.SleepMilliseconds(sleepTimeMsec);

                    // Increase sleepTimeMsec so that we sleep longer the next time, but cap the sleep time at 5.7 seconds
                    if (sleepTimeMsec < 5000)
                    {
                        sleepTimeMsec = Convert.ToInt32(Math.Round(sleepTimeMsec * 1.5, 0));
                    }
                }
            }

            if (fileDeleted)
            {
                errorMessage = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = "Unknown error deleting file " + fileToDelete.FullName;
            }

            // ReSharper disable once NotAssignedOutParameter
            return true;
        }

        /// <summary>
        /// Returns true if the file is _.swp or starts with a . and ends with .swp
        /// </summary>
        /// <param name="filePath"></param>
        public static bool IsVimSwapFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName == null)
                return false;

            return
                string.Equals(fileName, "_.swp", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith(".") && fileName.EndsWith(".swp", StringComparison.OrdinalIgnoreCase);
        }

        private void NotifyLockFilePaths(string lockFilePathSource, string lockFilePathTarget)
        {
            const string adminBypassBase = "To force the file copy and bypass the lock file queue";

            string adminBypassMessage;
            if (!string.IsNullOrWhiteSpace(lockFilePathSource) && !string.IsNullOrWhiteSpace(lockFilePathTarget))
            {
                adminBypassMessage = string.Format("{0}, delete {1} and {2}", adminBypassBase, lockFilePathSource, lockFilePathTarget);
            }
            else if (!string.IsNullOrWhiteSpace(lockFilePathSource))
            {
                adminBypassMessage = string.Format("{0}, delete {1}", adminBypassBase, lockFilePathSource);
            }
            else if (!string.IsNullOrWhiteSpace(lockFilePathTarget))
            {
                adminBypassMessage = string.Format("{0}, delete {1}", adminBypassBase, lockFilePathTarget);
            }
            else
            {
                adminBypassMessage = string.Format("Logic error; unable {0}", adminBypassBase.ToLower());
            }

            WaitingForLockQueueNotifyLockFilePaths?.Invoke(lockFilePathSource ?? string.Empty,
                                                           lockFilePathTarget ?? string.Empty,
                                                           adminBypassMessage);
        }

        /// <summary>
        /// Confirms that the drive for the target output file has a minimum amount of free disk space
        /// </summary>
        /// <param name="outputFilePath">Path to output file; defines the drive or server share for which we will determine the disk space</param>
        /// <param name="minimumFreeSpaceMB">
        /// Minimum free disk space, in MB.
        /// Will default to 150 MB if zero or negative.
        /// Takes into account outputFileExpectedSizeMB</param>
        /// <param name="currentDiskFreeSpaceBytes">
        /// Amount of free space on the given disk
        /// Determine on Windows using DiskInfo.GetDiskFreeSpace in PRISMWin.dll
        /// </param>
        /// <param name="errorMessage">Output message if there is not enough free space (or if the path is invalid)</param>
        /// <returns>True if more than minimumFreeSpaceMB is available; otherwise false</returns>
        public static bool ValidateFreeDiskSpace(string outputFilePath, double minimumFreeSpaceMB, long currentDiskFreeSpaceBytes, out string errorMessage)
        {
            const double outputFileExpectedSizeMB = 0;

            return ValidateFreeDiskSpace(outputFilePath, outputFileExpectedSizeMB, minimumFreeSpaceMB, currentDiskFreeSpaceBytes, out errorMessage);
        }

        /// <summary>
        /// Confirms that the drive for the target output file has a minimum amount of free disk space
        /// </summary>
        /// <param name="outputFilePath">Path to output file; defines the drive or server share for which we will determine the disk space</param>
        /// <param name="outputFileExpectedSizeMB">Expected size of the output file</param>
        /// <param name="minimumFreeSpaceMB">
        /// Minimum free disk space, in MB.
        /// Will default to 150 MB if zero or negative.
        /// Takes into account outputFileExpectedSizeMB</param>
        /// <param name="currentDiskFreeSpaceBytes">
        /// Amount of free space on the given disk
        /// Determine on Windows using DiskInfo.GetDiskFreeSpace in PRISMWin.dll
        /// </param>
        /// <param name="errorMessage">Output message if there is not enough free space (or if the path is invalid)</param>
        /// <returns>True if more than minimumFreeSpaceMB is available; otherwise false</returns>
        /// <remarks>If currentDiskFreeSpaceBytes is negative, this method always returns true (provided the target directory exists)</remarks>
        public static bool ValidateFreeDiskSpace(
            string outputFilePath,
            double outputFileExpectedSizeMB,
            double minimumFreeSpaceMB,
            long currentDiskFreeSpaceBytes,
            out string errorMessage)
        {
            const int DEFAULT_DATASET_STORAGE_MIN_FREE_SPACE_MB = 150;
            errorMessage = string.Empty;

            try
            {
                if (minimumFreeSpaceMB <= 0)
                    minimumFreeSpaceMB = DEFAULT_DATASET_STORAGE_MIN_FREE_SPACE_MB;

                if (outputFileExpectedSizeMB < 0)
                    outputFileExpectedSizeMB = 0;

                if (currentDiskFreeSpaceBytes < 0)
                {
                    // Always return true when currentDiskFreeSpaceBytes is negative
                    return true;
                }

                var freeSpaceMB = currentDiskFreeSpaceBytes / 1024.0 / 1024.0;

                if (outputFileExpectedSizeMB > 0)
                {
                    if (freeSpaceMB - outputFileExpectedSizeMB < minimumFreeSpaceMB)
                    {
                        errorMessage = "Target drive will have less than " + minimumFreeSpaceMB.ToString("0") + " MB free " +
                                       "after creating a " + outputFileExpectedSizeMB.ToString("0") + " MB file : " +
                                       freeSpaceMB.ToString("0.0") + " MB available prior to file creation";

                        return false;
                    }
                }
                else if (freeSpaceMB < minimumFreeSpaceMB)
                {
                    errorMessage = "Target drive has less than " + minimumFreeSpaceMB.ToString("0") + " MB free: " +
                        freeSpaceMB.ToString("0.0") + " MB available";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Exception validating target drive free space for " + outputFilePath + ": " + ex.Message;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Wait for the lock file queue to drop below a threshold
        /// </summary>
        /// <param name="lockFileTimestamp"></param>
        /// <param name="lockDirectorySource"></param>
        /// <param name="sourceFile"></param>
        /// <param name="maxWaitTimeMinutes"></param>
        public void WaitForLockFileQueue(long lockFileTimestamp, DirectoryInfo lockDirectorySource, FileInfo sourceFile, int maxWaitTimeMinutes)
        {
            WaitForLockFileQueue(lockFileTimestamp, lockDirectorySource, null,
                                 sourceFile, "Unknown_Target_File_Path",
                                 maxWaitTimeMinutes, string.Empty, string.Empty);
        }

        /// <summary>
        /// Wait for the lock file queue to drop below a threshold
        /// </summary>
        /// <param name="lockFileTimestamp"></param>
        /// <param name="lockDirectorySource"></param>
        /// <param name="lockDirectoryTarget"></param>
        /// <param name="sourceFile"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="maxWaitTimeMinutes"></param>
        /// <param name="lockFilePathSource"></param>
        /// <param name="lockFilePathTarget"></param>
        public void WaitForLockFileQueue(
            long lockFileTimestamp, DirectoryInfo lockDirectorySource,
            DirectoryInfo lockDirectoryTarget,
            FileInfo sourceFile, string targetFilePath,
            int maxWaitTimeMinutes,
            string lockFilePathSource, string lockFilePathTarget)
        {
            // Find the recent LockFiles present in the source and/or target lock directories
            // These lists contain the sizes of the lock files with timestamps less than lockFileTimestamp

            var mbBacklogSource = 0;
            var mbBacklogTarget = 0;

            var waitTimeStart = DateTime.UtcNow;

            var sourceFileSizeMB = Convert.ToInt32(sourceFile.Length / 1024.0 / 1024.0);

            var checkForDeletedSourceLockFile = !string.IsNullOrWhiteSpace(lockFilePathSource) && File.Exists(lockFilePathSource);
            var checkForDeletedTargetLockFile = !string.IsNullOrWhiteSpace(lockFilePathTarget) && File.Exists(lockFilePathTarget);

            int deletedLockFilesThreshold;

            if (checkForDeletedSourceLockFile && checkForDeletedTargetLockFile)
            {
                deletedLockFilesThreshold = 2;
            }
            else if (checkForDeletedSourceLockFile || checkForDeletedTargetLockFile)
            {
                deletedLockFilesThreshold = 1;
            }
            else
            {
                deletedLockFilesThreshold = 0;
            }

            var notifiedLockFilePaths = false;

            // Wait for up to 180 minutes (3 hours) for the server resources to free up

            // However, if retrieving files from agate.emsl.pnl.gov only wait for a maximum of 30 minutes
            // because sometimes that directory's permissions get messed up and we can create files there, but cannot delete them

            var maxWaitTimeSource = MAX_LOCKFILE_WAIT_TIME_MINUTES;
            var maxWaitTimeTarget = MAX_LOCKFILE_WAIT_TIME_MINUTES;
            if (maxWaitTimeMinutes > 0)
            {
                maxWaitTimeSource = maxWaitTimeMinutes;
                maxWaitTimeTarget = maxWaitTimeMinutes;
            }

            // Switched from a2.emsl.pnl.gov to aurora.emsl.pnl.gov in June 2016
            // Switched from aurora.emsl.pnl.gov to adms.emsl.pnl.gov in September 2016
            // Switched from adms.emsl.pnl.gov to agate.emsl.pnl.gov in 2020
            if (lockDirectorySource?.FullName.StartsWith(@"\\agate.emsl.pnl.gov\", StringComparison.OrdinalIgnoreCase) == true)
            {
                maxWaitTimeSource = Math.Min(maxWaitTimeSource, 30);
            }

            if (lockDirectoryTarget?.FullName.StartsWith(@"\\agate.emsl.pnl.gov\", StringComparison.OrdinalIgnoreCase) == true)
            {
                maxWaitTimeTarget = Math.Min(maxWaitTimeTarget, 30);
            }

            while (true)
            {
                // Refresh the lock files list by finding recent lock files with a timestamp less than lockFileTimestamp
                var lockFileMBSource = FindLockFiles(lockDirectorySource, lockFileTimestamp);
                var lockFileMBTarget = FindLockFiles(lockDirectoryTarget, lockFileTimestamp);

                var stopWaiting = false;

                if (lockFileMBSource.Count <= 1 && lockFileMBTarget.Count <= 1)
                {
                    stopWaiting = true;
                }
                else
                {
                    mbBacklogSource = lockFileMBSource.Sum();
                    mbBacklogTarget = lockFileMBTarget.Sum();

                    if (mbBacklogSource + sourceFileSizeMB < LOCKFILE_TRANSFER_THRESHOLD_MB || WaitedTooLong(waitTimeStart, maxWaitTimeSource))
                    {
                        // The source server has enough resources available to allow the copy
                        if (mbBacklogTarget + sourceFileSizeMB < LOCKFILE_TRANSFER_THRESHOLD_MB || WaitedTooLong(waitTimeStart, maxWaitTimeTarget))
                        {
                            // The target server has enough resources available to allow the copy
                            // Copy the file
                            stopWaiting = true;
                        }
                    }
                }

                if (!stopWaiting && deletedLockFilesThreshold > 0)
                {
                    var lockFilesDeleted = 0;
                    if (checkForDeletedSourceLockFile && !File.Exists(lockFilePathSource))
                        lockFilesDeleted++;

                    if (checkForDeletedTargetLockFile && !File.Exists(lockFilePathTarget))
                        lockFilesDeleted++;

                    stopWaiting = lockFilesDeleted >= deletedLockFilesThreshold;
                }

                if (stopWaiting)
                {
                    LockQueueWaitComplete?.Invoke(sourceFile.FullName, targetFilePath, DateTime.UtcNow.Subtract(waitTimeStart).TotalMinutes);
                    break;
                }

                var totalWaitTimeMinutes = DateTime.UtcNow.Subtract(waitTimeStart).TotalMinutes;

                // Server resources exceed the thresholds
                // Sleep for 1 to 30 seconds, depending on mbBacklogSource and mbBacklogTarget
                // We compute sleepTimeSec using the assumption that data can be copied to/from the server at a rate of 200 MB/sec
                // This is faster than reality, but helps minimize waiting too long between checking

                var sleepTimeSec = Math.Max(mbBacklogSource, mbBacklogTarget) / 200.0;

                if (sleepTimeSec < 1)
                    sleepTimeSec = 1;
                if (sleepTimeSec > 30)
                    sleepTimeSec = 30;

                if (totalWaitTimeMinutes >= 5 && !notifiedLockFilePaths)
                {
                    NotifyLockFilePaths(lockFilePathSource, lockFilePathTarget);
                    notifiedLockFilePaths = true;
                }

                WaitingForLockQueue?.Invoke(sourceFile.FullName, targetFilePath, mbBacklogSource, mbBacklogTarget);

                ProgRunner.SleepMilliseconds(Convert.ToInt32(sleepTimeSec) * 1000);

                if (totalWaitTimeMinutes < MAX_LOCKFILE_WAIT_TIME_MINUTES)
                    continue;

                LockQueueTimedOut?.Invoke(sourceFile.FullName, targetFilePath, totalWaitTimeMinutes);
                break;
            }
        }

        private bool WaitedTooLong(DateTime waitTimeStart, int maxLockfileWaitTimeMinutes)
        {
            return DateTime.UtcNow.Subtract(waitTimeStart).TotalMinutes >= maxLockfileWaitTimeMinutes;
        }

        #endregion

        #region "GZip Compression"

        /// <summary>
        /// Compress a file using the built-in GZipStream (stores minimal GZip metadata)
        /// </summary>
        /// <param name="fileToCompress">File to compress</param>
        /// <param name="compressedDirectoryPath">Path to directory where compressed file should be created</param>
        /// <param name="compressedFileName">Name of compressed file</param>
        public static void GZipCompress(FileInfo fileToCompress, string compressedDirectoryPath = null, string compressedFileName = null)
        {
            var compressedFilePath = ConstructCompressedGZipFilePath(fileToCompress, compressedDirectoryPath, compressedFileName);

            using (var decompressedFileStream = fileToCompress.OpenRead())
            using (var compressedFileStream = File.Create(compressedFilePath))
            using (var compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
            {
                decompressedFileStream.CopyTo(compressionStream);
            }

            // Update the modification time of the .gz file to match the time of fileToCompress
            File.SetLastWriteTimeUtc(compressedFilePath, fileToCompress.LastWriteTimeUtc);
        }

        /// <summary>
        /// Compress a file, using a special implementation to add supported metadata to the file (like last modified time and file name)
        /// </summary>
        /// <param name="fileToCompress">file to compress</param>
        /// <param name="compressedDirectoryPath">path to directory where the gzipped file should be created</param>
        /// <param name="compressedFileName">name for the gzipped file</param>
        /// <param name="doNotStoreFileName">if true, the filename is not stored in the gzip metadata (so contained file name depends on gzip file name)</param>
        /// <param name="comment">optional comment to add to gzip metadata (generally not used by decompression programs)</param>
        /// <param name="addHeaderCrc">if true, a CRC16 hash of the header information is written to the gzip metadata</param>
        public static void GZipCompressWithMetadata(FileInfo fileToCompress, string compressedDirectoryPath = null, string compressedFileName = null, bool doNotStoreFileName = false, string comment = null, bool addHeaderCrc = false)
        {
            var compressedFilePath = ConstructCompressedGZipFilePath(fileToCompress, compressedDirectoryPath, compressedFileName);

            string storedFileName = null;
            if (!doNotStoreFileName)
            {
                storedFileName = fileToCompress.Name;
            }

            // GZipMetadataStream wraps the file stream to add the metadata at the right time during the file write
            using var decompressedFileStream = fileToCompress.OpenRead();
            using var compressedFileStream = File.Create(compressedFilePath);
            using var metadataAdder = new GZipMetadataStream(compressedFileStream, fileToCompress.LastWriteTime, storedFileName, comment, addHeaderCrc);
            using var compressionStream = new GZipStream(metadataAdder, CompressionMode.Compress);

            decompressedFileStream.CopyTo(compressionStream);

            // Since metadata in the .gz file includes the last write time of the compressed file, do not change the last write time of the .gz file
        }

        /// <summary>
        /// Decompress a gzip file
        /// </summary>
        /// <param name="fileToDecompress">File to decompress</param>
        /// <param name="decompressedDirectoryPath">Path to directory where the decompressed file should be created</param>
        /// <param name="decompressedFileName">Name of decompressed file</param>
        public static void GZipDecompress(FileInfo fileToDecompress, string decompressedDirectoryPath = null, string decompressedFileName = null)
        {
            var currentFilePathWithoutGz = Path.ChangeExtension(fileToDecompress.FullName, null);
            string decompressedFilePath;

            // If decompressedDirectoryPath or decompressedFileName are provided, override the default path/filename appropriately
            if (!string.IsNullOrWhiteSpace(decompressedDirectoryPath) || !string.IsNullOrWhiteSpace(decompressedFileName))
            {
                // When combining directoryPath with name, we will assure that name only contains a filename and not a path
                var fileNameOrPath = string.IsNullOrWhiteSpace(decompressedFileName) ? currentFilePathWithoutGz : decompressedFileName;

                var directoryPath = string.IsNullOrWhiteSpace(decompressedDirectoryPath) ? fileToDecompress.DirectoryName : decompressedDirectoryPath;
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    directoryPath = ".";
                }

                decompressedFilePath = Path.Combine(directoryPath, Path.GetFileName(fileNameOrPath));
            }
            else
            {
                decompressedFilePath = currentFilePathWithoutGz;
            }

            using (var decompressedFileStream = File.Create(decompressedFilePath))
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(decompressedFileStream);
            }

            var decompressedFile = new FileInfo(decompressedFilePath);

            if (decompressedFile.LastWriteTimeUtc > fileToDecompress.LastWriteTimeUtc)
            {
                // Update the modification time of the decompressed file to match the time of the .gz file
                decompressedFile.LastWriteTimeUtc = fileToDecompress.LastWriteTimeUtc;
            }
        }

        /// <summary>
        /// Decompress a file, keeping the correct stored timestamp on the decompressed file and the stored file name
        /// </summary>
        /// <param name="fileToDecompress">GZip file to decompress</param>
        /// <param name="decompressedDirectoryPath">Path to directory where the decompressed file should be created</param>
        /// <param name="doNotUseStoredFileName">If true, the output file name will use the gzip filename (excluding .gz) even when the filename is available in the gzip metadata</param>
        /// <returns>Path to the decompressed file</returns>
        public static string GZipDecompressWithMetadata(FileInfo fileToDecompress, string decompressedDirectoryPath = null, bool doNotUseStoredFileName = false)
        {
            var currentFilePathWithoutGz = Path.ChangeExtension(fileToDecompress.Name, null);

            // If decompressedDirectoryPath is provided, override the default path appropriately
            var dir = string.IsNullOrWhiteSpace(decompressedDirectoryPath) ? fileToDecompress.DirectoryName : decompressedDirectoryPath;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = ".";
            }

            var lastModified = fileToDecompress.LastWriteTime;
            string decompressedFilePath;

            // GZipMetadataStream wraps the file stream to read the metadata before decompressing
            using (var originalFileStream = fileToDecompress.OpenRead())
            using (var gzipMetadataStream = new GZipMetadataStream(originalFileStream))
            {
                if (!gzipMetadataStream.InternalLastModified.Equals(DateTime.MinValue))
                {
                    lastModified = gzipMetadataStream.InternalLastModified;
                }

                string fileNameOrPath;
                if (!doNotUseStoredFileName && !string.IsNullOrWhiteSpace(gzipMetadataStream.InternalFilename))
                {
                    fileNameOrPath = gzipMetadataStream.InternalFilename;
                }
                else
                {
                    fileNameOrPath = currentFilePathWithoutGz;
                }

                decompressedFilePath = Path.Combine(dir, Path.GetFileName(fileNameOrPath));

                using var decompressedFileStream = File.Create(decompressedFilePath);
                using var decompressionStream = new GZipStream(gzipMetadataStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedFileStream);
            }

            // Update the modification time of the decompressed file to match the time from the metadata
            File.SetLastWriteTime(decompressedFilePath, lastModified);

            return decompressedFilePath;
        }

        /// <summary>
        /// Construct the path to the .gz file to create, possibly overriding the target directory and/or target filename
        /// </summary>
        /// <param name="fileToCompress"></param>
        /// <param name="compressedDirectoryPath"></param>
        /// <param name="compressedFileName"></param>
        private static string ConstructCompressedGZipFilePath(FileInfo fileToCompress, string compressedDirectoryPath, string compressedFileName)
        {
            if (string.IsNullOrWhiteSpace(compressedDirectoryPath) && string.IsNullOrWhiteSpace(compressedFileName))
                return fileToCompress.FullName + ".gz";

            var name = string.IsNullOrWhiteSpace(compressedFileName) ? fileToCompress.Name + ".gz" : Path.GetFileName(compressedFileName);

            var dir = string.IsNullOrWhiteSpace(compressedDirectoryPath) ? fileToCompress.DirectoryName : compressedDirectoryPath;
            if (string.IsNullOrWhiteSpace(dir))
            {
                return name;
            }

            return Path.Combine(dir, name);
        }

        #endregion
    }
}
