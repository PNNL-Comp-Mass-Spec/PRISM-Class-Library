using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PRISMWin
{
    /// <summary>
    /// Utility functions that will return the processes using a file; Requires Windows Vista or newer.
    /// </summary>
    /// <remarks>https://stackoverflow.com/questions/1304/how-to-check-for-file-lock</remarks>
    /// <remarks>https://blogs.msdn.microsoft.com/oldnewthing/20120217-00/?p=8283</remarks>
    public static class FileInUseUtils
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FILETIME
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
        struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public FILETIME ProcessStartTime;
        }

        const int RmRebootReasonNone = 0;
        const int CCH_RM_MAX_APP_NAME = 255;
        const int CCH_RM_MAX_SVC_NAME = 63;

        enum RM_APP_TYPE
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
        struct RM_PROCESS_INFO
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
        static extern int RmRegisterResources(uint pSessionHandle,
                                              UInt32 nFiles,
                                              string[] rgsFilenames,
                                              UInt32 nApplications,
                                              [In] RM_UNIQUE_PROCESS[] rgApplications,
                                              UInt32 nServices,
                                              string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
        static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll")]
        static extern int RmGetList(uint dwSessionHandle,
                                    out uint pnProcInfoNeeded,
                                    ref uint pnProcInfo,
                                    [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
                                    ref uint lpdwRebootReasons);

        /// <summary>
        /// Find out what process(es) have a lock on the specified file.
        /// </summary>
        /// <param name="paths">Full Path(s) of the file(s).</param>
        /// <returns>Processes locking the file</returns>
        /// <remarks>See also:
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa373661(v=vs.85).aspx
        /// http://wyupdate.googlecode.com/svn-history/r401/trunk/frmFilesInUse.cs (no copyright in code at time of viewing)
        /// </remarks>
        public static List<Process> WhoIsLocking(params string[] paths)
        {
            uint handle;
            string key = Guid.NewGuid().ToString();
            List<Process> processes = new List<Process>();

            int res = RmStartSession(out handle, 0, key);

            if (res != 0)
                throw new Exception("Could not begin restart session.  Unable to determine file locker.");

            try
            {
                const int ERROR_MORE_DATA = 234;
                uint pnProcInfoNeeded = 0,
                     pnProcInfo = 0,
                     lpdwRebootReasons = RmRebootReasonNone;

                string[] resources = paths; // Just checking on one resource.

                res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);

                if (res != 0)
                    throw new Exception("Could not register resource.");

                //Note: there's a race condition here -- the first call to RmGetList() returns
                //      the total number of process. However, when we call RmGetList() again to get
                //      the actual processes this number may have increased.
                res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

                if (res == ERROR_MORE_DATA)
                {
                    // Create an array to store the process results
                    RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    // Get the list
                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);

                    if (res == 0)
                    {
                        processes = new List<Process>((int)pnProcInfo);

                        // Enumerate all of the results and add them to the list to be returned
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                var process = Process.GetProcessById(processInfo[i].Process.dwProcessId);
                                // Check the process start time to ensure this is the same process
                                // There is minor possibility that the process id that was returned has been recycled.
                                if (process.StartTime <= processInfo[i].Process.ProcessStartTime)
                                {
                                    processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
                                }
                            }
                            // catch the error -- in case the process is no longer running
                            catch (ArgumentException) { }
                        }
                    }
                    else
                        throw new Exception("Could not list processes locking resource.");
                }
                else if (res != 0)
                    throw new Exception("Could not list processes locking resource. Failed to get size of result.");
            }
            finally
            {
                RmEndSession(handle);
            }

            return processes;
        }

        /// <summary>
        /// Find out what process(es) have a lock on files in the specified directory.
        /// </summary>
        /// <param name="path">Full Path of the directory.</param>
        /// <returns>Processes locking files in the directory</returns>
        public static List<Process> WhoIsLockingDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                return WhoIsLocking(path);
            }

            var filePaths = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

            return WhoIsLocking(filePaths);
        }
    }
}
