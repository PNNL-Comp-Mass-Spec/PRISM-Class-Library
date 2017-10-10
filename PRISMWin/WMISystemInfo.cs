﻿using System;

namespace PRISMWin
{
    /// <summary>
    /// A utility class that uses WMI to get some CPU and memory information. The PRISM.WindowsSystemInfo provides more information, and is more accurate.
    /// </summary>
    public class WMISystemInfo
    {
        private static int cachedCoreCount;
        private static int cachedPhysicalProcessorCount;
        private static float cachedTotalMemoryMB;

        /// <summary>
        /// Report the number of cores on this system
        /// </summary>
        /// <returns>The number of cores on this computer</returns>
        /// <remarks>
        /// Should not be affected by hyperthreading, so a computer with two 8-core chips will report 16 cores, even if Hyperthreading is enabled
        /// </remarks>
        public int GetCoreCount()
        {
            if (cachedCoreCount > 0)
            {
                return cachedCoreCount;
            }

            // Try to get the number of physical cores in the system - requires System.Management.dll and a WMI query, but the performance penalty for
            // using the number of logical processors in a hyperthreaded system is significant, and worse than the penalty for using fewer than all physical cores.
            var numPhysicalCores = 0;
            var numPhysicalProcessors = 0;

            try
            {
                foreach (var item in new System.Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get())
                {
                    numPhysicalProcessors++;
                    numPhysicalCores += int.Parse(item["NumberOfCores"].ToString());
                }
            }
            catch (Exception)
            {
                // Use the logical processor count, divided by 2 to avoid the greater performance penalty of over-threading.
                numPhysicalCores = (int)(Math.Ceiling(Environment.ProcessorCount / 2.0));
            }

            cachedCoreCount = numPhysicalCores;

            return numPhysicalCores;
        }

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

        /// <summary>
        /// Determine the free system memory, in MB, on Linux
        /// </summary>
        /// <returns>Free memory, or -1 if an error</returns>
        public float GetFreeMemoryMB()
        {
            double memoryFreeKB = 0;
            //var osVersion = Environment.OSVersion.Version;
            //if (osVersion < new Version(6, 0)) // Older than Vista
            //{
            //    // For pre-Vista: "SELECT * FROM Win32_LogicalMemoryConfiguration", and a different property.
            //    // Have no good systems to test on (Sequest head nodes??)
            //}
            foreach (var item in new System.Management.ManagementObjectSearcher("SELECT * FROM CIM_OperatingSystem").Get())
            {
                memoryFreeKB += double.Parse(item["FreePhysicalMemory"].ToString());
                //foreach (var p in item.Properties)
                //{
                //    Console.WriteLine("{0}: {1}", p.Name, p.Value);
                //}
            }

            return (float)(memoryFreeKB / 1024);
        }

        /// <summary>
        /// Determine the total system memory, in MB
        /// </summary>
        /// <returns>Total memory, or -1 if an error</returns>
        public float GetTotalMemoryMB()
        {
            if (cachedTotalMemoryMB > 0)
            {
                return cachedTotalMemoryMB;
            }

            double totalMemKB = 0;
            // Get total physical memory
            foreach (var item in new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem").Get())
            {
                // TotalPhysicalMemory is in Bytes, so divide by 1024
                totalMemKB += double.Parse(item["TotalPhysicalMemory"].ToString()) / 1024.0;
                //foreach (var p in item.Properties)
                //{
                //    Console.WriteLine("{0}: {1}", p.Name, p.Value);
                //}
            }

            var totalMemMB = (float)(totalMemKB / 1024);

            cachedTotalMemoryMB = totalMemMB;

            return totalMemMB;
        }
    }
}
