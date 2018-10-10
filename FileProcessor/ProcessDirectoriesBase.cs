
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace PRISM.FileProcessor
{

    /// <summary>
    /// This class can be used as a base class for classes that process a directory or directories
    /// Note that this class contains simple error codes that can be set from any derived classes.
    /// The derived classes can also set their own local error codes
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public abstract class ProcessDirectoriesBase : ProcessFilesOrDirectoriesBase
    {

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        protected ProcessDirectoriesBase()
        {
            mFileDate = "October 10, 2018";
            ErrorCode = ProcessDirectoriesErrorCodes.NoError;
        }

        #region "Constants and Enums"

        /// <summary>
        /// Error code enums
        /// </summary>
        public enum ProcessDirectoriesErrorCodes
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Invalid input directory path
            /// </summary>
            InvalidInputDirectoryPath = 1,

            [Obsolete("Use InvalidInputDirectoryPath")]
            InvalidInputFolderPath = 1,

            /// <summary>
            /// Invalid output directory path
            /// </summary>
            InvalidOutputDirectoryPath = 2,

            [Obsolete("Use InvalidOutputDirectoryPath")]
            InvalidOutputFolderPath = 2,

            /// <summary>
            /// Parameter file not found
            /// </summary>
            ParameterFileNotFound = 4,

            /// <summary>
            /// Invalid parameter file
            /// </summary>
            InvalidParameterFile = 8,

            /// <summary>
            /// File path error
            /// </summary>
            FilePathError = 16,

            /// <summary>
            /// Localized error
            /// </summary>
            LocalizedError = 32,

            /// <summary>
            /// Unspecified error
            /// </summary>
            UnspecifiedError = -1
        }

        // Copy the following to any derived classes
        //enum eDerivedClassErrorCodes
        //{
        //    NoError = 0,
        //    UnspecifiedError = -1
        //}

        #endregion

        #region "Classwide Variables"

        // Copy the following to any derived classes
        //
        //private eDerivedClassErrorCodes mLocalErrorCode;

        //public eDerivedClassErrorCodes LocalErrorCode
        //{
        //    get
        //    {
        //        return mLocalErrorCode;
        //    }
        //}

        #endregion

        #region "Interface Functions"

        /// <summary>
        /// Error code reflecting processing outcome
        /// </summary>
        public ProcessDirectoriesErrorCodes ErrorCode { get; set; }

        #endregion

        /// <summary>
        /// Cleanup file/directory paths
        /// </summary>
        /// <param name="inputFileOrDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        protected override void CleanupPaths(ref string inputFileOrDirectoryPath, ref string outputDirectoryPath)
        {
            CleanupDirectoryPaths(ref inputFileOrDirectoryPath, ref outputDirectoryPath);
        }

        /// <summary>
        /// Make sure inputDirectoryPath points to a valid directory and validate the output directory (defining it if null or empty)
        /// </summary>
        /// <param name="inputDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Create outputDirectoryPath if it does not exist</remarks>
        protected bool CleanupDirectoryPaths(ref string inputDirectoryPath, ref string outputDirectoryPath)
        {

            try
            {
                var inputDirectory = new DirectoryInfo(inputDirectoryPath);

                if (!inputDirectory.Exists)
                {
                    NotifyInvalidInputDirectory();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(outputDirectoryPath))
                {
                    // Define outputDirectoryPath based on inputDirectoryPath
                    outputDirectoryPath = inputDirectory.FullName;
                }

                // Make sure outputDirectoryPath points to a directory
                var outputDirectory = new DirectoryInfo(outputDirectoryPath);

                if (!outputDirectory.Exists)
                {
                    // outputDirectoryPath points to a non-existent directory; attempt to create it
                    outputDirectory.Create();
                }

                mOutputDirectoryPath = outputDirectory.FullName;

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error cleaning up the directory paths", ex);
                return false;
            }

        }

        /// <summary>
        /// Get the base class error message, or an empty string if no error
        /// </summary>
        /// <returns>Error message</returns>
        protected string GetBaseClassErrorMessage()
        {

            string errorMessage;

            switch (ErrorCode)
            {
                case ProcessDirectoriesErrorCodes.NoError:
                    errorMessage = string.Empty;
                    break;
                case ProcessDirectoriesErrorCodes.InvalidInputDirectoryPath:
                    errorMessage = "Invalid input directory path";
                    break;
                case ProcessDirectoriesErrorCodes.InvalidOutputDirectoryPath:
                    errorMessage = "Invalid output directory path";
                    break;
                case ProcessDirectoriesErrorCodes.ParameterFileNotFound:
                    errorMessage = "Parameter file not found";
                    break;
                case ProcessDirectoriesErrorCodes.InvalidParameterFile:
                    errorMessage = "Invalid parameter file";
                    break;
                case ProcessDirectoriesErrorCodes.FilePathError:
                    errorMessage = "General file path error";
                    break;
                case ProcessDirectoriesErrorCodes.LocalizedError:
                    errorMessage = "Localized error";
                    break;
                case ProcessDirectoriesErrorCodes.UnspecifiedError:
                    errorMessage = "Unspecified error";
                    break;
                default:
                    // This shouldn't happen
                    errorMessage = "Unknown error state";
                    break;
            }

            return errorMessage;

        }

        private DirectoryInfo GetInputDirectoryAndMatchSpec(string inputDirectoryPathSpec, out string directoryNameMatchPattern)
        {
            // Copy the path into cleanPath and replace any * or ? characters with _
            var cleanPath = inputDirectoryPathSpec.Replace("*", "_").Replace("?", "_");

            var inputDirectorySpec = new DirectoryInfo(cleanPath);
            string inputDirectoryToUse;

            if (inputDirectorySpec.Parent != null && inputDirectorySpec.Parent.Exists)
            {
                inputDirectoryToUse = inputDirectorySpec.Parent.FullName;
            }
            else
            {
                // Use the current working directory
                inputDirectoryToUse = ".";
            }

            var inputDirectory = new DirectoryInfo(inputDirectoryToUse);

            // Remove any directory information from inputDirectoryPathSpec
            directoryNameMatchPattern = Path.GetFileName(inputDirectoryPathSpec);

            return inputDirectory;
        }

        private void NotifyInvalidInputDirectory()
        {
            ShowErrorMessage("Input directory cannot be empty");
            ErrorCode = ProcessDirectoriesErrorCodes.InvalidInputDirectoryPath;
        }

        /// <summary>
        /// Process one or more directories
        /// </summary>
        /// <param name="inputDirectoryPath">Match spec for finding directories, can contain * and ?</param>
        /// <param name="outputDirectoryAlternatePath">Alternate output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If True, reset ErrorCode</param>
        /// <returns> True if success, false if an error</returns>
        public bool ProcessDirectoriesWildcard(
            string inputDirectoryPath,
            string outputDirectoryAlternatePath = "",
            string parameterFilePath = "",
            bool resetErrorCode = true)
        {

            AbortProcessing = false;
            var success = true;

            try
            {
                // Possibly reset the error code
                if (resetErrorCode)
                    ErrorCode = ProcessDirectoriesErrorCodes.NoError;

                if (string.IsNullOrWhiteSpace(inputDirectoryPath))
                {
                    NotifyInvalidInputDirectory();
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(outputDirectoryAlternatePath))
                {
                    // Update the cached output directory path
                    mOutputDirectoryPath = string.Copy(outputDirectoryAlternatePath);
                }

                // See if inputDirectoryPath contains a wildcard (* or ?)
                if (!inputDirectoryPath.Contains("*") && !inputDirectoryPath.Contains("?"))
                {
                    success = ProcessDirectory(inputDirectoryPath, outputDirectoryAlternatePath, parameterFilePath, resetErrorCode);
                    return success;
                }

                var inputDirectory = GetInputDirectoryAndMatchSpec(inputDirectoryPath, out var directoryNameMatchPattern);

                var matchCount = 0;
                var directoriesToProcess = inputDirectory.GetDirectories(directoryNameMatchPattern).ToList();
                var lastProgress = DateTime.UtcNow;

                foreach (var directory in directoriesToProcess)
                {
                    matchCount += 1;

                    var percentComplete = matchCount / (float)directoriesToProcess.Count * 100;
                    OnProgressUpdate("Process " + directory.FullName, percentComplete);

                    success = ProcessDirectory(directory.FullName, outputDirectoryAlternatePath, parameterFilePath, true);

                    if (!success || AbortProcessing)
                        break;

                    if (!(DateTime.UtcNow.Subtract(lastProgress).TotalSeconds >= 1))
                        continue;

                    lastProgress = DateTime.UtcNow;
                    OnStatusEvent(string.Format("{0:F1}% complete", percentComplete));
                }

                if (matchCount > 0)
                {
                    Console.WriteLine();
                    return success;
                }

                if (ErrorCode != ProcessDirectoriesErrorCodes.NoError)
                    return success;

                ShowErrorMessage("No match was found for the input directory path: " + inputDirectoryPath);

                return success;

            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessDirectoriesWildcard", ex);
                return false;
            }

        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputDirectoryPath">Input directory path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessDirectory(string inputDirectoryPath)
        {
            return ProcessDirectory(inputDirectoryPath, string.Empty, string.Empty, true);
        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputDirectoryPath">Input directory path</param>
        /// <param name="outputDirectoryAlternatePath">Alternate output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessDirectory(string inputDirectoryPath, string outputDirectoryAlternatePath, string parameterFilePath)
        {
            return ProcessDirectory(inputDirectoryPath, outputDirectoryAlternatePath, parameterFilePath, true);
        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputDirectoryPath">Input directory path</param>
        /// <param name="outputDirectoryAlternatePath">Alternate directory directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If true, reset the error code</param>
        /// <returns>True if success, otherwise false</returns>
        public abstract bool ProcessDirectory(string inputDirectoryPath, string outputDirectoryAlternatePath,
                                              string parameterFilePath, bool resetErrorCode);

        /// <summary>
        /// Process directories and subdirectories
        /// </summary>
        /// <param name="inputDirectoryPath"></param>
        /// <param name="maxLevelsToRecurse"></param>
        /// <returns></returns>
        public bool ProcessAndRecurseDirectories(string inputDirectoryPath, int maxLevelsToRecurse)
        {
            return ProcessAndRecurseDirectories(inputDirectoryPath, string.Empty, string.Empty, maxLevelsToRecurse);
        }

        /// <summary>
        /// Process directories and subdirectories
        /// </summary>
        /// <param name="inputDirectoryPath">Input directory path (supports wildcards)</param>
        /// <param name="outputDirectoryAlternatePath">Alternate directory directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="maxLevelsToRecurse">If 0 or negative, recurse infinitely</param>
        /// <returns></returns>
        /// <remarks>Calls ProcessDirectories for all matching directories in inputDirectoryPath</remarks>
        public bool ProcessAndRecurseDirectories(
            string inputDirectoryPath,
            string outputDirectoryAlternatePath = "",
            string parameterFilePath = "",
            int maxLevelsToRecurse = 0)
        {

            AbortProcessing = false;

            // Examine inputFolderPath to see if it contains a * or ?
            try
            {
                if (string.IsNullOrWhiteSpace(inputDirectoryPath))
                {
                    NotifyInvalidInputDirectory();
                    return false;
                }

                DirectoryInfo inputDirectory;
                string directoryNameMatchPattern;

                if (inputDirectoryPath.Contains("*") || inputDirectoryPath.Contains("?"))
                {
                    inputDirectory = GetInputDirectoryAndMatchSpec(inputDirectoryPath, out directoryNameMatchPattern);
                }
                else
                {

                    if (Directory.Exists(inputDirectoryPath))
                    {
                        inputDirectory = new DirectoryInfo(inputDirectoryPath);
                    }
                    else
                    {
                        // Use the current working directory
                        inputDirectory = new DirectoryInfo(".");
                    }
                    directoryNameMatchPattern = "*";
                }

                // Validate the output directory path
                if (!string.IsNullOrWhiteSpace(outputDirectoryAlternatePath))
                {
                    try
                    {
                        var alternateOutputDirectory = new DirectoryInfo(outputDirectoryAlternatePath);
                        if (!alternateOutputDirectory.Exists)
                            alternateOutputDirectory.Create();
                    }
                    catch (Exception ex)
                    {
                        HandleException("Error validating the alternate output directory path in ProcessAndRecurseDirectories", ex);
                        ErrorCode = ProcessDirectoriesErrorCodes.InvalidOutputDirectoryPath;
                        return false;
                    }
                }

                // Initialize some parameters
                AbortProcessing = false;
                var folderProcessCount = 0;
                var folderProcessFailCount = 0;

                // Call RecurseFoldersWork
                success = RecurseFoldersWork(inputFolder.FullName, directoryNameMatchPattern, parameterFilePath,
                                             outputFolderAlternatePath, ref folderProcessCount,
                                             ref folderProcessFailCount, 1, recurseFoldersMaxLevels);
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessAndRecurseDirectories", ex);
                return false;
            }


        }

        private bool RecurseFoldersWork(string inputFolderPath, string folderNameMatchPattern,
                                        string parameterFilePath, string outputFolderAlternatePath,
                                        ref int folderProcessCount, ref int folderProcessFailCount,
                                        int recursionLevel, int recurseFoldersMaxLevels)
        {
            // If maxLevelsToRecurse is <=0, we recurse infinitely

            DirectoryInfo inputDirectory;

            string outputDirectoryPathToUse;

            try
            {
                inputDirectory = new DirectoryInfo(inputDirectoryPath);
            }
            catch (Exception ex)
            {
                // Input directory path error
                HandleException("Error in RecurseDirectoriesWork", ex);
                ErrorCode = ProcessDirectoriesErrorCodes.InvalidInputDirectoryPath;
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(outputDirectoryAlternatePath))
                {
                    outputDirectoryAlternatePath = Path.Combine(outputDirectoryAlternatePath, inputDirectory.Name);
                    outputDirectoryPathToUse = string.Copy(outputDirectoryAlternatePath);
                }
                else
                {
                    outputDirectoryPathToUse = string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Output file path error
                HandleException("Error in RecurseDirectoriesWork", ex);
                ErrorCode = ProcessDirectoriesErrorCodes.InvalidOutputDirectoryPath;
                return false;
            }

            try
            {
                OnDebugEvent("Examining " + inputDirectoryPath);
                bool success;

                if (recursionLevel == 1 && directoryNameMatchPattern == "*")
                {
                    // Need to process the current directory
                    success = ProcessFolder(inputFolder.FullName, outputFolderPathToUse, parameterFilePath, true);
                    if (!success)
                    {
                        folderProcessFailCount += 1;
                    }
                    else
                    {
                        folderProcessCount += 1;
                    }
                }

                // Process any matching subdirectory in this directory

                var matchCount = 0;
                var lastProgress = DateTime.UtcNow;

                var directoriesToProcess = inputDirectory.GetDirectories(directoryNameMatchPattern).ToList();

                foreach (var directory in directoriesToProcess)
                {
                    matchCount++;

                    // This is the % complete in this directory only; not overall
                    var percentComplete = matchCount / (float)directoriesToProcess.Count * 100;
                    OnProgressUpdate("Process " + directory.FullName, percentComplete);

                    if (outputDirectoryPathToUse.Length > 0)
                    {
                        var alternateOutputDirectoryPath = Path.Combine(outputDirectoryPathToUse, directory.Name);
                        success = ProcessDirectory(directory.FullName, alternateOutputDirectoryPath, parameterFilePath, true);
                    }
                    else
                    {
                        success = ProcessDirectory(directory.FullName, string.Empty, parameterFilePath, true);
                    }

                    if (!success)
                    {
                        folderProcessFailCount += 1;
                        success = true;
                    }
                    else
                    {
                        folderProcessCount += 1;
                    }

                    if (AbortProcessing)
                        break;

                    if (!(DateTime.UtcNow.Subtract(lastProgress).TotalSeconds >= 1))
                        continue;

                    lastProgress = DateTime.UtcNow;
                    OnStatusEvent(string.Format("{0:F1}% complete in {1}", percentComplete, FileTools.CompactPathString(inputDirectoryPath)));
                }

            }
            catch (Exception ex)
            {
                HandleException("Error in RecurseDirectoriesWork", ex);
                ErrorCode = ProcessDirectoriesErrorCodes.InvalidInputDirectoryPath;
                return false;
            }

            if (AbortProcessing)
                return false;

            // If maxLevelsToRecurse is <=0, we recurse infinitely
            // otherwise, compare recursionLevel to maxLevelsToRecurse
            if (maxLevelsToRecurse > 0 && recursionLevel > maxLevelsToRecurse)
                return true;

            // Call this function for each of the subdirectories of inputDirectory
            foreach (var subdirectory in inputDirectory.GetDirectories())
            {
                // Call this function for each of the subdirectories of inputFolder
                foreach (var subdirectory in inputFolder.GetDirectories())
                {
                    success = RecurseFoldersWork(subdirectory.FullName, folderNameMatchPattern, parameterFilePath,
                                                 outputFolderAlternatePath, ref folderProcessCount,
                                                 ref folderProcessFailCount, recursionLevel + 1, recurseFoldersMaxLevels);
                    if (!success)
                        break;
                }
            }

            return true;

        }

        /// <summary>
        /// Update the base class error code
        /// </summary>
        /// <param name="eNewErrorCode"></param>
        protected void SetBaseClassErrorCode(ProcessDirectoriesErrorCodes eNewErrorCode)
        {
            ErrorCode = eNewErrorCode;
        }

        // The following functions should be placed in any derived class
        // Cannot define as MustOverride since it contains a customized enumerated type (eDerivedClassErrorCodes) in the function declaration
        //
        //private void SetLocalErrorCode(eDerivedClassErrorCodes eNewErrorCode)
        //{
        //    SetLocalErrorCode(eNewErrorCode, false);
        //}

        //private void SetLocalErrorCode(eDerivedClassErrorCodes eNewErrorCode, bool leaveExistingErrorCodeUnchanged)
        //{
        //    if (leaveExistingErrorCodeUnchanged && mLocalErrorCode != eDerivedClassErrorCodes.NoError)
        //    {
        //        // An error code is already defined; do not change it
        //    }
        //    else
        //    {
        //        mLocalErrorCode = eNewErrorCode;

        //        if (eNewErrorCode == eDerivedClassErrorCodes.NoError)
        //        {
        //            if (base.ErrorCode == ProcessDirectoriesBase.ProcessDirectoriesErrorCodes.LocalizedError)
        //            {
        //                base.SetBaseClassErrorCode(ProcessDirectoriesBase.ProcessDirectoriesErrorCodes.NoError);
        //            }
        //        }
        //        else
        //        {
        //            base.SetBaseClassErrorCode(ProcessDirectoriesBase.ProcessDirectoriesErrorCodes.LocalizedError);
        //        }
        //    }

        //}

    }

}
