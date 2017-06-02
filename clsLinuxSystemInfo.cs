using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace PRISM
{

    /// <summary>
    /// Methods to determine memory usage, CPU usage, and Linux system version
    /// </summary>
    public class clsLinuxSystemInfo : clsEventNotifier
    {

        #region "Constants and Enums"

        public const string ROOT_PROC_DIRECTORY = "/proc";

        public const string CPUINFO_FILE = "cpuinfo";

        public const string MEMINFO_FILE = "meminfo";

        #endregion

        #region "Classwide Variables"

        private int mCoreCountCached;

        private readonly bool mLimitLoggingByTimeOfDay;

        private DateTime mLastDebugInfoTimeCoreCount;

        private DateTime mLastDebugInfoTimeCoreUseByProcessID;

        private DateTime mLastDebugInfoTimeMemory;

        private readonly Regex mRegexMemorySize;

        private readonly Regex mRegexMemorySizeNoUnits;

        private readonly Regex mStatLineWithCommand;

        private readonly Regex mStatLineNoCommand;
        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="limitLoggingByTimeOfDay">When true, only log errors between 12 am and 12:30 am</param>
        public clsLinuxSystemInfo(bool limitLoggingByTimeOfDay = false)
        {
            mCoreCountCached = 0;

            mLimitLoggingByTimeOfDay = limitLoggingByTimeOfDay;

            mLastDebugInfoTimeCoreCount = DateTime.UtcNow.AddMinutes(-1);

            mLastDebugInfoTimeCoreUseByProcessID = DateTime.UtcNow.AddMinutes(-1);

            mLastDebugInfoTimeMemory = DateTime.UtcNow.AddMinutes(-1);

            mRegexMemorySize = new Regex(@"(?<Size>\d+) +(?<Units>(KB|MB|GB|TB|))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            mRegexMemorySizeNoUnits = new Regex(@"(?<Size>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // Stat fields are:
            //  Field          Content
            //   pid           process id
            //   tcomm         filename of the executable
            //   state         state (R is running, S is sleeping, D is waiting, Z is zombie, T is stopped, t is TraceStopped, W is paging, X or x is dead, K is Wakekill, W is waking, P is parked)
            //   ppid          process id of the parent process
            //   pgrp          pgrp of the process; all threads in a tree having the same pgrp value, equivalent to the pid of the primary (initial) process
            //   sid           session id
            //   tty_nr        tty the process uses
            //   tty_pgrp      pgrp of the tty
            //   flags         task flags
            //   min_flt       number of minor faults
            //   cmin_flt      number of minor faults with child's
            //   maj_flt       number of major faults
            //   cmaj_flt      number of major faults with child's
            //   utime         user mode jiffies
            //   stime         kernel mode jiffies
            //   cutime        user mode jiffies with child's
            //   cstime        kernel mode jiffies with child's

            // This regex matches the ProcessID and command name, plus the various stats
            mStatLineWithCommand = new Regex(@"(?<pid>\d+) (?<command>\([^)]+\)) (?<state>\S) (?<ppid>[0-9-]+) (?<pgrp>[0-9-]+) (?<session>[0-9-]+) (?<tty_nr>[0-9-]+) (?<tty_pgrp>[0-9-]+) (?<flags>\d+) (?<minflt>\d+) (?<cminflt>\d+) (?<majflt>\d+) (?<cmajflt>\d+) (?<utime>\d+) (?<stime>\d+)");

            // This is  fallback Regex that starts at state in case mStatLineWithCommand fails
            mStatLineNoCommand = new Regex(@"(?<state>[A-Za-z]) (?<ppid>[0-9-]+) (?<pgrp>[0-9-]+) (?<session>[0-9-]+) (?<tty_nr>[0-9-]+) (?<tty_pgrp>[0-9-]+) (?<flags>\d+) (?<minflt>\d+) (?<cminflt>\d+) (?<majflt>\d+) (?<cmajflt>\d+) (?<utime>\d+) (?<stime>\d+)");

        }

        private long ComputeTotalCPUTime()
        {

            var cpuStatFilePath = clsPathUtils.CombineLinuxPaths(ROOT_PROC_DIRECTORY, "stat");
            var cpuStatFile = new FileInfo(cpuStatFilePath);

            if (!cpuStatFile.Exists)
            {
                OnDebugEvent("CPU stats file not found at " + cpuStatFilePath);
                return 0;
            }

            using (var reader = new StreamReader(new FileStream(cpuStatFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                if (reader.EndOfStream)
                    return 0;

                var dataLine = reader.ReadLine();

                if (string.IsNullOrWhiteSpace(dataLine))
                {
                    return 0;
                }

                // Example data line:
                // cpu  37404353 50864 14997555 18383015477 7107004 462 218065 0 0

                // Sum all of the numbers following cpu
                var fields = dataLine.Split(' ');
                if (fields.Length < 2)
                    return 0;

                long totalJiffies = 0;

                for (var i = 1; i < fields.Length; i++)
                {
                    if (long.TryParse(fields[i], out var clockTimeJiffies))
                    {
                        totalJiffies += clockTimeJiffies;
                    }
                }

                return totalJiffies;
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


        private bool ExtractCPUTimes(FileSystemInfo statFile, out long utime, out long stime)
        {

            using (var reader = new StreamReader(new FileStream(statFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                if (!reader.EndOfStream)
                {
                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(dataLine))
                    {
                        utime = 0;
                        stime = 0;
                        return false;
                    }

                    // Example data lines:
                    // 166488 (mono) S 88338 166488 87940 34818 166488 4202496 3181 0 0 0 14990 9 0 0 20 0 21 0 576435325 1486651392 6596 18446744073709551615 4194304 7975508 140724251272320 140724251268432 237064205964 0 0 4096 1260 18446744073709551615 0 0 17 0 0 0 0 0 0
                    // 166497 (Threadpool work) R 88338 166488 87940 34818 166488 4202560 3228 0 0 0 67591 10 0 0 20 0 17 0 576435333 1486585856 6137 18446744073709551615 4194304 7975508 140724251272320 139666082726648 1086447976 0 0 4096 1260 18446744073709551615 0 0 -1 13 0 0 0 0 0

                    // The second field is the filename of the executable, and it should be surrounded by parentheses


                    var match = mStatLineWithCommand.Match(dataLine);

                    if (!match.Success)
                    {
                        match = mStatLineNoCommand.Match(dataLine);
                    }

                    if (!match.Success)
                    {
                        utime = 0;
                        stime = 0;
                        return false;
                    }


                    utime = long.Parse(match.Groups["utime"].Value);
                    stime = long.Parse(match.Groups["stime"].Value);
                    return true;
                }
            }

            utime = 0;
            stime = 0;
            return false;
        }

        /// <summary>
        /// Match the dataline with the Regex matcher
        /// If success, extract the ID group, returning the integer via parameter id
        /// </summary>
        /// <param name="reIdMatcher"></param>
        /// <param name="dataLine"></param>
        /// <param name="id">Output: matched ID, or 0 if no match</param>
        /// <returns>True if success, otherwise false</returns>
        private bool ExtractID(Regex reIdMatcher, string dataLine, out int id)
        {
            var match = reIdMatcher.Match(dataLine);
            if (!match.Success)
            {
                id = 0;
                return false;
            }

            id = int.Parse(match.Groups["ID"].Value);
            return true;
        }

        private float ExtractMemoryMB(string dataLine, bool showDebugInfo)
        {

            Match match;
            string units;

            var matchUnits = mRegexMemorySize.Match(dataLine);

            if (matchUnits.Success)
            {
                match = matchUnits;
                units = matchUnits.Groups["Units"].Value.ToLower();
            }
            else
            {
                var matchNoUnits = mRegexMemorySizeNoUnits.Match(dataLine);

                if (matchNoUnits.Success)
                {
                    match = matchNoUnits;
                    units = "bytes";
                }
                else
                {
                    if (showDebugInfo)
                        ConditionalLogError("Memory size not in the expected format of 12345678 kB; actually " + dataLine);

                    return -1;
                }
            }

            if (!long.TryParse(match.Groups["Size"].Value, out var memorySize))
            {
                if (showDebugInfo)
                    ConditionalLogError("Memory size parse error; could not extract an integer from " + dataLine);

                return -1;
            }

            float memorySizeMB;

            switch (units)
            {
                case "b":
                case "bytes":
                    memorySizeMB = (float)(memorySize / 1024.0 / 1024.0);
                    break;
                case "kb":
                    memorySizeMB = (float)(memorySize / 1024.0);
                    break;
                case "mb":
                    memorySizeMB = (float)(memorySize);
                    break;
                case "gb":
                    memorySizeMB = (float)(memorySize * 1024.0);
                    break;
                case "tb":
                    memorySizeMB = (float)(memorySize * 1024.0 * 1024);
                    break;
                case "pb":
                    memorySizeMB = (float)(memorySize * 1024.0 * 1024 * 1024);
                    break;
                default:
                    if (showDebugInfo)
                        ConditionalLogError("Memory size parse error; unknown units for " + dataLine);

                    return -1;
            }

            return memorySizeMB;
        }

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// The computed core count is cached to avoid needing to re-parse /proc/cpuinfo repeatedly
        /// </remarks>
        public int GetCoreCount()
        {

            if (mCoreCountCached > 0)
                return mCoreCountCached;

            var showDebugInfo = DateTime.UtcNow.Subtract(mLastDebugInfoTimeCoreCount).TotalSeconds > 15;
            if (showDebugInfo)
                mLastDebugInfoTimeCoreCount = DateTime.UtcNow;

            try
            {
                var cpuInfoFilePath = clsPathUtils.CombineLinuxPaths(ROOT_PROC_DIRECTORY, CPUINFO_FILE);

                var cpuInfoFile = new FileInfo(cpuInfoFilePath);
                if (!cpuInfoFile.Exists)
                {
                    if (showDebugInfo)
                        ConditionalLogError("CPU info file not found: " + cpuInfoFilePath);

                    return -1;
                }


                var processorList = new Dictionary<int, clsProcessorCoreInfo>();
                var currentProcessorID = -1;

                var reIdMatcher = new Regex(@": *(?<ID>\d+)", RegexOptions.Compiled);

                using (var reader = new StreamReader(new FileStream(cpuInfoFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        if (dataLine.ToLower().StartsWith("processor"))
                        {
                            if (ExtractID(reIdMatcher, dataLine, out var processorID))
                            {
                                if (processorList.ContainsKey(processorID))
                                {
                                    // Skip this duplicate processor ID
                                    continue;
                                }

                                processorList.Add(processorID, new clsProcessorCoreInfo(processorID));
                                currentProcessorID = processorID;
                            }
                            continue;
                        }

                        if (dataLine.ToLower().StartsWith("core id"))
                        {
                            if (ExtractID(reIdMatcher, dataLine, out var coreID))
                            {
                                if (processorList.TryGetValue(currentProcessorID, out var coreInfo))
                                {
                                    coreInfo.CoreID = coreID;
                                }
                            }
                        }

                        if (dataLine.ToLower().StartsWith("physical id"))
                        {
                            if (ExtractID(reIdMatcher, dataLine, out var physicalID))
                            {
                                if (processorList.TryGetValue(currentProcessorID, out var coreInfo))
                                {
                                    coreInfo.PhysicalID = physicalID;
                                }
                            }
                        }

                    }
                }

                var hyperthreadedCoreCount= processorList.Count;

                var uniquePhysicalCoreIDs = new SortedSet<string>();

                // To determine the number of actual cores, ignoring hyperthreading, we generate a unique list of PhysicalID_CoreID combos
                foreach (var processor in processorList)
                {
                    var key = processor.Value.PhysicalID + "_" + processor.Value.CoreID;
                    if (!uniquePhysicalCoreIDs.Contains(key))
                        uniquePhysicalCoreIDs.Add(key);
                }

                var coreCountIgnoreHyperthreading = uniquePhysicalCoreIDs.Count;

                if (coreCountIgnoreHyperthreading > 0 && hyperthreadedCoreCount % coreCountIgnoreHyperthreading == 0)
                {
                    // hyperthreadedCoreCount is a multiple of coreCountIgnoreHyperthreading (typically 2x difference)
                    mCoreCountCached = coreCountIgnoreHyperthreading;
                    return coreCountIgnoreHyperthreading;
                }

                if (hyperthreadedCoreCount >= coreCountIgnoreHyperthreading * 2)
                {
                    // hyperthreadedCoreCount is at least double coreCountIgnoreHyperthreading
                    mCoreCountCached = coreCountIgnoreHyperthreading;
                    return coreCountIgnoreHyperthreading;
                }

                if (hyperthreadedCoreCount > 0)
                {
                    mCoreCountCached = hyperthreadedCoreCount;
                    return hyperthreadedCoreCount;
                }

                if (showDebugInfo)
                    ConditionalLogError("Cannot determine core count; expected fields not found in " + cpuInfoFilePath);

                return -1;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetCoreCount: " + ex.Message);

                return -1;
            }
        }

        public float GetCoreUsageByProcessName(string processName, out List<int> processIDs)
        {
            throw new NotImplementedException();

            processIDs = new List<int>();
            return 0;
        }

        /// <summary>
        /// Determine the core usage for a given process
        /// </summary>
        /// <param name="processID"></param>
        /// <param name="cpuUsageTotal">Output: Total CPU usage (value between 0 and 100)</param>
        /// <param name="samplingTimeSeconds">Time (in seconds) to wait while determining CPU usage; default 1, minimum 0.1, maximum 10</param>
        /// <returns>Core usage, or 0 if process not found</returns>
        /// <remarks>If a single core was 100% utilized, this method returns 1</remarks>
        public float GetCoreUsageByProcessID(int processID, out float cpuUsageTotal, float samplingTimeSeconds = 1)
        {
            // Use approach described at
            // https://stackoverflow.com/questions/1420426/how-to-calculate-the-cpu-usage-of-a-process-by-pid-in-linux-from-c
            // See also https://github.com/scaidermern/top-processes/blob/master/top_proc.c


            var showDebugInfo = DateTime.UtcNow.Subtract(mLastDebugInfoTimeCoreUseByProcessID).TotalSeconds > 15;
            if (showDebugInfo)
                mLastDebugInfoTimeCoreUseByProcessID = DateTime.UtcNow;

            cpuUsageTotal = 0;

            try
            {

                var statFilePath = clsPathUtils.CombineLinuxPaths(clsPathUtils.CombineLinuxPaths(
                    ROOT_PROC_DIRECTORY, processID.ToString()), "stat");

                var statFile = new FileInfo(statFilePath);
                if (!statFile.Exists)
                {
                    if (showDebugInfo)
                        OnDebugEvent("stat file not found for ProcessID "+ processID);

                    return 0;
                }

                // Read utime and stime from the stat file for this process ID
                // Wait samplingTimeSeconds seconds, then read the values again

                var successStart = ExtractCPUTimes(statFile, out var utime1, out var stime1);

                if (!successStart)
                {
                    if (showDebugInfo)
                        OnDebugEvent("stat file could not be parsed for ProcessID " + processID);

                    return 0;
                }

                var timeTotal1 = ComputeTotalCPUTime();
                if (timeTotal1 == 0)
                {
                    if (showDebugInfo)
                        OnDebugEvent("system stat file could not be parsed to determine total CPU time");

                    return 0;
                }

                if (samplingTimeSeconds < 0.1)
                    Thread.Sleep(100);
                if (samplingTimeSeconds > 10)
                    Thread.Sleep(10000);
                else
                    Thread.Sleep((int)(samplingTimeSeconds * 1000));

                statFile.Refresh();
                if (!statFile.Exists)
                {
                    // Stat file no longer exists; the process has ended
                    return 0;
                }

                var successEnd = ExtractCPUTimes(statFile, out var utime2, out var stime2);
                if (!successEnd)
                {
                    // Stat file no longer exists; the process has ended
                    return 0;
                }

                var timeTotal2 = ComputeTotalCPUTime();
                if (timeTotal2 == 0)
                {
                    if (showDebugInfo)
                        OnDebugEvent("system stat file could not be parsed to determine total CPU time");

                    return 0;
                }

                var deltaTimeTotal = timeTotal2 - timeTotal1;
                if (deltaTimeTotal < 1)
                {
                    return 0;
                }

                var cpuUsageUser = 100 * (utime2 - utime1) / deltaTimeTotal;
                var cpuUsageSystem = 100 * (stime2 - stime1) / deltaTimeTotal;

                cpuUsageTotal = cpuUsageUser + cpuUsageSystem;
                if (cpuUsageTotal > 100)
                    cpuUsageTotal = 100;

                var coreCount = GetCoreCount();
                var coreUsage = coreCount * cpuUsageTotal / 100;

                return coreUsage;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetCoreUsageByProcessID: " + ex.Message);

                return 0;
            }
        }

        /// <summary>
        /// Determine the free system memory, in MB, on Linux
        /// </summary>
        /// <returns>Free memory, or -1 if an error</returns>
        public float GetFreeMemoryMB()
        {

            var showDebugInfo = DateTime.UtcNow.Subtract(mLastDebugInfoTimeMemory).TotalSeconds > 15;
            if (showDebugInfo)
                mLastDebugInfoTimeMemory = DateTime.UtcNow;

            try
            {
                var memInfoFilePath = clsPathUtils.CombineLinuxPaths(ROOT_PROC_DIRECTORY, MEMINFO_FILE);

                var memInfoFile = new FileInfo(memInfoFilePath);
                if (!memInfoFile.Exists)
                {
                    if (showDebugInfo)
                        ConditionalLogError("Memory info file not found: " + memInfoFilePath);

                    return -1;
                }

                // CentOS 7 and Ubuntu report statistic MemAvailable:
                //   an estimate of how much memory is available for starting new applications, without swapping
                // If present, we use this value, otherwise we report the sum of the matched stats in memoryStatsToSum
                const string MEMAVAILABLE_KEY = "MemAvailable";

                // Keys in this dictionary are memory stats to find
                // Values are initially false, then set to true if a match is found
                var memoryStatsToSum = new Dictionary<string, bool>
                {
                    {"MemFree", false},
                    {"Inactive(file)", false},
                    {"SReclaimable", false}
                };

                var memoryStatKeys = memoryStatsToSum.Keys;

                float totalAvailableMemoryMB = 0;

                Console.WriteLine();

                using (var reader = new StreamReader(new FileStream(memInfoFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        if (dataLine.ToLower().StartsWith(MEMAVAILABLE_KEY))
                        {
                            var memAvailableMB = ExtractMemoryMB(dataLine, showDebugInfo);

                            if (showDebugInfo)
                                OnDebugEvent(string.Format("  {0,17}: {1,6:0} MB", "Available memory", memAvailableMB));

                            return memAvailableMB;
                        }

                        foreach (var memoryStatKey in memoryStatKeys)
                        {
                            if (memoryStatsToSum[memoryStatKey])
                            {
                                // Stat already matched
                                continue;
                            }

                            if (!dataLine.StartsWith(memoryStatKey, StringComparison.OrdinalIgnoreCase))
                                continue;

                            var memorySizeMB = ExtractMemoryMB(dataLine, showDebugInfo);
                            if (memorySizeMB > -1)
                            {
                                if (showDebugInfo)
                                    OnDebugEvent(string.Format("  {0,17}: {1,6:0} MB", memoryStatKey, memorySizeMB));

                                totalAvailableMemoryMB += memorySizeMB;
                                memoryStatsToSum[memoryStatKey] = true;
                                break;
                            }
                        }
                    }
                }

                if ((from item in memoryStatsToSum where item.Value select item).Any())
                {
                    if (showDebugInfo)
                    {
                        OnDebugEvent("   ---------------------------");
                        OnDebugEvent(string.Format("  {0,17}: {1,6:0} MB", "Available memory", totalAvailableMemoryMB));
                    }

                    return totalAvailableMemoryMB;
                }

                if (showDebugInfo)
                    ConditionalLogError("MemFree statistic not found in " + memInfoFilePath);

                return -1;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetFreeMemoryMB: " + ex.Message);

                return -1;
            }
        }

        /// <summary>
        /// Determine the version of Linux that we're running
        /// </summary>
        /// <returns>String describing the OS version</returns>
        public string GetLinuxVersion()
        {
            var osVersionInfo = new clsOSVersionInfo();
            return osVersionInfo.GetLinuxVersion();
        }

        #endregion

    }
}
