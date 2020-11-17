using System;
using System.Collections.Generic;
using System.IO;

// ReSharper disable once CheckNamespace
namespace PRISM
{
#pragma warning disable CS1591  // Missing XML comments
#pragma warning disable IDE1006 // Naming Styles

    [Obsolete("Use the EventNotifier class")]
    public abstract class clsEventNotifier : EventNotifier
    {
    }

    [Obsolete("Use the DBTools class")]
    public class clsDBTools : DBTools
    {
        public clsDBTools(string connectionString) : base(connectionString)
        {
        }
    }

    [Obsolete("Use the ExecuteDatabaseSP class")]
    public class clsExecuteDatabaseSP : ExecuteDatabaseSP
    {
        public clsExecuteDatabaseSP(string connectionString) : base(connectionString)
        {
        }

        public clsExecuteDatabaseSP(string connectionString, int timeoutSeconds) : base(connectionString, timeoutSeconds)
        {
        }
    }

    [Obsolete("Use the FileTools class")]
    public class clsFileTools : FileTools
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public clsFileTools() : base("Unknown-Manager", 1)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="managerName">Manager name</param>
        /// <param name="debugLevel">1 results in fewer messages; 2 for additional messages, 3 for all messages</param>
        public clsFileTools(string managerName, int debugLevel) : base(managerName, debugLevel)
        {
        }
    }

    [Obsolete("Use the LinuxSystemInfo class")]
    public class clsLinuxSystemInfo : LinuxSystemInfo
    {
        public clsLinuxSystemInfo(bool limitLoggingByTimeOfDay = false) : base(limitLoggingByTimeOfDay)
        {
        }
    }

    [Obsolete("Use the OSVersionInfo class")]
    public class clsOSVersionInfo : OSVersionInfo
    {
    }

    [Obsolete("Use the PathUtils class")]
    public static class clsPathUtils
    {
        public static string CombineLinuxPaths(string path1, string path2)
        {
            return PathUtils.CombineLinuxPaths(path1, path2);
        }

        public static string CombinePathsLocalSepChar(string path1, string path2)
        {
            return PathUtils.CombinePathsLocalSepChar(path1, path2);
        }

        public static string CompactPathString(string pathToCompact, int maxLength = 40)
        {
            return PathUtils.CompactPathString(pathToCompact, maxLength);
        }

        public static List<FileInfo> FindFilesWildcard(string pathSpec, bool recurse = false)
        {
            return PathUtils.FindFilesWildcard(pathSpec, recurse);
        }

        public static bool FitsMask(string fileName, string fileMask)
        {
            return PathUtils.FitsMask(fileName, fileMask);
        }

        public static string GetParentDirectoryPath(string directoryPath, out string directoryName)
        {
            return PathUtils.GetParentDirectoryPath(directoryPath, out directoryName);
        }

        public static string PossiblyQuotePath(string filePath)
        {
            return PathUtils.PossiblyQuotePath(filePath);
        }

        public static string ReplaceFilenameInPath(string existingFilePath, string newFileName)
        {
            return PathUtils.ReplaceFilenameInPath(existingFilePath, newFileName);
        }
    }

    [Obsolete("Use the ProgRunner class")]
    public class clsProgRunner : ProgRunner
    {
    }

    [Obsolete("Use the StackTraceFormatter class")]
    public class clsStackTraceFormatter : StackTraceFormatter
    {
    }

    [Obsolete("Use the WindowsUpdateStatus class")]
    public class clsWindowsUpdateStatus : WindowsUpdateStatus
    {
    }

#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591  // Missing XML comments

}
