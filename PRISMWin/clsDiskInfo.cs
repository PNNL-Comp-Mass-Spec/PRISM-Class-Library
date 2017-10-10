using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PRISMWin
{
    public class clsDiskInfo
    {
        #region "GetDiskFreeSpace"

        [DllImport("Kernel32.dll", EntryPoint = "GetDiskFreeSpaceEx", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName, ref UInt64 lpFreeBytesAvailable, ref UInt64 lpTotalNumberOfBytes, ref UInt64 lpTotalNumberOfFreeBytes);

        /// <summary>
        /// Report the space on the drive used by file filePath
        /// </summary>
        /// <param name="filePath">File path to examine (the file does not need to exist)</param>
        /// <param name="freeSpaceBytes"></param>
        /// <param name="errorMessage"></param>
        /// <param name="reportFreeSpaceAvailableToUser">
        /// When true, report the free space available to the current user
        /// Otherwise, report total free space (ignoring user-based quotas)
        /// </param>
        /// <returns>True if success, false if an error</returns>
        public static bool GetDiskFreeSpace(
            string filePath,
            out long freeSpaceBytes,
            out string errorMessage,
            bool reportFreeSpaceAvailableToUser = true)
        {
            freeSpaceBytes = 0;
            errorMessage = string.Empty;

            try
            {

                var diFolderInfo = new FileInfo(filePath).Directory;
                if (diFolderInfo == null)
                {
                    errorMessage = "Unable to determine the parent directory of " + filePath;
                    freeSpaceBytes = 0;
                    return false;
                }

                // Step up the folder structure until a valid folder is found
                while (!diFolderInfo.Exists && diFolderInfo.Parent != null)
                {
                    diFolderInfo = diFolderInfo.Parent;
                }


                if (GetDiskFreeSpace(
                    diFolderInfo.FullName,
                    out var freeBytesAvailableToUser,
                    out _,
                    out var totalNumberOfFreeBytes))
                {
                    if (reportFreeSpaceAvailableToUser)
                        freeSpaceBytes = freeBytesAvailableToUser;
                    else
                        freeSpaceBytes = totalNumberOfFreeBytes;

                    return true;
                }

                errorMessage = string.Format("Error validating target drive free space " +
                                             "(GetDiskFreeSpaceEx returned false): {0}", diFolderInfo.FullName);

                return false;

            }
            catch (Exception ex)
            {
                errorMessage = "Exception validating target drive free space for " + filePath + ": " + ex.Message;
                return false;
            }

        }

        /// <summary>
        /// Determine the free space on the disk or share with the given directory
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="freeBytesAvailableToUser"></param>
        /// <param name="totalDriveCapacityBytes"></param>
        /// <param name="totalNumberOfFreeBytes"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses GetDiskFreeSpaceEx in Kernel32.dll
        /// All three out params will be 0 if an error
        /// </remarks>
        public static bool GetDiskFreeSpace(
            string directoryPath,
            out long freeBytesAvailableToUser,
            out long totalDriveCapacityBytes,
            out long totalNumberOfFreeBytes)
        {

            ulong freeAvailableUser = 0;
            ulong totalDriveCapacity = 0;
            ulong totalFree = 0;

            // Make sure directoryPath ends in a back slash
            if (!directoryPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                directoryPath += Path.DirectorySeparatorChar;

            var bResult = GetDiskFreeSpaceEx(directoryPath, ref freeAvailableUser, ref totalDriveCapacity, ref totalFree);

            if (!bResult)
            {
                freeBytesAvailableToUser = 0;
                totalDriveCapacityBytes = 0;
                totalNumberOfFreeBytes = 0;

                return false;
            }

            freeBytesAvailableToUser = ULongToLong(freeAvailableUser);
            totalDriveCapacityBytes = ULongToLong(totalDriveCapacity);
            totalNumberOfFreeBytes = ULongToLong(totalFree);

            return true;
        }

        private static long ULongToLong(ulong bytes)
        {
            if (bytes < long.MaxValue)
                return Convert.ToInt64(bytes);

            return long.MaxValue;

        }

        #endregion

    }
}
