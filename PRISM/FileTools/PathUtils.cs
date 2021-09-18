using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Cross-platform path utilities
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// Convert a path to be Linux-compatible (backslash to forward slash
        /// </summary>
        /// <param name="pathSpec"></param>
        public static string AssureLinuxPath(string pathSpec)
        {
            return pathSpec.Replace('\\', '/');
        }

        /// <summary>
        /// Convert a path to be Windows-compatible (forward slash to backslash
        /// </summary>
        /// <param name="pathSpec"></param>
        public static string AssureWindowsPath(string pathSpec)
        {
            return pathSpec.Replace('/', '\\');
        }

        /// <summary>
        /// Combine paths using the system default path separator character
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        // ReSharper disable once UnusedMember.Global
        public static string CombinePathsLocalSepChar(string path1, string path2)
        {
            return CombinePaths(path1, path2, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Combine paths using a forward slash as a path separator character
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        public static string CombineLinuxPaths(string path1, string path2)
        {
            return CombinePaths(path1, path2, '/');
        }

        /// <summary>
        /// Combine paths using a backslash as a path separator character
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        public static string CombineWindowsPaths(string path1, string path2)
        {
            return CombinePaths(path1, path2, '\\');
        }

        /// <summary>
        /// Combine paths using the specified path separator character
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        /// <param name="directorySepChar"></param>
        public static string CombinePaths(string path1, string path2, char directorySepChar)
        {
            if (path1 == null || path2 == null)
                throw new ArgumentNullException(path1 == null ? "path1" : "path2");

            if (string.IsNullOrWhiteSpace(path2))
                return path1;

            if (string.IsNullOrWhiteSpace(path1))
                return path2;

            if (Path.IsPathRooted(path2))
                return path2;

            var ch = path1[path1.Length - 1];
            if (ch != Path.DirectorySeparatorChar && ch != Path.AltDirectorySeparatorChar && ch != Path.VolumeSeparatorChar)
                return path1 + directorySepChar + path2;

            return path1 + path2;
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
        // ReSharper disable once UnusedMember.Global
        public static string CompactPathString(string pathToCompact, int maxLength = 40)
        {
            return FileTools.CompactPathString(pathToCompact, maxLength);
        }

        /// <summary>
        /// Find all files that match the given file name pattern, optionally recursing
        /// </summary>
        /// <remarks>When recursing, skips directories for which the user does not have permission</remarks>
        /// <param name="pathSpec">Directory/file search specification, e.g. C:\Windows\*.ini</param>
        /// <param name="recurse">True to recurse</param>
        /// <returns>List of FileInfo objects (empty list if the directory does not exist)</returns>
        public static List<FileInfo> FindFilesWildcard(string pathSpec, bool recurse = false)
        {
            var cleanPath = GetCleanPath(pathSpec);
            FileInfo cleanFileInfo;

            if (cleanPath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD && !SystemInfo.IsLinux)
            {
                cleanFileInfo = new FileInfo(NativeIOFileTools.GetWin32LongPath(cleanPath));
            }
            else
            {
                cleanFileInfo = new FileInfo(cleanPath);
            }

            string directoryPath;
            if (cleanFileInfo.Directory?.Exists == true && cleanFileInfo.DirectoryName != null)
            {
                directoryPath = cleanFileInfo.DirectoryName;
            }
            else
            {
                directoryPath = ".";
            }

            var directory = new DirectoryInfo(directoryPath);

            // Remove any directory information from pathSpec
            var fileMask = Path.GetFileName(pathSpec);

            return FindFilesWildcard(directory, fileMask, recurse);
        }

        /// <summary>
        /// Find all files that match the given file name pattern in the given directory, optionally recursing
        /// </summary>
        /// <remarks>When recursing, skips directories for which the user does not have permission</remarks>
        /// <param name="directory">Directory to search</param>
        /// <param name="fileMask">Filename mask to find, e.g. *.txt</param>
        /// <param name="recurse">True to recurse</param>
        /// <returns>List of FileInfo objects (empty list if the directory does not exist)</returns>
        public static List<FileInfo> FindFilesWildcard(DirectoryInfo directory, string fileMask, bool recurse = false)
        {
            if (directory?.Exists != true)
                return new List<FileInfo>();

            try
            {
                DirectoryInfo parentDirectory;
                if (directory.FullName.Length + fileMask.Length + 2 >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD && !SystemInfo.IsLinux)
                {
                    parentDirectory = new DirectoryInfo(NativeIOFileTools.GetWin32LongPath(directory.FullName));
                }
                else
                {
                    parentDirectory = directory;
                }

                var matchedFiles = parentDirectory.GetFiles(fileMask).ToList();

                if (!recurse)
                    return matchedFiles;

                foreach (var subdirectory in parentDirectory.GetDirectories())
                {
                    DirectoryInfo subdirectoryToUse;
                    if (subdirectory.FullName.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD && !SystemInfo.IsLinux)
                    {
                        subdirectoryToUse = new DirectoryInfo(NativeIOFileTools.GetWin32LongPath(subdirectory.FullName));
                    }
                    else
                    {
                        subdirectoryToUse = subdirectory;
                    }

                    var additionalFiles = FindFilesWildcard(subdirectoryToUse, fileMask, true);

                    matchedFiles.AddRange(additionalFiles);
                }

                return matchedFiles;
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied
                return new List<FileInfo>();
            }
        }

        // ReSharper disable once CommentTypo

        /// <summary>
        /// Check a filename against a file mask (like * or *.txt or MSGF*)
        /// </summary>
        /// <remarks>From https://stackoverflow.com/a/725352/1179467/ how-to-determine-if-a-file-matches-a-file-mask</remarks>
        /// <param name="fileName"></param>
        /// <param name="fileMask"></param>
        /// <returns>True if a match, otherwise false</returns>
        public static bool FitsMask(string fileName, string fileMask)
        {
            var convertedMask = "^" + Regex.Escape(fileMask).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            var regexMask = new Regex(convertedMask, RegexOptions.IgnoreCase);
            return regexMask.IsMatch(fileName);
        }

        /// <summary>
        /// Replace * and ? characters in fileOrDirectoryPath with underscores
        /// </summary>
        /// <param name="fileOrDirectoryPath"></param>
        public static string GetCleanPath(string fileOrDirectoryPath)
        {
            if (!fileOrDirectoryPath.StartsWith(NativeIOFileTools.WIN32_LONG_PATH_PREFIX))
            {
                return fileOrDirectoryPath.Replace("*", "_").Replace("?", "_");
            }

            // This is a Win32 long path
            var cleanPath = fileOrDirectoryPath.Substring(4).Replace("*", "_").Replace("?", "_");

            return NativeIOFileTools.WIN32_LONG_PATH_PREFIX + cleanPath;
        }

        /// <summary>
        /// Return the parent directory of directoryPath
        /// Supports both Windows paths and Linux paths
        /// </summary>
        /// <remarks>Returns \ or / if the path is rooted and the parent is a path</remarks>
        /// <param name="directoryPath">Directory path to examine</param>
        /// <param name="directoryName">Name of the directory in directoryPath but without the parent path</param>
        /// <returns>Parent directory path, or an empty string if no parent</returns>
        public static string GetParentDirectoryPath(string directoryPath, out string directoryName)
        {
            if (directoryPath.Contains(Path.DirectorySeparatorChar) && Path.IsPathRooted(directoryPath))
            {
                var directory = new DirectoryInfo(directoryPath);
                directoryName = directory.Name;

                var parent = directory.Parent;
                return parent?.FullName ?? string.Empty;
            }

            char sepChar;
            if (directoryPath.Contains(Path.DirectorySeparatorChar))
            {
                sepChar = Path.DirectorySeparatorChar;
            }
            else
            {
                sepChar = Path.DirectorySeparatorChar == '\\' ? '/' : '\\';
            }

            if (sepChar == '\\')
            {
                // Check for a windows server without a share name
                if (Regex.IsMatch(directoryPath, @"^\\\\[^\\]+\\?$") ||
                    Regex.IsMatch(directoryPath, @"^[a-z]:\\?$"))
                {
                    directoryName = string.Empty;
                    return "";
                }
            }
            else
            {
                // sepChar is /
                if (directoryPath == "/")
                {
                    directoryName = string.Empty;
                    return "";
                }
            }

            if (directoryPath.EndsWith(sepChar.ToString()))
                directoryPath = directoryPath.TrimEnd(sepChar);

            bool rootedLinuxPath;
            string[] pathParts;
            if (directoryPath.StartsWith("/"))
            {
                rootedLinuxPath = true;
                pathParts = directoryPath.Substring(1).Split(sepChar);
            }
            else
            {
                rootedLinuxPath = false;
                pathParts = directoryPath.Split(sepChar);
            }

            if (pathParts.Length == 1)
            {
                directoryName = pathParts[0];
                return rootedLinuxPath ? "/" : "";
            }

            directoryName = pathParts[pathParts.Length - 1];

            var parentPath = directoryPath.Substring(0, directoryPath.Length - directoryName.Length - 1);
            if (rootedLinuxPath && !parentPath.StartsWith("/"))
                return "/" + parentPath;

            return parentPath;
        }

        /// <summary>
        /// Examines filePath to look for spaces
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>filePath as-is if no spaces, otherwise filePath surrounded by double quotes </returns>
        public static string PossiblyQuotePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            if (!filePath.Contains(" "))
                return filePath;

            if (!filePath.StartsWith("\""))
            {
                filePath = "\"" + filePath;
            }

            if (!filePath.EndsWith("\""))
            {
                filePath += "\"";
            }

            return filePath;
        }

        /// <summary>
        /// Replace the filename in the path with a new filename
        /// </summary>
        /// <param name="existingFilePath"></param>
        /// <param name="newFileName"></param>
        public static string ReplaceFilenameInPath(string existingFilePath, string newFileName)
        {
            if (string.IsNullOrWhiteSpace(existingFilePath))
                return newFileName;

            var existingFile = new FileInfo(existingFilePath);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (existingFile.DirectoryName == null)
                return newFileName;

            return Path.Combine(existingFile.DirectoryName, newFileName);
        }
    }
}
