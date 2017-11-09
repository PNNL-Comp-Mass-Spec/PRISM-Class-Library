using System;
using System.IO;
using System.Linq;

namespace PRISM.FileProcessor
{

    /// <summary>
    /// This class can be used as a base class for classes that process a folder or folders
    /// Note that this class contains simple error codes that can be set from any derived classes.
    /// The derived classes can also set their own local error codes
    /// </summary>
    public abstract class ProcessFoldersBase : ProcessFilesOrFoldersBase
    {

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        protected ProcessFoldersBase()
        {
            mFileDate = "November 8, 2017";
            ErrorCode = eProcessFoldersErrorCodes.NoError;
        }

        #region "Constants and Enums"

        /// <summary>
        /// Error code enums
        /// </summary>
        public enum eProcessFoldersErrorCodes
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Invalid input folder path
            /// </summary>
            InvalidInputFolderPath = 1,

            /// <summary>
            /// Invalid output folder path
            /// </summary>
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
        public eProcessFoldersErrorCodes ErrorCode { get; set; }

        #endregion

        /// <summary>
        /// Cleanup file/folder paths
        /// </summary>
        /// <param name="inputFileOrFolderPath"></param>
        /// <param name="outputFolderPath"></param>
        protected override void CleanupPaths(ref string inputFileOrFolderPath, ref string outputFolderPath)
        {
            CleanupFolderPaths(ref inputFileOrFolderPath, ref outputFolderPath);
        }

        /// <summary>
        /// Make sure inputFolderPath points to a valid directory and validate the output folder (defining it if null or empty)
        /// </summary>
        /// <param name="inputFolderPath"></param>
        /// <param name="outputFolderPath"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Create outputFolderPath if it does not exist</remarks>
        protected bool CleanupFolderPaths(ref string inputFolderPath, ref string outputFolderPath)
        {

            try
            {
                var inputFolder = new DirectoryInfo(inputFolderPath);

                if (!inputFolder.Exists)
                {
                    NotifyInvalidInputFolder();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(outputFolderPath))
                {
                    // Define outputFolderPath based on inputFolderPath
                    outputFolderPath = inputFolder.FullName;
                }

                // Make sure outputFolderPath points to a folder
                var outputFolder = new DirectoryInfo(outputFolderPath);

                if (!outputFolder.Exists)
                {
                    // outputFolderPath points to a non-existent folder; attempt to create it
                    outputFolder.Create();
                }

                mOutputFolderPath = outputFolder.FullName;

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error cleaning up the folder paths", ex);
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
                case eProcessFoldersErrorCodes.NoError:
                    errorMessage = string.Empty;
                    break;
                case eProcessFoldersErrorCodes.InvalidInputFolderPath:
                    errorMessage = "Invalid input folder path";
                    break;
                case eProcessFoldersErrorCodes.InvalidOutputFolderPath:
                    errorMessage = "Invalid output folder path";
                    break;
                case eProcessFoldersErrorCodes.ParameterFileNotFound:
                    errorMessage = "Parameter file not found";
                    break;
                case eProcessFoldersErrorCodes.InvalidParameterFile:
                    errorMessage = "Invalid parameter file";
                    break;
                case eProcessFoldersErrorCodes.FilePathError:
                    errorMessage = "General file path error";
                    break;
                case eProcessFoldersErrorCodes.LocalizedError:
                    errorMessage = "Localized error";
                    break;
                case eProcessFoldersErrorCodes.UnspecifiedError:
                    errorMessage = "Unspecified error";
                    break;
                default:
                    // This shouldn't happen
                    errorMessage = "Unknown error state";
                    break;
            }

            return errorMessage;

        }

        private DirectoryInfo GetInputFolderAndMatchSpec(string inputFolderPathSpec, out string folderNameMatchPattern)
        {
            // Copy the path into cleanPath and replace any * or ? characters with _
            var cleanPath = inputFolderPathSpec.Replace("*", "_").Replace("?", "_");

            var inputFolderSpec = new DirectoryInfo(cleanPath);
            string inputFolderToUse;

            if (inputFolderSpec.Parent != null && inputFolderSpec.Parent.Exists)
            {
                inputFolderToUse = inputFolderSpec.Parent.FullName;
            }
            else
            {
                // Use the current working directory
                inputFolderToUse = ".";
            }

            var inputFolder = new DirectoryInfo(inputFolderToUse);

            // Remove any directory information from inputFolderPathSpec
            folderNameMatchPattern = Path.GetFileName(inputFolderPathSpec);

            return inputFolder;
        }

        private void NotifyInvalidInputFolder()
        {
            ShowErrorMessage("Input folder cannot be empty");
            ErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
        }

