using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Interface for OS-specific classes for accessing Hardware Information
    /// </summary>
    public interface ISystemInfo
    {
        // Ignore Spelling: hyperthreading

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of cores on this computer</returns>
        int GetCoreCount();

        /// <summary>
        /// Report the number of logical cores on this system
        /// </summary>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if Hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of logical cores on this computer</returns>
        int GetLogicalCoreCount();

        /// <summary>
        /// Report the number of processor packages on this system
        /// </summary>
        /// <returns>The number of processor packages on this computer</returns>
        int GetProcessorPackageCount();

        /// <summary>
        /// Report the number of NUMA Nodes on this system
        /// </summary>
        /// <returns>The number of NUMA Nodes on this computer</returns>
        int GetNumaNodeCount();

        /// <summary>
        /// Determine the free system memory, in MB
        /// </summary>
        /// <returns>Free memory, or -1 if an error</returns>
        float GetFreeMemoryMB();

        /// <summary>
        /// Look for currently active processes
        /// </summary>
        /// <remarks>Command line lookup can be slow on Windows because it uses WMI</remarks>
        /// <param name="lookupCommandLineInfo">When true, the process info dictionary will include the exe path and command line arguments</param>
        /// <returns>Dictionary where keys are process ID and values are ProcessInfo</returns>
        Dictionary<int, ProcessInfo> GetProcesses(bool lookupCommandLineInfo = true);

        /// <summary>
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        float GetTotalMemoryMB();
    }
}
