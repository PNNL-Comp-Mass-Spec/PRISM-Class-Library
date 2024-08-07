﻿using System;
using NUnit.Framework;
using PRISM;
#if !NETCOREAPP2_0
using PRISMWin;
#endif

namespace PRISMTest
{
    [TestFixture]
    public class TestWindowsSystemInfo
    {
        // Ignore Spelling: PInv

        [Test]
        public void TestGetTotalMemory()
        {
#if !NETCOREAPP2_0
            var windowsSystemInfo = new WMISystemInfo();
            var wmiMem = windowsSystemInfo.GetTotalMemoryMB();
            Console.WriteLine("WMI Total memory:  {0:F2} MB", wmiMem);
#endif
            // Use P/Invoke to access kernel32.dll
            var wpsi = new WindowsSystemInfo();
            var pinvMem = wpsi.GetTotalMemoryMB();
            Console.WriteLine("PInv Total memory: {0:F2} MB", pinvMem);

#if !NETCOREAPP2_0
            Assert.That(pinvMem, Is.EqualTo(wmiMem).Within(0.001));
#endif
        }

        [Test]
        public void TestGetFreeMemory()
        {
#if !NETCOREAPP2_0
            var windowsSystemInfo = new WMISystemInfo();
            var wmiMem = windowsSystemInfo.GetFreeMemoryMB();
            Console.WriteLine("WMI Free memory:  {0:F2} MB", wmiMem);
#endif

            // Use P/Invoke to access kernel32.dll
            var wpsi = new WindowsSystemInfo();
            var pinvMem = wpsi.GetFreeMemoryMB();
            Console.WriteLine("PInv Free memory: {0:F2} MB", pinvMem);

#if !NETCOREAPP2_0
            Assert.That(pinvMem, Is.EqualTo(wmiMem).Within(20));
#endif
        }

        [Test]
        public void TestGetCoreCount()
        {
#if !NETCOREAPP2_0
            var windowsSystemInfo = new WMISystemInfo();
            var wmiCore = windowsSystemInfo.GetCoreCount(out var wmiPhysicalProcs);
            Console.WriteLine("WMI:  {0} processor(s) and {1} cores", wmiPhysicalProcs, wmiCore);
#endif
            // Use P/Invoke to access kernel32.dll
            var wpsi = new WindowsSystemInfo();
            var pinvCore = wpsi.GetCoreCount();
            var pirvProcCount = wpsi.GetProcessorPackageCount();

            Console.WriteLine("PInv: {0} processor(s) and {1} cores", pirvProcCount, pinvCore);

#if !NETCOREAPP2_0
            Assert.Multiple(() =>
            {
                Assert.That(pirvProcCount, Is.EqualTo(wmiPhysicalProcs));
                Assert.That(pinvCore, Is.EqualTo(wmiCore));
            });
#endif
        }

        [Test]
        public void TestGetCoreCountData()
        {
#if !NETCOREAPP2_0
            var windowsSystemInfo = new WMISystemInfo();
            var wmiCore = windowsSystemInfo.GetCoreCount(out var wmiPhysicalProcs);
            Console.WriteLine("WMI:  {0} processor(s) and {1} cores", wmiPhysicalProcs, wmiCore);
#endif

            var wpsi = new WindowsSystemInfo();
            Console.WriteLine("PInv: {0} processor(s) and {1} cores", wpsi.GetProcessorPackageCount(), wpsi.GetCoreCount());
            Console.WriteLine("PInv NUMA Nodes: {0}", wpsi.GetNumaNodeCount());
            Console.WriteLine("PInv Logical Cores: {0}", wpsi.GetLogicalCoreCount());
        }
    }
}
