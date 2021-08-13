namespace PRISM.Logging
{
    /// <summary>
    /// Interface for IEventNotifier; primary use is for interfaces whose implementations should inherit from EventNotifier
    /// </summary>
    public interface IEventNotifier
    {
        /// <summary>
        /// Debug event
        /// </summary>
        event EventNotifier.DebugEventEventHandler DebugEvent;

        /// <summary>
        /// Error event
        /// </summary>
        event EventNotifier.ErrorEventEventHandler ErrorEvent;

        /// <summary>
        /// Progress updated
        /// </summary>
        event EventNotifier.ProgressUpdateEventHandler ProgressUpdate;

        /// <summary>
        /// Status event
        /// </summary>
        event EventNotifier.StatusEventEventHandler StatusEvent;

        /// <summary>
        /// Warning event
        /// </summary>
        event EventNotifier.WarningEventEventHandler WarningEvent;

        /// <summary>
        /// Number of empty lines to write to the console before displaying a debug message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        int EmptyLinesBeforeDebugMessages { get; set; }

        /// <summary>
        /// Number of empty lines to write to the console before displaying an error message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        int EmptyLinesBeforeErrorMessages { get; set; }

        /// <summary>
        /// Number of empty lines to write to the console before displaying a status message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        int EmptyLinesBeforeStatusMessages { get; set; }

        /// <summary>
        /// Number of empty lines to write to the console before displaying a warning message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        int EmptyLinesBeforeWarningMessages { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write debug messages to the console if no listener
        /// </summary>
        bool SkipConsoleWriteIfNoDebugListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write errors to the console if no listener
        /// </summary>
        bool SkipConsoleWriteIfNoErrorListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write progress updates to the console if no listener
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
    }
}
