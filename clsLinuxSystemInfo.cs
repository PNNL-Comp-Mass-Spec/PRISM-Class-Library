using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PRISM
{

    /// <summary>
    /// Methods to determine memory usage, CPU usage, and Linux system version
    /// </summary>
    public class clsLinuxSystemInfo : clsEventNotifier
    {

        #region "Constants and Enums"

        public const string CPUINFO_FILE_PATH = "/proc/cpuinfo";

        public const string MEMINFO_FILE_PATH = "/proc/meminfo";

        #endregion

        #region "Classwide Variables"

        private readonly bool mLimitLoggingByTimeOfDay;

        private DateTime mLastDebugInfoTimeCoreCount;

        private DateTime mLastDebugInfoTimeMemory;

        private readonly Regex mRegexMemorySize;

        private readonly Regex mRegexMemorySizeNoUnits;

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        public clsLinuxSystemInfo(bool limitLoggingByTimeOfDay)
        {
            mLimitLoggingByTimeOfDay = limitLoggingByTimeOfDay;

            mLastDebugInfoTimeCoreCount = DateTime.UtcNow.AddMinutes(-1);

            mLastDebugInfoTimeMemory = DateTime.UtcNow.AddMinutes(-1);

            mRegexMemorySize = new Regex(@"(?<Size>\d+) +(?<Units>(KB|MB|GB|TB|))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            mRegexMemorySizeNoUnits = new Regex(@"(?<Size>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private void ConditionalLogError(string message, Exception ex = null)
        {
            if (mLimitLoggingByTimeOfDay)
            {

                // To avoid seeing this in the logs continually, we will only post this log message between 12 am and 12:30 am
                // A possible fix for this is to add the user who is running this process to the "Performance Monitor Users" group
                // in "Local Users and Groups" on the machine showing this error.  Alternatively, add the user to the "Administrators" group.
                // In either case, you will need to reboot the computer for the change to take effect
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
        /// </remarks>
        public int GetCoreCount()
        {

            var showDebugInfo = DateTime.UtcNow.Subtract(mLastDebugInfoTimeCoreCount).TotalSeconds > 15;
            if (showDebugInfo)
                mLastDebugInfoTimeCoreCount = DateTime.UtcNow;

            try
            {

                var cpuInfoFile = new FileInfo(CPUINFO_FILE_PATH);
                if (!cpuInfoFile.Exists)
                {
                    if (showDebugInfo)
                        ConditionalLogError("CPU info file not found: " + CPUINFO_FILE_PATH);

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
                    return coreCountIgnoreHyperthreading;
                }

                if (hyperthreadedCoreCount >= coreCountIgnoreHyperthreading * 2)
                {
                    // hyperthreadedCoreCount is at least double coreCountIgnoreHyperthreading
                    return coreCountIgnoreHyperthreading;
                }

                if (hyperthreadedCoreCount > 0)
                {
                    return hyperthreadedCoreCount;
                }

                if (showDebugInfo)
                    ConditionalLogError("Cannot determine core count; expected fields not found in " + CPUINFO_FILE_PATH);

                return -1;

            }
            catch (Exception ex)
            {
                if (showDebugInfo)
                    ConditionalLogError("Error in GetCoreCount: " + ex.Message);

                return -1;
            }
        }

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

        public static float GetCoreUsageByProcessName(string processName, out List<int> processIDs)
        {
            processIDs = new List<int>();
            return 0;
        }

        public static float GetCoreUsageByProcessID(int processID)
        {
            // Use approach described at
            // https://stackoverflow.com/questions/1420426/how-to-calculate-the-cpu-usage-of-a-process-by-pid-in-linux-from-c
            // See also https://github.com/scaidermern/top-processes/blob/master/top_proc.c

            return 0;
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

                var memInfoFile = new FileInfo(MEMINFO_FILE_PATH);
                if (!memInfoFile.Exists)
                {
                    if (showDebugInfo)
                        ConditionalLogError("Memory info file not found: " + MEMINFO_FILE_PATH);

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
                    ConditionalLogError("MemFree statistic not found in " + MEMINFO_FILE_PATH);

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
