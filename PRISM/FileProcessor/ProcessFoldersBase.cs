using System;

// ReSharper disable UnusedMember.Global

namespace PRISM.FileProcessor
{
    /// <summary>
    /// This class can be used as a base class for classes that process a directory or directories
    /// </summary>
    /// <remarks>
    /// This class contains simple error codes that can be set from any derived classes;
    /// the derived classes can also set their own local error codes
    /// </remarks>
    [Obsolete("Use ProcessDirectoriesBase instead")]
    public abstract class ProcessFoldersBase : ProcessDirectoriesBase
    {
        // Ignore Spelling: wildcards

        /// <summary>
        /// Error code enums
        /// </summary>
        [Obsolete("Use ProcessDirectoriesErrorCodes in ProcessDirectoriesBase instead")]
        public enum eProcessFoldersErrorCodes
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Invalid input directory path
            /// </summary>
            InvalidInputFolderPath = 1,

            /// <summary>
            /// Invalid output directory path
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

        /// <summary>
        /// Make sure inputFolderPath points to a valid directory and validate the output directory (defining it if null or empty)
        /// </summary>
        /// <remarks>Create outputFolderPath if it does not exist</remarks>
        /// <param name="inputFolderPath">Input directory path</param>
        /// <param name="outputFolderPath">Output directory path</param>
        /// <returns>True if success, false if an error</returns>
        protected bool CleanupFolderPaths(ref string inputFolderPath, ref string outputFolderPath)
        {
            return CleanupDirectoryPaths(ref inputFolderPath, ref outputFolderPath);
        }

        /// <summary>
        /// Process one or more folders (aka directories)
        /// </summary>
        /// <param name="inputFolderPath">Match spec for finding directories, can contain * and ?</param>
        /// <param name="outputFolderAlternatePath">Alternate output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If True, reset ErrorCode</param>
        /// <returns> True if success, false if an error</returns>
        public bool ProcessFoldersWildcard(
            string inputFolderPath,
            string outputFolderAlternatePath = "",
            string parameterFilePath = "",
            bool resetErrorCode = true)
        {
            return ProcessDirectoriesWildcard(inputFolderPath, outputFolderAlternatePath, parameterFilePath, resetErrorCode);
        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputFolderPath">Input directory path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFolder(string inputFolderPath)
        {
            return ProcessFolder(inputFolderPath, string.Empty, string.Empty, true);
        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputFolderPath">Input directory path</param>
        /// <param name="outputFolderAlternatePath">Alternate output directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessFolder(string inputFolderPath, string outputFolderAlternatePath, string parameterFilePath)
        {
            return ProcessFolder(inputFolderPath, outputFolderAlternatePath, parameterFilePath, true);
        }

        /// <summary>
        /// Process a single directory
        /// </summary>
        /// <param name="inputFolderPath">Input directory path</param>
        /// <param name="outputFolderAlternatePath">Alternate directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">If true, reset the error code</param>
        /// <returns>True if success, otherwise false</returns>
        public abstract bool ProcessFolder(string inputFolderPath, string outputFolderAlternatePath,
                                           string parameterFilePath, bool resetErrorCode);

        /// <summary>
        /// Process directories and subdirectories
        /// </summary>
        /// <param name="inputFolderPath">Input directory path</param>
        /// <param name="recurseFoldersMaxLevels">
        /// When 0 or negative, recurse infinitely
        /// When 1, only process the current directory
        /// When 2, process the current directory and files in its subdirectories
        /// </param>
        public bool ProcessAndRecurseFolders(string inputFolderPath, int recurseFoldersMaxLevels)
        {
            return ProcessAndRecurseFolders(inputFolderPath, string.Empty, string.Empty, recurseFoldersMaxLevels);
        }

        /// <summary>
        /// Process directories and subdirectories
        /// </summary>
        /// <remarks>Calls ProcessFolders for all matching directories in inputFolderPath</remarks>
        /// <param name="inputFolderPath">Input directory path (supports wildcards)</param>
        /// <param name="outputFolderAlternatePath">Alternate directory path</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="recurseFoldersMaxLevels">
        /// When 0 or negative, recurse infinitely
        /// When 1, only process the current directory
        /// When 2, process the current directory and files in its subdirectories
        /// </param>
        public bool ProcessAndRecurseFolders(
            string inputFolderPath,
            string outputFolderAlternatePath = "",
            string parameterFilePath = "",
            int recurseFoldersMaxLevels = 0)
        {
            return ProcessAndRecurseDirectories(inputFolderPath, outputFolderAlternatePath, parameterFilePath, recurseFoldersMaxLevels);
        }
    }
}
