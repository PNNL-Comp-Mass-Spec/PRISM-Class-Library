using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PRISM
{

    /// <summary>
    /// This class runs a single program as an external process and monitors it with an internal thread
    /// </summary>
    public class clsProgRunner : ILoggerAware
    {

        #region "Constants and Enums"

        public const int DEFAULT_MONITOR_INTERVAL_MSEC = 5000;

        public const int MINIMUM_MONITOR_INTERVAL_MSEC = 250;

        /// <summary>
        /// clsProgRunner states
        /// </summary>
        public enum States
        {
            NotMonitoring,
            Monitoring,
            Waiting,
            CleaningUp,
            Initializing,
            StartingProcess
        }
        #endregion

        #region "Classwide Variables"

        /// <summary>
        /// Interface used for logging exceptions
        /// </summary>
        private ILogger m_ExceptionLogger;

        /// <summary>
        /// Interface used for logging errors and health related messages
        /// </summary>
        private ILogger m_EventLogger;

        /// <summary>
        /// overall state of this object
        /// </summary>
        private States m_state = States.NotMonitoring;

        /// <summary>
        /// Used to start and monitor the external program
        /// </summary>
        private readonly Process m_Process = new Process();

        /// <summary>
        /// The process id of the currently running incarnation of the external program
        /// </summary>
        private int m_pid;

        /// <summary>
        /// The instance name of the most recent performance counter used by GetCoreUsageByProcessID
        /// </summary>
        /// <remarks></remarks>
        private string m_processIdInstanceName;

        /// <summary>
        /// The internal thread used to run the monitoring code
        /// </summary>
        /// <remarks>
        /// That starts and monitors the external program
        /// </remarks>
        private Thread m_Thread;

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

        /// <summary>
        /// Number of cores on this computer
        /// </summary>
        /// <remarks></remarks>
        private static int mCachedCoreCount;

        /// <summary>
        /// Maps processId to a PerformanceCounter instance
        /// </summary>
        /// <remarks>The KeyValuePair tracks the performance counter instance name (could be empty string) and the PerformanceCounter instance</remarks>
        private static readonly ConcurrentDictionary<int, KeyValuePair<string, PerformanceCounter>> mCachedPerfCounters = new ConcurrentDictionary<int, KeyValuePair<string, PerformanceCounter>>();

        #endregion

        #region "Events"

        /// <summary>
        /// This event is raised at regular intervals while monitoring the program
        /// </summary>
        /// <remarks>Raised every m_monitorInterval milliseconds</remarks>
        public event ProgChangedEventHandler ProgChanged;
        public delegate void ProgChangedEventHandler(clsProgRunner obj);

        public event ConsoleOutputEventEventHandler ConsoleOutputEvent;

        /// <summary>
        /// This event is raised when the external program writes text to the console
        /// </summary>
        /// <param name="NewText"></param>
        /// <remarks></remarks>
        public delegate void ConsoleOutputEventEventHandler(string NewText);

        public event ConsoleErrorEventEventHandler ConsoleErrorEvent;

        /// <summary>
        /// This event is raised when the external program writes text to the console's error stream
        /// </summary>
        /// <param name="NewText"></param>
        /// <remarks></remarks>
        public delegate void ConsoleErrorEventEventHandler(string NewText);

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
        /// Can retrieve using the CachedConsoleOutput readonly property
        /// Will also fire event ConsoleOutputEvent as new text is written to the console
        /// </summary>
        /// <remarks>If this is true, then no window will be shown, even if CreateNoWindow=False</remarks>
        public bool CacheStandardOutput { get; set; }

        /// <summary>
        /// When true, the program name and command line arguments will be added to the top of the console output file
        /// </summary>
        public bool ConsoleOutputFileIncludesCommandLine { get; set; }

        /// <summary>
        /// File path to which the console output will be written if WriteConsoleOutputToFile is true
        /// If blank, then file path will be auto-defined in the WorkDir  when program execution starts
        /// </summary>
        public string ConsoleOutputFilePath { get; set; }

        /// <summary>
        /// Determine if window should be displayed
        /// Will be forced to True if CacheStandardOutput = True
        /// </summary>
        public bool CreateNoWindow { get; set; }

        /// <summary>
        /// When true, then echoes, in real time, text written to the Console by the external program 
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
            get { return m_monitorInterval; }
            set
            {
                if (value < MINIMUM_MONITOR_INTERVAL_MSEC)
                    value = MINIMUM_MONITOR_INTERVAL_MSEC;
                m_monitorInterval = value;
            }
        }

        /// <summary>
        /// Name of this progrunner
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// When true, raises event ProgChanged
        /// </summary>
        public bool NotifyOnEvent { get; set; }

        /// <summary>
        /// When true, and if m_ExceptionLogger is defined, re-throws the exception
        /// </summary>
        public bool NotifyOnException { get; set; }


        /// <summary>
        /// Process id of currently running external program's process
        /// </summary>
        public int PID => m_pid;

        /// <summary>
        /// External program that prog runner will run
        /// This is the full path to the program file
        /// </summary>
        public string Program { get; set; }

        /// <summary>
        /// Whether prog runner will restart external program after it exits
        /// </summary>
        public bool Repeat { get; set; }

        /// <summary>
        /// Time (in seconds) that prog runner waits to restart the external program after it exits
        /// </summary>
        public double RepeatHoldOffTime { get; set; }

        /// <summary>
        /// Current state of prog runner (as number)
        /// </summary>
        public States State => m_state;

        /// <summary>
        /// Current state of prog runner (as descriptive name)
        /// </summary>
        public string StateName
        {
            get
            {
                string functionReturnValue;
                switch (m_state)
                {
                    case States.NotMonitoring:
                        functionReturnValue = "not monitoring";
                        break;
                    case States.Monitoring:
                        functionReturnValue = "monitoring";
                        break;
                    case States.Waiting:
                        functionReturnValue = "waiting to restart";
                        break;
                    case States.CleaningUp:
                        functionReturnValue = "cleaning up";
                        break;
                    case States.Initializing:
                        functionReturnValue = "initializing";
                        break;
                    case States.StartingProcess:
                        functionReturnValue = "starting";
                        break;
                    default:
                        functionReturnValue = "???";
                        break;
                }
                return functionReturnValue;
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
        /// <remarks>If this is true, then no window will be shown, even if CreateNoWindow=False</remarks>
        public bool WriteConsoleOutputToFile { get; set; }

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        public clsProgRunner()
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
        /// <remarks></remarks>
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
        /// Clear any performance counters cached via a call to GetCoreUsage() or GetCoreUsageByProcessID()
        /// </summary>
        /// <remarks></remarks>
        public static void ClearCachedPerformanceCounters()
        {
            mCachedPerfCounters.Clear();
        }

        /// <summary>
        /// Clear the performance counter cached for the given Process ID
        /// </summary>
        /// <remarks></remarks>
        public static void ClearCachedPerformanceCounterForProcessID(int processId)
        {
            try
            {
                if (!mCachedPerfCounters.ContainsKey(processId))
                {
                    return;
                }

                KeyValuePair<string, PerformanceCounter> removedCounter;
                mCachedPerfCounters.TryRemove(processId, out removedCounter);
            }
            catch (Exception)
            {
                // Ignore errors
            }

        }

        /// <summary>
        /// Clears any console error text that is currently cached
        /// </summary>
        /// <remarks></remarks>
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
            // Collect the console output

            if ((outLine.Data != null))
            {
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

                if (WriteConsoleOutputToFile && (m_ConsoleOutputStreamWriter != null))
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

        }

        /// <summary>
        /// Force garbage collection
        /// </summary>
        /// <remarks>Waits up to 1 second for the collection to finish</remarks>
        public static void GarbageCollectNow()
        {
            const int maxWaitTimeMSec = 1000;
            GarbageCollectNow(maxWaitTimeMSec);
        }

        /// <summary>
        /// Force garbage collection
        /// </summary>
        /// <remarks></remarks>
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

                var intTotalThreadWaitTimeMsec = 0;
                while (gcThread.IsAlive && intTotalThreadWaitTimeMsec < maxWaitTimeMSec)
                {
                    Thread.Sleep(THREAD_SLEEP_TIME_MSEC);
                    intTotalThreadWaitTimeMsec += THREAD_SLEEP_TIME_MSEC;
                }
                if (gcThread.IsAlive)
                    gcThread.Abort();

            }
            catch (Exception)
            {
                // Ignore errors here
            }

        }

        protected static void GarbageCollectWaitForGC()
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
        /// <returns></returns>
        /// <remarks>Before calling this function, define WorkDir (working directory folder) and Program (full path to the .exe to run)</remarks>
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
        /// Returns the number of cores
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>Should not be affected by hyperthreading, so a computer with two 4-core chips will report 8 cores</remarks>
        public static int GetCoreCount()
        {


            try
            {
                if (mCachedCoreCount > 0)
                {
                    return mCachedCoreCount;
                }

                var result = new System.Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor");
                var coreCount = 0;

                foreach (var item in result.Get())
                {
                    coreCount += int.Parse(item["NumberOfCores"].ToString());
                }

                Interlocked.Exchange(ref mCachedCoreCount, coreCount);

                return mCachedCoreCount;

            }
            catch (Exception)
            {
                // This value will be affected by hyperthreading
                return Environment.ProcessorCount;
            }

        }

        /// <summary>
        /// Reports the number of cores in use by the program started with StartAndMonitorProgram
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <returns>Number of cores in use; -1 if an error</returns>
        /// <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        public float GetCoreUsage()
        {

            if (m_pid == 0)
            {
                return 0;
            }

            try
            {
                return GetCoreUsageByProcessID(m_pid, ref m_processIdInstanceName);
            }
            catch (Exception ex)
            {
                ThrowConditionalException(ex, "processId not recognized or permissions error");
                return -1;
            }

        }

        /// <summary>
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <param name="processId">Process ID for the program</param>
        /// <returns>Number of cores in use; 0 if the process is terminated.  Exception is thrown if a problem</returns>
        /// <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        public static float GetCoreUsageByProcessID(int processId)
        {
            var processIdInstanceName = "";
            return GetCoreUsageByProcessID(processId, ref processIdInstanceName);
        }

        /// <summary>
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <param name="processId">Process ID for the program</param>
        /// <param name="processIdInstanceName">Expected instance name for the given processId; ignored if empty string. Updated to actual instance name if a new performance counter is created</param>
        /// <returns>Number of cores in use; 0 if the process is terminated. Exception is thrown if a problem</returns>
        /// <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        public static float GetCoreUsageByProcessID(int processId, ref string processIdInstanceName)
        {


            try
            {
                if (mCachedCoreCount == 0)
                {
                    mCachedCoreCount = GetCoreCount();
                }

                KeyValuePair<string, PerformanceCounter> perfCounterContainer;
                var getNewPerfCounter = true;
                var maxAttempts = 2;

                // Look for a cached performance counter instance

                if (mCachedPerfCounters.TryGetValue(processId, out perfCounterContainer))
                {
                    var cachedProcessIdInstanceName = perfCounterContainer.Key;

                    if (string.IsNullOrEmpty(processIdInstanceName) || string.IsNullOrEmpty(cachedProcessIdInstanceName))
                    {
                        // Use the existing performance counter
                        getNewPerfCounter = false;
                    }
                    else
                    {
                        // Confirm that the existing performance counter matches the expected instance name                        
                        if (cachedProcessIdInstanceName.Equals(processIdInstanceName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            getNewPerfCounter = false;
                        }
                    }

                    if (perfCounterContainer.Value == null)
                    {
                        getNewPerfCounter = true;
                    }
                    else
                    {
                        // Existing performance counter found
                        maxAttempts = 1;
                    }
                }

                if (getNewPerfCounter)
                {
                    string newProcessIdInstanceName;
                    var perfCounter = GetPerfCounterForProcessID(processId, out newProcessIdInstanceName);

                    if (perfCounter == null)
                    {
                        throw new Exception("GetCoreUsageByProcessID: Performance counter not found for processId " + processId);
                    }

                    processIdInstanceName = newProcessIdInstanceName;

                    ClearCachedPerformanceCounterForProcessID(processId);

                    // Cache this performance counter so that it is quickly available on the next call to this method
                    mCachedPerfCounters.TryAdd(processId, new KeyValuePair<string, PerformanceCounter>(newProcessIdInstanceName, perfCounter));

                    mCachedPerfCounters.TryGetValue(processId, out perfCounterContainer);
                }

                var cpuUsage = GetCoreUsageForPerfCounter(perfCounterContainer.Value, maxAttempts);

                var coresInUse = cpuUsage / 100.0;

                return Convert.ToSingle(coresInUse);

            }
            catch (InvalidOperationException)
            {
                // The process is likely terminated
                return 0;
            }
            catch (Exception ex)
            {
                throw new Exception("Exception in GetCoreUsageByProcessID for processId " + processId + ": " + ex.Message, ex);
            }

        }

        /// <summary>
        /// Sample the given performance counter to determine the CPU usage
        /// </summary>
        /// <param name="perfCounter">Performance counter instance</param>
        /// <param name="maxAttempts">Number of attempts</param>
        /// <returns>Number of cores in use; 0 if the process is terminated. Exception is thrown if a problem</returns>
        /// <remarks>
        /// The first time perfCounter.NextSample() is called a Permissions exception is sometimes thrown
        /// Set maxAttempts to 2 or higher to gracefully handle this
        /// </remarks>
        private static float GetCoreUsageForPerfCounter(PerformanceCounter perfCounter, int maxAttempts)
        {

            if (maxAttempts < 1)
                maxAttempts = 1;

            for (var iteration = 1; iteration <= maxAttempts; iteration++)
            {

                try
                {
                    // Take a sample, wait 1 second, then sample again
                    var sample1 = perfCounter.NextSample();
                    Thread.Sleep(1000);
                    var sample2 = perfCounter.NextSample();

                    // Each core contributes "100" to the overall cpuUsage
                    var cpuUsage = CounterSample.Calculate(sample1, sample2);
                    return cpuUsage;

                }
                catch (InvalidOperationException)
                {
                    // The process is likely terminated
                    return 0;
                }
                catch (Exception)
                {
                    if (iteration == maxAttempts)
                    {
                        throw;
                    }
                    
                    // Wait 500 milliseconds then try again
                    Thread.Sleep(500);
                }

            }

            return 0;

        }

        /// <summary>
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <param name="processName">Process name, for example chrome (do not include .exe)</param>
        /// <returns>Number of cores in use; -1 if process not found; exception is thrown if a problem</returns>
        /// <remarks>
        /// Core count is typically an integer, but can be a fractional number if not using a core 100%
        /// If multiple processes are running with the given name then returns the total core usage for all of them
        /// </remarks>
        public static float GetCoreUsageByProcessName(string processName)
        {
            List<int> processIDs;
            return GetCoreUsageByProcessName(processName, out processIDs);
        }

        /// <summary>
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <param name="processName">Process name, for example chrome (do not include .exe)</param>
        /// <param name="processIDs">List of ProcessIDs matching the given process name</param>
        /// <returns>Number of cores in use; -1 if process not found; exception is thrown if a problem</returns>
        /// <remarks>
        /// Core count is typically an integer, but can be a fractional number if not using a core 100%
        /// If multiple processes are running with the given name then returns the total core usage for all of them
        /// </remarks>
        public static float GetCoreUsageByProcessName(string processName, out List<int> processIDs)
        {

            processIDs = new List<int>();
            var processInstances = Process.GetProcessesByName(processName);
            if (processInstances.Length == 0)
                return -1;

            float coreUsageOverall = 0;
            foreach (var runningProcess in processInstances)
            {
                var processID = runningProcess.Id;
                processIDs.Add(processID);

                var processIdInstanceName = "";
                var coreUsage = GetCoreUsageByProcessID(processID, ref processIdInstanceName);
                if (coreUsage > 0)
                {
                    coreUsageOverall += coreUsage;
                }
            }

            return coreUsageOverall;

        }

        /// <summary>
        /// Obtain the performance counter for the given process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="instanceName">Output: instance name corresponding to processId</param>
        /// <param name="processCounterName">Performance counter to return</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static PerformanceCounter GetPerfCounterForProcessID(int processId, out string instanceName, string processCounterName = "% Processor Time")
        {

            instanceName = GetInstanceNameForProcessId(processId);
            if (string.IsNullOrEmpty(instanceName))
            {
                return null;
            }

            return new PerformanceCounter("Process", processCounterName, instanceName);

        }

        /// <summary>
        /// Get the specific Windows instance name for a program
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>Instance name if found, otherwise an empty string</returns>
        /// <remarks>If multiple programs named Chrome.exe are running, the first is Chrome.exe, the second is Chrome.exe#1, etc.</remarks>
        public static string GetInstanceNameForProcessId(int processId)
        {

            try
            {
                var runningProcess = Process.GetProcessById(processId);

                var processName = Path.GetFileNameWithoutExtension(runningProcess.ProcessName);

                var processCategory = new PerformanceCounterCategory("Process");

                var perfCounterInstances = (from item in processCategory.GetInstanceNames() where item.StartsWith(processName) select item).ToList();


                foreach (var instanceName in perfCounterInstances)
                {
                    using (var counterInstance = new PerformanceCounter("Process", "ID Process", instanceName, true))
                    {
                        var instanceProcessID = Convert.ToInt32(counterInstance.RawValue);
                        if (instanceProcessID == processId)
                        {
                            return instanceName;
                        }
                    }

                }

            }
            catch (Exception)
            {
                return string.Empty;
            }

            return string.Empty;

        }


        public void JoinThreadNow()
        {
            if (m_Thread == null)
                return;

            try
            {
                // Attempt to re-join the thread (wait for 5 seconds, at most)
                m_Thread.Join(5000);
            }
            catch (ThreadStateException ex)
            {
                ThrowConditionalException(ex, "Caught ThreadStateException while trying to join thread.");
            }
            catch (ThreadInterruptedException ex)
            {
                ThrowConditionalException(ex, "Caught ThreadInterruptedException while trying to join thread.");
            }
            catch (Exception ex)
            {
                ThrowConditionalException(ex, "Caught exception while trying to join thread.");
            }

        }

        /// <summary>
        /// Sets the name of the exception logger
        /// </summary>
        public void RegisterExceptionLogger(ILogger logger)
        {
            m_ExceptionLogger = logger;
        }

        void ILoggerAware.RegisterEventLogger(ILogger logger)
        {
            RegisterExceptionLogger(logger);
        }

        /// <summary>
        /// Sets the name of the event logger
        /// </summary>
        public void RegisterEventLogger(ILogger logger)
        {
            m_EventLogger = logger;
        }

        void ILoggerAware.RegisterExceptionLogger(ILogger logger)
        {
            RegisterEventLogger(logger);
        }

        private void RaiseConditionalProgChangedEvent(clsProgRunner obj)
        {
            if (NotifyOnEvent)
            {
                m_EventLogger?.PostEntry("Raising ProgChanged event for " + obj.Name + ".", logMsgType.logHealth, true);
                ProgChanged?.Invoke(obj);
            }
        }

        /// <summary>
        /// Start program as external process and monitor its state
        /// </summary>
        private void StartProcess()
        {
            bool blnStandardOutputRedirected;

            // set up parameters for external process
            //
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
                blnStandardOutputRedirected = true;
            }
            else
            {
                m_Process.StartInfo.UseShellExecute = true;
                m_Process.StartInfo.RedirectStandardOutput = false;
                blnStandardOutputRedirected = false;
            }


            if (!File.Exists(m_Process.StartInfo.FileName))
            {
                ThrowConditionalException(new Exception("Process filename " + m_Process.StartInfo.FileName + " not found."), "clsProgRunner m_ProgName was not set correctly.");
                m_state = States.NotMonitoring;
                return;
            }

            if (!Directory.Exists(m_Process.StartInfo.WorkingDirectory))
            {
                ThrowConditionalException(new Exception("Process working directory " + m_Process.StartInfo.WorkingDirectory + " not found."), "clsProgRunner m_WorkDir was not set correctly.");
                m_state = States.NotMonitoring;
                return;
            }

            if (blnStandardOutputRedirected)
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

            do
            {
                // Start the program as an external process
                //
                try
                {
                    m_state = States.StartingProcess;
                    m_Process.Start();
                }
                catch (Exception ex)
                {
                    ThrowConditionalException(ex, "Problem starting process. Parameters: " + m_Process.StartInfo.WorkingDirectory + m_Process.StartInfo.FileName + " " + m_Process.StartInfo.Arguments + ".");
                    m_ExitCode = -1234567;
                    m_state = States.NotMonitoring;
                    return;
                }

                try
                {
                    m_state = States.Monitoring;
                    m_pid = m_Process.Id;
                }
                catch (Exception)
                {
                    // Exception looking up the process ID
                    m_pid = 999999999;
                }

                m_processIdInstanceName = string.Empty;

                if (blnStandardOutputRedirected)
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
                        blnStandardOutputRedirected = false;
                    }
                }

                RaiseConditionalProgChangedEvent(this);

                // Wait for program to exit (loop on interval)
                //
                // We wait until the external process exits or 
                // the class is instructed to stop monitoring the process (m_doCleanup = true)
                //

                while (!(m_doCleanup))
                {
                    if (m_monitorInterval < MINIMUM_MONITOR_INTERVAL_MSEC)
                        m_monitorInterval = MINIMUM_MONITOR_INTERVAL_MSEC;

                    try
                    {
                        m_Process.WaitForExit(m_monitorInterval);
                        if (m_Process.HasExited)
                            break; 
                    }
                    catch (Exception)
                    {
                        // Exception calling .WaitForExit or .HasExited; most likely the process has exited
                        break;
                    }

                }

                // Need to free up resources used to keep
                // track of the external process
                //
                m_pid = 0;
                m_processIdInstanceName = string.Empty;

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
                    m_Process.Close();
                }
                catch (Exception)
                {
                    // Exception closing the process; ignore
                }

                if ((m_EventLogger != null))
                {
                    m_EventLogger.PostEntry("Process " + Name + " terminated with exit code " + m_ExitCode, logMsgType.logHealth, true);

                    if ((m_CachedConsoleError != null) && m_CachedConsoleError.Length > 0)
                    {
                        m_EventLogger.PostEntry("Cached error text for process " + Name + ": " + m_CachedConsoleError, logMsgType.logError, true);
                    }
                }

                if ((m_ConsoleOutputStreamWriter != null))
                {
                    // Give the other threads time to write any additional info to m_ConsoleOutputStreamWriter
                    var maxWaitTimeMSec = 1000;
                    GarbageCollectNow(maxWaitTimeMSec);
                    m_ConsoleOutputStreamWriter.Close();
                }

                // Decide whether or not to repeat starting
                // the external process again, or quit
                //
                if (Repeat & !m_doCleanup)
                {
                    // Repeat starting the process
                    // after waiting for minimum hold off time interval
                    //
                    m_state = States.Waiting;

                    RaiseConditionalProgChangedEvent(this);

                    var holdoffMilliseconds = Convert.ToInt32(RepeatHoldOffTime * 1000);
                    Thread.Sleep(holdoffMilliseconds);

                    m_state = States.Monitoring;
                }
                else
                {
                    // Don't repeat starting the process - just quit                    
                    m_state = States.NotMonitoring;
                    RaiseConditionalProgChangedEvent(this);
                    break;
                }
            } while (true);

        }

        /// <summary>
        /// Creates a new thread and starts code that runs and monitors a program in it
        /// </summary>
        public void StartAndMonitorProgram()
        {
            if (m_state == States.NotMonitoring)
            {
                m_state = States.Initializing;
                m_doCleanup = false;

                // arrange to start the program as an external process
                // and monitor it in a separate internal thread
                //
                try
                {
                    var m_ThreadStart = new ThreadStart(StartProcess);
                    m_Thread = new Thread(m_ThreadStart);
                    m_Thread.Start();
                }
                catch (Exception ex)
                {
                    ThrowConditionalException(ex, "Caught exception while trying to start thread.");

                }
            }
        }

        protected bool StartingOrMonitoring()
        {
            if (m_state == States.Initializing || m_state == States.StartingProcess || m_state == States.Monitoring)
            {
                return true;
            }

            return false;
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
                    m_Process.Kill();
                    m_Thread.Abort();
                }
                catch (ThreadAbortException ex)
                {
                    ThrowConditionalException(ex, "Caught ThreadAbortException while trying to abort thread.");
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
            if (m_state == States.Waiting & kill)
            {
                try
                {
                    m_Thread.Abort();
                }
                catch (ThreadAbortException ex)
                {
                    ThrowConditionalException(ex, "Caught ThreadAbortException while trying to abort thread.");
                }
                catch (Exception ex)
                {
                    ThrowConditionalException(ex, "Caught exception while trying to abort thread.");
                }
            }

            if (StartingOrMonitoring() || m_state == States.Waiting)
            {
                m_state = States.CleaningUp;
                m_doCleanup = true;
                try
                {
                    // Attempt to re-join the thread (wait for 5 seconds, at most)
                    m_Thread.Join(5000);
                }
                catch (ThreadStateException ex)
                {
                    ThrowConditionalException(ex, "Caught ThreadStateException while trying to join thread.");
                }
                catch (ThreadInterruptedException ex)
                {
                    ThrowConditionalException(ex, "Caught ThreadInterruptedException while trying to join thread.");
                }
                catch (Exception ex)
                {
                    ThrowConditionalException(ex, "Caught exception while trying to join thread.");
                }
                m_state = States.NotMonitoring;
            }
        }

        private void ThrowConditionalException(Exception ex, string loggerMessage)
        {
            m_ExceptionLogger?.PostError(loggerMessage, ex, true);

            if (NotifyOnException)
            {
                if (m_ExceptionLogger == null)
                {
                    Console.WriteLine("Exception caught (but ignored): " + loggerMessage + "; " + ex.Message);
                }
                else
                {
                    m_ExceptionLogger.PostError("Rethrowing exception", ex, true);
                    throw ex;
                }
            }
        }

        #endregion

    }

}
