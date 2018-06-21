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
    public class clsLinuxSystemInfo : clsEventNotifier, ISystemInfo
    {

        #region "Constants and Enums"

        /// <summary>
        /// Path to proc virtual filesystem
        /// </summary>
        public const string ROOT_PROC_DIRECTORY = "/proc";

        /// <summary>
        /// Name of cpuinfo file
        /// </summary>
        public const string CPUINFO_FILE = "cpuinfo";

        /// <summary>
        /// name of meminfo file
        /// </summary>
        public const string MEMINFO_FILE = "meminfo";

        #endregion

        #region "Classwide Variables"

        private int mCoreCountCached;
        private int mProcessorPackageCountCached;

        private float mTotalMemoryMBCached;

        private readonly bool mLimitLoggingByTimeOfDay;

        private DateTime mLastDebugInfoTimeCoreCount;

        private DateTime mLastDebugInfoTimeCoreUseByProcessID;

        private DateTime mLastDebugInfoTimeMemory;

        private DateTime mLastDebugInfoTimeProcesses;

        private readonly Regex mMemorySizeMatcher;

        private readonly Regex mMemorySizeMatcherNoUnits;

        private readonly Regex mCpuIdleTimeMatcher;
        private readonly Regex mCpuIdleTimeMatcherNoIOWait;

        private readonly Regex mStatLineMatcher;
        private readonly Regex mStatLineMatcherNoCommand;

        #endregion

        #region "Properties"

        /// <summary>
        /// When true, additional debug messages are reported using DebugEvent
        /// </summary>
        public bool TraceEnabled { get; set; }

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="limitLoggingByTimeOfDay">When true, only log errors between 12 am and 12:30 am</param>
        /// <remarks>
        /// To view debug events raised by this class, either subscribe to event DebugEvent
        /// or set SkipConsoleWriteIfNoDebugListener to false
        /// </remarks>
        public clsLinuxSystemInfo(bool limitLoggingByTimeOfDay = false)
        {
            mCoreCountCached = 0;

            mTotalMemoryMBCached = 0;

            mLimitLoggingByTimeOfDay = limitLoggingByTimeOfDay;

            mLastDebugInfoTimeCoreCount = DateTime.UtcNow.AddMinutes(-1);

            mLastDebugInfoTimeCoreUseByProcessID = DateTime.UtcNow.AddMinutes(-1);

            mLastDebugInfoTimeMemory = DateTime.UtcNow.AddMinutes(-1);

            mLastDebugInfoTimeProcesses = DateTime.UtcNow.AddMinutes(-1);

            mMemorySizeMatcher = new Regex(@"(?<Size>\d+) +(?<Units>(KB|MB|GB|TB|))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            mMemorySizeMatcherNoUnits = new Regex(@"(?<Size>\d+)", RegexOptions.Compiled);

            // CPU time fields are described in method ComputeTotalCPUTime
            mCpuIdleTimeMatcher = new Regex(@"^\S+\s+(?<User>\d+) (?<Nice>\d+) (?<System>\d+) (?<Idle>\d+) (?<IOWait>\d+)", RegexOptions.Compiled);

            mCpuIdleTimeMatcherNoIOWait = new Regex(@"^\S+\s+(?<User>\d+) (?<Nice>\d+) (?<System>\d+) (?<Idle>\d+)", RegexOptions.Compiled);

            // The following two Regex are used to parse stat files for running processes
            // Fields are described in method ExtractCPUTimes

            // This regex matches the ProcessID and command name, plus the various stats
            mStatLineMatcher = new Regex(@"^(?<pid>\d+) (?<command>\([^)]+\)) (?<state>\S) (?<ppid>[0-9-]+) (?<pgrp>[0-9-]+) (?<session>[0-9-]+) (?<tty_nr>[0-9-]+) (?<tty_pgrp>[0-9-]+) (?<flags>\d+) (?<minflt>\d+) (?<cminflt>\d+) (?<majflt>\d+) (?<cmajflt>\d+) (?<utime>\d+) (?<stime>\d+)");

            // This is a fallback Regex that starts at state in case mStatLineMatcher fails
            mStatLineMatcherNoCommand = new Regex(@"(?<state>[A-Za-z]) (?<ppid>[0-9-]+) (?<pgrp>[0-9-]+) (?<session>[0-9-]+) (?<tty_nr>[0-9-]+) (?<tty_pgrp>[0-9-]+) (?<flags>\d+) (?<minflt>\d+) (?<cminflt>\d+) (?<majflt>\d+) (?<cmajflt>\d+) (?<utime>\d+) (?<stime>\d+)");

            // Prevent DebugEvent messages from being displayed at console if the calling class has not subscribed to DebugEvent
            SkipConsoleWriteIfNoDebugListener = true;
        }

        /// <summary>
        /// Compute total CPU time (sum of processing times, idle times, wait times, etc.)
        /// </summary>
        /// <returns>Total CPU time, in jiffies</returns>
        private long ComputeTotalCPUTime()
        {
            return ComputeTotalCPUTime(out _);
        }

        /// <summary>
        /// Compute total CPU time (sum of processing times, idle times, wait times, etc.)
        /// </summary>
        /// <param name="idleTime">Idle time, in jiffies (sum of idle and iowait times)</param>
        /// <returns>Total CPU time, in jiffies</returns>
        private long ComputeTotalCPUTime(out long idleTime)
        {

            idleTime = 0;

            var cpuStatFilePath = clsPathUtils.CombineLinuxPaths(ROOT_PROC_DIRECTORY, "stat");
            var cpuStatFile = new FileInfo(cpuStatFilePath);

            if (!cpuStatFile.Exists)
            {
                OnDebugEvent("CPU stats file not found at " + cpuStatFilePath);
                return 0;
            }

            if (TraceEnabled)
            {
                OnDebugEvent("Opening " + cpuStatFile.FullName);
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

                // Fields are:
                //  user       Running normal processes executing in user mode
                //  nice       Running user processes with low priority
                //  system     Running system processes
                //  idle       Idle time
                //  iowait     Waiting for I/O to complete
                //  irq        Servicing interrupts
                //  softirq    Servicing softirqs
                //  steal      Time spent in other operating systems when running in a virtualized environment
                //  guest      Time spent running a virtual CPU for guest operating systems (included in user)
                //  guest_nice Time spent running a virtual CPU with low priority for guest operating systems (included in nice)

                // Sum all of the numbers following cpu
                var fields = dataLine.Split(' ');
                if (fields.Length < 2)
                    return 0;

                long totalJiffies = 0;

                for (var i = 1; i < fields.Length; i++)
                {
                    if (i > 8)
                    {
                        // Do not include the "guest" columns
                        // This is mentioned at https://stackoverflow.com/questions/23367857/1179467/accurate-calculation-of-cpu-usage-given-in-percentage-in-linux/
                        break;
                    }

                    if (long.TryParse(fields[i], out var clockTimeJiffies))
                    {
                        totalJiffies += clockTimeJiffies;
                    }
                }

                var match = mCpuIdleTimeMatcher.Match(dataLine);
                if (match.Success)
                {
                    idleTime = long.Parse(match.Groups["Idle"].Value) + long.Parse(match.Groups["IOWait"].Value);
                }
                else
                {
                    var match2 = mCpuIdleTimeMatcherNoIOWait.Match(dataLine);
                    if (match2.Success)
                    {
                        idleTime = long.Parse(match2.Groups["Idle"].Value);
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

        /// <summary>
        /// Parse utime and stime from a stat file for a given process
        /// </summary>
        /// <param name="statFile"></param>
        /// <param name="utime">Amount of time that the process has been scheduled in user mode, in jiffies</param>
        /// <param name="stime">Amount of time that the process has been scheduled in kernel mode, in jiffies</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// For multithreaded applications, the task directory below the ProcessID directory will have
        /// separate ProcessID directories for each thread.  Those directories could be parsed to determine
        /// the processing time for each thread.  However, the stat file in the base ProcessID directory
        /// has the combined processing time for all threads, so parsing of individual thread stat times
        /// is not necessary to determine overall processing time.
        /// </remarks>
        private bool ExtractCPUTimes(FileSystemInfo statFile, out long utime, out long stime)
        {

            if (TraceEnabled)
            {
                OnDebugEvent("Opening " + statFile.FullName);
            }

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

                    // Process Stat fields (for details, see: man proc)
                    //
                    //  Field          Content
                    //  -----          -------
                    //  pid            Process id
                    //  tcomm          Filename of the executable, in parentheses
                    //  state          State (R is running, S is sleeping, D is waiting, Z is zombie, T is stopped, t is TraceStopped, W is paging, X or x is dead, K is Wakekill, W is waking, P is parked)
                    //  ppid           Process id of the parent process
                    //  pgrp           Process group id; child threads of a parent process all have the same pgrp value, equivalent to the pid of the parent (initial) process
                    //  sid            Session id
                    //  tty_nr         Controlling terminal of the process
                    //  tty_pgrp       ID of the foreground process group of the controlling terminal of the process
                    //  flags          Task flags
                    //  min_flt        Number of minor faults
                    //  cmin_flt       Number of minor faults that the process's waited-for children have made
                    //  maj_flt        Number of major faults
                    //  cmaj_flt       Number of major faults that the process's waited-for children have made
                    //  utime          Amount of time that the process has been scheduled in user mode, in jiffies
                    //  stime          Amount of time that the process has been scheduled in kernel mode, in jiffies
                    //  cutime         Amount of time that the process's waited-for children have been scheduled in user mode, in jiffies
                    //  cstime         Amount of time that the process's waited-for children have been scheduled in kernel mode, in jiffies
                    //  priority       Value between 0 (high priority) and 39 (low priority), default 20; corresponds to the user-visible nice range of -20 to 19
                    //  nice           Value in the range -20 (high priority) to 19 (low priority), default 0
                    //  numthreads     Number of threads in the process
                    //  it_real_value  Obsolete (always 0)
                    //  start_time     The time in jiffies the process started after system boot
                    //  vsize          Virtual memory size in bytes
                    //  rss            Resident Set Size: number of pages the process has in real memory

                    var match = mStatLineMatcher.Match(dataLine);

                    if (!match.Success)
                    {
                        match = mStatLineMatcherNoCommand.Match(dataLine);
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

            var matchUnits = mMemorySizeMatcher.Match(dataLine);

            if (matchUnits.Success)
            {
                match = matchUnits;
                units = matchUnits.Groups["Units"].Value.ToLower();
            }
            else
            {
                var matchNoUnits = mMemorySizeMatcherNoUnits.Match(dataLine);

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
                    memorySizeMB = memorySize;
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

        private bool GetCmdLineFileInfo(FileSystemInfo cmdLineFile, out string exePath, out List<string> argumentList)
        {
            exePath = string.Empty;
            argumentList = new List<string>();

            try
            {
                if (TraceEnabled)
                {
                    OnDebugEvent("Opening " + cmdLineFile.FullName);
                }

                using (var reader = new StreamReader(new FileStream(cmdLineFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    if (reader.EndOfStream)
                        return false;

                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(dataLine))
                        return false;

                    // Split dataLine on the null terminator
                    var fields = dataLine.Split('\0');

                    if (fields.Length == 0)
                        return false;

                    for (var i = 0; i < fields.Length; i++)
                    {
                        if (i == 0)
                            exePath = fields[i];
                        else if (!string.IsNullOrWhiteSpace(fields[i]))
                            argumentList.Add(fields[i]);
                    }

                    return true;
                }
            }
            catch
            {
                // Ignore errors
                return false;
            }
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

            // Can also use lscpu (if installed) to quickly get this and more processor information

            try
            {
                var cpuInfoFilePath = clsPathUtils.CombineLinuxPaths(ROOT_PROC_DIRECTORY, CPUINFO_FILE);

                var cpuInfoFile = new FileInfo(cpuInfoFilePath);
                if (!cpuInfoFile.Exists)
                {
                    if (showDebugInfo)
                        ConditionalLogError("CPU info file not found: " + cpuInfoFile.FullName);

                    return -1;
                }

                if (TraceEnabled)
                {
                    OnDebugEvent("Opening " + cpuInfoFile.FullName);
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

                var hyperthreadedCoreCount = processorList.Count;

                var uniquePhysicalCoreIDs = new SortedSet<string>();

                // To determine the number of actual cores, ignoring hyperthreading, we generate a unique list of PhysicalID_CoreID combos
                foreach (var processor in processorList)
                {
                    var key = processor.Value.PhysicalID + "_" + processor.Value.CoreID;
                    if (!uniquePhysicalCoreIDs.Contains(key))
                        uniquePhysicalCoreIDs.Add(key);
                }

                // Distinct processor packages
                mProcessorPackageCountCached = processorList.Select(x => x.Value.PhysicalID).Distinct().Count();

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

        /// <inheritdoc />
        /// <summary>
        /// Report the number of logical cores on this system
        /// </summary>
        /// <returns>The number of logical cores on this computer</returns>
        /// <remarks>
        /// Will be affected by hyperthreading, so a computer with two 8-core chips will report 32 cores if Hyperthreading is enabled
        /// </remarks>
        public int GetLogicalCoreCount()
        {
            return Environment.ProcessorCount;
        }

        /// <inheritdoc />
        /// <summary>
        /// Report the number of processor packages on this system
        /// </summary>
        /// <returns>The number of processor packages on this computer</returns>
        public int GetProcessorPackageCount()
        {
            if (mProcessorPackageCountCached > 0)
            {
                return mProcessorPackageCountCached;
            }

            GetCoreCount();

            return mProcessorPackageCountCached;
        }

        /// <inheritdoc />
        /// <summary>
        /// Report the number of NUMA Nodes on this system
        /// </summary>
        /// <returns>The number of NUMA Nodes on this computer</returns>
        public int GetNumaNodeCount()
        {
            // TODO: actually get the number of NUMA nodes in a generally-supported way. lscpu is one potential option.
            return GetProcessorPackageCount();
        }

        /// <summary>
        /// Reports the number of cores in use by the given process
        /// This method takes at least 1000 msec to execute
        /// </summary>
        /// <param name="processName">
        /// Process name, for example mono (full matches only; partial matches are ignored)
        /// Can either be just a program name like mono, or the full path to the program (e.g. /usr/local/bin/mono)</param>
        /// <param name="argumentText">Optional text to require is contained in one of the command line arguments passed to the program</param>
        /// <param name="processIDs">Output: list of matching process IDs</param>
        /// <param name="samplingTimeSeconds">Time (in seconds) to wait while determining CPU usage; default 1, minimum 0.1, maximum 10</param>
        /// <returns>Number of cores in use; -1 if process not found or if a problem</returns>
        /// <remarks>
        /// Core count is typically an integer, but can be a fractional number if not using a core 100%
        /// If multiple processes are running with the given name, returns the total core usage for all of them
        /// </remarks>
        public float GetCoreUsageByProcessName(string processName, string argumentText, out List<int> processIDs, float samplingTimeSeconds = 1)
        {

            var showDebugInfo = DateTime.UtcNow.Subtract(mLastDebugInfoTimeCoreUseByProcessID).TotalSeconds > 15;
            if (showDebugInfo)
                mLastDebugInfoTimeCoreUseByProcessID = DateTime.UtcNow;

            processIDs = new List<int>();

            try
            {
                // Examine the processes tracked by Process IDs in the /proc directory
                var processes = GetProcesses();
                if (processes.Count == 0)
                {
                    // No processes found; an error has likely already been logged
                    return -1;
                }

                var matchProgramNameOnly = !processName.Contains("/");

                foreach (var process in processes.Values)
                {

                    if (matchProgramNameOnly)
                    {
                        var processIdProgName = Path.GetFileName(process.ExePath);
                        if (string.IsNullOrWhiteSpace(processIdProgName))
                            continue;

                        if (!processIdProgName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    else
                    {
                        if (!process.ExePath.Equals(processName, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    if (!string.IsNullOrWhiteSpace(argumentText))
                    {
                        var validMatch = process.ArgumentList.Any(argument => argument.IndexOf(argumentText, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!validMatch)
                            continue;
                    }

                    processIDs.Add(process.ProcessID);
                }

                if (processIDs.Count == 0)
                    return -1;

                var coreUsage = GetCoreUsageByProcessID(processIDs, out _, samplingTimeSeconds);
                return coreUsage;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetCoreUsageByProcessName: " + ex.Message);

                return -1;
            }
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
            return GetCoreUsageByProcessID(new List<int> { processID }, out cpuUsageTotal, samplingTimeSeconds);
        }

        /// <summary>
        /// Determine the total core usage for a list of Process IDs
        /// </summary>
        /// <param name="processIDs">List of Process IDs to examine</param>
        /// <param name="cpuUsageTotal">Output: Total CPU usage (value between 0 and 100)</param>
        /// <param name="samplingTimeSeconds">Time (in seconds) to wait while determining CPU usage; default 1, minimum 0.1, maximum 10</param>
        /// <returns>Core usage, or 0 if process not found</returns>
        /// <remarks>If a single core was 100% utilized, this method returns 1</remarks>
        public float GetCoreUsageByProcessID(List<int> processIDs, out float cpuUsageTotal, float samplingTimeSeconds = 1)
        {
            // Use approach described at
            // https://stackoverflow.com/questions/1420426/1179467/how-to-calculate-the-cpu-usage-of-a-process-by-pid-in-linux-from-c
            // See also https://github.com/scaidermern/top-processes/blob/master/top_proc.c


            var showDebugInfo = DateTime.UtcNow.Subtract(mLastDebugInfoTimeCoreUseByProcessID).TotalSeconds > 15;
            if (showDebugInfo)
                mLastDebugInfoTimeCoreUseByProcessID = DateTime.UtcNow;

            cpuUsageTotal = 0;

            try
            {
                var coreCount = GetCoreCount();
                if (coreCount < 1)
                {
                    if (showDebugInfo)
                        OnDebugEvent("Could not determine the number of cores on this system");

                    return 0;
                }

                var timeTotal1 = ComputeTotalCPUTime();
                if (timeTotal1 == 0)
                {
                    if (showDebugInfo)
                        OnDebugEvent("System stat file could not be parsed to determine total CPU time");

                    return 0;
                }

                // Keys in this dictionary are paths to stat files, values are utime and stime values parsed from the stat flie
                var statFileTimes = new Dictionary<FileInfo, Tuple<long, long>>();
                var errorMessage = string.Empty;

                foreach (var processID in processIDs)
                {
                    var statFilePath = clsPathUtils.CombineLinuxPaths(clsPathUtils.CombineLinuxPaths(
                        ROOT_PROC_DIRECTORY, processID.ToString()), "stat");

                    var statFile = new FileInfo(statFilePath);
                    if (!statFile.Exists)
                    {
                        errorMessage = "Stat file not found for ProcessID " + processID;
                        continue;
                    }

                    // Read utime and stime from the stat file for processID
                    var success = ExtractCPUTimes(statFile, out var utime, out var stime);

                    if (!success)
                    {
                        errorMessage = "Stat file could not be parsed for ProcessID " + processID;
                        continue;
                    }

                    statFileTimes.Add(statFile, new Tuple<long, long>(utime, stime));
                }

                if (processIDs.Count == 1 && statFileTimes.Count == 0)
                {
                    if (showDebugInfo && !string.IsNullOrWhiteSpace(errorMessage))
                        OnDebugEvent(errorMessage);

                    return 0;
                }

                // Wait samplingTimeSeconds seconds, then read the values again
                if (samplingTimeSeconds < 0.1)
                    Thread.Sleep(100);
                if (samplingTimeSeconds > 10)
                    Thread.Sleep(10000);
                else
                    Thread.Sleep((int)(samplingTimeSeconds * 1000));

                var timeTotal2 = ComputeTotalCPUTime();
                if (timeTotal2 == 0)
                {
                    if (showDebugInfo)
                        OnDebugEvent("System stat file could not be parsed to determine total CPU time");

                    return 0;
                }

                var deltaTimeTotal = timeTotal2 - timeTotal1;
                if (deltaTimeTotal < 1)
                {
                    // No increase in CPU time
                    return 0;
                }

                float totalCoreUsage = 0;

                foreach (var item in statFileTimes)
                {
                    var statFile = item.Key;
                    var utime1 = item.Value.Item1;
                    var stime1 = item.Value.Item2;

                    statFile.Refresh();
                    if (!statFile.Exists)
                    {
                        // Stat file no longer exists; the process has ended
                        continue;
                    }

                    var success = ExtractCPUTimes(statFile, out var utime2, out var stime2);
                    if (!success)
                    {
                        // Stat file no longer exists; the process has ended
                        continue;
                    }

                    var cpuUsage = 0f;

                    var deltaUserTime = utime2 - utime1;
                    if (deltaUserTime > 0)
                    {
                        var cpuUsageUser = deltaUserTime / (float)deltaTimeTotal * 100;
                        cpuUsage += cpuUsageUser;
                    }

                    var deltaSystemTime = stime2 - stime1;
                    if (deltaSystemTime > 0)
                    {
                        var cpuUsageSystem = deltaSystemTime / (float)deltaTimeTotal * 100;
                        cpuUsage += cpuUsageSystem;
                    }

                    if (cpuUsage > 100)
                        cpuUsage = 100;

                    var coreUsage = coreCount * cpuUsage / 100;

                    totalCoreUsage += coreUsage;
                    cpuUsageTotal += cpuUsage;
                }

                return totalCoreUsage;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetCoreUsageByProcessID: " + ex.Message);

                return 0;
            }
        }

        /// <summary>
        /// Returns the CPU usage
        /// </summary>
        /// <returns>Value between 0 and 100</returns>
        /// <remarks>
        /// <param name="samplingTimeSeconds">Time (in seconds) to wait while determining CPU usage; default 1, minimum 0.1, maximum 10</param>
        /// This is CPU usage for all running applications, not just this application
        /// For CPU usage of a single application use GetCoreUsageByProcessID()
        /// </remarks>
        public float GetCPUUtilization(float samplingTimeSeconds = 1)
        {
            try
            {
                var timeTotal1 = ComputeTotalCPUTime(out var idleTime1);
                if (timeTotal1 == 0)
                {
                    return 0;
                }

                if (samplingTimeSeconds < 0.1)
                    Thread.Sleep(100);
                if (samplingTimeSeconds > 10)
                    Thread.Sleep(10000);
                else
                    Thread.Sleep((int)(samplingTimeSeconds * 1000));


                var timeTotal2 = ComputeTotalCPUTime(out var idleTime2);
                if (timeTotal2 == 0)
                {
                    return 0;
                }

                var deltaTimeTotal = timeTotal2 - timeTotal1;
                if (deltaTimeTotal < 1)
                {
                    return 0;
                }

                var cpuUtilization = 100 - (idleTime2 - idleTime1) / (float)deltaTimeTotal * 100;

                return cpuUtilization;
            }
            catch (Exception ex)
            {
                ConditionalLogError("Error in GetCPUUtilization: " + ex.Message);

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

                if (TraceEnabled)
                {
                    OnDebugEvent("Opening " + memInfoFile.FullName);
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
        /// Look for currently active processes
        /// </summary>
        /// <param name="lookupCommandLineInfo">Ignored on Linux, but required due to this class implementing ISystemInfo</param>
        /// <returns>Dictionary where keys are process ID and values are ProcessInfo</returns>
        /// <remarks></remarks>
        public Dictionary<int, ProcessInfo> GetProcesses(bool lookupCommandLineInfo = true)
        {

            var showDebugInfo = DateTime.UtcNow.Subtract(mLastDebugInfoTimeProcesses).TotalSeconds > 15;
            if (showDebugInfo)
                mLastDebugInfoTimeProcesses = DateTime.UtcNow;

            var processList = new Dictionary<int, ProcessInfo>();

            try
            {
                // Examine the processes tracked by Process IDs in the /proc directory
                var procDirectory = new DirectoryInfo(ROOT_PROC_DIRECTORY);
                if (!procDirectory.Exists)
                {
                    if (showDebugInfo)
                        OnDebugEvent("Proc directory not found at " + ROOT_PROC_DIRECTORY);

                    return processList;
                }

                foreach (var processIdDirectory in procDirectory.GetDirectories())
                {
                    if (!int.TryParse(processIdDirectory.Name, out var processId))
                    {
                        // Skip directories that are not an integer
                        continue;
                    }

                    // Open the cmdline file (if it exists) to determine the process name and commandline arguments
                    var cmdLineFilePath = clsPathUtils.CombineLinuxPaths(clsPathUtils.CombineLinuxPaths(
                        ROOT_PROC_DIRECTORY, processIdDirectory.Name), "cmdline");

                    var cmdLineFile = new FileInfo(cmdLineFilePath);
                    if (!cmdLineFile.Exists)
                        continue;

                    var success = GetCmdLineFileInfo(cmdLineFile, out var exePath, out var arguments);
                    if (!success)
                        continue;

                    string processName;
                    if (exePath.StartsWith("sshd:"))
                    {
                        processName = string.Copy(exePath);
                    }
                    else
                    {
                        processName = Path.GetFileName(exePath);
                    }

                    var process = new ProcessInfo(processId, processName, exePath, arguments);

                    processList.Add(processId, process);
                }

                return processList;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetProcesses: " + ex.Message);

                return processList;
            }

        }

        /// <summary>
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        public float GetTotalMemoryMB()
        {
            if (mTotalMemoryMBCached > 0)
            {
                return mTotalMemoryMBCached;
            }

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

                if (TraceEnabled)
                {
                    OnDebugEvent("Opening " + memInfoFile.FullName);
                }

                using (var reader = new StreamReader(new FileStream(memInfoFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        if (dataLine.ToLower().StartsWith("MemTotal", StringComparison.OrdinalIgnoreCase))
                        {
                            var memTotalMB = ExtractMemoryMB(dataLine, showDebugInfo);

                            if (showDebugInfo)
                                OnDebugEvent(string.Format("  {0,17}: {1,6:0} MB", "Total memory", memTotalMB));

                            mTotalMemoryMBCached = memTotalMB;

                            return memTotalMB;
                        }
                    }
                }

                if (showDebugInfo)
                    ConditionalLogError("MemTotal statistic not found in " + memInfoFilePath);

                return -1;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetTotalMemoryMB: " + ex.Message);

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
