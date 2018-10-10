using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace PRISM.FileProcessor
{
    /// <summary>
    /// Old base class for ProcessFilesBase and ProcessFoldersBase
    /// </summary>
    [Obsolete("Use ProcessFilesOrDirectoriesBase instead")]
    public abstract class ProcessFilesOrFoldersBase : ProcessFilesOrDirectoriesBase
    {

        #region "Interface Functions"

        /// <summary>
        /// Log folder path (ignored if LogFilePath is rooted)
        /// </summary>
        /// <remarks>
        /// If blank, mOutputFolderPath will be used
        /// If mOutputFolderPath is also blank, the log file is created in the same folder as the executing assembly
        /// </remarks>
        [Obsolete("Use LogDirectoryPath in ProcessFilesOrDirectoriesBase")]
        public string LogFolderPath
        {
            get => LogDirectoryPath;
            set => LogDirectoryPath = value;
        }

        #endregion

        /// <summary>
        /// Returns the full path to the folder into which this application should read/write settings file information
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName</remarks>
        [Obsolete("Use GetAppDataDirectoryPath in ProcessFilesOrDirectoriesBase")]
        public static string GetAppDataFolderPath(string appName)
        {
            return GetAppDataDirectoryPath(appName);
        }

        /// <summary>
        /// Returns the full path to the folder that contains the currently executing .Exe or .Dll
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        [Obsolete("Use GetAppDirectoryPath in ProcessFilesOrDirectoriesBase")]
        public static string GetAppFolderPath()
        {
            return GetAppDirectoryPath();
        }

    }
}
