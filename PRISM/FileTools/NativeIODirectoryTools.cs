using System;
using System.Collections.Generic;
using System.IO;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Methods for copying and deleting directories with path lengths of 260 characters or longer
    /// These only work on Windows
    /// </summary>
    /// <remarks>
    /// From https://stackoverflow.com/a/39534444/1179467
    /// </remarks>
    public static class NativeIODirectoryTools
    {
        /// <summary>
        /// Directory path length threshold at which we should switch to NativeIO calls
        /// </summary>
        public const int DIRECTORY_PATH_LENGTH_THRESHOLD = 248;

        /// <summary>
        /// Check whether the directory exists
        /// </summary>
        /// <param name="path"></param>
        public static bool Exists(string path)
        {
            if (path.Length < DIRECTORY_PATH_LENGTH_THRESHOLD)
            {
                return Directory.Exists(path);
            }

            var result = NativeIOMethods.GetFileAttributesW(NativeIOFileTools.GetWin32LongPath(path));
            return result > 0;
        }

        /// <summary>
        /// Create a directory, optionally having a long path
        /// </summary>
        /// <param name="path"></param>
        public static void CreateDirectory(string path)
        {
            if (path.Length < DIRECTORY_PATH_LENGTH_THRESHOLD)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                var ok = NativeIOMethods.CreateDirectory(NativeIOFileTools.GetWin32LongPath(path), IntPtr.Zero);

                if (!ok)
                {
                    NativeIOFileTools.ThrowWin32Exception();
                }
            }
        }

        /// <summary>
        /// Delete a directory, optionally having a long path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="recursive"></param>
        public static void Delete(string path, bool recursive)
        {
            if (path.Length < NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD && !recursive)
            {
                Directory.Delete(path, false);
            }
            else
            {
                if (!recursive)
                {
                    var ok = NativeIOMethods.RemoveDirectory(NativeIOFileTools.GetWin32LongPath(path));

                    if (!ok)
                    {
                        NativeIOFileTools.ThrowWin32Exception();
                    }
                }
                else
                {
                    DeleteDirectories(new List<string> { NativeIOFileTools.GetWin32LongPath(path) });
                }
            }
        }

        private static void DeleteDirectories(IEnumerable<string> directoryPaths)
        {
            foreach (var path in directoryPaths)
            {
                var files = GetFiles(path, null, SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    NativeIOFileTools.Delete(file);
                }

                var matchingDirectories = GetDirectories(path, null, SearchOption.TopDirectoryOnly);
                DeleteDirectories(matchingDirectories);

                var ok = NativeIOMethods.RemoveDirectory(NativeIOFileTools.GetWin32LongPath(path));

                if (!ok)
                {
                    NativeIOFileTools.ThrowWin32Exception();
                }
            }
        }

        /// <summary>
        /// Find directories that match a search pattern
        /// </summary>
        /// <param name="path">Path to the directory to examine</param>
        /// <param name="searchPattern">Search pattern; use null or * to find all subdirectories</param>
        /// <param name="searchOption">Whether to search the current directory only, or also search below all subdirectories</param>
        /// <returns>List of paths</returns>
        public static List<string> GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            searchPattern ??= "*";
            var dirs = new List<string>();
            InternalGetDirectories(path, searchPattern, searchOption, ref dirs);
            return dirs;
        }

        private static void InternalGetDirectories(string path, string searchPattern, SearchOption searchOption, ref List<string> dirs)
        {
            var findHandle = NativeIOMethods.FindFirstFile(Path.Combine(NativeIOFileTools.GetWin32LongPath(path), searchPattern), out var findData);

            try
            {
                if (findHandle != new IntPtr(-1))
                {
                    do
                    {
                        if ((findData.dwFileAttributes & FileAttributes.Directory) != 0)
                        {
                            if (findData.cFileName != "." && findData.cFileName != "..")
                            {
                                var subdirectory = Path.Combine(path, findData.cFileName);
                                dirs.Add(NativeIOFileTools.GetCleanPath(subdirectory));
                                if (searchOption == SearchOption.AllDirectories)
                                {
                                    InternalGetDirectories(subdirectory, searchPattern, searchOption, ref dirs);
                                }
                            }
                        }
                    } while (NativeIOMethods.FindNextFile(findHandle, out findData));
                    NativeIOMethods.FindClose(findHandle);
                }
                else
                {
                    NativeIOFileTools.ThrowWin32Exception();
                }
            }
            catch (Exception)
            {
                NativeIOMethods.FindClose(findHandle);
                throw;
            }
        }

        /// <summary>
        /// Find files that match a search pattern
        /// </summary>
        /// <param name="path">Path to the directory to examine</param>
        /// <param name="searchPattern">Search pattern; use null or * to find all subdirectories</param>
        /// <param name="searchOption">Whether to search the current directory only, or also search below all subdirectories</param>
        /// <returns>List of paths</returns>
        public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            searchPattern ??= "*";

            var files = new List<string>();
            var dirs = new List<string> { path };

            if (searchOption == SearchOption.AllDirectories)
            {
                // Add all the subdirectory paths
                dirs.AddRange(GetDirectories(path, null, SearchOption.AllDirectories));
            }

            foreach (var dir in dirs)
            {
                var findHandle = NativeIOMethods.FindFirstFile(Path.Combine(NativeIOFileTools.GetWin32LongPath(dir), searchPattern), out var findData);

                try
                {
                    if (findHandle != new IntPtr(-1))
                    {
                        do
                        {
                            if ((findData.dwFileAttributes & FileAttributes.Directory) == 0)
                            {
                                var filename = Path.Combine(dir, findData.cFileName);
                                files.Add(NativeIOFileTools.GetCleanPath(filename));
                            }
                        } while (NativeIOMethods.FindNextFile(findHandle, out findData));
                        NativeIOMethods.FindClose(findHandle);
                    }
                }
                catch (Exception)
                {
                    NativeIOMethods.FindClose(findHandle);
                    throw;
                }
            }

            return files.ToArray();
        }
    }
}
