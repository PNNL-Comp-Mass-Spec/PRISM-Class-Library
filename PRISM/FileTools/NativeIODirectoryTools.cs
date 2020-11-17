using System;
using System.Collections.Generic;
using System.IO;

namespace PRISM
{
    /// <summary>
    /// Methods for copying and deleting directories with path lengths of 260 characters or longer
    /// </summary>
    /// <remarks>
    /// From https://stackoverflow.com/a/39534444/1179467
    /// </remarks>
    internal static class NativeIODirectoryTools
    {
        public static void Delete(string path, bool recursive)
        {
            if (path.Length < NativeIOFileTools.MAX_PATH && !recursive)
            {
                Directory.Delete(path, false);
            }
            else
            {
                if (!recursive)
                {
                    var ok = NativeIOMethods.RemoveDirectory(NativeIOFileTools.GetWin32LongPath(path));
                    if (!ok)
                        NativeIOFileTools.ThrowWin32Exception();
                }
                else
                {
                    DeleteDirectories(new[] { NativeIOFileTools.GetWin32LongPath(path) });
                }
            }
        }

        private static void DeleteDirectories(string[] directories)
        {
            foreach (var directory in directories)
            {
                var files = GetFiles(directory, null, SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    NativeIOFileTools.Delete(file);
                }

                directories = GetDirectories(directory, null, SearchOption.TopDirectoryOnly);
                DeleteDirectories(directories);

                var ok = NativeIOMethods.RemoveDirectory(NativeIOFileTools.GetWin32LongPath(directory));
                if (!ok)
                    NativeIOFileTools.ThrowWin32Exception();
            }
        }

        public static string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            searchPattern = searchPattern ?? "*";
            var dirs = new List<string>();
            InternalGetDirectories(path, searchPattern, searchOption, ref dirs);
            return dirs.ToArray();
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

        public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            searchPattern = searchPattern ?? "*";

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
