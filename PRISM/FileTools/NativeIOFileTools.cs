using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// ReSharper disable UnusedMember.Global

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Methods for copying and deleting files with path lengths of 260 characters or longer
    /// These only work on Windows
    /// </summary>
    /// <remarks>
    /// From https://stackoverflow.com/a/39534444/1179467
    /// </remarks>
    public static class NativeIOFileTools
    {
        /// <summary>
        /// File path length threshold at which we should switch to NativeIO calls
        /// </summary>
        public const int FILE_PATH_LENGTH_THRESHOLD = 260;

        /// <summary>
        /// Check whether the file exists
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True if the file exists, otherwise false</returns>
        public static bool Exists(string path)
        {
            if (path.Length < FILE_PATH_LENGTH_THRESHOLD)
            {
                return File.Exists(path);
            }

            var result = NativeIOMethods.GetFileAttributesW(GetWin32LongPath(path));
            return result > 0;
        }

        /// <summary>
        /// Copy the file
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        /// <param name="overwrite"></param>
        public static void Copy(string sourcePath, string destPath, bool overwrite)
        {
            if (sourcePath.Length < FILE_PATH_LENGTH_THRESHOLD && destPath.Length < FILE_PATH_LENGTH_THRESHOLD)
            {
                File.Copy(sourcePath, destPath, overwrite);
            }
            else
            {
                var ok = NativeIOMethods.CopyFileW(GetWin32LongPath(sourcePath), GetWin32LongPath(destPath), !overwrite);
                if (!ok)
                    ThrowWin32Exception();
            }
        }

        /// <summary>
        /// Delete the file
        /// </summary>
        /// <param name="filePath"></param>
        public static void Delete(string filePath)
        {
            if (filePath.Length < FILE_PATH_LENGTH_THRESHOLD)
            {
                File.Delete(filePath);
            }
            else
            {
                var ok = NativeIOMethods.DeleteFileW(GetWin32LongPath(filePath));
                if (!ok)
                    ThrowWin32Exception();
            }
        }

        /// <summary>
        /// Format the path as a Win32 long path
        /// </summary>
        /// <param name="path"></param>
        public static string GetWin32LongPath(string path)
        {
            if (path.StartsWith(@"\\?\"))
                return path;

            var newPath = path;
            if (newPath.StartsWith("\\"))
            {
                newPath = @"\\?\UNC\" + newPath.Substring(2);
            }
            else if (newPath.Contains(":"))
            {
                newPath = @"\\?\" + newPath;
            }
            else
            {
                var currentDirectory = Environment.CurrentDirectory;
                newPath = Path.Combine(currentDirectory, newPath);
                while (newPath.Contains("\\.\\"))
                {
                    newPath = newPath.Replace("\\.\\", "\\");
                }

                newPath = @"\\?\" + newPath;
            }
            return newPath.TrimEnd('.');
        }

        /// <summary>
        /// Remove Win32 long path characters
        /// </summary>
        /// <param name="path"></param>
        public static string GetCleanPath(string path)
        {
            if (path.StartsWith(@"\\?\UNC\"))
                return @"\\" + path.Substring(8);

            if (path.StartsWith(@"\\?\"))
                return path.Substring(4);

            return path;
        }


        [DebuggerStepThrough]
        internal static void ThrowWin32Exception()
        {
            var code = Marshal.GetLastWin32Error();
            if (code != 0)
            {
                throw new System.ComponentModel.Win32Exception(code);
            }
        }
    }
}
