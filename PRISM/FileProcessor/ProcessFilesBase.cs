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
        // Ignore Spelling: bool, wildcards

        /// <summary>
        /// Constructor
        /// </summary>
        protected ProcessFilesBase()
        {
            mFileDate = "March 18, 2020";
            ErrorCode = ProcessFilesErrorCodes.NoError;
        }

        /// <summary>
        /// Error code enums
        /// </summary>
        public enum ProcessFilesErrorCodes
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

        /// <summary>
        /// Error code enums
        /// </summary>
        [Obsolete("Use ProcessFilesErrorCodes")]
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

            /// <summary>
            /// Invalid output directory path
            /// </summary>
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

        /// <summary>
        /// Error code reflecting processing outcome
        /// </summary>
        public ProcessFilesErrorCodes ErrorCode { get; set; }

        /// <summary>
        /// Number of files processed successfully when using ProcessFilesAndRecurseDirectories or ProcessFilesWildcard
        /// </summary>
        public int FilesProcessed { get; private set; }

        /// <summary>
        /// Number of files that could not be processed when using ProcessFilesAndRecurseDirectories or ProcessFilesWildcard
        /// </summary>
        public int FileProcessErrors { get; private set; }

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
                    {
                        outputDirectoryPath = ".";
                    }
                    else
                    {
                        // Define outputDirectoryPath based on inputFilePath
                        outputDirectoryPath = inputFile.DirectoryName;
                    }
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

                    ErrorCode = ProcessFilesErrorCodes.InvalidInputFilePath;
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
            return ErrorCode switch
            {
                ProcessFilesErrorCodes.NoError => string.Empty,
                ProcessFilesErrorCodes.InvalidInputFilePath => "Invalid input file path",
                ProcessFilesErrorCodes.InvalidOutputDirectoryPath => "Invalid output directory path",
                ProcessFilesErrorCodes.ParameterFileNotFound => "Parameter file not found",
                ProcessFilesErrorCodes.InvalidParameterFile => "Invalid parameter file",
                ProcessFilesErrorCodes.FilePathError => "General file path error",
                ProcessFilesErrorCodes.LocalizedError => "Localized error",
                ProcessFilesErrorCodes.UnspecifiedError => "Unspecified error",
                _ => "Unknown error state",     // This shouldn't happen
            };
        }

        /// <summary>
        /// Get the default file extensions to parse
        /// </summary>
        public virtual IList<string> GetDefaultExtensionsToParse()
        {
            return new List<string> { ".*" };
        }

        private static DirectoryInfo GetInputDirectoryAndMatchSpec(string inputFilePathSpec, out string fileNameMatchPattern)
        {
            // Copy the path into cleanPath and replace any * or ? characters with _
            var cleanPath = PathUtils.GetCleanPath(inputFilePathSpec);

            var inputFileSpec = new FileInfo(cleanPath);
            string inputDirectoryToUse;

            if (inputFileSpec.Directory?.Exists == true)
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

            ErrorCode = ProcessFilesErrorCodes.InvalidInputFilePath;
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
                    ErrorCode = ProcessFilesErrorCodes.NoError;

                if (string.IsNullOrWhiteSpace(inputFilePath))
                {
                    NotifyInvalidInputFile();
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(outputDirectoryPath))
                {
                    // Update the cached output directory path
                    mOutputDirectoryPath = outputDirectoryPath;
                }

                // See if inputFilePath contains a wildcard (* or ?)
                if (!inputFilePath.Contains("*") && !inputFilePath.Contains("?"))
                {
                    var wildcardSuccess = ProcessFile(inputFilePath, outputDirectoryPath, parameterFilePath, resetErrorCode);
                    return wildcardSuccess;
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

                if (ErrorCode != ProcessFilesErrorCodes.NoError)
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
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, false if an error</returns>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory, string outputDirectoryPath = "", string parameterFilePath = "")
        {
            return ProcessFilesAndRecurseDirectories(
                inputFilePathOrDirectory, outputDirectoryPath,
                string.Empty, false,
                parameterFilePath);
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrDirectory">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="maxLevelsToRecurse">
        /// When 0 or negative, recurse infinitely
        /// When 1, only process the current directory
        /// When 2, process the current directory and files in its subdirectories
        /// </param>
        /// <returns>True if success, false if an error</returns>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory, string outputDirectoryPath, int maxLevelsToRecurse)
        {
            return ProcessFilesAndRecurseDirectories(
                inputFilePathOrDirectory, outputDirectoryPath,
                string.Empty, false,
                string.Empty, maxLevelsToRecurse,
                GetDefaultExtensionsToParse());
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrDirectory">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="extensionsToParse">List of file extensions to parse</param>
        /// <returns>True if success, false if an error</returns>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory, string outputDirectoryPath,
            string parameterFilePath, IList<string> extensionsToParse)
        {
            return ProcessFilesAndRecurseDirectories(
                inputFilePathOrDirectory, outputDirectoryPath,
                string.Empty, false,
                parameterFilePath, 0,
                extensionsToParse);
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputFilePathOrDirectory">
        /// Input directory or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryNameOrPath">
        /// If outputDirectoryAlternatePath is empty, this is the output directory path
        /// If outputDirectoryAlternatePath is defined, this is the output directory name
        /// </param>
        /// <param name="outputDirectoryAlternatePath">Output directory alternate path</param>
        /// <param name="recreateDirectoryHierarchyInAlternatePath">Recreate directory hierarchy in alternate path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="maxLevelsToRecurse">
        /// When 0 or negative, recurse infinitely
        /// When 1, only process the current directory
        /// When 2, process the current directory and files in its subdirectories
        /// </param>
        /// <returns>True if success, false if an error</returns>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory, string outputDirectoryNameOrPath, string outputDirectoryAlternatePath,
            bool recreateDirectoryHierarchyInAlternatePath, string parameterFilePath = "", int maxLevelsToRecurse = 0)
        {
            return ProcessFilesAndRecurseDirectories(
                inputFilePathOrDirectory, outputDirectoryNameOrPath,
                outputDirectoryAlternatePath, recreateDirectoryHierarchyInAlternatePath,
                parameterFilePath, maxLevelsToRecurse, GetDefaultExtensionsToParse());
        }

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <remarks>
        /// The extensions should be of the form ".TXT" or ".RAW" (i.e. a period then the extension)
        /// If any of the extensions is "*" or ".*", all files will be processed
        /// </remarks>
        /// <param name="inputFilePathOrDirectory">
        /// Input file or directory (supports wildcards)
        /// If a directory path, or if empty, processes files with known entries in the working directory
        /// If a file path, will process matching files, ignoring the default extensions and ignoring extensionsToParse
        /// </param>
        /// <param name="outputDirectoryNameOrPath">
        /// If outputDirectoryAlternatePath is empty, this is the output directory path
        /// If outputDirectoryAlternatePath is defined, this is the output directory name
        /// </param>
        /// <param name="outputDirectoryAlternatePath">Output directory alternate path</param>
        /// <param name="recreateDirectoryHierarchyInAlternatePath">Recreate directory hierarchy in alternate path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="maxLevelsToRecurse">
        /// When 0 or negative, recurse infinitely
        /// When 1, only process the current directory
        /// When 2, process the current directory and files in its subdirectories
        /// </param>
        /// <param name="extensionsToParse">List of file extensions to parse</param>
        /// <returns>True if success, false if an error</returns>
        public bool ProcessFilesAndRecurseDirectories(
            string inputFilePathOrDirectory,
            string outputDirectoryNameOrPath,
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
                        if (candidateInputDirectory.Parent?.Exists == true)
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
                        HandleException("Error validating the alternate output directory path in ProcessFilesAndRecurseDirectories", ex);
                        ErrorCode = ProcessFilesErrorCodes.InvalidOutputDirectoryPath;
                        return false;
                    }
                }

                // Call RecurseDirectoriesWork
                const int recursionLevel = 1;
                var success = RecurseDirectoriesWork(inputDirectory.FullName, fileNameMatchPattern, outputDirectoryNameOrPath,
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

        /// <summary>
        /// Process files in a directory and in its subdirectories
        /// </summary>
        /// <param name="inputDirectoryPath">Path to the directory with files to process</param>
        /// <param name="fileNameMatch">File name or file spec to process</param>
        /// <param name="outputDirectoryNameOrPath">
        /// If outputDirectoryAlternatePath is empty, this is the output directory path
        /// If outputDirectoryAlternatePath is defined, this is the output directory name
        /// </param>
        /// <param name="parameterFilePath">Optional parameter file path; loading of parameters is handled by classes that inherit this base class</param>
        /// <param name="outputDirectoryAlternatePath">Output directory alternate path; primarily useful if recreateDirectoryHierarchyInAlternatePath is true</param>
        /// <param name="recreateDirectoryHierarchyInAlternatePath">Recreate directory hierarchy in alternate path</param>
        /// <param name="extensionsToParse">
        /// List of file extensions to process
        /// If the list is empty, or if it contains "*" or ".*", process all files
        /// Otherwise, a list of extensions to process, e.g. ".txt" and ".tsv"
        /// </param>
        /// <param name="recursionLevel">Current level of recursion</param>
        /// <param name="maxLevelsToRecurse">
        /// When 0 or negative, recurse infinitely
        /// When 1, only process the current directory
        /// When 2, process the current directory and files in its subdirectories
        /// </param>
        /// <returns>True if success, false if an error</returns>
        private bool RecurseDirectoriesWork(
            string inputDirectoryPath,
            string fileNameMatch,
            string outputDirectoryNameOrPath,
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
                HandleException("Error in RecurseDirectoriesWork examining inputDirectoryPath", ex);
                ErrorCode = ProcessFilesErrorCodes.InvalidInputFilePath;
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

                    outputDirectoryPathToUse = Path.Combine(outputDirectoryAlternatePath, outputDirectoryNameOrPath);
                }
                else
                {
                    outputDirectoryPathToUse = outputDirectoryNameOrPath;
                }
            }
            catch (Exception ex)
            {
                // Output file path error
                HandleException("Error in RecurseDirectoriesWork validating the alternate output directory path", ex);
                ErrorCode = ProcessFilesErrorCodes.InvalidOutputDirectoryPath;
                return false;
            }

            try
            {
                if (extensionsToParse.Count == 0)
                {
                    processAllExtensions = true;
                }

                // Validate extensionsToParse()
                for (var extensionIndex = 0; extensionIndex < extensionsToParse.Count; extensionIndex++)
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
                HandleException("Error in RecurseDirectoriesWork validating the extensions to parse", ex);
                ErrorCode = ProcessFilesErrorCodes.UnspecifiedError;
                return false;
            }

            var filesToProcess = new List<FileInfo>();

            try
            {
                if (!string.IsNullOrWhiteSpace(outputDirectoryPathToUse))
                {
                    // Update the cached output directory path
                    mOutputDirectoryPath = outputDirectoryPathToUse;
                }

                OnDebugEvent("Examining " + inputDirectoryPath);

                // Find matching files in this directory
                foreach (var inputFile in inputDirectory.GetFiles(fileNameMatch))
                {
                    foreach (var extension in extensionsToParse)
                    {
                        if (processAllExtensions ||
                            string.Equals(inputFile.Extension, extension, StringComparison.OrdinalIgnoreCase))
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

                HandleException("Error in RecurseDirectoriesWork while finding files to process", ex);
                ErrorCode = ProcessFilesErrorCodes.InvalidInputFilePath;
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
                HandleException("Error in RecurseDirectoriesWork while processing files", ex);
                ErrorCode = ProcessFilesErrorCodes.InvalidInputFilePath;
                return false;
            }

            if (AbortProcessing)
                return false;

            // If maxLevelsToRecurse is <=0, we recurse infinitely
            // otherwise, compare recursionLevel to maxLevelsToRecurse
            if (maxLevelsToRecurse > 0 && recursionLevel > maxLevelsToRecurse)
                return true;

            // Call this method for each of the subdirectories of inputDirectory
            foreach (var subdirectory in inputDirectory.GetDirectories())
            {
                var success = RecurseDirectoriesWork(subdirectory.FullName, fileNameMatch, outputDirectoryNameOrPath,
                                                     parameterFilePath, outputDirectoryAlternatePath,
                                                     recreateDirectoryHierarchyInAlternatePath, extensionsToParse,
                                                     recursionLevel + 1, maxLevelsToRecurse);

                if (!success && !IgnoreErrorsWhenUsingWildcardMatching)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Update the base class error code
        /// </summary>
        /// <param name="newErrorCode"></param>
        protected void SetBaseClassErrorCode(ProcessFilesErrorCodes newErrorCode)
        {
            ErrorCode = newErrorCode;
        }

        // The following methods should be placed in any derived class
        // Cannot define as abstract since it contains a customized enumerated type (eDerivedClassErrorCodes) in the method declaration

        // private void SetLocalErrorCode(eDerivedClassErrorCodes newErrorCode)
        // {
        //     SetLocalErrorCode(newErrorCode, false);
        // }
        //
        // private void SetLocalErrorCode(eDerivedClassErrorCodes newErrorCode, bool leaveExistingErrorCodeUnchanged)
        // {
        //     if (leaveExistingErrorCodeUnchanged && mLocalErrorCode != eDerivedClassErrorCodes.NoError)
        //     {
        //         // An error code is already defined; do not change it
        //     }
        //     else
        //     {
        //         mLocalErrorCode = newErrorCode;
        //
        //         if (newErrorCode == eDerivedClassErrorCodes.NoError)
        //         {
        //             if (base.ErrorCode == ProcessFilesBase.ProcessFilesErrorCodes.LocalizedError)
        //             {
        //                 base.SetBaseClassErrorCode(ProcessFilesBase.ProcessFilesErrorCodes.NoError);
        //             }
        //         }
        //         else
        //         {
        //             base.SetBaseClassErrorCode(ProcessFilesBase.ProcessFilesErrorCodes.LocalizedError);
        //         }
        //     }
        // }
    }
}
