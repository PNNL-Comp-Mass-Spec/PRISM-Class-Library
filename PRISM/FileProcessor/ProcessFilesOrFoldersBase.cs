using System;

namespace PRISM.FileProcessor
{
    /// <summary>
    /// Old base class for ProcessFilesBase and ProcessFoldersBase
    /// </summary>
    [Obsolete("Use ProcessFilesOrDirectoriesBase instead")]
    public abstract class ProcessFilesOrFoldersBase : ProcessFilesOrDirectoriesBase
    {
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
