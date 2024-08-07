﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace PRISMWin
{
    /// <summary>
    /// Monitor running processes to determine CPU usage
    /// Uses Windows Performance Counters
    /// </summary>
    public class ProcessStats
    {
        // Ignore Spelling: hyperthreading, perf

        #region "Events and Event Handlers"

        /// <summary>
        /// Error event
        /// </summary>
        public event ErrorEventEventHandler ErrorEvent;

        /// <summary>
        /// Error event
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception</param>
        public delegate void ErrorEventEventHandler(string message, Exception ex);

        #endregion

        /// <summary>
        /// Number of cores on this computer
        /// </summary>
        private int mCachedCoreCount;

        /// <summary>
        /// Used to determine overall CPU utilization
        /// </summary>
        private PerformanceCounter mCPUUsagePerformanceCounter;

        /// <summary>
        /// Maps processId to a PerformanceCounter instance
        /// </summary>
        /// <remarks>The KeyValuePair tracks the performance counter instance name (could be empty string) and the PerformanceCounter instance</remarks>
        private readonly ConcurrentDictionary<int, KeyValuePair<string, PerformanceCounter>> mCachedPerfCounters = new();

        private readonly bool mLimitLoggingByTimeOfDay;

        /// <summary>
        /// The instance name of the most recent performance counter used by GetCoreUsageByProcessID
        /// </summary>
        private string mProcessIdInstanceName;

        private bool mPerformanceCountersInitialized;

        private int mPerformanceCounterInitAttempts;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="limitLoggingByTimeOfDay">When true, only log errors between 12 am and 12:30 am</param>
        public ProcessStats(bool limitLoggingByTimeOfDay = false)
        {
            mLimitLoggingByTimeOfDay = limitLoggingByTimeOfDay;

            mPerformanceCountersInitialized = false;
            mPerformanceCounterInitAttempts = 0;
        }

        /// <summary>
        /// Clear any performance counters cached via a call to GetCoreUsage() or GetCoreUsageByProcessID()
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public void ClearCachedPerformanceCounters()
        {
            mCachedPerfCounters.Clear();
        }

        /// <summary>
        /// Clear the performance counter cached for the given Process ID
        /// </summary>
        public void ClearCachedPerformanceCounterForProcessID(int processId)
        {
            try
            {
                if (!mCachedPerfCounters.ContainsKey(processId))
                {
                    return;
                }

                mCachedPerfCounters.TryRemove(processId, out _);
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }

        private void ConditionalLogError(string message, Exception ex = null)
        {
            if (mLimitLoggingByTimeOfDay)
            {
                // To avoid seeing this in the logs continually, we will only post this log message between 12 am and 12:30 am
                if (DateTime.Now.Hour == 0 && DateTime.Now.Minute <= 30)
                {
                    OnErrorEvent(message + " (this message is only logged between 12 am and 12:30 am)", ex);
                }
            }
            else
            {
                OnErrorEvent(message);
            }
        }

        /// <summary>
        /// Returns the number of cores
        /// </summary>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 4-core chips will report 8 cores, even if Hyperthreading is enabled
        /// </remarks>
        /// <returns>The number of cores on this computer</returns>
        public int GetCoreCount()
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
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        /// <param name="processId">Process ID for the program</param>
        /// <returns>Number of cores in use; 0 if the process is terminated.  Exception is thrown if a problem</returns>
        // ReSharper disable once UnusedMember.Global
        public float GetCoreUsageByProcessID(int processId)
        {
            return GetCoreUsageByProcessID(processId, ref mProcessIdInstanceName);
        }

        /// <summary>
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        /// <param name="processId">Process ID for the program</param>
        /// <param name="processIdInstanceName">Expected instance name for the given processId; ignored if empty string. Updated to actual instance name if a new performance counter is created</param>
        /// <returns>Number of cores in use; 0 if the process is terminated. Exception is thrown if a problem</returns>
        public float GetCoreUsageByProcessID(int processId, ref string processIdInstanceName)
        {
            try
            {
                if (mCachedCoreCount == 0)
                {
                    mCachedCoreCount = GetCoreCount();
                }

                var getNewPerfCounter = true;
                var maxAttempts = 2;

                // Look for a cached performance counter instance

                if (mCachedPerfCounters.TryGetValue(processId, out var perfCounterContainer))
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
                    var perfCounter = GetPerfCounterForProcessID(processId, out var newProcessIdInstanceName);

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
        /// <remarks>
        /// The first time perfCounter.NextSample() is called a Permissions exception is sometimes thrown
        /// Set maxAttempts to 2 or higher to gracefully handle this
        /// </remarks>
        /// <param name="perfCounter">Performance counter instance</param>
        /// <param name="maxAttempts">Number of attempts</param>
        /// <returns>Number of cores in use; 0 if the process is terminated. Exception is thrown if a problem</returns>
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
        /// <remarks>
        /// Core count is typically an integer, but can be a fractional number if not using a core 100%
        /// If multiple processes are running with the given name, returns the total core usage for all of them
        /// </remarks>
        /// <param name="processName">Process name, for example chrome (do not include .exe)</param>
        /// <returns>Number of cores in use; -1 if process not found; exception is thrown if a problem</returns>
        // ReSharper disable once UnusedMember.Global
        public float GetCoreUsageByProcessName(string processName)
        {
            return GetCoreUsageByProcessName(processName, out _);
        }

        /// <summary>
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <remarks>
        /// Core count is typically an integer, but can be a fractional number if not using a core 100%
        /// If multiple processes are running with the given name, returns the total core usage for all of them
        /// </remarks>
        /// <param name="processName">Process name, for example chrome (do not include .exe)</param>
        /// <param name="processIDs">List of ProcessIDs matching the given process name</param>
        /// <returns>Number of cores in use; -1 if process not found; exception is thrown if a problem</returns>
        public float GetCoreUsageByProcessName(string processName, out List<int> processIDs)
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
        /// Returns the CPU usage
        /// </summary>
        /// <remarks>
        /// This is CPU usage for all running applications, not just this application
        /// For CPU usage of a single application use GetCoreUsageByProcessID()
        /// </remarks>
        /// <returns>Value between 0 and 100</returns>
        // ReSharper disable once UnusedMember.Global
        public float GetCPUUtilization()
        {
            float cpuUtilization = 0;

            InitializePerformanceCounters();

            try
            {
                if (mCPUUsagePerformanceCounter != null)
                {
                    cpuUtilization = mCPUUsagePerformanceCounter.NextValue();
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }

            return cpuUtilization;
        }

        /// <summary>
        /// Obtain the performance counter for the given process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="instanceName">Output: instance name corresponding to processId</param>
        /// <param name="processCounterName">Performance counter to return</param>
        public PerformanceCounter GetPerfCounterForProcessID(int processId, out string instanceName, string processCounterName = "% Processor Time")
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
        /// <remarks>If multiple programs named Chrome.exe are running, the first is Chrome.exe, the second is Chrome.exe#1, etc.</remarks>
        /// <param name="processId">Process ID</param>
        /// <returns>Instance name if found, otherwise an empty string</returns>
        public string GetInstanceNameForProcessId(int processId)
        {
            try
            {
                var runningProcess = Process.GetProcessById(processId);

                var processName = Path.GetFileNameWithoutExtension(runningProcess.ProcessName);

                var processCategory = new PerformanceCounterCategory("Process");

                var perfCounterInstances = (from item in processCategory.GetInstanceNames() where item.StartsWith(processName) select item).ToList();

                foreach (var instanceName in perfCounterInstances)
                {
                    using var counterInstance = new PerformanceCounter("Process", "ID Process", instanceName, true);

                    var instanceProcessID = Convert.ToInt32(counterInstance.RawValue);

                    if (instanceProcessID == processId)
                    {
                        return instanceName;
                    }
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private void InitializePerformanceCounters()
        {
            if (mPerformanceCountersInitialized)
                return;

            try
            {
                if (mPerformanceCounterInitAttempts > 2)
                {
                    // Initialization has failed 3 times
                    // Stop trying to initialize the performance counters
                    mPerformanceCountersInitialized = true;
                    return;
                }

                mPerformanceCounterInitAttempts++;

                mCPUUsagePerformanceCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total")
                {
                    ReadOnly = true
                };

                mPerformanceCountersInitialized = true;
            }
            catch (Exception ex)
            {
                ConditionalLogError("Error instantiating the Processor.[% Processor Time] performance counter: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="message">Message</param>
        private void OnErrorEvent(string message)
        {
            ErrorEvent?.Invoke(message, null);
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception (allowed to be null)</param>
        private void OnErrorEvent(string message, Exception ex)
        {
            ErrorEvent?.Invoke(message, ex);
        }
    }
}
