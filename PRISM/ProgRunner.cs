using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PRISM.Logging;

namespace PRISM
{
    /// <summary>
    /// This class runs a single program as an external process and monitors it with an internal thread
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class ProgRunner : EventNotifier
    {
        #region "Constants and Enums"

        /// <summary>
        /// Default monitoring interval, in milliseconds
        /// </summary>
        public const int DEFAULT_MONITOR_INTERVAL_MSEC = 5000;

        /// <summary>
        /// Minimum monitoring interval, in milliseconds
        /// </summary>
        public const int MINIMUM_MONITOR_INTERVAL_MSEC = 250;

        /// <summary>
        /// ProgRunner states
        /// </summary>
        public enum States
        {
            /// <summary>
            /// Not Monitoring
            /// </summary>
            NotMonitoring,
            /// <summary>
            /// Monitoring
            /// </summary>
            Monitoring,
            /// <summary>
            /// Waiting
            /// </summary>
            Waiting,
            /// <summary>
            /// Cleaning up
            /// </summary>
            CleaningUp,
            /// <summary>
            /// Initializing
            /// </summary>
            Initializing,
            /// <summary>
            /// Starting the process
            /// </summary>
            StartingProcess
        }
        #endregion

        #region "Class wide Variables"

        /// <summary>
        /// Log class
        /// </summary>
        private BaseLogger mLogger;

#pragma warning disable 618
        /// <summary>
        /// Interface used for logging exceptions
        /// </summary>
        [Obsolete("Use mLogger (typically a FileLogger)")]
        private ILogger mExceptionLogger;

        /// <summary>
        /// Interface used for logging errors and health related messages
        /// </summary>
        [Obsolete("Use mLogger (typically a FileLogger)")]
        private ILogger mEventLogger;
#pragma warning restore 618

        /// <summary>
        /// Used to start and monitor the external program
        /// </summary>
        private readonly Process m_Process = new();

        /// <summary>
        /// Thread cancellation token
        /// </summary>
        private CancellationTokenSource m_CancellationToken;

        /// <summary>
        /// Flag that tells internal thread to quit monitoring external program and exit
        /// </summary>
        private bool m_doCleanup;

        /// <summary>
        /// The interval, in milliseconds, for monitoring the thread to wake up and check m_doCleanup
        /// </summary>
        /// <remarks>Default is 5000 msec</remarks>
        private int m_monitorInterval;

        /// <summary>
        /// Exit code returned by completed process
        /// </summary>
        /// <remarks>Initially set to -123454321</remarks>
        private int m_ExitCode;

        private StreamWriter m_ConsoleOutputStreamWriter;

        /// <summary>
        /// Caches the text written to the Console by the external program
        /// </summary>
        private StringBuilder m_CachedConsoleOutput;

        /// <summary>
        /// Caches the text written to the Error buffer by the external program
        /// </summary>
        private StringBuilder m_CachedConsoleError;

        #endregion

        #region "Events"

        /// <summary>
        /// This event is raised at regular intervals while monitoring the program
        /// </summary>
        /// <remarks>Raised every m_monitorInterval milliseconds</remarks>
        public event ProgChangedEventHandler ProgChanged;

        /// <summary>
        /// Progress changed event delegate
        /// </summary>
        /// <param name="obj"></param>
        public delegate void ProgChangedEventHandler(ProgRunner obj);

        /// <summary>
        /// This event is raised when new text is written to the console
        /// </summary>
        public event ConsoleOutputEventEventHandler ConsoleOutputEvent;

        /// <summary>
        /// Console output event delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void ConsoleOutputEventEventHandler(string message);

        /// <summary>
        /// This event is raised when the external program writes text to the console's error stream
        /// </summary>
        public event ConsoleErrorEventEventHandler ConsoleErrorEvent;

        /// <summary>
        /// Console error event delegate
        /// </summary>
        /// <param name="message"></param>
        public delegate void ConsoleErrorEventEventHandler(string message);

        #endregion

        #region "Properties"

        /// <summary>
        /// Arguments supplied to external program when it is run
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// Text written to the Console by the external program (including carriage returns)
        /// </summary>
        public string CachedConsoleOutput
        {
            get
            {
                if (m_CachedConsoleOutput == null)
                {
                    return string.Empty;
                }

                return m_CachedConsoleOutput.ToString();
            }
        }

        /// <summary>
        /// Any text written to the Error buffer by the external program
        /// </summary>
        public string CachedConsoleError
        {
            get
            {
                if (m_CachedConsoleError == null)
                {
                    return string.Empty;
                }

                return m_CachedConsoleError.ToString();
            }
        }

        /// <summary>
        /// When true then will cache the text the external program writes to the console
        /// Can retrieve using the CachedConsoleOutput ReadOnly property
        /// Will also fire event ConsoleOutputEvent as new text is written to the console
        /// </summary>
        /// <remarks>If this is true, no window will be shown, even if CreateNoWindow=False</remarks>
        public bool CacheStandardOutput { get; set; }

        /// <summary>
        /// When true, the program name and command line arguments will be added to the top of the console output file
        /// </summary>
        public bool ConsoleOutputFileIncludesCommandLine { get; set; }

        /// <summary>
        /// File path to which the console output will be written if WriteConsoleOutputToFile is true
        /// If blank, the file path will be auto-defined to use the WorkDir when program execution starts
        /// </summary>
        public string ConsoleOutputFilePath { get; set; }

        /// <summary>
        /// Determine if window should be displayed
        /// Will be forced to True if CacheStandardOutput = True
        /// </summary>
        public bool CreateNoWindow { get; set; }

        /// <summary>
        /// When true, echoes, in real time, text written to the Console by the external program
        /// Ignored if CreateNoWindow = False
        /// </summary>
        public bool EchoOutputToConsole { get; set; }

        /// <summary>
        /// Exit code when process completes
        /// </summary>
        public int ExitCode => m_ExitCode;

        /// <summary>
        /// How often (milliseconds) internal monitoring thread checks status of external program
        /// </summary>
        /// <remarks>Minimum allowed value is 100 milliseconds</remarks>
        public int MonitoringInterval
        {
            get => m_monitorInterval;
            set
            {
                if (value < MINIMUM_MONITOR_INTERVAL_MSEC)
                    value = MINIMUM_MONITOR_INTERVAL_MSEC;
                m_monitorInterval = value;
            }
        }

        /// <summary>
        /// Name of this program runner
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// When true, raises event ProgChanged
        /// </summary>
        public bool NotifyOnEvent { get; set; }

        /// <summary>
        /// When true, and if mLogger or mExceptionLogger is defined, re-throws the exception
        /// </summary>
        public bool NotifyOnException { get; set; }

        /// <summary>
        /// Process id of the currently running external program's process
        /// </summary>
        public int PID { get; private set; }

        /// <summary>
        /// External program that the program runner will execute
        /// This is the full path to the program file
        /// </summary>
        public string Program { get; set; }

        /// <summary>
        /// Whether the program runner will restart the external program after it exits
        /// </summary>
        public bool Repeat { get; set; }

        /// <summary>
        /// Time (in seconds) that the program runner waits to restart the external program after it exits
        /// </summary>
        public double RepeatHoldOffTime { get; set; }

        /// <summary>
        /// Current state of the program runner (as number)
        /// </summary>
        public States State { get; private set; } = States.NotMonitoring;

        /// <summary>
        /// Current state of the program runner (as descriptive name)
        /// </summary>
        public string StateName
        {
            get
            {
                return State switch
                {
                    States.NotMonitoring => "not monitoring",
                    States.Monitoring => "monitoring",
                    States.Waiting => "waiting to restart",
                    States.CleaningUp => "cleaning up",
                    States.Initializing => "initializing",
                    States.StartingProcess => "starting",
                    _ => "???",
                };
            }
        }

        /// <summary>
        /// Window style to use when CreateNoWindow is False
        /// </summary>
        public ProcessWindowStyle WindowStyle { get; set; }

        /// <summary>
        /// Working directory for process execution
        /// Not necessarily the same as the directory that contains the program we're running
        /// </summary>
        public string WorkDir { get; set; }

        /// <summary>
        /// When true then will write the standard output to a file in real-time
        /// Will also fire event ConsoleOutputEvent as new text is written to the console
        /// Define the path to the file using property ConsoleOutputFilePath; if not defined, the file will be created in the WorkDir
        /// </summary>
        /// <remarks>If this is true, no window will be shown, even if CreateNoWindow=False</remarks>
        public bool WriteConsoleOutputToFile { get; set; }

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        public ProgRunner()
        {
            WorkDir = string.Empty;
            CreateNoWindow = false;
            m_ExitCode = -123454321;
            // Unreasonable value
            m_monitorInterval = DEFAULT_MONITOR_INTERVAL_MSEC;
            NotifyOnEvent = true;
            NotifyOnException = true;
            CacheStandardOutput = false;
            EchoOutputToConsole = true;
            WriteConsoleOutputToFile = false;
            ConsoleOutputFileIncludesCommandLine = true;
            ConsoleOutputFilePath = string.Empty;
        }

        /// <summary>
        /// Clears any console output text that is currently cached
        /// </summary>
        public void ClearCachedConsoleOutput()
        {
            if (m_CachedConsoleOutput == null)
            {
                m_CachedConsoleOutput = new StringBuilder();
            }
            else
            {
                m_CachedConsoleOutput.Clear();
            }
        }

        /// <summary>
        /// Clears any console error text that is currently cached
        /// </summary>
        public void ClearCachedConsoleError()
        {
            if (m_CachedConsoleError == null)
            {
                m_CachedConsoleError = new StringBuilder();
            }
            else
            {
                m_CachedConsoleError.Clear();
            }
        }

        /// <summary>
        /// Asynchronously handles the error stream from m_Process
        /// </summary>
        private void ConsoleErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Handle the error data

            if (!string.IsNullOrEmpty(outLine.Data))
            {
                // Send to the console output stream to maximize the chance of somebody noticing this error
                ConsoleOutputHandler(sendingProcess, outLine);

                ConsoleErrorEvent?.Invoke(outLine.Data);

                m_CachedConsoleError?.Append(outLine.Data);
            }
        }

