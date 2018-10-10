using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace PRISM.FileProcessor
{
    /// <summary>
    /// This class can be used as a base class for classes that process a file or files, and create
    /// new output files in an output directory.  Note that this class contains simple error codes that
    /// can be set from any derived classes.  The derived classes can also set their own local error codes
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public abstract class ProcessFilesBase : ProcessFilesOrDirectoriesBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        protected ProcessFilesBase()
        {
            mFileDate = "October 10, 2018";
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

        /// <summary>
        /// Number of files processed successfully when using ProcessFilesAndRecurseDirectories or ProcessFilesWildcard
        /// </summary>
        public int FilesProcessed { get; private set; }

        /// <summary>
        /// Number of files that could not be processed when using ProcessFilesAndRecurseDirectories or ProcessFilesWildcard
        /// </summary>
        public int FileProcessErrors { get; private set; }

        #endregion

        /// <summary>
        /// Cleanup file/directory paths
        /// </summary>
        /// <param name="inputFileOrDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        protected override void CleanupPaths(ref string inputFileOrDirectoryPath, ref string outputDirectoryPath)
        {
            CleanupFilePaths(ref inputFileOrDirectoryPath, ref outputDirectoryPath);
        }

        /// <summary>
        /// Make sure inputFilePath points to a valid file and validate the output directory (defining it if null or empty)
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if success, false if an error</returns>
        protected bool CleanupFilePaths(ref string inputFilePath, ref string outputDirectoryPath)
        {

            try
            {
                var validFile = CleanupInputFilePath(ref inputFilePath);

                if (!validFile)
                {
                    return false;
                }

                var inputFile = new FileInfo(inputFilePath);

                if (string.IsNullOrWhiteSpace(outputDirectoryPath))
                {
                    if (string.IsNullOrWhiteSpace(inputFile.DirectoryName))
                        outputDirectoryPath = ".";
                    else
                        // Define outputDirectoryPath based on inputFilePath
                        outputDirectoryPath = inputFile.DirectoryName;
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

        private DirectoryInfo GetInputDirectoryAndMatchSpec(string inputFilePathSpec, out string fileNameMatchPattern)
        {

            // Copy the path into cleanPath and replace any * or ? characters with _
            var cleanPath = inputFilePathSpec.Replace("*", "_").Replace("?", "_");

            var inputFileSpec = new FileInfo(cleanPath);
            string inputDirectoryToUse;

            if (inputFileSpec.Directory != null && inputFileSpec.Directory.Exists)
            {
                inputDirectoryToUse = inputFileSpec.Directory.FullName;
            }
            else
            {
                // Use the current working directory
                inputDirectoryToUse = ".";
            }

            var inputDirectory = new DirectoryInfo(inputDirectoryToUse);

            // Remove any directory information from inputFilePathSpec
            fileNameMatchPattern = Path.GetFileName(inputFilePathSpec);

            return inputDirectory;
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
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If True, reset ErrorCode</param>
        /// <returns> True if success, false if an error</returns>
        public bool ProcessFilesWildcard(
            string inputFilePath,
            string outputDirectoryPath = "",
            string parameterFilePath = "",
            bool resetErrorCode = true)
        {

            AbortProcessing = false;
            FilesProcessed = 0;
            FileProcessErrors = 0;

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

                if (!string.IsNullOrWhiteSpace(outputDirectoryPath))
                {
                    // Update the cached output directory path
                    mOutputDirectoryPath = string.Copy(outputDirectoryPath);
                }

                // See if inputFilePath contains a wildcard (* or ?)
                if (!inputFilePath.Contains("*") && !inputFilePath.Contains("?"))
                {
                    success = ProcessFile(inputFilePath, outputDirectoryPath, parameterFilePath, resetErrorCode);
                    return success;
                }

                var inputDirectory = GetInputDirectoryAndMatchSpec(inputFilePath, out var fileNameMatchPattern);

                var matchCount = 0;
                var filesToProcess = inputDirectory.GetFiles(fileNameMatchPattern).ToList();
                var lastProgress = DateTime.UtcNow;

                foreach (var inputFile in filesToProcess)
                {
                    matchCount++;

                    var percentComplete = matchCount / (float)filesToProcess.Count * 100;
                    OnProgressUpdate("Process " + inputFile.FullName, percentComplete);

                    success = ProcessFile(inputFile.FullName, outputDirectoryPath, parameterFilePath, resetErrorCode);

                    if (AbortProcessing)
                    {
                        break;
                    }

                    if (success)
                    {
                        FilesProcessed++;
                    }
                    else
                    {
                        FileProcessErrors++;
                        if (!IgnoreErrorsWhenUsingWildcardMatching)
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
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFile(string inputFilePath, string outputDirectoryPath)
        {
            return ProcessFile(inputFilePath, outputDirectoryPath, string.Empty);
        }

        /// <summary>
        /// Process a single file
        /// </summary>
        /// <param name="inputFilePath">Input file path</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath)
        {
            return ProcessFile(inputFilePath, outputDirectoryPath, parameterFilePath, true);
        }

        /// <summary>
        /// Process a single file
        /// </summary>
        /// <param name="inputFilePath">Input file path</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If true, reset the error code</param>
        /// <returns>True if success, otherwise false</returns>
        public abstract bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath, bool resetErrorCode);

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrDirectory">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryName">Output directory name</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, false if an error</returns>
        /// <returns></returns>
        public bool ProcessFilesAndRecurseDirectories(string inputFilePathOrDirectory, string outputDirectoryName = "", string parameterFilePath = "")
        {
            return ProcessFilesAndRecurseDirectories(inputFilePathOrDirectory, outputDirectoryName, string.Empty, false, parameterFilePath);
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrDirectory">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryName">Output directory name</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="extensionsToParse">List of file extensions to parse</param>
        /// <returns>True if success, false if an error</returns>
        /// <returns></returns>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory, string outputDirectoryName,
            string parameterFilePath, IList<string> extensionsToParse)
        {
            return ProcessFilesAndRecurseDirectories(inputFilePathOrDirectory, outputDirectoryName, string.Empty, false, parameterFilePath, 0, extensionsToParse);
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrDirectory">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryName">Output directory name</param>
        /// <param name="outputDirectoryAlternatePath">Output directory alternate path</param>
        /// <param name="recreateDirectoryHierarchyInAlternatePath">Recreate directory hierarchy in alternate path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="maxLevelsToRecurse">Levels to recurse, 0 or negative to process all subdirectories</param>
        /// <returns>True if success, false if an error</returns>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory, string outputDirectoryName, string outputDirectoryAlternatePath,
            bool recreateDirectoryHierarchyInAlternatePath, string parameterFilePath = "", int maxLevelsToRecurse = 0)
        {
            return ProcessFilesAndRecurseDirectories(
                inputFilePathOrDirectory, outputDirectoryName, outputDirectoryAlternatePath, recreateDirectoryHierarchyInAlternatePath,
                parameterFilePath, maxLevelsToRecurse, GetDefaultExtensionsToParse());
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrDirectory">
        /// Input file or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryName">Output directory name</param>
        /// <param name="outputDirectoryAlternatePath">Output directory alternate path</param>
        /// <param name="recreateDirectoryHierarchyInAlternatePath">Recreate directory hierarchy in alternate path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="maxLevelsToRecurse">Levels to recurse, 0 or negative to process all subdirectories</param>
        /// <param name="extensionsToParse">List of file extensions to parse</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// The extensions should be of the form ".TXT" or ".RAW" (i.e. a period then the extension)
        /// If any of the extensions is "*" or ".*", all files will be processed
        /// </remarks>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory,
            string outputDirectoryName,
            string outputDirectoryAlternatePath,
            bool recreateDirectoryHierarchyInAlternatePath,
            string parameterFilePath,
            int maxLevelsToRecurse,
            IList<string> extensionsToParse)
        {

            AbortProcessing = false;

            FilesProcessed = 0;
            FileProcessErrors = 0;

            // Examine inputFilePathOrDirectory to see if it contains a filename; if not, assume it points to a directory
            // First, see if it contains a * or ?
            try
            {
                DirectoryInfo inputDirectory;
                string fileNameMatchPattern;

                if (extensionsToParse?.Count == 0)
                    extensionsToParse = GetDefaultExtensionsToParse();

                if (string.IsNullOrWhiteSpace(inputFilePathOrDirectory))
                {
                    // If an empty string, we process all files with known extensions in the current directory
                    inputDirectory = new DirectoryInfo(".");
                    fileNameMatchPattern = string.Empty;
                }
                else if (inputFilePathOrDirectory.Contains("*") || inputFilePathOrDirectory.Contains("?"))
                {
                    inputDirectory = GetInputDirectoryAndMatchSpec(inputFilePathOrDirectory, out fileNameMatchPattern);
                }
                else
                {

                    var candidateInputDirectory = new DirectoryInfo(inputFilePathOrDirectory);
                    if (candidateInputDirectory.Exists)
                    {
                        inputDirectory = candidateInputDirectory;
                        fileNameMatchPattern = "*";
                    }
                    else
                    {
                        if (candidateInputDirectory.Parent != null && candidateInputDirectory.Parent.Exists)
                        {
                            inputDirectory = candidateInputDirectory.Parent;
                        }
                        else
                        {
                            // Unable to determine the input directory path
                            // Use the current working directory
                            inputDirectory = new DirectoryInfo(".");
                        }

                        var fileName = Path.GetFileName(inputFilePathOrDirectory);

                        if (string.IsNullOrWhiteSpace(fileName))
                            fileNameMatchPattern = "*";
                        else
                            fileNameMatchPattern = fileName;
                    }
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
                        ErrorCode = eProcessFilesErrorCodes.InvalidOutputFolderPath;
                        HandleException("Error validating the alternate output directory path in ProcessFilesAndRecurseDirectories", ex);
                        return false;
                    }
                }

                // Call RecurseDirectoriesWork
                const int recursionLevel = 1;
                var success = RecurseDirectoriesWork(inputDirectory.FullName, fileNameMatchPattern, outputDirectoryName,
                                                 parameterFilePath, outputDirectoryAlternatePath,
                                                 recreateDirectoryHierarchyInAlternatePath, extensionsToParse,
                                                 recursionLevel, maxLevelsToRecurse);

                return success;
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFilesAndRecurseDirectories", ex);
                return false;
            }

        }

        private bool RecurseDirectoriesWork(
            string inputDirectoryPath,
            string fileNameMatch,
            string outputDirectoryName,
            string parameterFilePath,
            string outputDirectoryAlternatePath,
            bool recreateDirectoryHierarchyInAlternatePath,
            IList<string> extensionsToParse,
            int recursionLevel,
            int maxLevelsToRecurse)
        {
            // If maxLevelsToRecurse is <=0, process all subdirectories

            DirectoryInfo inputDirectory;

            var processAllExtensions = false;

            string outputDirectoryPathToUse;

            try
            {
                inputDirectory = new DirectoryInfo(inputDirectoryPath);
            }
            catch (Exception ex)
            {
                // Input directory path error
                ErrorCode = eProcessFilesErrorCodes.InvalidInputFilePath;
                HandleException("Error in RecurseDirectoriesWork examining inputDirectoryPath", ex);
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(outputDirectoryAlternatePath))
                {
                    if (recreateDirectoryHierarchyInAlternatePath)
                    {
                        outputDirectoryAlternatePath = Path.Combine(outputDirectoryAlternatePath, inputDirectory.Name);
                    }

                    outputDirectoryPathToUse = Path.Combine(outputDirectoryAlternatePath, outputDirectoryName);
                }
                else
                {
                    outputDirectoryPathToUse = outputDirectoryName;
                }
            }
            catch (Exception ex)
            {
                // Output file path error
                ErrorCode = eProcessFilesErrorCodes.InvalidOutputFolderPath;
                HandleException("Error in RecurseDirectoriesWork validating the alternate output directory path", ex);
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
                ErrorCode = eProcessFilesErrorCodes.UnspecifiedError;
                HandleException("Error in RecurseDirectoriesWork validating the extensions to parse", ex);
                return false;
            }

            var filesToProcess = new List<FileInfo>();

            try
            {
                if (!string.IsNullOrWhiteSpace(outputDirectoryPathToUse))
                {
                    // Update the cached output directory path
                    mOutputDirectoryPath = string.Copy(outputDirectoryPathToUse);
                }

                OnDebugEvent("Examining " + inputDirectoryPath);

                // Find matching files in this directory
                foreach (var inputFile in inputDirectory.GetFiles(fileNameMatch))
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

            }
            catch (UnauthorizedAccessException)
            {
                OnWarningEvent("Access denied to " + inputDirectoryPath);
                return true;
            }
            catch (Exception ex)
            {
                //if (ex.Message.StartsWith("Access", StringComparison.OrdinalIgnoreCase) &&
                //    ex.Message.EndsWith("Denied.", StringComparison.OrdinalIgnoreCase))
                //{
                //    OnWarningEvent("Access denied to " + inputDirectoryPath);
                //    return true;
                //}

                ErrorCode = eProcessFilesErrorCodes.InvalidInputFilePath;
                HandleException("Error in RecurseDirectoriesWork while finding files to process", ex);
                return false;
            }

            try
            {

                var matchCount = 0;
                var lastProgress = DateTime.UtcNow;

                // Process the files that were found
                foreach (var inputFile in filesToProcess)
                {
                    matchCount++;

                    // This is the % complete in this directory only; not overall
                    var percentComplete = matchCount / (float)filesToProcess.Count * 100;
                    OnProgressUpdate("Process " + inputFile.FullName, percentComplete);

                    var success = ProcessFile(inputFile.FullName, outputDirectoryPathToUse, parameterFilePath, true);

                    if (success)
                    {
                        FilesProcessed++;
                    }
                    else
                    {
                        FileProcessErrors++;
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
                ErrorCode = eProcessFilesErrorCodes.InvalidInputFilePath;
                HandleException("Error in RecurseDirectoriesWork while processing files", ex);
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
                var success = RecurseDirectoriesWork(subdirectory.FullName, fileNameMatch, outputDirectoryName,
                                                     parameterFilePath, outputDirectoryAlternatePath,
                                                     recreateDirectoryHierarchyInAlternatePath, extensionsToParse,
                                                     recursionLevel + 1, maxLevelsToRecurse);

                if (!success)
                    return false;
            }

            return true;
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
