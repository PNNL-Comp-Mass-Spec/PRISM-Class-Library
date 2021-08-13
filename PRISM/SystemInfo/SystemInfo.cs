using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Class for streamlined access to system processor and memory information
    /// </summary>
    /// <remarks>Supports both Windows and Linux (uses <see cref="OSVersionInfo"/> to determine the OS at runtime)</remarks>
    public class SystemInfo
    {
        // Ignore Spelling: hyperthreading

        /// <summary>
        /// True if this is a Linux system
        /// </summary>
        public static bool IsLinux { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        static SystemInfo()
        {
            var c = new OSVersionInfo();
            if (c.GetOSVersion().IndexOf("windows", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SystemInfoObject = new WindowsSystemInfo();
                IsLinux = false;
            }
            else
            {
                SystemInfoObject = new LinuxSystemInfo();
                IsLinux = true;
            }
        }

        /// <summary>
        /// Get the implementation of <see cref="ISystemInfo"/> that is providing the data
        /// </summary>
        public static ISystemInfo SystemInfoObject { get; }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
        public static int GetCoreCount()
        {
            return SystemInfoObject.GetCoreCount();
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if Hyperthreading is enabled
        /// </remarks>
        public static int GetLogicalCoreCount()
        {
            return SystemInfoObject.GetLogicalCoreCount();
        }

        /// <summary>
        /// Report the number of processor packages on this system
        /// </summary>
        /// <returns>The number of processor packages on this computer</returns>
        public static int GetProcessorPackageCount()
        {
            return SystemInfoObject.GetProcessorPackageCount();
        }

        /// <summary>
        /// Report the number of NUMA Nodes on this system
        /// </summary>
        /// <returns>The number of NUMA Nodes on this computer</returns>
        public static int GetNumaNodeCount()
        {
            return SystemInfoObject.GetNumaNodeCount();
        }

        /// <summary>
        /// Determine the free system memory, in MB
        /// </summary>
        /// <returns>Free memory, or -1 if an error</returns>
        public static float GetFreeMemoryMB()
        {
            return SystemInfoObject.GetFreeMemoryMB();
        }

        /// <summary>
        /// Look for currently active processes
        /// </summary>
        /// <param name="lookupCommandLineInfo">When true, the process info dictionary will include the exe path and command line arguments</param>
        /// <returns>Dictionary where keys are process ID and values are ProcessInfo</returns>
        /// <remarks>Command line lookup can be slow on Windows because it uses WMI</remarks>
        public Dictionary<int, ProcessInfo> GetProcesses(bool lookupCommandLineInfo = true)
        {
            return SystemInfoObject.GetProcesses(lookupCommandLineInfo);
        }

        /// <summary>
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        public static float GetTotalMemoryMB()
        {
            return SystemInfoObject.GetTotalMemoryMB();
        }
    }
}
