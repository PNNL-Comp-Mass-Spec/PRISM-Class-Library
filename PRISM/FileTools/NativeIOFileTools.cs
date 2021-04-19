﻿using System;
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
        /// Prefix that indicates a Win32 Long Path
        /// </summary>
        public const string WIN32_LONG_PATH_PREFIX = @"\\?\";

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
            if (path.StartsWith(WIN32_LONG_PATH_PREFIX))
                return path;

            var newPath = path;
            if (newPath.StartsWith("\\"))
            {
                newPath = @"\\?\UNC\" + newPath.Substring(2);
            }
            else if (newPath.Contains(":"))
            {
                newPath = WIN32_LONG_PATH_PREFIX + newPath;
            }
            else
            {
                var currentDirectory = Environment.CurrentDirectory;
                newPath = Path.Combine(currentDirectory, newPath);
                while (newPath.Contains("\\.\\"))
                {
                    newPath = newPath.Replace("\\.\\", "\\");
                }

                newPath = WIN32_LONG_PATH_PREFIX + newPath;
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

            if (path.StartsWith(WIN32_LONG_PATH_PREFIX))
                return path.Substring(4);

            return path;
        }

        /// <summary>
        /// Get the size of a file, in bytes
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>File size, in bytes</returns>
        public static long GetFileLength(string filePath)
        {
            if (filePath.Length < FILE_PATH_LENGTH_THRESHOLD)
            {
                var fileInfo = new FileInfo(filePath);
                return fileInfo.Length;
            }

            return InternalGetFileSize(filePath, out _);
        }

        private static long InternalGetFileSize(string filePath, out NativeIOMethods.WIN32_FIND_DATA findData)
        {
            long size = 0;
            var INVALID_HANDLE_VALUE = new IntPtr(-1);

            var findHandle = NativeIOMethods.FindFirstFile(WIN32_LONG_PATH_PREFIX + filePath, out findData);

            if (findHandle != INVALID_HANDLE_VALUE)
            {
                if ((findData.dwFileAttributes & FileAttributes.Directory) == 0)
                {
                    size = (long)findData.nFileSizeLow + (long)findData.nFileSizeHigh * 4294967296;
                }
            }

            return size;
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
