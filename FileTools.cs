using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace PRISM
{

    /// <summary>
    /// Tools to manipulate paths and directories.
    /// </summary>
    /// <remarks>
    /// There is a set of functions to properly terminate directory paths.
    /// There is a set of functions to copy an entire directory tree.
    /// There is a set of functions to copy an entire directory tree and resume copying interrupted files.
    /// There is a set of functions to get the size of an entire directory tree, including the number of files and directories.
    ///</remarks>
    public class clsFileTools : clsEventNotifier
    {

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
        /// Waiting for the lock queue
        /// </summary>
        public event WaitingForLockQueueEventHandler WaitingForLockQueue;

        /// <summary>
        /// Waiting for the lock queue
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="backlogSourceMB">Source computer backlog, in MB</param>
        /// <param name="backlogTargetMB">Target computer backlog, in MB</param>
        public delegate void WaitingForLockQueueEventHandler(string sourceFilePath, string targetFilePath, int backlogSourceMB, int backlogTargetMB);

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
        /// Event is raised when we are done waiting waiting for our turn in the lock file queue
        /// </summary>
        public event LockQueueWaitCompleteEventHandler LockQueueWaitComplete;

        /// <summary>
        /// Event is raised when we are done waiting waiting for our turn in the lock file queue
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="waitTimeMinutes"></param>
        public delegate void LockQueueWaitCompleteEventHandler(string sourceFilePath, string targetFilePath, double waitTimeMinutes);

        #endregion

        #region "Module constants and variables"

        private const int MAX_LOCKFILE_WAIT_TIME_MINUTES = 180;

        /// <summary>
        /// Minimum source file size (in MB) for the lock queue to be used
        /// </summary>
        public const int LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB = 20;

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
        /// <remarks></remarks>
        public const int DEFAULT_CHUNK_SIZE_MB = 1;

        /// <summary>
        /// Used by CopyFileWithResume; defines how often the data is flushed out to disk; must be larger than the ChunkSize
        /// </summary>
        /// <remarks></remarks>
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
            /// OverWrite if any difference in size or date; note that newer files in target folder will get overwritten since their date doesn't match
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
            /// File is geing copied via .NET and cannot be resumed
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
        /// Debug level
        /// </summary>
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
        /// <remarks></remarks>
        public clsFileTools() : this("Unknown-Manager", 1)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="managerName"></param>
        /// <param name="debugLevel"></param>
        public clsFileTools(string managerName, int debugLevel)
        {
            ManagerName = managerName;
            DebugLevel = debugLevel;

            mInvalidDosChars = new Regex(@"[\\/:*?""<>| ]", RegexOptions.Compiled);

            mParseLockFileName = new Regex(@"^(?<QueueTime>\d+)_(?<FileSizeMB>\d+)_", RegexOptions.Compiled);
        }
        #endregion

        #region "CheckTerminator function"

        //Functions
        /// <summary>
        /// Modifies input directory path string depending on optional settings.
        /// </summary>
        /// <param name="folderPath">The input directory path.</param>
        /// <param name="addTerm">Specifies whether the directory path string ends with the specified directory separation character.</param>
        /// <param name="termChar">The specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string folderPath, bool addTerm, char termChar)
        {

            //Overload for all parameters specified
            return CheckTerminatorEX(folderPath, addTerm, termChar);

        }

        /// <summary>
        /// Adds or removes the DOS path separation character from the end of the directory path.
        /// </summary>
        /// <param name="folderPath">The input directory path.</param>
        /// <param name="addTerm">Specifies whether the directory path string ends with the specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string folderPath, bool addTerm)
        {

            return CheckTerminatorEX(folderPath, addTerm, Path.DirectorySeparatorChar);

        }

        /// <summary>
        /// Assures the directory path ends with the specified path separation character.
        /// </summary>
        /// <param name="folderPath">The input directory path.</param>
        /// <param name="termChar">The specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string folderPath, string termChar)
        {
            return CheckTerminatorEX(folderPath, addTerm: true, termChar: Path.DirectorySeparatorChar);

        }

        /// <summary>
        /// Assures the directory path ends with the DOS path separation character.
        /// </summary>
        /// <param name="folderPath">The input directory path.</param>
        /// <returns>The modified directory path.</returns>
        public static string CheckTerminator(string folderPath)
        {

            // Overload for using all defaults (add DOS terminator char)
            return CheckTerminatorEX(folderPath, addTerm: true, termChar: Path.DirectorySeparatorChar);

        }

        /// <summary>
        /// Modifies input directory path string depending on addTerm
        /// </summary>
        /// <param name="folderPath">The input directory path.</param>
        /// <param name="addTerm">Specifies whether the directory path should end with the specified directory separation character</param>
        /// <param name="termChar">The specified directory separation character.</param>
        /// <returns>The modified directory path.</returns>
        /// <remarks>addTerm=True forces the path to end with specified termChar while addTerm=False will remove termChar from the end if present</remarks>
        private static string CheckTerminatorEX(string folderPath, bool addTerm, char termChar)
        {

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return folderPath;
            }

            if (addTerm)
            {
                if (folderPath.EndsWith(termChar.ToString()))
                {
                    return folderPath;
                }
                return folderPath + termChar;
            }

            if (folderPath.EndsWith(termChar.ToString()))
            {
                return folderPath.TrimEnd(termChar);
            }

            return folderPath;
        }
        #endregion

        #region "CopyFile function"

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
        /// This function is unique in that it allows you to specify a destination path where
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
            var folderPath = Path.GetDirectoryName(destPath);

            if (folderPath == null)
            {
                throw new DirectoryNotFoundException("Unable to determine the parent directory for " + destPath);
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (backupDestFileBeforeCopy)
            {
                BackupFileBeforeCopy(destPath, versionCountToKeep);
            }

            if (DebugLevel >= 3)
            {
                OnDebugEvent("Copying file with CopyFileEx", sourcePath + " to " + destPath);
            }

            UpdateCurrentStatus(CopyStatus.NormalCopy, sourcePath);
            File.Copy(sourcePath, destPath, overWrite);
            UpdateCurrentStatusIdle();
        }

        #endregion

        #region "Lock File Copying functions"

        /// <summary>
        /// Copy the source file to the target path; do not overWrite existing files
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="overWrite">True to overWrite existing files</param>
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

            var lockFolderPathSource = GetLockFolder(sourceFile);
            var lockFolderPathTarget = GetLockFolder(targetFile);

            if (!string.IsNullOrEmpty(lockFolderPathSource) || !string.IsNullOrEmpty(lockFolderPathTarget))
            {
                useLockFile = true;
            }

            if (useLockFile)
            {
                var success = CopyFileUsingLocks(
                    lockFolderPathSource, lockFolderPathTarget,
                    sourceFile, targetFilePath,
                    managerName, overWrite);
                return success;
            }

            var expectedSourceLockFolder = GetLockFolderPath(sourceFile);
            var expectedTargetLockFolder = GetLockFolderPath(targetFile);

            if (string.IsNullOrEmpty(expectedSourceLockFolder) && string.IsNullOrEmpty(expectedTargetLockFolder))
            {
                // File is being copied locally; we don't use lock folders
                // Do not raise this as a DebugEvent
            }
            else
            {
                if (string.IsNullOrEmpty(expectedSourceLockFolder))
                {
                    // Source file is local; lock folder would not be used
                    expectedSourceLockFolder = "Source file is local";
                }

                if (string.IsNullOrEmpty(expectedTargetLockFolder))
                {
                    // Target file is local; lock folder would not be used
                    expectedTargetLockFolder = "Target file is local";
                }

                if (DebugLevel >= 1)
                {
                    OnDebugEvent("Lock file folder not found on the source or target",
                                 expectedSourceLockFolder + " and " + expectedTargetLockFolder);
                }
            }

            CopyFileEx(sourceFile.FullName, targetFilePath, overWrite, backupDestFileBeforeCopy: false);

            return true;
        }


        /// <summary>
        /// Given a file path, return the lock file folder if it exsists
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns>Lock folder path if it exists</returns>
        /// <remarks>Lock folders are only returned for remote shares (shares that start with \\)</remarks>
        public string GetLockFolder(FileInfo dataFile)
        {

            var lockFolderPath = GetLockFolderPath(dataFile);

            if (!string.IsNullOrEmpty(lockFolderPath) && Directory.Exists(lockFolderPath))
            {
                return lockFolderPath;
            }

            return string.Empty;

        }

        /// <summary>
        /// Given a file path, return the lock file folder path (does not verify that it exists)
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns>Lock folder path</returns>
        /// <remarks>Lock folders are only returned for remote shares (shares that start with \\)</remarks>
        private string GetLockFolderPath(FileInfo dataFile)
        {

            if (Path.IsPathRooted(dataFile.FullName))
            {
                var directory = dataFile.Directory;
                if (directory != null && directory.Root.FullName.StartsWith(@"\\"))
                {
                    return Path.Combine(GetServerShareBase(directory.Root.FullName), "DMS_LockFiles");
                }
            }

            return string.Empty;

        }

        /// <summary>
        /// Copy the source file to the target path
        /// </summary>
        /// <param name="lockFolderPathSource">Path to the lock folder for the source file; can be an empty string</param>
        /// <param name="lockFolderPathTarget">Path to the lock folder for the target file; can be an empty string</param>
        /// <param name="sourceFile">Source file object</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="managerName">Manager name (included in the lock file name)</param>
        /// <param name="overWrite">True to overWrite existing files</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>If the file exists yet overWrite is false, will not copy the file but will still return true</remarks>
        public bool CopyFileUsingLocks(
            string lockFolderPathSource, string lockFolderPathTarget,
            FileInfo sourceFile, string targetFilePath, string managerName, bool overWrite)
        {
            if (!overWrite && File.Exists(targetFilePath))
            {
                if (DebugLevel >= 2)
                {
                    OnDebugEvent("Skipping file since target exists", targetFilePath);
                }
                return true;
            }

            // Examine the size of the source file
            // If less than LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB then
            // copy the file normally
            var sourceFileSizeMB = Convert.ToInt32(sourceFile.Length / 1024.0 / 1024.0);
            if (sourceFileSizeMB < LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB || string.IsNullOrWhiteSpace(lockFolderPathSource) && string.IsNullOrWhiteSpace(lockFolderPathTarget))
            {
                const bool backupDestFileBeforeCopy = false;
                if (DebugLevel >= 2)
                {
                    var debugMsg = string.Format(
                        "File to copy is {0:F2} MB, which is less than {1} MB; will use CopyFileEx for {2}",
                        sourceFile.Length / 1024.0 / 1024.0, LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB, sourceFile.Name);

                    OnDebugEvent(debugMsg, sourceFile.FullName);
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

                DirectoryInfo lockFolderSource = null;
                DirectoryInfo lockFolderTarget = null;
                var lockFileTimestamp = GetLockFileTimeStamp();

                if (!string.IsNullOrWhiteSpace(lockFolderPathSource))
                {
                    lockFolderSource = new DirectoryInfo(lockFolderPathSource);
                    lockFilePathSource = CreateLockFile(lockFolderSource, lockFileTimestamp, sourceFile, targetFilePath, managerName);
                }

                if (!string.IsNullOrWhiteSpace(lockFolderPathTarget))
                {
                    lockFolderTarget = new DirectoryInfo(lockFolderPathTarget);
                    lockFilePathTarget = CreateLockFile(lockFolderTarget, lockFileTimestamp, sourceFile, targetFilePath, managerName);
                }

                WaitForLockFileQueue(lockFileTimestamp, lockFolderSource, lockFolderTarget, sourceFile, targetFilePath, MAX_LOCKFILE_WAIT_TIME_MINUTES);

                if (DebugLevel >= 1)
                {
                    OnDebugEvent("Copying " + sourceFile.Name + " using Locks", sourceFile.FullName + " to " + targetFilePath);
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
        /// Create a lock file in the specified lock folder
        /// </summary>
        /// <param name="lockFolder"></param>
        /// <param name="lockFileTimestamp"></param>
        /// <param name="sourceFile"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="managerName"></param>
        /// <returns>Full path to the lock file; empty string if an error or if lockFolder is null</returns>
        /// <remarks></remarks>
        public string CreateLockFile(DirectoryInfo lockFolder, long lockFileTimestamp, FileInfo sourceFile, string targetFilePath, string managerName)
        {

            if (lockFolder == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(managerName))
            {
                managerName = "UnknownManager";
            }

            // Define the lock file name
            var lockFileName = GenerateLockFileName(lockFileTimestamp, sourceFile, managerName);
            var lockFilePath = Path.Combine(lockFolder.FullName, lockFileName);
            while (File.Exists(lockFilePath))
            {
                // File already exists for this manager; append a dash to the path
                lockFileName = Path.GetFileNameWithoutExtension(lockFileName) + "-" + Path.GetExtension(lockFileName);
                lockFilePath = Path.Combine(lockFolder.FullName, lockFileName);
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

                OnDebugEvent("Created lock file in " + lockFolder.FullName, lockFilePath);

            }
            catch (Exception ex)
            {
                // Error creating the lock file
                // Return an empty string
                OnWarningEvent("Error creating lock file in " + lockFolder.FullName + ": " + ex.Message);
                return string.Empty;
            }

            return lockFilePath;

        }

        /// <summary>
        ///  Deletes the specified directory and all subdirectories
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks></remarks>
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
        /// <remarks></remarks>
        public bool DeleteDirectory(string directoryPath, bool ignoreErrors)
        {

            var localDotDFolder = new DirectoryInfo(directoryPath);

            try
            {
                localDotDFolder.Delete(true);
            }
            catch (Exception)
            {
                // Problems deleting one or more of the files
                if (!ignoreErrors)
                    throw;

                // Collect garbage, then delete the files one-by-one
                clsProgRunner.GarbageCollectNow();

                return DeleteDirectoryFiles(directoryPath, deleteFolderIfEmpty: true);
            }

            return true;

        }

        /// <summary>
        /// Deletes the specified directory and all subdirectories; does not delete the target folder
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Deletes each file individually.  Deletion errors are reported but are not treated as a fatal error</remarks>
        public bool DeleteDirectoryFiles(string directoryPath)
        {
            return DeleteDirectoryFiles(directoryPath, deleteFolderIfEmpty: false);
        }

        /// <summary>
        /// Deletes the specified directory and all subdirectories
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="deleteFolderIfEmpty">Set to True to delete the folder, if it is empty</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Deletes each file individually.  Deletion errors are reported but are not treated as a fatal error</remarks>
        public bool DeleteDirectoryFiles(string directoryPath, bool deleteFolderIfEmpty)
        {

            var folderToDelete = new DirectoryInfo(directoryPath);
            var errorCount = 0;

            foreach (var targetFile in folderToDelete.GetFiles("*", SearchOption.AllDirectories))
            {
                if (!DeleteFileIgnoreErrors(targetFile.FullName))
                {
                    errorCount += 1;
                }
            }

            if (errorCount == 0 && deleteFolderIfEmpty)
            {
                try
                {
                    folderToDelete.Delete(true);
                }
                catch (Exception ex)
                {
                    OnWarningEvent("Error removing empty directory", "Unable to delete directory " + folderToDelete.FullName + ": " + ex.Message);
                    errorCount += 1;
                }
            }

            if (errorCount == 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Delete the specified file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>True if successfully deleted (or if the file doesn't exist); false if an error</returns>
        /// <remarks>If the initial attempt fails, then checks the readonly bit and tries again.  If not readonly, then performs a garbage collection (every 500 msec)</remarks>
        private bool DeleteFileIgnoreErrors(string filePath)
        {

            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            var targetFile = new FileInfo(filePath);

            try
            {
                if (targetFile.Exists)
                {
                    targetFile.Delete();
                }
                return true;
            }
            catch (Exception)
            {
                // Ignore errors here
            }

            try
            {
                // The file might be readonly; check for this then re-try the delete
                if (targetFile.IsReadOnly)
                {
                    targetFile.IsReadOnly = false;
                }
                else
                {
                    if (DateTime.UtcNow.Subtract(mLastGC).TotalMilliseconds >= 500)
                    {
                        mLastGC = DateTime.UtcNow;
                        clsProgRunner.GarbageCollectNow();
                    }
                }

                targetFile.Delete();

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
        /// Finds lock files with a timestamp less than
        /// </summary>
        /// <param name="lockFolder"></param>
        /// <param name="lockFileTimestamp"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private List<int> FindLockFiles(DirectoryInfo lockFolder, long lockFileTimestamp)
        {
            var lockFiles = new List<int>();

            if (lockFolder == null)
            {
                return lockFiles;
            }

            lockFolder.Refresh();

            foreach (var lockFile in lockFolder.GetFiles("*" + LOCKFILE_EXTENSION))
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
        /// Generate the lock file name, which starts with a msec-based timestamp, then has the source file size (in MB), then has information on the machine creating the file
        /// </summary>
        /// <param name="lockFileTimestamp"></param>
        /// <param name="sourceFile"></param>
        /// <param name="managerName"></param>
        /// <returns></returns>
        /// <remarks></remarks>
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

            var lockFileName = lockFileTimestamp + "_" + (sourceFile.Length / 1024.0 / 1024.0).ToString("0000") + "_" + hostName + "_" + managerName + LOCKFILE_EXTENSION;

            // Replace any invalid characters (including spaces) with an underscore
            return mInvalidDosChars.Replace(lockFileName, "_");

        }

        /// <summary>
        /// Get the time stamp to be used when naming a lock file
        /// </summary>
        /// <returns></returns>
        public long GetLockFileTimeStamp()
        {
            return (long)Math.Round(DateTime.UtcNow.Subtract(new DateTime(2010, 1, 1)).TotalMilliseconds, 0);
        }

        /// <summary>
        /// Returns the first portion of a network share path, for example \\MyServer is returned for \\MyServer\Share\Filename.txt
        /// </summary>
        /// <param name="serverSharePath"></param>
        /// <returns></returns>
        /// <remarks>Treats \\picfs as a special share since DMS-related files are at \\picfs\projects\DMS</remarks>
        public string GetServerShareBase(string serverSharePath)
        {
            if (!serverSharePath.StartsWith(@"\\"))
                return string.Empty;

            var slashIndex = serverSharePath.IndexOf('\\', 2);
            if (slashIndex <= 0)
                return serverSharePath;

            var serverShareBase = serverSharePath.Substring(0, slashIndex);
            if (serverShareBase.ToLower() == @"\\picfs")
            {
                serverShareBase = @"\\picfs\projects\DMS";
            }
            return serverShareBase;
        }
        #endregion

        #region "CopyDirectory function"

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
        /// <param name="fileNamesToSkip">
        /// List of file names to skip when copying the directory (and subdirectories);
        /// can optionally contain full path names to skip</param>
        /// <param name="managerName"></param>
        public void CopyDirectory(string sourcePath, string destPath, bool overWrite, bool readOnly, List<string> fileNamesToSkip, string managerName)
        {
            const bool setAttribute = true;
            CopyDirectoryEx(sourcePath, destPath, overWrite, setAttribute, readOnly, fileNamesToSkip, managerName);

        }

        /// <summary>
        /// Copies a source directory to the destination directory. Allows overwriting.
        /// </summary>
        /// <remarks>Usage: CopyDirectory("C:\Misc", "D:\MiscBackup")
        /// Original code obtained from vb2themax.com
        /// </remarks>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="destPath">The destination directory path.</param>
        /// <param name="overWrite">true if the destination file can be overwritten; otherwise, false.</param>
        /// <param name="setAttribute">true if the read-only attribute of the destination file is to be modified, false otherwise.</param>
        /// <param name="readOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        /// <param name="fileNamesToSkip">
        /// List of file names to skip when copying the directory (and subdirectories);
        /// can optionally contain full path names to skip</param>
        /// <param name="managerName">Name of the calling program; used when calling CopyFileUsingLocks</param>
        private void CopyDirectoryEx(string sourcePath, string destPath, bool overWrite, bool setAttribute, bool readOnly,
            IReadOnlyCollection<string> fileNamesToSkip, string managerName)
        {
            var sourceDir = new DirectoryInfo(sourcePath);
            var destDir = new DirectoryInfo(destPath);

            // the source directory must exist, otherwise throw an exception
            if (sourceDir.Exists)
            {
                // If destination SubDir's parent SubDir does not exist throw an exception
                if (destDir.Parent != null && !destDir.Parent.Exists)
                {
                    throw new DirectoryNotFoundException("Destination directory does not exist: " + destDir.Parent.FullName);
                }

                if (!destDir.Exists)
                {
                    destDir.Create();
                }

                // Populate dctFileNamesToSkip
                var dctFileNamesToSkip = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                if (fileNamesToSkip != null)
                {
                    foreach (var fileName in fileNamesToSkip)
                    {
                        dctFileNamesToSkip.Add(fileName, "");
                    }
                }

                // Copy all the files of the current directory
                foreach (var childFile in sourceDir.GetFiles())
                {
                    // Look for both the file name and the full path in dctFileNamesToSkip
                    // If either matches, then to not copy the file
                    bool copyFile;
                    if (dctFileNamesToSkip.ContainsKey(childFile.Name))
                    {
                        copyFile = false;
                    }
                    else if (dctFileNamesToSkip.ContainsKey(childFile.FullName))
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
                foreach (var subFolder in sourceDir.GetDirectories())
                {
                    if (subFolder.FullName.Equals(destDir.FullName))
                    {
                        // Skip this subdirectory since it is our destination folder
                        continue;
                    }
                    CopyDirectoryEx(subFolder.FullName, Path.Combine(destDir.FullName, subFolder.Name), overWrite, setAttribute, readOnly, fileNamesToSkip, managerName);
                }
            }
            else
            {
                throw new DirectoryNotFoundException("Source directory does not exist: " + sourceDir.FullName);
            }

        }

        /// <summary>
        /// Copies the file attributes from a source file to a target file, explicitly updating the read-only bit based on readOnly
        /// </summary>
        /// <param name="sourceFile">Source FileInfo</param>
        /// <param name="targetFilePath">Target file path</param>
        /// <param name="readOnly">True to force the ReadOnly bit on, False to force it off</param>
        /// <remarks></remarks>
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

        #region "CopyDirectoryWithResume function"

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// Overwrites existing files if they differ in modification time or size.
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceFolderPath">The source directory path.</param>
        /// <param name="targetFolderPath">The destination directory path.</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(string sourceFolderPath, string targetFolderPath)
        {

            const bool recurse = false;
            const FileOverwriteMode fileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer;
            var fileNamesToSkip = new List<string>();

            return CopyDirectoryWithResume(sourceFolderPath, targetFolderPath, recurse, fileOverwriteMode, fileNamesToSkip);
        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// Overwrites existing files if they differ in modification time or size.
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceFolderPath">The source directory path.</param>
        /// <param name="targetFolderPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(string sourceFolderPath, string targetFolderPath, bool recurse)
        {

            const FileOverwriteMode fileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer;
            var fileNamesToSkip = new List<string>();

            return CopyDirectoryWithResume(sourceFolderPath, targetFolderPath, recurse, fileOverwriteMode, fileNamesToSkip);
        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceFolderPath">The source directory path.</param>
        /// <param name="targetFolderPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceFolderPath, string targetFolderPath,
            bool recurse, FileOverwriteMode fileOverwriteMode, List<string> fileNamesToSkip)
        {
            const bool setAttribute = false;
            const bool readOnly = false;

            return CopyDirectoryWithResume(sourceFolderPath, targetFolderPath, recurse, fileOverwriteMode, setAttribute, readOnly,
                fileNamesToSkip, out _, out _, out _);

        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceFolderPath">The source directory path.</param>
        /// <param name="targetFolderPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="fileCountSkipped">Number of files skipped (output)</param>
        /// <param name="fileCountResumed">Number of files resumed (output)</param>
        /// <param name="fileCountNewlyCopied">Number of files newly copied (output)</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceFolderPath, string targetFolderPath,
            bool recurse, FileOverwriteMode fileOverwriteMode,
            out int fileCountSkipped, out int fileCountResumed, out int fileCountNewlyCopied)
        {

            const bool setAttribute = false;
            const bool readOnly = false;
            var fileNamesToSkip = new List<string>();

            return CopyDirectoryWithResume(sourceFolderPath, targetFolderPath, recurse, fileOverwriteMode, setAttribute, readOnly,
                fileNamesToSkip, out fileCountSkipped, out fileCountResumed, out fileCountNewlyCopied);

        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceFolderPath">The source directory path.</param>
        /// <param name="targetFolderPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <param name="fileCountSkipped">Number of files skipped (output)</param>
        /// <param name="fileCountResumed">Number of files resumed (output)</param>
        /// <param name="fileCountNewlyCopied">Number of files newly copied (output)</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceFolderPath, string targetFolderPath,
            bool recurse, FileOverwriteMode fileOverwriteMode, List<string> fileNamesToSkip,
            out int fileCountSkipped, out int fileCountResumed, out int fileCountNewlyCopied)
        {

            const bool setAttribute = false;
            const bool readOnly = false;

            return CopyDirectoryWithResume(sourceFolderPath, targetFolderPath, recurse, fileOverwriteMode, setAttribute, readOnly,
                fileNamesToSkip, out fileCountSkipped, out fileCountResumed, out fileCountNewlyCopied);

        }

        /// <summary>
        /// Copies a source directory to the destination directory.
        /// overWrite behavior is governed by fileOverwriteMode
        /// Copies large files in chunks and allows resuming copying a large file if interrupted.
        /// </summary>
        /// <param name="sourceFolderPath">The source directory path.</param>
        /// <param name="targetFolderPath">The destination directory path.</param>
        /// <param name="recurse">True to copy subdirectories</param>
        /// <param name="fileOverwriteMode">Behavior when a file already exists at the destination</param>
        /// <param name="setAttribute">True if the read-only attribute of the destination file is to be modified, false otherwise.</param>
        /// <param name="readOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        /// <param name="fileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        /// <param name="fileCountSkipped">Number of files skipped (output)</param>
        /// <param name="fileCountResumed">Number of files resumed (output)</param>
        /// <param name="fileCountNewlyCopied">Number of files newly copied (output)</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        public bool CopyDirectoryWithResume(
            string sourceFolderPath, string targetFolderPath,
            bool recurse, FileOverwriteMode fileOverwriteMode,
            bool setAttribute, bool readOnly, List<string> fileNamesToSkip,
            out int fileCountSkipped, out int fileCountResumed, out int fileCountNewlyCopied)
        {
            var success = true;

            fileCountSkipped = 0;
            fileCountResumed = 0;
            fileCountNewlyCopied = 0;

            var sourceFolder = new DirectoryInfo(sourceFolderPath);
            var targetFolder = new DirectoryInfo(targetFolderPath);

            // The source directory must exist, otherwise throw an exception
            if (!sourceFolder.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist: " + sourceFolder.FullName);
            }

            if (targetFolder.Parent == null)
            {
                throw new DirectoryNotFoundException("Unable to determine the parent folder of " + targetFolder.FullName);
            }
            // If destination SubDir's parent directory does not exist throw an exception
            if (!targetFolder.Parent.Exists)
            {
                throw new DirectoryNotFoundException("Destination directory does not exist: " + targetFolder.Parent.FullName);
            }

            if (sourceFolder.FullName == targetFolder.FullName)
            {
                throw new IOException("Source and target directories cannot be the same: " + targetFolder.FullName);
            }

            try
            {
                // Create the target folder if necessary
                if (!targetFolder.Exists)
                {
                    targetFolder.Create();
                }

                // Populate dctFileNamesToSkip
                var dctFileNamesToSkip = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                if (fileNamesToSkip != null)
                {
                    // Copy the values from fileNamesToSkip to dctFileNamesToSkip so that we can perform case-insensitive searching
                    foreach (var item in fileNamesToSkip)
                    {
                        dctFileNamesToSkip.Add(item, string.Empty);
                    }
                }

                // Copy all the files of the current directory

                foreach (var sourceFile in sourceFolder.GetFiles())
                {
                    // Look for both the file name and the full path in dctFileNamesToSkip
                    // If either matches, then do not copy the file
                    bool copyFile;
                    if (dctFileNamesToSkip.ContainsKey(sourceFile.Name))
                    {
                        copyFile = false;
                    }
                    else if (dctFileNamesToSkip.ContainsKey(sourceFile.FullName))
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
                        var existingFile = new FileInfo(Path.Combine(targetFolder.FullName, sourceFile.Name));

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
                        fileCountSkipped += 1;

                    }
                    else
                    {
                        var targetFilePath = Path.Combine(targetFolder.FullName, sourceFile.Name);
                        success = CopyFileWithResume(sourceFile, targetFilePath, out var copyResumed);

                        if (!success)
                            break;

                        if (copyResumed)
                        {
                            fileCountResumed += 1;
                        }
                        else
                        {
                            fileCountNewlyCopied += 1;
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
                    foreach (var subFolder in sourceFolder.GetDirectories())
                    {
                        var subDirtargetFolderPath = Path.Combine(targetFolderPath, subFolder.Name);

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        success = CopyDirectoryWithResume(
                            subFolder.FullName, subDirtargetFolderPath,
                            recurse, fileOverwriteMode, setAttribute, readOnly, fileNamesToSkip,
                            out fileCountSkipped, out fileCountResumed, out fileCountNewlyCopied);
                    }
                }

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
        /// <returns></returns>
        /// <remarks></remarks>
        public bool CopyFileWithResume(string sourceFilePath, string targetFilePath, out bool copyResumed)
        {
            var sourceFile = new FileInfo(sourceFilePath);
            return CopyFileWithResume(sourceFile, targetFilePath, out copyResumed);

        }

        /// <summary>
        /// Copy sourceFile to targetFolder
        /// Copies the file using chunks, thus allowing for resuming
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="copyResumed">Output parameter; true if copying was resumed</param>
        /// <returns>True if success; false if an error</returns>
        /// <remarks></remarks>
        public bool CopyFileWithResume(FileInfo sourceFile, string targetFilePath, out bool copyResumed)
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
                    sourceFile.CopyTo(targetFilePath, true);

                    UpdateCurrentStatusIdle();
                    copyResumed = false;
                    return true;

                }

                // Delete the target file if it already exists
                if (File.Exists(targetFilePath))
                {
                    File.Delete(targetFilePath);
                    clsProgRunner.SleepMilliseconds(25);
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

                        using (var infoFileReader = new StreamReader(new FileStream(filePartInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                        {

                            var lstSourceLines = new List<string>();

                            while (!infoFileReader.EndOfStream)
                            {
                                lstSourceLines.Add(infoFileReader.ReadLine());
                            }

                            if (lstSourceLines.Count >= 3)
                            {
                                // The first line contains the source file path
                                // The second contains the file length, in bytes
                                // The third contains the file modification time (UTC)

                                if (lstSourceLines[0] == sourceFile.FullName && lstSourceLines[1] == sourceFile.Length.ToString())
                                {
                                    // Name and size are the same
                                    // See if the timestamps agree within 2 seconds (need to allow for this in case we're comparing NTFS and FAT32)

                                    if (DateTime.TryParse(lstSourceLines[2], out var cachedLastWriteTimeUTC))
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

                    // Delete FilePart file in the target folder if it already exists
                    if (filePart.Exists)
                    {
                        filePart.Delete();
                        clsProgRunner.SleepMilliseconds(25);
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

                // Now copy the file, appending data to swFilePart
                // Open the source and seek to fileOffsetStart if > 0
                using (var reader = new FileStream(sourceFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                        // Read data in 1MB chunks and append to swFilePart
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
                clsProgRunner.GarbageCollectNow();

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
        /// <remarks></remarks>
        private bool NearlyEqualFileTimes(DateTime time1, DateTime time2)
        {
            if (Math.Abs(time1.Subtract(time2).TotalSeconds) <= 2.05)
            {
                return true;
            }

            return false;
        }

        private void OnDebugEvent(string message, string detailedMessage)
        {
            OnStatusEvent(message);
            OnDebugEvent("  " + detailedMessage);
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

        #region "GetDirectorySize function"
        /// <summary>
        /// Get the directory size.
        /// </summary>
        /// <param name="folderPath">The path to the directory.</param>
        /// <returns>The directory size.</returns>
        public long GetDirectorySize(string folderPath)
        {

            // Overload for returning directory size only

            long DumfileCount = 0;
            long DumDirCount = 0;

            return GetDirectorySizeEX(folderPath, ref DumfileCount, ref DumDirCount);

        }

        /// <summary>
        /// Get the directory size, file count, and directory count for the entire directory tree.
        /// </summary>
        /// <param name="folderPath">The path to the directory.</param>
        /// <param name="fileCount">The number of files in the entire directory tree.</param>
        /// <param name="subFolderCount">The number of directories in the entire directory tree.</param>
        /// <returns>The directory size.</returns>
        public long GetDirectorySize(string folderPath, ref long fileCount, ref long subFolderCount)
        {

            //Overload for returning directory size, file count and directory count for entire directory tree
            return GetDirectorySizeEX(folderPath, ref fileCount, ref subFolderCount);

        }

        /// <summary>
        /// Get the directory size, file count, and directory count for the entire directory tree.
        /// </summary>
        /// <param name="folderPath">The path to the directory.</param>
        /// <param name="fileCount">The number of files in the entire directory tree.</param>
        /// <param name="subFolderCount">The number of directories in the entire directory tree.</param>
        /// <returns>The directory size.</returns>
        private long GetDirectorySizeEX(string folderPath, ref long fileCount, ref long subFolderCount)
        {

            // Returns the size of the specified directory, number of files in the directory tree, and number of subdirectories
            // - Note: requires Imports System.IO
            // - Usage: Dim DirSize As Long = GetDirectorySize("D:\Projects")
            //
            // Original code obtained from vb2themax.com
            long folderSize = 0;
            var folder = new DirectoryInfo(folderPath);

            // Add the size of each file
            foreach (var childFile in folder.GetFiles())
            {
                folderSize += childFile.Length;
                fileCount += 1;
            }

            // Add the size of each sub-directory, that is retrieved by recursively
            // calling this same routine
            foreach (var subDir in folder.GetDirectories())
            {
                folderSize += GetDirectorySizeEX(subDir.FullName, ref fileCount, ref subFolderCount);
                subFolderCount += 1;
            }

            return folderSize;

        }
        #endregion

        #region "MoveDirectory Function"

        /// <summary>
        /// Move a directory
        /// </summary>
        /// <param name="sourceFolderPath"></param>
        /// <param name="targetFolderPath"></param>
        /// <param name="overwriteFiles"></param>
        /// <returns></returns>
        public bool MoveDirectory(string sourceFolderPath, string targetFolderPath, bool overwriteFiles)
        {
            return MoveDirectory(sourceFolderPath, targetFolderPath, overwriteFiles, ManagerName);
        }

        /// <summary>
        /// Move a directory
        /// </summary>
        /// <param name="sourceFolderPath"></param>
        /// <param name="targetFolderPath"></param>
        /// <param name="overwriteFiles"></param>
        /// <param name="managerName"></param>
        /// <returns></returns>
        public bool MoveDirectory(string sourceFolderPath, string targetFolderPath, bool overwriteFiles, string managerName)
        {
            bool success;

            var sourceFolder = new DirectoryInfo(sourceFolderPath);

            // Recursively call this function for each subdirectory
            foreach (var subFolder in sourceFolder.GetDirectories())
            {
                success = MoveDirectory(subFolder.FullName, Path.Combine(targetFolderPath, subFolder.Name), overwriteFiles, managerName);
                if (!success)
                {
                    throw new Exception("Error moving directory " + subFolder.FullName + " to " + targetFolderPath + "; MoveDirectory returned False");
                }
            }

            foreach (var sourceFile in sourceFolder.GetFiles())
            {
                success = CopyFileUsingLocks(sourceFile.FullName, Path.Combine(targetFolderPath, sourceFile.Name), managerName, overwriteFiles);
                if (!success)
                {
                    throw new Exception("Error copying file " + sourceFile.FullName + " to " + targetFolderPath + "; CopyFileUsingLocks returned False");
                }

                // Delete the source file
                DeleteFileIgnoreErrors(sourceFile.FullName);
            }

            sourceFolder.Refresh();
            if (sourceFolder.GetFileSystemInfos("*", SearchOption.AllDirectories).Length == 0)
            {
                // This folder is now empty; delete it
                try
                {
                    sourceFolder.Delete(true);
                }
                catch (Exception)
                {
                    // Ignore errors here
                }
            }

            return true;

        }

        #endregion

        #region "Utility Functions"

        /// <summary>
        /// Renames targetFilePath to have _Old1 before the file extension
        /// Also looks for and renames other backed up versions of the file (those with _Old2, _Old3, etc.)
        /// Use this function to backup old versions of a file before copying a new version to a target folder
        /// Keeps up to 9 old versions of a file
        /// </summary>
        /// <param name="targetFilePath">Full path to the file to backup</param>
        /// <returns>True if the file was successfully renamed (also returns True if the target file does not exist)</returns>
        /// <remarks></remarks>
        public static bool BackupFileBeforeCopy(string targetFilePath)
        {
            return BackupFileBeforeCopy(targetFilePath, DEFAULT_VERSION_COUNT_TO_KEEP);
        }

        /// <summary>
        /// Renames targetFilePath to have _Old1 before the file extension
        /// Also looks for and renames other backed up versions of the file (those with _Old2, _Old3, etc.)
        /// Use this function to backup old versions of a file before copying a new version to a target folder
        /// </summary>
        /// <param name="targetFilePath">Full path to the file to backup</param>
        /// <param name="versionCountToKeep">Maximum backup copies of the file to keep</param>
        /// <returns>True if the file was successfully renamed (also returns True if the target file does not exist)</returns>
        /// <remarks></remarks>
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

            var targetFolderPath = targetFile.Directory.FullName;

            // Backup any existing copies of targetFilePath

            for (var revision = versionCountToKeep - 1; revision >= 0; revision += -1)
            {
                var baseNameCurrent = baseName;
                if (revision > 0)
                {
                    baseNameCurrent += "_Old" + revision;
                }
                baseNameCurrent += extension;

                var fileToRename = new FileInfo(Path.Combine(targetFolderPath, baseNameCurrent));
                var newFilePath = Path.Combine(targetFolderPath, baseName + "_Old" + (revision + 1) + extension);

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
        /// <returns></returns>
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
        /// C:\My Docum..\Word\Business\Finances.doc
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
            //   Length for 25 characters= C:\My Docume..\Readme.txt

            // For "C:\My Documents\Word\Business\Finances.doc"
            //   Minimum string returned=  C:\...\B..\Fin..
            //   Length for 20 characters= C:\...\B..\Finance..
            //   Length for 25 characters= C:\...\Bus..\Finances.doc
            //   Length for 32 characters= C:\...\W..\Business\Finances.doc
            //   Length for 40 characters= C:\My Docum..\Word\Business\Finances.doc

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

            for (pathPartCount = 0; pathPartCount <= pathParts.Length - 1; pathPartCount++)
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

            if (pathToCompact.StartsWith("\\\\"))
            {
                leadingChars = "\\\\";
                charIndex = pathToCompact.IndexOfAny(pathSepChars, 2);

                if (charIndex > 0)
                {
                    leadingChars = "\\\\" + pathToCompact.Substring(2, charIndex - 1);
                    pathParts[0] = pathToCompact.Substring(charIndex + 1);
                }
                else
                {
                    pathParts[0] = pathToCompact.Substring(2);
                }
            }
            else if (pathToCompact.StartsWith("\\") || pathToCompact.StartsWith("/"))
            {
                leadingChars = pathToCompact.Substring(0, 1);
                pathParts[0] = pathToCompact.Substring(1);
            }
            else if (pathToCompact.StartsWith(".\\") || pathToCompact.StartsWith("./"))
            {
                leadingChars = pathToCompact.Substring(0, 2);
                pathParts[0] = pathToCompact.Substring(2);
            }
            else if (pathToCompact.StartsWith("..\\") || pathToCompact.Substring(1, 2) == ":\\" || pathToCompact.StartsWith("../") || pathToCompact.Substring(1, 2) == ":/")
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
                    pathPartCount += 1;
                }
                else
                {
                    break;
                }
                loopCount += 1;
            } while (loopCount < 3);


            if (pathPartCount == 1)
            {
                // No \ or / found, we're forced to shorten the filename (though if a UNC, then can shorten part of the UNC)

                if (leadingChars.StartsWith("\\\\"))
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
                            leadingChars = leadingChars.Substring(0, shortLength) + "..\\";
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
                if (pathParts[0] == "...\\" || pathParts[0] == ".../")
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
                // If not short enough, replace the first with ... and call this function again
                shortLength = maxLength - leadingChars.Length - pathParts[3].Length - pathParts[2].Length - pathParts[1].Length - 3 - multiPathCorrection;
                if (shortLength < 1 && pathParts[2].Length > 0)
                {
                    // Not short enough, but other subdirectories are present
                    // Thus, can call this function recursively
                    shortenedPath = leadingChars + "..." + pathSepCharPreferred + pathParts[1] + pathParts[2] + pathParts[3];
                    shortenedPath = CompactPathString(shortenedPath, maxLength);
                }
                else
                {
                    if (leadingChars.StartsWith("\\\\"))
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
                                leadingChars = leadingChars.Substring(0, shortLength) + "..\\";
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
                    // If it is, then will need to shorten the filename too
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
        /// <returns></returns>
        /// <remarks></remarks>
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
        /// <returns></returns>
        /// <remarks></remarks>
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
                retriesRemaining -= 1;

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

                    // Make sure the readonly bit is not set
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
                    clsProgRunner.SleepMilliseconds(sleepTimeMsec);

                    // Increase sleepTimeMsec so that we sleep longer the next time, but cap the sleep time at 5.7 seconds
                    if (sleepTimeMsec < 5)
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
        /// <returns></returns>
        /// <remarks></remarks>
        public static bool IsVimSwapFile(string filePath)
        {

            var fileName = Path.GetFileName(filePath);
            if (fileName == null)
                return false;

            if (fileName.ToLower() == "_.swp" || fileName.StartsWith(".") && fileName.ToLower().EndsWith(".swp"))
            {
                return true;
            }

            return false;
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
        /// Determine on Windows using clsDiskInfo.GetDiskFreeSpace in PRISMWin.dll
        /// </param>
        /// <param name="errorMessage">Output message if there is not enough free space (or if the path is invalid)</param>
        /// <returns>True if more than minimumFreeSpaceMB is available; otherwise false</returns>
        /// <remarks></remarks>
        public static bool ValidateFreeDiskSpace(string outputFilePath, double minimumFreeSpaceMB, long currentDiskFreeSpaceBytes, out string errorMessage)
        {
            double outputFileExpectedSizeMB = 0;

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
        /// Determine on Windows using clsDiskInfo.GetDiskFreeSpace in PRISMWin.dll
        /// </param>
        /// <param name="errorMessage">Output message if there is not enough free space (or if the path is invalid)</param>
        /// <returns>True if more than minimumFreeSpaceMB is available; otherwise false</returns>
        /// <remarks>If currentDiskFreeSpaceBytes is negative, this function always returns true (provided the target directory exists)</remarks>
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
        /// <param name="lockFolderSource"></param>
        /// <param name="sourceFile"></param>
        /// <param name="maxWaitTimeMinutes"></param>
        public void WaitForLockFileQueue(long lockFileTimestamp, DirectoryInfo lockFolderSource, FileInfo sourceFile, int maxWaitTimeMinutes)
        {
            WaitForLockFileQueue(lockFileTimestamp, lockFolderSource, null, sourceFile, "Unknown_Target_File_Path", maxWaitTimeMinutes);

        }

        /// <summary>
        /// Wait for the lock file queue to drop below a threshold
        /// </summary>
        /// <param name="lockFileTimestamp"></param>
        /// <param name="lockFolderSource"></param>
        /// <param name="lockFolderTarget"></param>
        /// <param name="sourceFile"></param>
        /// <param name="targetFilePath"></param>
        /// <param name="maxWaitTimeMinutes"></param>
        public void WaitForLockFileQueue(
            long lockFileTimestamp, DirectoryInfo lockFolderSource,
            DirectoryInfo lockFolderTarget, FileInfo sourceFile, string targetFilePath, int maxWaitTimeMinutes)
        {
            // Find the recent LockFiles present in the source and/or target lock folders
            // These lists contain the sizes of the lock files with timestamps less than lockFileTimestamp

            var mbBacklogSource = 0;
            var mbBacklogTarget = 0;

            var waitTimeStart = DateTime.UtcNow;

            var sourceFileSizeMB = Convert.ToInt32(sourceFile.Length / 1024.0 / 1024.0);

            // Wait for up to 180 minutes (3 hours) for the server resources to free up

            // However, if retrieving files from adms.emsl.pnl.gov only wait for a maximum of 30 minutes
            // because sometimes that folder's permissions get messed up and we can create files there, but cannot delete them

            var maxWaitTimeSource = MAX_LOCKFILE_WAIT_TIME_MINUTES;
            var maxWaitTimeTarget = MAX_LOCKFILE_WAIT_TIME_MINUTES;

            // Switched from a2.emsl.pnl.gov to aurora.emsl.pnl.gov in June 2016
            // Switched from aurora.emsl.pnl.gov to adms.emsl.pnl.gov in September 2016
            if (lockFolderSource != null && lockFolderSource.FullName.ToLower().StartsWith("\\\\adms.emsl.pnl.gov\\"))
            {
                maxWaitTimeSource = 30;
            }

            if (lockFolderTarget != null && lockFolderTarget.FullName.ToLower().StartsWith("\\\\adms.emsl.pnl.gov\\"))
            {
                maxWaitTimeTarget = 30;
            }


            while (true)
            {
                // Refresh the lock files list by finding recent lock files with a timestamp less than lockFileTimestamp
                var lstLockFileMBSource = FindLockFiles(lockFolderSource, lockFileTimestamp);
                var lstLockFileMBTarget = FindLockFiles(lockFolderTarget, lockFileTimestamp);

                var stopWaiting = false;

                if (lstLockFileMBSource.Count <= 1 && lstLockFileMBTarget.Count <= 1)
                {
                    stopWaiting = true;

                }
                else
                {
                    mbBacklogSource = lstLockFileMBSource.Sum();
                    mbBacklogTarget = lstLockFileMBTarget.Sum();

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

                if (stopWaiting)
                {
                    LockQueueWaitComplete?.Invoke(sourceFile.FullName, targetFilePath, DateTime.UtcNow.Subtract(waitTimeStart).TotalMinutes);
                    break;
                }

                // Server resources exceed the thresholds
                // Sleep for 1 to 30 seconds, depending on mbBacklogSource and mbBacklogTarget
                // We compute sleepTimeMsec using the assumption that data can be copied to/from the server at a rate of 200 MB/sec
                // This is faster than reality, but helps minimize waiting too long between checking

                var sleepTimeSec = Math.Max(mbBacklogSource, mbBacklogTarget) / 200.0;

                if (sleepTimeSec < 1)
                    sleepTimeSec = 1;
                if (sleepTimeSec > 30)
                    sleepTimeSec = 30;

                WaitingForLockQueue?.Invoke(sourceFile.FullName, targetFilePath, mbBacklogSource, mbBacklogTarget);

                clsProgRunner.SleepMilliseconds(Convert.ToInt32(sleepTimeSec) * 1000);

                if (WaitedTooLong(waitTimeStart, MAX_LOCKFILE_WAIT_TIME_MINUTES))
                {
                    LockQueueTimedOut?.Invoke(sourceFile.FullName, targetFilePath, DateTime.UtcNow.Subtract(waitTimeStart).TotalMinutes);
                    break;
                }
            }

        }

        private bool WaitedTooLong(DateTime waitTimeStart, int maxLockfileWaitTimeMinutes)
        {
            if (DateTime.UtcNow.Subtract(waitTimeStart).TotalMinutes < maxLockfileWaitTimeMinutes)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region "GZip Compression"

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
                // When combining dir with name, we will assure that name only contains a filename and not a path
                var fileNameOrPath = string.IsNullOrWhiteSpace(decompressedFileName) ? currentFilePathWithoutGz : decompressedFileName;

                var dir = string.IsNullOrWhiteSpace(decompressedDirectoryPath) ? fileToDecompress.DirectoryName : decompressedDirectoryPath;
                if (string.IsNullOrWhiteSpace(dir))
                {
                    dir = ".";
                }

                decompressedFilePath = Path.Combine(dir, Path.GetFileName(fileNameOrPath));
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

            // GZipMetadataStream wraps the filestream to read the metadata before decompressing
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

                using (var decompressedFileStream = File.Create(decompressedFilePath))
                using (var decompressionStream = new GZipStream(gzipMetadataStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(decompressedFileStream);
                }
            }

            // Update the modification time of the decompressed file to match the time from the metadata
            File.SetLastWriteTime(decompressedFilePath, lastModified);

            return decompressedFilePath;
        }

        ///  <summary>
        /// Compress a file, using a special implementation to add supported metadata to the file (like last modified time and file name)
        ///  </summary>
        ///  <param name="fileToCompress">file to compress</param>
        ///  <param name="compressedDirectoryPath">path to directory where the gzipped file should be created</param>
        ///  <param name="compressedFileName">name for the gzipped file</param>
        ///  <param name="doNotStoreFileName">if true, the filename is not stored in the gzip metadata (so contained file name depends on gzip file name)</param>
        ///  <param name="comment">optional comment to add to gzip metadata (generally not used by decompression programs)</param>
        /// <param name="addHeaderCrc">if true, a CRC16 hash of the header information is written to the gzip metadata</param>
        public static void GZipCompressWithMetadata(FileInfo fileToCompress, string compressedDirectoryPath = null, string compressedFileName = null, bool doNotStoreFileName = false, string comment = null, bool addHeaderCrc = false)
        {

            var compressedFilePath = ConstructCompressedGZipFilePath(fileToCompress, compressedDirectoryPath, compressedFileName);

            string storedFileName = null;
            if (!doNotStoreFileName)
            {
                storedFileName = fileToCompress.Name;
            }

            // GZipMetadataStream wraps the filestream to add the metadata at the right time during the file write
            using (var decompressedFileStream = fileToCompress.OpenRead())
            using (var compressedFileStream = File.Create(compressedFilePath))
            using (var metadataAdder = new GZipMetadataStream(compressedFileStream, fileToCompress.LastWriteTime, storedFileName, comment, addHeaderCrc))
            using (var compressionStream = new GZipStream(metadataAdder, CompressionMode.Compress))
            {
                decompressedFileStream.CopyTo(compressionStream);
            }

            // Since metadata in the .gz file includes the last write time of the compressed file, do not change the last write time of the .gz file
        }

        /// <summary>
        /// Construct the path to the .gz file to create, possibly overriding the target directory and/or target file anme
        /// </summary>
        /// <param name="fileToCompress"></param>
        /// <param name="compressedDirectoryPath"></param>
        /// <param name="compressedFileName"></param>
        /// <returns></returns>
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
