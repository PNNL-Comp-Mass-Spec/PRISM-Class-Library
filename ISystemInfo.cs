namespace PRISM
{
    /// <summary>
    /// Interface for OS-specific classes for accessing Hardware Information
    /// </summary>
    public interface ISystemInfo
    {
        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
        int GetCoreCount();

        /// <summary>
        /// Report the number of logical cores on this system
        /// </summary>
        /// <returns>The number of logical cores on this computer</returns>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if Hyperthreading is enabled
        /// </remarks>
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
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        float GetTotalMemoryMB();

        #region "Event Viewer Events"

        event clsEventNotifier.DebugEventEventHandler DebugEvent;
        event clsEventNotifier.ErrorEventEventHandler ErrorEvent;
        event clsEventNotifier.ProgressUpdateEventHandler ProgressUpdate;
        event clsEventNotifier.StatusEventEventHandler StatusEvent;
        event clsEventNotifier.WarningEventEventHandler WarningEvent;

        #endregion

        #region "Event Viewer Properties"

        bool SkipConsoleWriteIfNoDebugListener { get; set; }
        bool SkipConsoleWriteIfNoErrorListener { get; set; }
        bool SkipConsoleWriteIfNoProgressListener { get; set; }
        bool SkipConsoleWriteIfNoStatusListener { get; set; }
        bool SkipConsoleWriteIfNoWarningListener { get; set; }

        bool WriteToConsoleIfNoListener { get; set; }

        #endregion
    }
}
