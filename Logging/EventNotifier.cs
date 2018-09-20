using System;

[assembly: CLSCompliant(true)]

namespace PRISM
{
    /// <summary>
    /// This class implements various status events, including status, debug, error, and warning
    /// </summary>
    public abstract class EventNotifier
    {

        #region "Events and Delegates"

        /// <summary>
        /// Debug event
        /// </summary>
        public event DebugEventEventHandler DebugEvent;

        /// <summary>
        /// Debug event
        /// </summary>
        /// <param name="message"></param>
        public delegate void DebugEventEventHandler(string message);

        /// <summary>
        /// Error event
        /// </summary>
        public event ErrorEventEventHandler ErrorEvent;

        /// <summary>
        /// Error event
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public delegate void ErrorEventEventHandler(string message, Exception ex);

        /// <summary>
        /// Progress updated
        /// </summary>
        public event ProgressUpdateEventHandler ProgressUpdate;

        /// <summary>
        /// Progress updated
        /// </summary>
        /// <param name="progressMessage"></param>
        /// <param name="percentComplete">Value between 0 and 100</param>
        public delegate void ProgressUpdateEventHandler(string progressMessage, float percentComplete);

        /// <summary>
        /// Status event
        /// </summary>
        public event StatusEventEventHandler StatusEvent;

        /// <summary>
        /// Status event
        /// </summary>
        /// <param name="message"></param>
        public delegate void StatusEventEventHandler(string message);

        /// <summary>
        /// Warning event
        /// </summary>
        public event WarningEventEventHandler WarningEvent;

        /// <summary>
        /// Warning event
        /// </summary>
        /// <param name="message"></param>
        public delegate void WarningEventEventHandler(string message);

        #endregion

        #region "Properties"

        /// <summary>
        /// Number of empty lines to write to the console before displaying a debug message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        public int EmptyLinesBeforeDebugMessages { get; set; } = 1;

        /// <summary>
        /// Number of empty lines to write to the console before displaying an error message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        public int EmptyLinesBeforeErrorMessages { get; set; } = 1;

        /// <summary>
        /// Number of empty lines to write to the console before displaying a status message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        public int EmptyLinesBeforeStatusMessages { get; set; } = 0;

        /// <summary>
        /// Number of empty lines to write to the console before displaying a warning message
        /// This is only applicable if WriteToConsoleIfNoListener is true and the event has no listeners
        /// </summary>
        public int EmptyLinesBeforeWarningMessages { get; set; } = 1;

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write debug messages to the console if no listener
        /// </summary>
        public bool SkipConsoleWriteIfNoDebugListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write errors to the console if no listener
        /// </summary>
        public bool SkipConsoleWriteIfNoErrorListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write progress updates to the console if no listener
        /// </summary>
        public bool SkipConsoleWriteIfNoProgressListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write status messages to the console if no listener
        /// </summary>
        public bool SkipConsoleWriteIfNoStatusListener { get; set; }

        /// <summary>
        /// If WriteToConsoleIfNoListener is true, optionally set this to true to not write warnings to the console if no listener
        /// </summary>
        public bool SkipConsoleWriteIfNoWarningListener { get; set; }

        /// <summary>
        /// If true, and if an event does not have a listener, display the message at the console
        /// </summary>
        /// <remarks>Defaults to true. Silence individual event types using the SkipConsoleWrite properties</remarks>
        public bool WriteToConsoleIfNoListener { get; set; } = true;

        // ReSharper disable UnusedMember.Global

        /// <summary>
        /// True if the Debug event has any listeners
        /// </summary>
        protected bool HasEventListenerDebug => DebugEvent != null;

        /// <summary>
        /// True if the Error event has any listeners
        /// </summary>
        protected bool HasEventListenerError => ErrorEvent != null;

        /// <summary>
        /// True if the ProgressUpdate event has any listeners
        /// </summary>
        protected bool HasEventListenerProgressUpdate => ProgressUpdate != null;

        /// <summary>
        /// True if the StatusEvent event has any listeners
        /// </summary>
        protected bool HasEventListenerStatusEvent => StatusEvent != null;

        /// <summary>
        /// True if the WarningEvent event has any listeners
        /// </summary>
        protected bool HasEventListenerWarningEvent => WarningEvent != null;

        // ReSharper restore UnusedMember.Global

        #endregion

        #region "Event Handlers"

        /// <summary>
        /// Report a debug message
        /// </summary>
        /// <param name="message">Debug message</param>
        protected void OnDebugEvent(string message)
        {
            if (DebugEvent == null && WriteToConsoleIfNoListener && !SkipConsoleWriteIfNoDebugListener)
            {
                ConsoleMsgUtils.ShowDebug(message, emptyLinesBeforeMessage: EmptyLinesBeforeDebugMessages);
            }

            DebugEvent?.Invoke(message);
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="message">Error message</param>
        protected void OnErrorEvent(string message)
        {
            if (ErrorEvent == null && WriteToConsoleIfNoListener && !SkipConsoleWriteIfNoErrorListener)
            {
                ConsoleMsgUtils.ShowError(message, false, false, EmptyLinesBeforeErrorMessages);
            }

            ErrorEvent?.Invoke(message, null);
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="ex">Exception (allowed to be nothing)</param>
        protected void OnErrorEvent(string message, Exception ex)
        {
            if (ErrorEvent == null && WriteToConsoleIfNoListener && !SkipConsoleWriteIfNoErrorListener)
            {
                ConsoleMsgUtils.ShowError(message, ex, false, false, EmptyLinesBeforeErrorMessages);
            }

            ErrorEvent?.Invoke(message, ex);
        }

        /// <summary>
        /// Progress update
        /// </summary>
        /// <param name="progressMessage">Progress message</param>
        /// <param name="percentComplete">Value between 0 and 100</param>
        protected void OnProgressUpdate(string progressMessage, float percentComplete)
        {
            if (ProgressUpdate == null && WriteToConsoleIfNoListener && !SkipConsoleWriteIfNoProgressListener)
            {
                Console.WriteLine("{0:F2}%: {1}", percentComplete, progressMessage);
            }

            ProgressUpdate?.Invoke(progressMessage, percentComplete);
        }

        /// <summary>
        /// Report a status message
        /// </summary>
        /// <param name="message"></param>
        protected void OnStatusEvent(string message)
        {
            if (StatusEvent == null && WriteToConsoleIfNoListener && !SkipConsoleWriteIfNoStatusListener)
            {
                ConsoleMsgUtils.ConsoleWriteEmptyLines(EmptyLinesBeforeStatusMessages);
                Console.WriteLine(message);
            }

            StatusEvent?.Invoke(message);
        }

        /// <summary>
        /// Report a warning
        /// </summary>
        /// <param name="message"></param>
        protected void OnWarningEvent(string message)
        {
            if (WarningEvent == null && WriteToConsoleIfNoListener && !SkipConsoleWriteIfNoWarningListener)
            {
                ConsoleMsgUtils.ShowWarning(message, EmptyLinesBeforeWarningMessages);
            }

            WarningEvent?.Invoke(message);
        }

        #endregion

        /// <summary>
        /// Use this method to chain events between classes
        /// </summary>
        /// <param name="sourceClass"></param>
        protected void RegisterEvents(EventNotifier sourceClass)
        {
            sourceClass.DebugEvent += OnDebugEvent;
            sourceClass.StatusEvent += OnStatusEvent;
            sourceClass.ErrorEvent += OnErrorEvent;
            sourceClass.WarningEvent += OnWarningEvent;
            sourceClass.ProgressUpdate += OnProgressUpdate;
        }

    }
}