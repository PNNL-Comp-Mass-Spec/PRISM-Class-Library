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

        /// <summary>
        /// Debug event
        /// </summary>
        event clsEventNotifier.DebugEventEventHandler DebugEvent;

        /// <summary>
        /// Error event
        /// </summary>
        event clsEventNotifier.ErrorEventEventHandler ErrorEvent;

        /// <summary>
        /// Progress updated
        /// </summary>
        event clsEventNotifier.ProgressUpdateEventHandler ProgressUpdate;

        /// <summary>
        /// Status event
        /// </summary>
        event clsEventNotifier.StatusEventEventHandler StatusEvent;

        /// <summary>
        /// Warning event
        /// </summary>
        event clsEventNotifier.WarningEventEventHandler WarningEvent;

        #endregion

        #region "Event Viewer Properties"

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write debug messages to the console if no listener
        /// </summary>
        bool SkipConsoleWriteIfNoDebugListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write errors to the console if no listener
        /// </summary>
        bool SkipConsoleWriteIfNoErrorListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write progress updatess to the console if no listener
        /// </summary>
        bool SkipConsoleWriteIfNoProgressListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write status messages to the console if no listener
        /// </summary>
        bool SkipConsoleWriteIfNoStatusListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write warnings to the console if no listener
        /// </summary>
        bool SkipConsoleWriteIfNoWarningListener { get; set; }

        /// <summary>
        /// If true, and if an event does not have a listener, display the message at the console
        /// </summary>
        /// <remarks>Defaults to true. Silence individual event types using the SkipConsoleWrite properties</remarks>
        bool WriteToConsoleIfNoListener { get; set; }

        #endregion
    }
}
