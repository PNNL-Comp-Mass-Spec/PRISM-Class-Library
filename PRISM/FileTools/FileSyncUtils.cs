using System;
using System.IO;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Methods for copying files to/from a remote server, including creating a .hashcheck file and a .lastused file
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public class FileSyncUtils : EventNotifier
    {
        // Ignore Spelling: hashcheck, hashtype, utc

        /// <summary>
        /// Extension for .LastUsed files that track when a data file was last used
        /// </summary>
        public const string LASTUSED_FILE_EXTENSION = ".LastUsed";

        private readonly FileTools mFileTools;

        /// <summary>
        /// Constructor that takes a manager name
        /// </summary>
        /// <remarks>Will instantiate a new instance of clsFileTools</remarks>
        /// <param name="managerName"></param>
        public FileSyncUtils(string managerName)
        {
            mFileTools = new FileTools(managerName, 1);
        }

        /// <summary>
        /// Constructor that takes a clsFileTools instance
        /// </summary>
        /// <param name="fileTools"></param>
        public FileSyncUtils(FileTools fileTools)
        {
            mFileTools = fileTools;
        }

        /// <summary>
        /// Copy a file from a remote path and store it locally, including creating a .hashcheck file and a .lastused file
        /// If the file exists and the SHA1 hash matches, do not re-copy the file
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="targetDirectoryPath">Target directory path</param>
        /// <param name="errorMessage">Output: error message</param>
        /// <param name="recheckIntervalDays">
        /// If the .hashcheck file is more than this number of days old, re-compute the hash value of the local file and compare to the hashcheck file
        /// Set to 0 to check the hash on every call to this method
        /// </param>
        /// <param name="hashType">Hash type for newly created .hashcheck files</param>
        // ReSharper disable once UnusedMember.Global
        public bool CopyFileToLocal(
            string sourceFilePath,
            string targetDirectoryPath,
            out string errorMessage,
            int recheckIntervalDays = 0,
            HashUtilities.HashTypeConstants hashType = HashUtilities.HashTypeConstants.SHA1)
        {
            try
            {
                // Look for the source file
                var sourceFile = new FileInfo(sourceFilePath);

                if (!sourceFile.Exists)
                {
                    errorMessage = "File not found: " + sourceFile;
                    return false;
                }

                var sourceHashcheckFile = new FileInfo(sourceFile.FullName + HashUtilities.HASHCHECK_FILE_SUFFIX);

                var sourceHashInfo = new HashUtilities.HashInfoType();
                sourceHashInfo.Clear();

                var targetDirectory = new DirectoryInfo(targetDirectoryPath);

                // Look for the local .hashcheck file
                // If there is a hash validation error, we might delay re-copying the file, depending on whether this local .hashcheck file exists or was changed recently
                var localHashCheckFile = new FileInfo(Path.Combine(targetDirectory.FullName, sourceFile.Name + HashUtilities.HASHCHECK_FILE_SUFFIX));

                if (sourceHashcheckFile.Exists)
                {
                    // Read the .hashcheck file
                    sourceHashInfo = HashUtilities.ReadHashcheckFile(sourceHashcheckFile.FullName);
                }
                else
                {
                    // .hashcheck file not found; create it for the source file (in the source directory)
                    // Raise a warning if unable to create it, but continue

                    try
                    {
                        OnStatusEvent(string.Format("Creating .hashcheck file for {0}", sourceFile.FullName));

                        HashUtilities.CreateHashcheckFile(sourceFile.FullName, hashType, out var hashValueSource, out var warningMessage);

                        if (string.IsNullOrWhiteSpace(hashValueSource))
                        {
                            if (string.IsNullOrWhiteSpace(warningMessage))
                                OnWarningEvent("Unable to create the hash value for remote file " + sourceFile.FullName);
                            else
                                OnWarningEvent(warningMessage);
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(warningMessage))
                                OnWarningEvent(warningMessage);

                            sourceHashInfo.HashValue = hashValueSource;
                            sourceHashInfo.HashType = hashType;
                            sourceHashInfo.FileSize = sourceFile.Length;
                            sourceHashInfo.FileDateUtc = sourceFile.LastWriteTimeUtc;
                        }
                    }
                    catch (Exception ex2)
                    {
                        // Treat this as a non-critical error
                        OnWarningEvent(string.Format("Unable to create the .hashcheck file for source file {0}: {1}",
                                                     sourceFile.FullName, ex2.Message));
                    }
                }

                // Validate the target directory
                if (!targetDirectory.Exists)
                {
                    OnStatusEvent(string.Format("Creating directory {0}", targetDirectory.FullName));
                    targetDirectory.Create();
                }

                // Look for the target file in the target directory
                var targetFile = new FileInfo(Path.Combine(targetDirectory.FullName, sourceFile.Name));

                if (!targetFile.Exists)
                {
                    DeleteHashCheckFileForDataFile(targetFile);

                    OnStatusEvent(string.Format("Copying {0} to {1}", sourceFile.FullName, targetDirectory.FullName));

                    // Copy the source file locally
                    mFileTools.CopyFileUsingLocks(sourceFile, targetFile.FullName, true);

                    // Create the local .hashcheck file, sending localFilePath and the hash info of the source file
                    var validNewFile = ValidateFileVsHashcheck(targetFile.FullName, out errorMessage, sourceHashInfo, recheckIntervalDays);
                    return validNewFile;
                }

                OnDebugEvent(string.Format("Validating {0} vs. expected hash {1}", targetFile.FullName, sourceHashInfo.HashValue));

                // The target file exists
                // Create or validate the local .hashcheck file, sending localFilePath and the hash info of the source file
                var validFile = ValidateFileVsHashcheck(targetFile.FullName, out errorMessage, sourceHashInfo, recheckIntervalDays);
                if (validFile)
                    return true;

                // Existing local file and/or local file hash does not match the source file hash

                if (localHashCheckFile.Exists && DateTime.UtcNow.Subtract(localHashCheckFile.LastWriteTimeUtc).TotalMinutes > 10)
                {
                    // The local hash check file already existed and is over 10 minutes old
                    // Do not use a delay; immediately re-copy the file locally
                }
                else
                {
                    // Wait for a random time between 5 and 15 seconds, plus an additional 1 second per 50 MB, to give other processes a chance to copy the file
                    var rand = new Random();
                    var fileSizeMB = sourceFile.Length / 1024.0 / 1024;
                    var waitTimeSeconds = rand.Next(5, 15) + fileSizeMB / 50;

                    OnStatusEvent(string.Format("Hashcheck mismatch for {0}; waiting {1} seconds then re-checking", targetFile.FullName, waitTimeSeconds));

                    ConsoleMsgUtils.SleepSeconds(waitTimeSeconds);

                    // Repeat the validation of the .hashcheck file
                    // If valid, return true
                    // Otherwise, delete the local file and the local hashcheck file and re-try the copy to the local directory
                    var validFileB = ValidateFileVsHashcheck(targetFile.FullName, out errorMessage, sourceHashInfo, recheckIntervalDays: 0);
                    if (validFileB)
                    {
                        OnStatusEvent(string.Format("Hash value is now the expected value: {0}", sourceHashInfo.HashValue));
                        return true;
                    }
                }

                OnWarningEvent(string.Format("Hash for local file does not match the remote file; recopying {0} to {1}",
                                             sourceFile.FullName, targetDirectory.FullName));

                DeleteHashCheckFileForDataFile(targetFile);

                OnStatusEvent(string.Format("Copying {0} to {1}", sourceFile.FullName, targetDirectory.FullName));

                // Repeat copying the remote file locally
                mFileTools.CopyFileUsingLocks(sourceFile, targetFile.FullName, true);

                // Create the local .hashcheck file, sending localFilePath and the hash info of the source file
                var validFileC = ValidateFileVsHashcheck(targetFile.FullName, out errorMessage, sourceHashInfo, recheckIntervalDays: 0);
                return validFileC;
            }
            catch (Exception ex)
            {
                errorMessage = "Error retrieving/validating " + sourceFilePath + ": " + ex.Message;
                OnWarningEvent(errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Delete the .hashcheck file for the given data file
        /// Does nothing if the data file does not have a .hashcheck file
        /// </summary>
        /// <param name="dataFile"></param>
        private void DeleteHashCheckFileForDataFile(FileInfo dataFile)
        {
            try
            {
                var dataFileDirectory = dataFile.Directory;
                if (dataFileDirectory == null)
                    return;

                var localHashCheckFile = new FileInfo(Path.Combine(dataFileDirectory.FullName, dataFile.Name + HashUtilities.HASHCHECK_FILE_SUFFIX));
                if (localHashCheckFile.Exists)
                    localHashCheckFile.Delete();
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Update the .lastused file for the given data file
        /// </summary>
        /// <param name="dataFile"></param>
        public static void UpdateLastUsedFile(FileInfo dataFile)
        {
            var lastUsedFilePath = dataFile.FullName + LASTUSED_FILE_EXTENSION;

            try
            {
                using var writer = new StreamWriter(new FileStream(lastUsedFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
                writer.WriteLine(DateTime.UtcNow.ToString(HashUtilities.DATE_TIME_FORMAT));
            }
            catch (IOException)
            {
                // The file is likely open by another program; ignore this
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowWarning("Unable to create a new .LastUsed file at {0}: {1}", lastUsedFilePath, ex.Message);
            }
        }

        /// <summary>
        /// Validate that the hash value of a local file matches the expected hash info, creating the .hashcheck file if missing
        /// </summary>
        /// <param name="localFilePath">Local file path</param>
        /// <param name="errorMessage">Output: error message</param>
        /// <param name="expectedHashInfo">Expected hash info</param>
        /// <param name="recheckIntervalDays">
        /// If the .hashcheck file is more than this number of days old, re-compute the hash value of the local file and compare to the hashcheck file
        /// Set to 0 to check the hash on every call to this method
        /// </param>
        private bool ValidateFileVsHashcheck(string localFilePath, out string errorMessage, HashUtilities.HashInfoType expectedHashInfo, int recheckIntervalDays = 0)
        {
            var hashCheckFilePath = string.Empty;
            const bool checkDate = true;
            const bool computeHash = true;
            const bool checkSize = true;
            return ValidateFileVsHashcheck(localFilePath, hashCheckFilePath, out errorMessage, expectedHashInfo, checkDate, computeHash, checkSize, recheckIntervalDays);
        }

        /// <summary>
        /// Validate that the hash value of a local file matches the expected hash value
        /// </summary>
        /// <param name="localFilePath">Local file path</param>
        /// <param name="errorMessage">Output: error message</param>
        /// <param name="expectedHash">Expected hash value</param>
        /// <param name="expectedHashType">Hash type (CRC32, MD5, or SHA1)</param>
        /// <returns>True if the file is valid, otherwise false</returns>
        // ReSharper disable once UnusedMember.Global
        public bool ValidateFileVsHashcheck(string localFilePath, out string errorMessage, string expectedHash, HashUtilities.HashTypeConstants expectedHashType)
        {
            var expectedHashInfo = new HashUtilities.HashInfoType
            {
                HashValue = expectedHash,
                HashType = expectedHashType
            };

            var hashCheckFilePath = string.Empty;
            return ValidateFileVsHashcheck(localFilePath, hashCheckFilePath, out errorMessage, expectedHashInfo);
        }

        /// <summary>
        /// Validate that the hash value of a local file matches the expected hash info, creating the .hashcheck file if missing
        /// </summary>
        /// <remarks>
        /// Will create the .hashcheck file if missing
        /// Will also update the .lastused file for the local file
        /// </remarks>
        /// <param name="localFilePath">Local file path</param>
        /// <param name="hashCheckFilePath">Hashcheck file for the given data file (auto-defined if blank)</param>
        /// <param name="errorMessage">Output: error message</param>
        /// <param name="expectedHashType">Hash type that is presumed to be in the hashcheck file</param>
        /// <param name="recheckIntervalDays">
        /// If the .hashcheck file is more than this number of days old, re-compute the hash value of the local file and compare to the hashcheck file
        /// Set to 0 to check the hash on every call to this method
        /// </param>
        /// <returns>True if the file is valid, otherwise false</returns>
        // ReSharper disable once UnusedMember.Global
        public static bool ValidateFileVsHashcheck(
            string localFilePath, string hashCheckFilePath,
            out string errorMessage,
            HashUtilities.HashTypeConstants expectedHashType,
            int recheckIntervalDays)
        {
            const bool checkDate = true;
            const bool computeHash = true;
            const bool checkSize = true;

            var expectedHashInfo = new HashUtilities.HashInfoType
            {
                HashType = expectedHashType
            };
            return ValidateFileVsHashcheck(localFilePath, hashCheckFilePath, out errorMessage, expectedHashInfo, checkDate, computeHash, checkSize, recheckIntervalDays);
        }

        /// <summary>
        /// Validate that the hash value of a local file matches the expected hash info, creating the .hashcheck file if missing
        /// </summary>
        /// <remarks>
        /// Will create the .hashcheck file if missing
        /// Will also update the .lastused file for the local file
        /// </remarks>
        /// <param name="localFilePath">Local file path</param>
        /// <param name="hashCheckFilePath">Hashcheck file for the given data file (auto-defined if blank)</param>
        /// <param name="errorMessage">Output: error message</param>
        /// <param name="expectedHashInfo">Expected hash info (e.g. based on a remote file)</param>
        /// <param name="checkDate">If True, compares UTC modification time; times must agree within 2 seconds</param>
        /// <param name="computeHash">If true, compute the file hash every recheckIntervalDays (or every time if recheckIntervalDays is 0)</param>
        /// <param name="checkSize">If true, compare the actual file size to that in the hashcheck file</param>
        /// <param name="recheckIntervalDays">
        /// If the .hashcheck file is more than this number of days old, re-compute the hash value of the local file and compare to the hashcheck file
        /// Set to 0 to check the hash on every call to this method
        /// </param>
        /// <returns>True if the file is valid, otherwise false</returns>
        public static bool ValidateFileVsHashcheck(
            string localFilePath, string hashCheckFilePath,
            out string errorMessage,
            HashUtilities.HashInfoType expectedHashInfo,
            bool checkDate = true, bool computeHash = true, bool checkSize = true,
            int recheckIntervalDays = 0)
        {
            try
            {
                var localFile = new FileInfo(localFilePath);
                if (!localFile.Exists)
                {
                    errorMessage = "File not found: " + localFilePath;
                    ConsoleMsgUtils.ShowWarning(errorMessage);
                    return false;
                }

                FileInfo localHashcheckFile;
                if (string.IsNullOrWhiteSpace(hashCheckFilePath))
                    localHashcheckFile = new FileInfo(localFile.FullName + HashUtilities.HASHCHECK_FILE_SUFFIX);
                else
                    localHashcheckFile = new FileInfo(hashCheckFilePath);

                if (!localHashcheckFile.Exists)
                {
                    // Local .hashcheck file not found; create it
                    if (expectedHashInfo.HashType == HashUtilities.HashTypeConstants.Undefined)
                        expectedHashInfo.HashType = HashUtilities.HashTypeConstants.SHA1;

                    HashUtilities.CreateHashcheckFile(localFile.FullName, expectedHashInfo.HashType, out var localFileHash, out var warningMessage);

                    if (string.IsNullOrWhiteSpace(localFileHash))
                    {
                        if (string.IsNullOrWhiteSpace(warningMessage))
                            errorMessage = "Unable to compute the hash value for local file " + localFile.FullName;
                        else
                            errorMessage = warningMessage;

                        ConsoleMsgUtils.ShowWarning(errorMessage);
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(warningMessage))
                        ConsoleMsgUtils.ShowWarning(warningMessage);

                    // Compare the hash to expectedHashInfo.HashValue (if .HashValue is not "")
                    if (!string.IsNullOrWhiteSpace(expectedHashInfo.HashValue) && !localFileHash.Equals(expectedHashInfo.HashValue))
                    {
                        errorMessage = string.Format("Mismatch between the expected hash value and the actual hash value for {0}: {1} vs. {2}",
                                                    localFile.Name, expectedHashInfo.HashValue, localFileHash);
                        ConsoleMsgUtils.ShowWarning(errorMessage);
                        return false;
                    }

                    // Create/update the .lastused file
                    UpdateLastUsedFile(localFile);

                    errorMessage = string.Empty;
                    return true;
                }

                // Local .hashcheck file exists
                var localHashInfo = HashUtilities.ReadHashcheckFile(localHashcheckFile.FullName);

                if (expectedHashInfo.HashType != HashUtilities.HashTypeConstants.Undefined &&
                    !string.IsNullOrWhiteSpace(expectedHashInfo.HashValue) &&
                    !localHashInfo.HashValue.Equals(expectedHashInfo.HashValue))
                {
                    errorMessage = string.Format("Hash mismatch for {0}: expected {1} but actually {2}",
                                                 localFile.Name, expectedHashInfo.HashValue, localHashInfo.HashValue);
                    ConsoleMsgUtils.ShowWarning(errorMessage);
                    return false;
                }

                if (checkSize && localFile.Length != localHashInfo.FileSize)
                {
                    errorMessage = string.Format("File size mismatch for {0}: expected {1:#,##0} bytes but actually {2:#,##0} bytes",
                                                 localFile.Name, localHashInfo.FileSize, localFile.Length);
                    ConsoleMsgUtils.ShowWarning(errorMessage);
                    return false;
                }

                // Only compare dates if we are not comparing hash values
                if (!computeHash && checkDate)
                {
                    if (Math.Abs(localFile.LastWriteTimeUtc.Subtract(localHashInfo.FileDateUtc).TotalSeconds) > 2)
                    {
                        errorMessage = string.Format("File date mismatch for {0}: expected {1} UTC but actually {2} UTC",
                                                     localFile.Name,
                                                     localHashInfo.FileDateUtc.ToString(HashUtilities.DATE_TIME_FORMAT),
                                                     localFile.LastWriteTimeUtc.ToString(HashUtilities.DATE_TIME_FORMAT));
                        ConsoleMsgUtils.ShowWarning(errorMessage);
                        return false;
                    }
                }

                if (computeHash)
                {
                    var lastCheckDays = DateTime.UtcNow.Subtract(localHashcheckFile.LastWriteTimeUtc).TotalDays;

                    if (recheckIntervalDays <= 0 || lastCheckDays > recheckIntervalDays)
                    {
                        // Compute the hash of the file
                        if (localHashInfo.HashType == HashUtilities.HashTypeConstants.Undefined)
                        {
                            errorMessage = "Hash type is undefined; cannot compute the file hash to compare to the .hashcheck file";
                            ConsoleMsgUtils.ShowWarning(errorMessage);
                            return false;
                        }

                        var actualHash = HashUtilities.ComputeFileHash(localFilePath, localHashInfo.HashType);

                        if (!actualHash.Equals(localHashInfo.HashValue))
                        {
                            errorMessage = "Hash mismatch: expecting " + localHashInfo.HashValue + " but computed " + actualHash;
                            ConsoleMsgUtils.ShowWarning(errorMessage);
                            return false;
                        }
                    }
                }

                // Create/update the .lastused file
                UpdateLastUsedFile(localFile);

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Error validating " + localFilePath + " against the expected hash: " + ex.Message;
                ConsoleMsgUtils.ShowWarning(errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Looks for a .hashcheck file for the specified data file; returns false if not found
        /// If found, compares the stored values to the actual values (size, modification_date_utc, and hash)
        /// </summary>
        /// <remarks>The .hashcheck file has the same name as the data file, but with ".hashcheck" appended</remarks>
        /// <param name="localFilePath">Data file to check</param>
        /// <param name="hashCheckFilePath">Hashcheck file for the given data file (auto-defined if blank)</param>
        /// <param name="errorMessage">Output: error message</param>
        /// <param name="checkDate">If True, compares UTC modification time; times must agree within 2 seconds</param>
        /// <param name="computeHash">If true, compute the file hash (every time); if false, only compare file size and date</param>
        /// <param name="checkSize">If true, compare the actual file size to that in the hashcheck file</param>
        /// <param name="assumedHashType">Hash type to assume if the .hashcheck file does not have a hashtype entry</param>
        /// <returns>True if the hashcheck file exists and the actual file matches the expected values; false if a mismatch, if .hashcheck is missing, or if a problem</returns>
        // ReSharper disable once UnusedMember.Global
        public static bool ValidateFileVsHashcheck(
            string localFilePath, string hashCheckFilePath, out string errorMessage,
            bool checkDate = true, bool computeHash = true, bool checkSize = true,
            HashUtilities.HashTypeConstants assumedHashType = HashUtilities.HashTypeConstants.MD5)
        {
            errorMessage = string.Empty;

            try
            {
                var localFile = new FileInfo(localFilePath);
                if (!localFile.Exists)
                {
                    errorMessage = "File not found: " + localFile.FullName;
                    return false;
                }

                var localHashcheckFile = new FileInfo(hashCheckFilePath);
                if (!localHashcheckFile.Exists)
                {
                    errorMessage = "Data file at " + localFile.FullName + " does not have a corresponding .hashcheck file named " + localHashcheckFile.Name;
                    return false;
                }

                var expectedHashInfo = new HashUtilities.HashInfoType
                {
                    HashType = assumedHashType
                };

                var validFile = ValidateFileVsHashcheck(localFilePath, hashCheckFilePath, out errorMessage,
                                                        expectedHashInfo, checkDate, computeHash, checkSize, recheckIntervalDays: 0);
                return validFile;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowWarning("Error in ValidateLocalFile: " + ex.Message);
                return false;
            }
        }
    }
}
