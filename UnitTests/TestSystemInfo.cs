using System;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    internal class TestSystemInfo
    {
        private SystemInfo mSysInfo;

        [OneTimeSetUp]
        public void InitSystemInfo()
        {
            mSysInfo = new SystemInfo();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetProcesses(bool lookupCommandLineInfo)
        {
            var processes = mSysInfo.GetProcesses(lookupCommandLineInfo);

            Console.WriteLine("{0,-8} {1,-40} {2}", "ID", "Name", "ExePath");

            foreach (var process in processes)
            {
                var procInfo = process.Value;
                Console.WriteLine("{0,-8} {1,-40} {2}", procInfo.ProcessID, procInfo.ProcessName, procInfo.ExePath);
            }

            if (!lookupCommandLineInfo)
                return;

            Console.WriteLine();
            Console.WriteLine("Processes with command line arguments");
            foreach (var process in processes)
            {
                var procInfo = process.Value;
                if (procInfo.ArgumentList.Count == 0)
                    continue;

                Console.WriteLine();
                Console.WriteLine("Process ID {0}", procInfo.ProcessID);
                Console.WriteLine("{0} {1}", procInfo.ExeName, procInfo.Arguments);
            }
        }

        [TestCase]
        public void GetCoreCount()
        {
            var coreCount = SystemInfo.GetCoreCount();
            var logicalCoreCount = SystemInfo.GetLogicalCoreCount();
            var numaNodeCount = SystemInfo.GetNumaNodeCount();
            var processorPackageCount = SystemInfo.GetProcessorPackageCount();

            Console.WriteLine("Core Count:          {0}", coreCount);
            Console.WriteLine("Logical Core Count:  {0}", logicalCoreCount);
            Console.WriteLine("NUMA Node Count:     {0}", numaNodeCount);
            Console.WriteLine("Processor Pkg Count: {0}", processorPackageCount);
        }

        [TestCase]
        public void GetMemoryStats()
        {
            var freeMemoryMB = SystemInfo.GetFreeMemoryMB();
            var totalMemoryMB = SystemInfo.GetTotalMemoryMB();

            Console.WriteLine("Free Memory:  {0:N0} MB", freeMemoryMB);
            Console.WriteLine("Total Memory: {0:N0} MB", totalMemoryMB);
        }
    }
}
