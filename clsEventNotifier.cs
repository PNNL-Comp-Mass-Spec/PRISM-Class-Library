using System;

[assembly: CLSCompliant(true)]
namespace PRISM
{
    /// <summary>
    /// This class implements various status events, including status, debug, error, and warning
    /// </summary>
    public abstract class clsEventNotifier
    {

        #region "Events and Event Handlers"

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

        /// <summary>
        /// Report a debug message
        /// </summary>
        /// <param name="message"></param>
        protected void OnDebugEvent(string message)
        {
            DebugEvent?.Invoke(message);
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="message"></param>
        protected void OnErrorEvent(string message)
        {
            ErrorEvent?.Invoke(message, null);
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex">Exception (allowed to be nothing)</param>
        protected void OnErrorEvent(string message, Exception ex)
        {
            ErrorEvent?.Invoke(message, ex);
        }

        /// <summary>
        /// Progress update
        /// </summary>
        /// <param name="progressMessage">Progress message</param>
        /// <param name="percentComplete">Value between 0 and 100</param>
        protected void OnProgressUpdate(string progressMessage, float percentComplete)
        {
            ProgressUpdate?.Invoke(progressMessage, percentComplete);
        }

        /// <summary>
        /// Report a status message
        /// </summary>
        /// <param name="message"></param>
        protected void OnStatusEvent(string message)
        {
            StatusEvent?.Invoke(message);
        }

        /// <summary>
        /// Report a warning
        /// </summary>
        /// <param name="message"></param>
        protected void OnWarningEvent(string message)
        {
            WarningEvent?.Invoke(message);
        }

        /// <summary>
        /// Use this method to chain events between classes
        /// </summary>
        /// <param name="oProcessingClass"></param>
        protected void RegisterEvents(clsEventNotifier oProcessingClass)
        {
            oProcessingClass.DebugEvent += OnDebugEvent;
            oProcessingClass.StatusEvent += OnStatusEvent;
            oProcessingClass.ErrorEvent += OnErrorEvent;
            oProcessingClass.WarningEvent += OnWarningEvent;
            oProcessingClass.ProgressUpdate += OnProgressUpdate;
        }

        #endregion
    }
}
