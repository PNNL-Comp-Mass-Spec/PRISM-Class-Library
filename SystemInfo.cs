namespace PRISM
{
    /// <summary>
    /// Class for streamlined access to system processor and memory information
    /// </summary>
    /// <remarks>Supports both Windows and Linux (uses clsOSVersionInfo to determine the OS at runtime)</remarks>
    public class SystemInfo
    {
        private static readonly ISystemInfo SysInfo;

        static SystemInfo()
        {
            var c = new clsOSVersionInfo();
            if (c.GetOSVersion().ToLower().Contains("windows"))
            {
                SysInfo = new WindowsSystemInfo();
            }
            else
            {
                SysInfo = new clsLinuxSystemInfo();
            }
        }

        /// <summary>
        /// Get the implementation of <see cref="ISystemInfo"/> that is providing the data
        /// </summary>
        public static ISystemInfo SystemInfoObject => SysInfo;

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
        public static int GetCoreCount()
        {
            return SysInfo.GetCoreCount();
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
            return SysInfo.GetLogicalCoreCount();
        }

        /// <summary>
        /// Report the number of processor packages on this system
        /// </summary>
        /// <returns>The number of processor packages on this computer</returns>
        public static int GetProcessorPackageCount()
        {
            return SysInfo.GetProcessorPackageCount();
        }

        /// <summary>
        /// Report the number of NUMA Nodes on this system
        /// </summary>
        /// <returns>The number of NUMA Nodes on this computer</returns>
        public static int GetNumaNodeCount()
        {
            return SysInfo.GetNumaNodeCount();
        }

        /// <summary>
        /// Determine the free system memory, in MB
        /// </summary>
        /// <returns>Free memory, or -1 if an error</returns>
        public static float GetFreeMemoryMB()
        {
            return SysInfo.GetFreeMemoryMB();
        }

        /// <summary>
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        public static float GetTotalMemoryMB()
        {
            return SysInfo.GetTotalMemoryMB();
        }
    }
}