        /// <summary>
        /// Asynchronously handles the console output from m_Process
        /// </summary>
        private void ConsoleOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (outLine.Data == null)
                return;

            ConsoleOutputEvent?.Invoke(outLine.Data);

            if (EchoOutputToConsole)
            {
                Console.WriteLine(outLine.Data);
            }

            if (CacheStandardOutput)
            {
                // Add the text to the collected output
                m_CachedConsoleOutput.AppendLine(outLine.Data);
            }

            if (WriteConsoleOutputToFile && m_ConsoleOutputStreamWriter != null)
            {
                // Write the standard output to the console output file
                try
                {
                    m_ConsoleOutputStreamWriter.WriteLine(outLine.Data);
                }
                catch (Exception)
                {
                    // Another thread is likely trying to write to a closed file
                    // Ignore errors here
                }
            }
        }

        /// <summary>
        /// Force the garbage collector to run, waiting up to 1 second for it to finish
        /// </summary>
        public static void GarbageCollectNow()
        {
            const int maxWaitTimeMSec = 1000;
            GarbageCollectNow(maxWaitTimeMSec);
        }

        /// <summary>
        /// Force the garbage collector to run
        /// </summary>
        /// <param name="maxWaitTimeMSec"></param>
        public static void GarbageCollectNow(int maxWaitTimeMSec)
        {
            const int THREAD_SLEEP_TIME_MSEC = 100;

            if (maxWaitTimeMSec < 100)
                maxWaitTimeMSec = 100;
            if (maxWaitTimeMSec > 5000)
                maxWaitTimeMSec = 5000;

            Thread.Sleep(100);

            try
            {
                var gcThread = new Thread(GarbageCollectWaitForGC);
                gcThread.Start();

                var totalThreadWaitTimeMsec = 0;
                while (gcThread.IsAlive && totalThreadWaitTimeMsec < maxWaitTimeMSec)
                {
                    Thread.Sleep(THREAD_SLEEP_TIME_MSEC);
                    totalThreadWaitTimeMsec += THREAD_SLEEP_TIME_MSEC;
                }

#if NETFRAMEWORK
                // Thread.Abort() Throws a "PlatformNotSupportedException" on all .NET Standard/.NET Core platforms; warning as of .NET 5.0
                if (gcThread.IsAlive)
                    gcThread.Abort();
#endif
            }
            catch
            {
                // Ignore errors here
            }
        }

        private static void GarbageCollectWaitForGC()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Returns the full path to the console output file that will be created if WriteConsoleOutputToFile is true
        /// </summary>
        /// <remarks>Before calling this function, define WorkDir (working directory) and Program (full path to the .exe to run)</remarks>
        public string GetConsoleOutputFilePath()
        {
            string consoleOutputFileName;
            if (string.IsNullOrEmpty(Program))
            {
                consoleOutputFileName = "ProgRunner_ConsoleOutput.txt";
            }
            else
            {
                consoleOutputFileName = Path.GetFileNameWithoutExtension(Program) + "_ConsoleOutput.txt";
            }

            if (string.IsNullOrEmpty(WorkDir))
            {
                return consoleOutputFileName;
            }

            return Path.Combine(WorkDir, consoleOutputFileName);
        }

        /// <summary>
        /// Attempt to re-join the thread running the external process
        /// </summary>
        [Obsolete("This method is no longer valid due to a change in how threads are started")]
        public void JoinThreadNow()
        {
        }

        /// <summary>
        /// Associate a logger with this class
        /// </summary>
        public void RegisterEventLogger(BaseLogger logger)
        {
            mLogger = logger;
        }

        /// <summary>
        /// Associate an event logger with this class
        /// </summary>
        [Obsolete("Use RegisterEventLogger that takes a BaseLogger (typically a FileLogger)")]
        public void RegisterEventLogger(ILogger logger)
        {
            mEventLogger = logger;
        }

        /// <summary>
        /// Sets the name of the exception logger
        /// </summary>
        [Obsolete("Use RegisterEventLogger that takes a BaseLogger (typically a FileLogger)")]
        public void RegisterExceptionLogger(ILogger logger)
        {
            mExceptionLogger = logger;
        }

        private void RaiseConditionalProgChangedEvent(ProgRunner obj)
        {
            if (NotifyOnEvent)
            {
                var msg = "Raising ProgChanged event for " + obj.Name;
#pragma warning disable 618
                mEventLogger?.PostEntry(msg, logMsgType.logHealth, true);
#pragma warning restore 618
                mLogger?.Debug(msg);

                ProgChanged?.Invoke(obj);
            }
        }

        /// <summary>
        /// Pause program execution for the specific number of milliseconds (maximum 10 seconds)
        /// </summary>
        /// <param name="sleepTimeMsec">Value between 10 and 10000 (i.e. between 10 msec and 10 seconds)</param>
        public static void SleepMilliseconds(int sleepTimeMsec)
        {
            if (sleepTimeMsec < 10)
                sleepTimeMsec = 10;
            else if (sleepTimeMsec > 10000)
                sleepTimeMsec = 10000;

            Task.Delay(sleepTimeMsec).Wait();

            // Option 2:
            // using (EventWaitHandle tempEvent = new ManualResetEvent(false))
            // {
            //     tempEvent.WaitOne(TimeSpan.FromMilliseconds(sleepTimeMsec));
            // }

            // Option 3, though this will be deprecated in .NET Standard
            // System.Threading.Thread.Sleep(sleepTimeMsec);
        }

        /// <summary>
        /// Pause program execution for the specific number of milliseconds (maximum 10 seconds)
        /// </summary>
        /// <param name="sleepTimeMsec">Value between 10 and 10000 (i.e. between 10 msec and 10 seconds)</param>
        public static async Task SleepMillisecondsAsync(int sleepTimeMsec)
        {
            if (sleepTimeMsec < 10)
                sleepTimeMsec = 10;
            else if (sleepTimeMsec > 10000)
                sleepTimeMsec = 10000;

            await Task.Delay(TimeSpan.FromMilliseconds(sleepTimeMsec)).ConfigureAwait(false);
        }

        /// <summary>
        /// Start program as external process and monitor its state
        /// </summary>
        private void StartProcess(object obj)
        {
            var token = (CancellationToken)obj;

            bool standardOutputRedirected;

            // Set up parameters for external process
            m_Process.StartInfo.FileName = Program;
            m_Process.StartInfo.WorkingDirectory = WorkDir;
            m_Process.StartInfo.Arguments = Arguments;
            m_Process.StartInfo.CreateNoWindow = CreateNoWindow;

            if (m_Process.StartInfo.CreateNoWindow)
            {
                m_Process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                m_Process.StartInfo.WindowStyle = WindowStyle;
            }

            if (m_Process.StartInfo.CreateNoWindow || CacheStandardOutput || WriteConsoleOutputToFile)
            {
                m_Process.StartInfo.UseShellExecute = false;
                m_Process.StartInfo.RedirectStandardOutput = true;
                m_Process.StartInfo.RedirectStandardError = true;
                standardOutputRedirected = true;
            }
            else
            {
                m_Process.StartInfo.UseShellExecute = true;
                m_Process.StartInfo.RedirectStandardOutput = false;
                standardOutputRedirected = false;
            }

            if (!File.Exists(m_Process.StartInfo.FileName))
            {
                ThrowConditionalException(new Exception("Process filename " + m_Process.StartInfo.FileName + " not found."), "ProgRunner m_ProgName was not set correctly.");
                State = States.NotMonitoring;
                return;
            }

            if (!Directory.Exists(m_Process.StartInfo.WorkingDirectory))
            {
                ThrowConditionalException(new Exception("Process working directory " + m_Process.StartInfo.WorkingDirectory + " not found."), "ProgRunner m_WorkDir was not set correctly.");
                State = States.NotMonitoring;
                return;
            }

            if (standardOutputRedirected)
            {
                // Add event handlers to asynchronously read the console output and error stream
                m_Process.OutputDataReceived += ConsoleOutputHandler;
                m_Process.ErrorDataReceived += ConsoleErrorHandler;

                if (WriteConsoleOutputToFile)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(ConsoleOutputFilePath))
                        {
                            // Need to auto-define m_ConsoleOutputFilePath
                            ConsoleOutputFilePath = GetConsoleOutputFilePath();
                        }

                        var consoleOutStream = new FileStream(ConsoleOutputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        m_ConsoleOutputStreamWriter = new StreamWriter(consoleOutStream)
                        {
                            AutoFlush = true
                        };

                        if (ConsoleOutputFileIncludesCommandLine)
                        {
                            m_ConsoleOutputStreamWriter.WriteLine(Path.GetFileName(Program) + " " + Arguments.Trim());
                            m_ConsoleOutputStreamWriter.WriteLine(new string('-', 80));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Report the error, but continue processing
                        ThrowConditionalException(ex, "Caught exception while trying to create the console output file, " + ConsoleOutputFilePath);
                    }
                }
            }

            // Make sure the cached output StringBuilders are initialized
            ClearCachedConsoleOutput();
            ClearCachedConsoleError();

            while (true)
            {
                // Start the program as an external process
                //
                try
                {
                    State = States.StartingProcess;
                    m_Process.Start();
                }
                catch (Exception ex)
                {
                    var errorMsg = "Problem starting process. Parameters: " +
                              Path.Combine(m_Process.StartInfo.WorkingDirectory, m_Process.StartInfo.FileName) + " " +
                              m_Process.StartInfo.Arguments;

                    ThrowConditionalException(ex, errorMsg);
                    m_ExitCode = -1234567;
                    State = States.NotMonitoring;
                    return;
                }

                try
                {
                    State = States.Monitoring;
                    PID = m_Process.Id;
                }
                catch (Exception)
                {
                    // Exception looking up the process ID
                    PID = 999999999;
                }

                if (standardOutputRedirected)
                {
                    try
                    {
                        // Initiate asynchronously reading the console output and error streams

                        m_Process.BeginOutputReadLine();
                        m_Process.BeginErrorReadLine();
                    }
                    catch (Exception)
                    {
                        // Exception attaching the standard output
                        standardOutputRedirected = false;
                    }
                }

                RaiseConditionalProgChangedEvent(this);

                // Wait for program to exit (loop on interval)
                //
                // We wait until the external process exits or
                // the class is instructed to stop monitoring the process (m_doCleanup = true)
                //

                while (!m_doCleanup)
                {
                    if (m_monitorInterval < MINIMUM_MONITOR_INTERVAL_MSEC)
                        m_monitorInterval = MINIMUM_MONITOR_INTERVAL_MSEC;

                    try
                    {
                        m_Process.WaitForExit(m_monitorInterval);
                        if (m_Process.HasExited)
                            break;

                        if (token.IsCancellationRequested)
                        {
                            m_Process.Kill();
                        }
                    }
                    catch (Exception)
                    {
                        // Exception calling .WaitForExit or .HasExited; most likely the process has exited
                        break;
                    }
                }

                // Need to free up resources used to keep
                // track of the external process
                PID = 0;

                try
                {
                    m_ExitCode = m_Process.ExitCode;
                }
                catch (Exception)
                {
                    // Exception looking up ExitCode; most likely the process has exited
                    m_ExitCode = 0;
                }

                try
                {
                    m_Process.Dispose();
                }
                catch (Exception)
                {
                    // Exception closing the process; ignore
                }

                var msg = "Process " + Name + " terminated with exit code " + m_ExitCode;
#pragma warning disable 618
                mEventLogger?.PostEntry(msg, logMsgType.logHealth, true);
#pragma warning restore 618
                mLogger?.Debug(msg);

                if (m_CachedConsoleError?.Length > 0)
                {
                    var errorMsg = "Cached error text for process " + Name + ": " + m_CachedConsoleError;
#pragma warning disable 618
                    mEventLogger?.PostEntry(errorMsg, logMsgType.logError, true);
#pragma warning restore 618
                    mLogger?.Error(errorMsg);
                }

                if (m_ConsoleOutputStreamWriter != null)
                {
                    // Give the other threads time to write any additional info to m_ConsoleOutputStreamWriter
                    GarbageCollectNow();
                    m_ConsoleOutputStreamWriter.Flush();
                    m_ConsoleOutputStreamWriter.Dispose();
                }

                // Decide whether or not to repeat starting
                // the external process again, or quit
                if (Repeat && !m_doCleanup)
                {
                    // Repeat starting the process
                    // after waiting for minimum hold off time interval
                    State = States.Waiting;

                    RaiseConditionalProgChangedEvent(this);

                    var holdoffMilliseconds = Convert.ToInt32(RepeatHoldOffTime * 1000);
                    SleepMilliseconds(holdoffMilliseconds);

                    State = States.Monitoring;
                }
                else
                {
                    // Don't repeat starting the process - just quit
                    State = States.NotMonitoring;
                    RaiseConditionalProgChangedEvent(this);
                    break;
                }
            }
        }

        /// <summary>
        /// Creates a new thread and starts code that runs and monitors a program in it
        /// </summary>
        public void StartAndMonitorProgram()
        {
            if (State != States.NotMonitoring)
                return;

            State = States.Initializing;
            m_doCleanup = false;

            m_CancellationToken = new CancellationTokenSource();

            // Arrange to start the program as an external process
            // and monitor it in a separate internal thread
            try
            {
                ThreadPool.QueueUserWorkItem(StartProcess, m_CancellationToken.Token);
            }
            catch (Exception ex)
            {
                ThrowConditionalException(ex, "Caught exception while trying to start thread.");
            }
        }

        /// <summary>
        /// Return True if the program is starting or running
        /// </summary>
        protected bool StartingOrMonitoring()
        {
            return State == States.Initializing || State == States.StartingProcess || State == States.Monitoring;
        }

        /// <summary>
        /// Causes monitoring thread to exit on its next monitoring cycle
        /// </summary>
        public void StopMonitoringProgram(bool kill = false)
        {
            // Program is running, kill it and abort thread
            if (StartingOrMonitoring() && kill)
            {
                try
                {
                    // m_Process.Kill();
                    m_CancellationToken?.Cancel();
                    SleepMilliseconds(500);
                    m_CancellationToken?.Dispose();
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    ThrowConditionalException(ex, "Caught Win32Exception while trying to kill thread.");
                }
                catch (InvalidOperationException ex)
                {
                    ThrowConditionalException(ex, "Caught InvalidOperationException while trying to kill thread.");
                }
                catch (SystemException ex)
                {
                    ThrowConditionalException(ex, "Caught SystemException while trying to kill thread.");
                }
                catch (Exception ex)
                {
                    ThrowConditionalException(ex, "Caught Exception while trying to kill or abort thread.");
                }
            }

            // Program not running, just abort thread
            if (State == States.Waiting && kill)
            {
                try
                {
                    if (m_Process?.HasExited == false)
                        m_Process.Kill();
                }
                catch (Exception ex)
                {
                    ThrowConditionalException(ex, "Caught exception while trying to kill thread that is still running");
                }
            }

            if (StartingOrMonitoring() || State == States.Waiting)
            {
                State = States.CleaningUp;
                m_doCleanup = true;
                State = States.NotMonitoring;
            }
        }

        private void ThrowConditionalException(Exception ex, string loggerMessage)
        {
#pragma warning disable 618
            mExceptionLogger?.PostError(loggerMessage, ex, true);
#pragma warning restore 618
            mLogger?.Error(loggerMessage, ex);

            if (!NotifyOnException)
                return;

#pragma warning disable 618
            var ignoreException = (mExceptionLogger == null && mLogger == null);
#pragma warning restore 618

            if (ignoreException)
            {
                OnWarningEvent("Exception caught (but ignored): " + loggerMessage + "; " + ex.Message);
            }
            else
            {
                OnWarningEvent("Re-throwing exception: " + ex.Message);
                throw ex;
            }
        }

        #endregion
    }
}
