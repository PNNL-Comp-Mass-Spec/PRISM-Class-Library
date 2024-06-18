using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local

namespace PRISMWin
{
    /// <summary>
    /// Utility methods that will return the processes using a file
    /// </summary>
    /// <remarks>https://stackoverflow.com/questions/1304/how-to-check-for-file-lock</remarks>
    /// <remarks>https://blogs.msdn.microsoft.com/oldnewthing/20120217-00/?p=8283</remarks>
    public static class FileInUseUtils
    {
        // Ignore Spelling: App, utils

        // ReSharper disable InconsistentNaming

        // ReSharper disable once IdentifierTypo

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;

            public static implicit operator long(FILETIME time)
            {
                return (long)((ulong)time.dwHighDateTime << 32 | time.dwLowDateTime);
            }

            public static implicit operator DateTime(FILETIME time)
            {
                return DateTime.FromFileTime(time);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public FILETIME ProcessStartTime;
        }

        private const int RmRebootReasonNone = 0;
        private const int CCH_RM_MAX_APP_NAME = 255;
        private const int CCH_RM_MAX_SVC_NAME = 63;

        private enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;

            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint pSessionHandle,
                                              UInt32 nFiles,
                                              string[] rgsFilenames,
                                              UInt32 nApplications,
                                              [In] RM_UNIQUE_PROCESS[] rgApplications,
                                              UInt32 nServices,
                                              string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint dwSessionHandle,
                                    out uint pnProcInfoNeeded,
                                    ref uint pnProcInfo,
                                    [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
                                    ref uint lpdwRebootReasons);

        /// <summary>
        /// Find out what process(es) have a lock on the specified file
        /// </summary>
        /// <remarks>See also:
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa373661(v=vs.85).aspx
        /// http://wyupdate.googlecode.com/svn-history/r401/trunk/frmFilesInUse.cs (no copyright in code at time of viewing)
        /// </remarks>
        /// <param name="paths">Full Path(s) of the file(s)</param>
        /// <param name="checkProcessStartTime">If true, tries to read and compare process start times</param>
        /// <returns>Processes locking the file</returns>
        public static List<Process> WhoIsLocking(string[] paths, bool checkProcessStartTime)
        {
            var key = Guid.NewGuid().ToString();
            var processes = new List<Process>();

            var res = RmStartSession(out var handle, 0, key);

            if (res != 0)
                throw new Exception("Could not begin restart session.  Unable to determine file locker.");

            try
            {
                const int ERROR_MORE_DATA = 234;
                uint pnProcInfo = 0,
                     lpdwRebootReasons = RmRebootReasonNone;

                var resources = paths; // Just checking on one resource

                res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);

                if (res != 0)
                    throw new Exception("Could not register resource.");

                // Note: there's a race condition here
                //  The first call to RmGetList() returns the total number of process.
                //  However, when we call RmGetList() again to get the actual processes this number may have increased.
                res = RmGetList(handle, out var pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

                if (res == ERROR_MORE_DATA)
                {
                    // Create an array to store the process results
                    var processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    // Get the list
                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);

                    if (res == 0)
                    {
                        processes = new List<Process>((int)pnProcInfo);

                        // Enumerate the results and add them to the list to be returned
                        for (var i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                var process = Process.GetProcessById(processInfo[i].Process.dwProcessId);
                                var add = true;

                                if (checkProcessStartTime)
                                {
                                    // Check the process start time to ensure this is the same process
                                    // There is minor possibility that the process id that was returned has been recycled
                                    try
                                    {
                                        add = process.StartTime <= processInfo[i].Process.ProcessStartTime;
                                    }
                                    catch
                                    {
                                        // Possibility of win32 exception, particularly 'access denied'. Assume it is the same process
                                    }
                                }

                                if (add)
                                {
                                    processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
                                }
                            }
                            // catch the error -- in case the process is no longer running
                            catch (ArgumentException) { }
                        }
                    }
                    else
                    {
                        throw new Exception("Could not list processes locking resource.");
                    }
                }
                else if (res != 0)
                {
                    throw new Exception("Could not list processes locking resource. Failed to get size of result.");
                }
            }
            finally
            {
                RmEndSession(handle);
            }

            return processes;
        }

        /// <summary>
        /// Find out what process(es) have a lock on the specified file
        /// </summary>
        /// <remarks>See also:
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa373661(v=vs.85).aspx
        /// http://wyupdate.googlecode.com/svn-history/r401/trunk/frmFilesInUse.cs (no copyright in code at time of viewing)
        /// </remarks>
        /// <param name="path">Full Path of the file</param>
        /// <param name="checkProcessStartTime">If true, tries to read and compare process start times</param>
        /// <returns>Processes locking the file</returns>
        public static List<Process> WhoIsLocking(string path, bool checkProcessStartTime = false)
        {
            return WhoIsLocking(new[] { path }, checkProcessStartTime);
        }

        /// <summary>
        /// Find out what process(es) have a lock on the specified file
        /// </summary>
        /// <remarks>See also:
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa373661(v=vs.85).aspx
        /// http://wyupdate.googlecode.com/svn-history/r401/trunk/frmFilesInUse.cs (no copyright in code at time of viewing)
        /// </remarks>
        /// <param name="paths">Full Path(s) of the file(s)</param>
        /// <returns>Processes locking the file</returns>
        public static List<Process> WhoIsLocking(params string[] paths)
        {
            return WhoIsLocking(paths, false);
        }

        /// <summary>
        /// Find out what process(es) have a lock on files in the specified directory
        /// </summary>
        /// <param name="path">Full Path of the directory</param>
        /// <param name="checkProcessStartTime">If true, tries to read and compare process start times</param>
        /// <returns>Processes locking files in the directory</returns>
        public static List<Process> WhoIsLockingDirectory(string path, bool checkProcessStartTime = false)
        {
            if (!Directory.Exists(path))
            {
                return WhoIsLocking(new[] { path }, checkProcessStartTime);
            }

            var filePaths = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

            return WhoIsLocking(filePaths, checkProcessStartTime);
        }
    }
}
