using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PRISM.FileProcessor
{
    /// <summary>
    /// This class can be used as a base class for classes that process a file or files, and create
    /// new output files in an output directory.  Note that this class contains simple error codes that
    /// can be set from any derived classes.  The derived classes can also set their own local error codes
    /// </summary>
    public abstract class ProcessFilesBase : ProcessFilesOrFoldersBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        protected ProcessFilesBase()
        {
            mFileDate = "March 15, 2018";
            ErrorCode = eProcessFilesErrorCodes.NoError;
        }

        #region "Constants and Enums"

        /// <summary>
        /// Error code enums
        /// </summary>
        public enum eProcessFilesErrorCodes
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Invalid input file path
            /// </summary>
            InvalidInputFilePath = 1,

            /// <summary>
            /// Invalid output folder (output directory) path
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
        /// This option applies when processing files matched with a wildcard
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool IgnoreErrorsWhenUsingWildcardMatching { get; set; }

        /// <summary>
        /// Error code reflecting processing outcome
        /// </summary>
        public eProcessFilesErrorCodes ErrorCode { get; set; }

        #endregion

        /// <summary>
        /// Cleanup file/directory paths
        /// </summary>
        /// <param name="inputFileOrFolderPath"></param>
        /// <param name="outputFolderPath"></param>
        protected override void CleanupPaths(ref string inputFileOrFolderPath, ref string outputFolderPath)
        {
            CleanupFilePaths(ref inputFileOrFolderPath, ref outputFolderPath);
        }

        /// <summary>
        /// Make sure inputFilePath points to a valid file and validate the output directory (defining it if null or empty)
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputFolderPath"></param>
        /// <returns>True if success, false if an error</returns>
        protected bool CleanupFilePaths(ref string inputFilePath, ref string outputFolderPath)
        {

            try
            {
                var validFile = CleanupInputFilePath(ref inputFilePath);

                if (!validFile)
                {
                    return false;
                }

                var inputFile = new FileInfo(inputFilePath);

                if (string.IsNullOrWhiteSpace(outputFolderPath))
                {
                    if (string.IsNullOrWhiteSpace(inputFile.DirectoryName))
                        outputFolderPath = ".";
                    else
                        // Define outputFolderPath based on inputFilePath
                        outputFolderPath = inputFile.DirectoryName;
                }

                // Make sure outputFolderPath points to a directory
                var outputFolder = new DirectoryInfo(outputFolderPath);

                if (!outputFolder.Exists)
                {
                    // outputFolderPath points to a non-existent directory; attempt to create it
                    outputFolder.Create();
                }

                mOutputFolderPath = outputFolder.FullName;

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error cleaning up the file paths", ex);
                return false;
            }

        }

        /// <summary>
        /// Make sure inputFilePath points to a valid file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <returns>True if success, false if an error</returns>
        protected bool CleanupInputFilePath(ref string inputFilePath)
        {

            try
            {
                var inputFile = new FileInfo(inputFilePath);

                if (!inputFile.Exists)
                {
                    ShowErrorMessage("Input file not found: " + inputFilePath);

                    ErrorCode = eProcessFilesErrorCodes.InvalidInputFilePath;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error cleaning up the file paths", ex);
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
                case eProcessFilesErrorCodes.NoError:
                    errorMessage = string.Empty;
                    break;
                case eProcessFilesErrorCodes.InvalidInputFilePath:
                    errorMessage = "Invalid input file path";
                    break;
                case eProcessFilesErrorCodes.InvalidOutputFolderPath:
                    errorMessage = "Invalid output directory path";
                    break;
                case eProcessFilesErrorCodes.ParameterFileNotFound:
                    errorMessage = "Parameter file not found";
                    break;
                case eProcessFilesErrorCodes.InvalidParameterFile:
                    errorMessage = "Invalid parameter file";
                    break;
                case eProcessFilesErrorCodes.FilePathError:
                    errorMessage = "General file path error";
                    break;
                case eProcessFilesErrorCodes.LocalizedError:
                    errorMessage = "Localized error";
                    break;
                case eProcessFilesErrorCodes.UnspecifiedError:
                    errorMessage = "Unspecified error";
                    break;
                default:
                    // This shouldn't happen
                    errorMessage = "Unknown error state";
                    break;
            }

            return errorMessage;
        }

        /// <summary>
        /// Get the default file extensions to parse
        /// </summary>
        /// <returns></returns>
        public virtual IList<string> GetDefaultExtensionsToParse()
        {
            var extensionsToParse = new List<string> { ".*" };

            return extensionsToParse;
        }

        private DirectoryInfo GetInputFolderAndMatchSpec(string inputFilePathSpec, out string fileNameMatchPattern)
        {

            // Copy the path into cleanPath and replace any * or ? characters with _
            var cleanPath = inputFilePathSpec.Replace("*", "_").Replace("?", "_");

            var inputFileSpec = new FileInfo(cleanPath);
            string inputFolderToUse;

            if (inputFileSpec.Directory != null && inputFileSpec.Directory.Exists)
            {
                inputFolderToUse = inputFileSpec.Directory.FullName;
            }
            else
            {
                // Use the current working directory
                inputFolderToUse = ".";
            }

            var inputFolder = new DirectoryInfo(inputFolderToUse);

            // Remove any directory information from inputFilePathSpec
            fileNameMatchPattern = Path.GetFileName(inputFilePathSpec);

            return inputFolder;
        }

        private void NotifyInvalidInputFile()
        {
            ShowErrorMessage("Input file cannot be empty");

            ErrorCode = eProcessFilesErrorCodes.InvalidInputFilePath;
        }

        /// <summary>
        /// Process one or more files
        /// </summary>
        /// <param name="inputFilePath">Match spec for finding files, can contain * and ?</param>
        /// <param name="outputFolderPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If True, reset ErrorCode</param>
        /// <returns> True if success, false if an error</returns>
        public bool ProcessFilesWildcard(
            string inputFilePath,
            string outputFolderPath = "",
            string parameterFilePath = "",
            bool resetErrorCode = true)
        {

            AbortProcessing = false;
            var success = true;

            try
            {
                // Possibly reset the error code
                if (resetErrorCode)
                    ErrorCode = eProcessFilesErrorCodes.NoError;

                if (string.IsNullOrWhiteSpace(inputFilePath))
                {
                    NotifyInvalidInputFile();
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(outputFolderPath))
                {
                    // Update the cached output directory path
                    mOutputFolderPath = string.Copy(outputFolderPath);
                }

                // See if inputFilePath contains a wildcard (* or ?)
                if (!inputFilePath.Contains("*") && !inputFilePath.Contains("?"))
                {
                    success = ProcessFile(inputFilePath, outputFolderPath, parameterFilePath, resetErrorCode);
                    return success;
                }

                var inputFolder = GetInputFolderAndMatchSpec(inputFilePath, out var fileNameMatchPattern);

                var matchCount = 0;
                var filesToProcess = inputFolder.GetFiles(fileNameMatchPattern).ToList();
                var lastProgress = DateTime.UtcNow;

                foreach (var inputFile in filesToProcess)
                {
                    matchCount++;

                    var percentComplete = matchCount / (float)filesToProcess.Count * 100;
                    OnProgressUpdate("Process " + inputFile.FullName, percentComplete);

                    success = ProcessFile(inputFile.FullName, outputFolderPath, parameterFilePath, resetErrorCode);

                    if (AbortProcessing)
                    {
                        break;
                    }

                    if (!success && !IgnoreErrorsWhenUsingWildcardMatching)
                    {
                        break;
                    }

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

                if (ErrorCode != eProcessFilesErrorCodes.NoError)
                    return success;

                ShowErrorMessage("No match was found for the input file path: " + inputFilePath);

                return success;

            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFilesWildcard", ex);
                return false;
            }

        }

        /// <summary>
        /// Process a single file
        /// </summary>
        /// <param name="inputFilePath">Input file path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFile(string inputFilePath)
        {
            return ProcessFile(inputFilePath, string.Empty, string.Empty);
        }

        /// <summary>
        /// Process a single file
        /// </summary>
        /// <param name="inputFilePath">Input file path</param>
        /// <param name="outputFolderPath">Output directory path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFile(string inputFilePath, string outputFolderPath)
        {
            return ProcessFile(inputFilePath, outputFolderPath, string.Empty);
        }

        /// <summary>
        /// Process a single file
        /// </summary>
        /// <param name="inputFilePath">Input file path</param>
        /// <param name="outputFolderPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFile(string inputFilePath, string outputFolderPath, string parameterFilePath)
        {
            return ProcessFile(inputFilePath, outputFolderPath, parameterFilePath, true);
        }

        /// <summary>
        /// Process a single file
        /// </summary>
        /// <param name="inputFilePath">Input file path</param>
        /// <param name="outputFolderPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If true, reset the error code</param>
        /// <returns>True if success, otherwise false</returns>
        public abstract bool ProcessFile(string inputFilePath, string outputFolderPath, string parameterFilePath, bool resetErrorCode);

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrFolder">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputFolderName">Output directory name</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, false if an error</returns>
        /// <returns></returns>
        public bool ProcessFilesAndRecurseFolders(string inputFilePathOrFolder, string outputFolderName = "", string parameterFilePath = "")
        {
            return ProcessFilesAndRecurseFolders(inputFilePathOrFolder, outputFolderName, string.Empty, false, parameterFilePath);
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrFolder">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputFolderName">Output directory name</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="extensionsToParse">List of file extensions to parse</param>
        /// <returns>True if success, false if an error</returns>
        /// <returns></returns>
        public bool ProcessFilesAndRecurseFolders(
            string inputFilePathOrFolder, string outputFolderName,
            string parameterFilePath, IList<string> extensionsToParse)
        {
            return ProcessFilesAndRecurseFolders(inputFilePathOrFolder, outputFolderName, string.Empty, false, parameterFilePath, 0, extensionsToParse);
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrFolder">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputFolderName">Output directory name</param>
        /// <param name="outputFolderAlternatePath">Output directory alternate path</param>
        /// <param name="recreateFolderHierarchyInAlternatePath">Recreate directory hierarchy in alternate path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="recurseFoldersMaxLevels">Levels to recurse, 0 or negative to process all subdirectories</param>
        /// <returns>True if success, false if an error</returns>
        public bool ProcessFilesAndRecurseFolders(
            string inputFilePathOrFolder, string outputFolderName, string outputFolderAlternatePath,
            bool recreateFolderHierarchyInAlternatePath, string parameterFilePath = "", int recurseFoldersMaxLevels = 0)
        {
            return ProcessFilesAndRecurseFolders(
                inputFilePathOrFolder, outputFolderName, outputFolderAlternatePath, recreateFolderHierarchyInAlternatePath,
                parameterFilePath, recurseFoldersMaxLevels, GetDefaultExtensionsToParse());
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrFolder">
        /// Input directory or directory (supports wildcards)
        /// If a folder path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputFolderName">Output directory name</param>
        /// <param name="outputFolderAlternatePath">Output directory alternate path</param>
        /// <param name="recreateFolderHierarchyInAlternatePath">Recreate directory hierarchy in alternate path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="recurseFoldersMaxLevels">Levels to recurse, 0 or negative to process all subdirectories</param>
        /// <param name="extensionsToParse">List of file extensions to parse</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// The extensions should be of the form ".TXT" or ".RAW" (i.e. a period then the extension)
        /// If any of the extensions is "*" or ".*", all files will be processed
        /// </remarks>
        public bool ProcessFilesAndRecurseFolders(
            string inputFilePathOrFolder,
            string outputFolderName,
            string outputFolderAlternatePath,
            bool recreateFolderHierarchyInAlternatePath,
            string parameterFilePath,
            int recurseFoldersMaxLevels,
            IList<string> extensionsToParse)
        {

            // Examine inputFilePathOrFolder to see if it contains a filename; if not, assume it points to a directory
            // First, see if it contains a * or ?
            try
            {
                DirectoryInfo inputDirectory;
                string fileNameMatchPattern;

                if (extensionsToParse?.Count == 0)
                    extensionsToParse = GetDefaultExtensionsToParse();

                if (string.IsNullOrWhiteSpace(inputFilePathOrFolder))
                {
                    // If an empty string, we process all files with known extensions in the current directory
                    inputDirectory = new DirectoryInfo(".");
                    fileNameMatchPattern = string.Empty;
                }
                else if (inputFilePathOrFolder.Contains("*") || inputFilePathOrFolder.Contains("?"))
                {
                    inputDirectory = GetInputFolderAndMatchSpec(inputFilePathOrFolder, out fileNameMatchPattern);
                }
                else
                {

                    var candidateInputFolder = new DirectoryInfo(inputFilePathOrFolder);
                    if (candidateInputFolder.Exists)
                    {
                        inputDirectory = candidateInputFolder;
                        fileNameMatchPattern = "*";
                    }
                    else
                    {
                        if (candidateInputFolder.Parent != null && candidateInputFolder.Parent.Exists)
                        {
                            inputDirectory = candidateInputFolder.Parent;
                        }
                        else
                        {
                            // Unable to determine the input directory path
                            // Use the current working directory
                            inputDirectory = new DirectoryInfo(".");
                        }

                        var fileName = Path.GetFileName(inputFilePathOrFolder);

                        if (string.IsNullOrWhiteSpace(fileName))
                            fileNameMatchPattern = "*";
                        else
                            fileNameMatchPattern = fileName;
                    }
                }

                // Validate the output directory path
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
                        ErrorCode = eProcessFilesErrorCodes.InvalidOutputFolderPath;
                        ShowErrorMessage("Error validating the alternate output directory path in ProcessFilesAndRecurseFolders: " + ex.Message);
                        return false;
                    }
                }

                // Initialize some parameters
                AbortProcessing = false;
                var fileProcessCount = 0;
                var fileProcessFailCount = 0;

                // Call RecurseFoldersWork
                const int recursionLevel = 1;
                var success = RecurseFoldersWork(inputDirectory.FullName, fileNameMatchPattern, outputFolderName,
                                             parameterFilePath, outputFolderAlternatePath,
                                             recreateFolderHierarchyInAlternatePath, extensionsToParse,
                                             ref fileProcessCount, ref fileProcessFailCount,
                                             recursionLevel, recurseFoldersMaxLevels);

                return success;
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFilesAndRecurseFolders", ex);
                return false;
            }

        }

        private bool RecurseFoldersWork(
            string inputFolderPath,
            string fileNameMatch,
            string outputFolderName,
            string parameterFilePath,
            string outputFolderAlternatePath,
            bool recreateFolderHierarchyInAlternatePath,
            IList<string> extensionsToParse,
            ref int fileProcessCount,
            ref int fileProcessFailCount,
            int recursionLevel,
        int recurseFoldersMaxLevels)
        {
            // If recurseFoldersMaxLevels is <=0, process all subdirectories

            DirectoryInfo inputFolder;

            var processAllExtensions = false;

            string outputFolderPathToUse;
            bool success;

            try
            {
                inputFolder = new DirectoryInfo(inputFolderPath);
            }
            catch (Exception ex)
            {
                // Input directory path error
                HandleException("Error in RecurseFoldersWork examining inputFolderPath", ex);
                ErrorCode = eProcessFilesErrorCodes.InvalidInputFilePath;
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(outputFolderAlternatePath))
                {
                    if (recreateFolderHierarchyInAlternatePath)
                    {
                        outputFolderAlternatePath = Path.Combine(outputFolderAlternatePath, inputFolder.Name);
                    }
                    outputFolderPathToUse = Path.Combine(outputFolderAlternatePath, outputFolderName);
                }
                else
                {
                    outputFolderPathToUse = outputFolderName;
                }
            }
            catch (Exception ex)
            {
                // Output file path error
                HandleException("Error in RecurseFoldersWork", ex);
                ErrorCode = eProcessFilesErrorCodes.InvalidOutputFolderPath;
                return false;
            }

            try
            {
                // Validate extensionsToParse()
                for (var extensionIndex = 0; extensionIndex <= extensionsToParse.Count - 1; extensionIndex++)
                {
                    if (extensionsToParse[extensionIndex] == null)
                    {
                        extensionsToParse[extensionIndex] = string.Empty;
                    }
                    else
                    {
                        if (!extensionsToParse[extensionIndex].StartsWith("."))
                        {
                            extensionsToParse[extensionIndex] = "." + extensionsToParse[extensionIndex];
                        }

                        if (extensionsToParse[extensionIndex] == ".*")
                        {
                            processAllExtensions = true;
                            break;
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in RecurseFoldersWork", ex);
                ErrorCode = eProcessFilesErrorCodes.UnspecifiedError;
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(outputFolderPathToUse))
                {
                    // Update the cached output directory path
                    mOutputFolderPath = string.Copy(outputFolderPathToUse);
                }

                OnDebugEvent("Examining " + inputFolderPath);

                // Process any matching files in this directory
                success = true;

                var filesToProcess = new List<FileInfo>();

                foreach (var inputFile in inputFolder.GetFiles(fileNameMatch))
                {
                    for (var extensionIndex = 0; extensionIndex <= extensionsToParse.Count - 1; extensionIndex++)
                    {
                        if (processAllExtensions ||
                            string.Equals(inputFile.Extension, extensionsToParse[extensionIndex], StringComparison.OrdinalIgnoreCase))
                        {
                            filesToProcess.Add(inputFile);
                        }
                    }
                }

                var matchCount = 0;
                var lastProgress = DateTime.UtcNow;

                foreach (var inputFile in filesToProcess)
                {
                    matchCount++;

                    // This is the % complete in this directory only; not overall
                    var percentComplete = matchCount / (float)filesToProcess.Count * 100;
                    OnProgressUpdate("Process " + inputFile.FullName, percentComplete);

                    success = ProcessFile(inputFile.FullName, outputFolderPathToUse, parameterFilePath, true);

                    if (!success)
                    {
                        fileProcessFailCount++;
                        success = true;
                    }
                    else
                    {
                        fileProcessCount++;
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
                ErrorCode = eProcessFilesErrorCodes.InvalidInputFilePath;
                return false;
            }

            if (!AbortProcessing)
            {
                // If recurseFoldersMaxLevels is <=0 then we recurse infinitely
                //  otherwise, compare recursionLevel to recurseFoldersMaxLevels
                if (recurseFoldersMaxLevels <= 0 || recursionLevel <= recurseFoldersMaxLevels)
                {
                    // Call this function for each of the subfolders of inputFolder
                    foreach (var subFolder in inputFolder.GetDirectories())
                    {
                        success = RecurseFoldersWork(subFolder.FullName, fileNameMatch, outputFolderName,
                                                        parameterFilePath, outputFolderAlternatePath,
                                                        recreateFolderHierarchyInAlternatePath, extensionsToParse,
                                                        ref fileProcessCount, ref fileProcessFailCount,
                                                        recursionLevel + 1, recurseFoldersMaxLevels);

                        if (!success)
                            break;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// Update the base class error code
        /// </summary>
        /// <param name="eNewErrorCode"></param>
        protected void SetBaseClassErrorCode(eProcessFilesErrorCodes eNewErrorCode)
        {
            ErrorCode = eNewErrorCode;
        }

        // The following functions should be placed in any derived class
        // Cannot define as abstract since it contains a customized enumerated type (eDerivedClassErrorCodes) in the function declaration

        // private void SetLocalErrorCode(eDerivedClassErrorCodes eNewErrorCode)
        // {
        //     SetLocalErrorCode(eNewErrorCode, false);
        // }
        //
        // private void SetLocalErrorCode(eDerivedClassErrorCodes eNewErrorCode, bool leaveExistingErrorCodeUnchanged)
        // {
        //     if (leaveExistingErrorCodeUnchanged && mLocalErrorCode != eDerivedClassErrorCodes.NoError)
        //     {
        //         // An error code is already defined; do not change it
        //     }
        //     else
        //     {
        //         mLocalErrorCode = eNewErrorCode;
        //
        //         if (eNewErrorCode == eDerivedClassErrorCodes.NoError)
        //         {
        //             if (base.ErrorCode == ProcessFilesBase.eProcessFilesErrorCodes.LocalizedError)
        //             {
        //                 base.SetBaseClassErrorCode(ProcessFilesBase.eProcessFilesErrorCodes.NoError);
        //             }
        //         }
        //         else
        //         {
        //             base.SetBaseClassErrorCode(ProcessFilesBase.eProcessFilesErrorCodes.LocalizedError);
        //         }
        //     }
        // }
    }
}