        /// <summary>
        /// Process one or more folders (aka directories)
        /// </summary>
        /// <param name="inputFolderPath">Match spec for finding folders, can contain * and ?</param>
        /// <param name="outputFolderAlternatePath">Alternate output folder path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If True, reset ErrorCode</param>
        /// <returns> True if success, false if an error</returns>
        public bool ProcessFoldersWildcard(
            string inputFolderPath,
            string outputFolderAlternatePath = "",
            string parameterFilePath = "",
            bool resetErrorCode = true)
        {

            AbortProcessing = false;
            var success = true;

            try
            {
                // Possibly reset the error code
                if (resetErrorCode)
                    ErrorCode = eProcessFoldersErrorCodes.NoError;

                if (string.IsNullOrWhiteSpace(inputFolderPath))
                {
                    NotifyInvalidInputFolder();
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(outputFolderAlternatePath))
                {
                    // Update the cached output folder path
                    mOutputFolderPath = string.Copy(outputFolderAlternatePath);
                }

                // See if inputFolderPath contains a wildcard (* or ?)
                if (!inputFolderPath.Contains("*") && !inputFolderPath.Contains("?"))
                {
                    success = ProcessFolder(inputFolderPath, outputFolderAlternatePath, parameterFilePath, resetErrorCode);
                    return success;
                }

                var inputFolder = GetInputFolderAndMatchSpec(inputFolderPath, out var folderNameMatchPattern);

                var matchCount = 0;
                var foldersToProcess = inputFolder.GetDirectories(folderNameMatchPattern).ToList();
                var lastProgress = DateTime.UtcNow;

                foreach (var folder in foldersToProcess)
                {
                    matchCount += 1;

                    var percentComplete = matchCount / (float)foldersToProcess.Count * 100;
                    OnProgressUpdate("Process " + folder.FullName, percentComplete);

                    success = ProcessFolder(folder.FullName, outputFolderAlternatePath, parameterFilePath, true);

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

                if (ErrorCode != eProcessFoldersErrorCodes.NoError)
                    return success;

                ShowErrorMessage("No match was found for the input folder path: " + inputFolderPath);

                return success;

            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFoldersWildcard", ex);
                return false;
            }

        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputFolderPath">Input folder path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFolder(string inputFolderPath)
        {
            return ProcessFolder(inputFolderPath, string.Empty, string.Empty, true);
        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputFolderPath">Input folder path</param>
        /// <param name="outputFolderAlternatePath">Alternate output folder path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFolder(string inputFolderPath, string outputFolderAlternatePath, string parameterFilePath)
        {
            return ProcessFolder(inputFolderPath, outputFolderAlternatePath, parameterFilePath, true);
        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputFolderPath">Input folder path</param>
        /// <param name="outputFolderAlternatePath">Alternate output folder path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If true, reset the error code</param>
        /// <returns>True if success, otherwise false</returns>
        public abstract bool ProcessFolder(string inputFolderPath, string outputFolderAlternatePath,
                                           string parameterFilePath, bool resetErrorCode);

        /// <summary>
        /// Process directories and subdirectories
        /// </summary>
        /// <param name="inputFolderPath"></param>
        /// <param name="recurseFoldersMaxLevels"></param>
        /// <returns></returns>
        public bool ProcessAndRecurseFolders(string inputFolderPath, int recurseFoldersMaxLevels)
        {
            return ProcessAndRecurseFolders(inputFolderPath, string.Empty, string.Empty, recurseFoldersMaxLevels);
        }

        /// <summary>
        /// Process directories and subdirectories
        /// </summary>
        /// <param name="inputFolderPath">Input folder path (supports wildcards)</param>
        /// <param name="outputFolderAlternatePath"></param>
        /// <param name="parameterFilePath"></param>
        /// <param name="recurseFoldersMaxLevels">If 0 or negative, recurse infinitely</param>
        /// <returns></returns>
        /// <remarks>Calls ProcessFolders for all matching folders in inputFolderPath</remarks>
        public bool ProcessAndRecurseFolders(
            string inputFolderPath,
            string outputFolderAlternatePath = "",
            string parameterFilePath = "",
            int recurseFoldersMaxLevels = 0)
        {

            AbortProcessing = false;
            bool success;

            // Examine inputFolderPath to see if it contains a * or ?
            try
            {
                if (string.IsNullOrWhiteSpace(inputFolderPath))
                {
                    NotifyInvalidInputFolder();
                    return false;
                }

                DirectoryInfo inputFolder;
                string folderNameMatchPattern;

                if (inputFolderPath.Contains("*") || inputFolderPath.Contains("?"))
                {
                    inputFolder = GetInputFolderAndMatchSpec(inputFolderPath, out folderNameMatchPattern);
                }
                else
                {

                    if (Directory.Exists(inputFolderPath))
                    {
                        inputFolder = new DirectoryInfo(inputFolderPath);
                    }
                    else
                    {
                        // Use the current working directory
                        inputFolder = new DirectoryInfo(".");
                    }
                    folderNameMatchPattern = "*";
                }

                // Validate the output folder path
                if (!string.IsNullOrWhiteSpace(outputFolderAlternatePath))
                {
                    try
                    {
                        var alternateOutputFolder = new DirectoryInfo(outputFolderAlternatePath);
                        if (!alternateOutputFolder.Exists)
                            alternateOutputFolder.Create();
                    }
                    catch (Exception ex)
                    {
                        HandleException("Error in ProcessAndRecurseFolders", ex);
                        ErrorCode = eProcessFoldersErrorCodes.InvalidOutputFolderPath;
                        return false;
                    }
                }

                // Initialize some parameters
                AbortProcessing = false;
                var folderProcessCount = 0;
                var folderProcessFailCount = 0;

                // Call RecurseFoldersWork
                success = RecurseFoldersWork(inputFolder.FullName, folderNameMatchPattern, parameterFilePath,
                                             outputFolderAlternatePath, ref folderProcessCount,
                                             ref folderProcessFailCount, 1, recurseFoldersMaxLevels);
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessAndRecurseFolders", ex);
                return false;
            }

            return success;

        }

        private bool RecurseFoldersWork(string inputFolderPath, string folderNameMatchPattern,
                                        string parameterFilePath, string outputFolderAlternatePath,
                                        ref int folderProcessCount, ref int folderProcessFailCount,
                                        int recursionLevel, int recurseFoldersMaxLevels)
        {
            // If recurseFoldersMaxLevels is <=0, we recurse infinitely

            DirectoryInfo inputFolder;

            string outputFolderPathToUse;
            bool success;

            try
            {
                inputFolder = new DirectoryInfo(inputFolderPath);
            }
            catch (Exception ex)
            {
                // Input folder path error
                HandleException("Error in RecurseFoldersWork", ex);
                ErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(outputFolderAlternatePath))
                {
                    outputFolderAlternatePath = Path.Combine(outputFolderAlternatePath, inputFolder.Name);
                    outputFolderPathToUse = string.Copy(outputFolderAlternatePath);
                }
                else
                {
                    outputFolderPathToUse = string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Output file path error
                HandleException("Error in RecurseFoldersWork", ex);
                ErrorCode = eProcessFoldersErrorCodes.InvalidOutputFolderPath;
                return false;
            }

            try
            {
                OnDebugEvent("Examining " + inputFolderPath);

                if (recursionLevel == 1 & folderNameMatchPattern == "*")
                {
                    // Need to process the current folder
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

                // Process any matching folder in this folder

                var matchCount = 0;
                var lastProgress = DateTime.UtcNow;

                var foldersToProcess = inputFolder.GetDirectories(folderNameMatchPattern).ToList();

                success = true;
                foreach (var folder in foldersToProcess)
                {
                    matchCount++;

                    // This is the % complete in this directory only; not overall
                    var percentComplete = matchCount / (float)foldersToProcess.Count * 100;
                    OnProgressUpdate("Process " + folder.FullName, percentComplete);

                    if (outputFolderPathToUse.Length > 0)
                    {
                        success = ProcessFolder(folder.FullName, Path.Combine(outputFolderPathToUse, folder.Name),
                                                   parameterFilePath, true);
                    }
                    else
                    {
                        success = ProcessFolder(folder.FullName, string.Empty, parameterFilePath, true);
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
                    OnStatusEvent(string.Format("{0:F1}% complete in {1}", percentComplete, clsFileTools.CompactPathString(inputFolderPath)));
                }

            }
            catch (Exception ex)
            {
                HandleException("Error in RecurseFoldersWork", ex);
                ErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
                return false;
            }

            if (AbortProcessing)
                return false;

            // If recurseFoldersMaxLevels is <=0, we recurse infinitely
            //  otherwise, compare recursionLevel to recurseFoldersMaxLevels
            if (recurseFoldersMaxLevels <= 0 || recursionLevel <= recurseFoldersMaxLevels)
            {
                // Call this function for each of the subfolders of ioInputFolderInfo
                foreach (var subFolder in inputFolder.GetDirectories())
                {
                    success = RecurseFoldersWork(subFolder.FullName, folderNameMatchPattern, parameterFilePath,
                                                 outputFolderAlternatePath, ref folderProcessCount,
                                                 ref folderProcessFailCount, recursionLevel + 1, recurseFoldersMaxLevels);
                    if (!success)
                        break;
                }
            }

            return success;

        }

        /// <summary>
        /// Update the base class error code
        /// </summary>
        /// <param name="eNewErrorCode"></param>
        protected void SetBaseClassErrorCode(eProcessFoldersErrorCodes eNewErrorCode)
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
        //            if (base.ErrorCode == ProcessFoldersBase.eProcessFoldersErrorCodes.LocalizedError)
        //            {
        //                base.SetBaseClassErrorCode(ProcessFoldersBase.eProcessFoldersErrorCodes.NoError);
        //            }
        //        }
        //        else
        //        {
        //            base.SetBaseClassErrorCode(ProcessFoldersBase.eProcessFoldersErrorCodes.LocalizedError);
        //        }
        //    }

        //}

    }

}
