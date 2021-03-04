using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PRISM
{
    /// <summary>
    /// Methods for copying and deleting files with path lengths of 260 characters or longer
    /// </summary>
    /// <remarks>
    /// From https://stackoverflow.com/a/39534444/1179467
    /// </remarks>
    internal static class NativeIOFileTools
    {
        public const int MAX_PATH = 260;

        public static bool Exists(string path)
        {
            if (path.Length < MAX_PATH)
            {
                return File.Exists(path);
            }

            var result = NativeIOMethods.GetFileAttributesW(GetWin32LongPath(path));
            return result > 0;
        }

        public static void Copy(string sourcePath, string destPath, bool overwrite)
        {
            if (sourcePath.Length < MAX_PATH && destPath.Length < MAX_PATH)
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

        public static void Delete(string filePath)
        {
            if (filePath.Length < MAX_PATH)
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

        public static string GetCleanPath(string path)
        {
            if (path.StartsWith(@"\\?\UNC\"))
                return @"\\" + path.Substring(8);

            if (path.StartsWith(@"\\?\"))
                return path.Substring(4);

            return path;
        }

        [DebuggerStepThrough]
        public static void ThrowWin32Exception()
        {
            var code = Marshal.GetLastWin32Error();
            if (code != 0)
            {
                throw new System.ComponentModel.Win32Exception(code);
            }
        }
    }
}
