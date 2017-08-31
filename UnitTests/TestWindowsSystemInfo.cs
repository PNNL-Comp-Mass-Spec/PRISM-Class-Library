using System;
using NUnit.Framework;
using PRISM;
#if !(NETCOREAPP2_0)
using PRISMWin;
#endif

namespace PRISMTest
{
    [TestFixture]
    public class TestWindowsSystemInfo
    {
        [Test]
        public void TestGetTotalMemory()
        {
#if !(NETCOREAPP2_0)
            var windowsSystemInfo = new WMISystemInfo();
            var wmiMem = windowsSystemInfo.GetTotalMemoryMB();
            Console.WriteLine("WMI Total memory: {0}", wmiMem);
#endif

            var wpsi = new WindowsSystemInfo();
            var pinvMem = wpsi.GetTotalMemoryMB();
            Console.WriteLine("PInv Total memory: {0}", pinvMem);

#if !(NETCOREAPP2_0)
            Assert.AreEqual(wmiMem, pinvMem, 0.001);
#endif
        }

        [Test]
        public void TestGetFreeMemory()
        {
#if !(NETCOREAPP2_0)
            var windowsSystemInfo = new WMISystemInfo();
            var wmiMem = windowsSystemInfo.GetFreeMemoryMB();
            Console.WriteLine("WMI Free memory: {0}", wmiMem);
#endif

            var wpsi = new WindowsSystemInfo();
            var pinvMem = wpsi.GetFreeMemoryMB();
            Console.WriteLine("PInv Free memory: {0}", pinvMem);

#if !(NETCOREAPP2_0)
            Assert.AreEqual(wmiMem, pinvMem, 0.1);
#endif
        }

        [Test]
        public void TestGetCoreCount()
        {
#if !(NETCOREAPP2_0)
            var windowsSystemInfo = new WMISystemInfo();
            var wmiCore = windowsSystemInfo.GetCoreCount();
            Console.WriteLine("WMI Cores: {0}", wmiCore);
#endif

            var wpsi = new WindowsSystemInfo();
            var pinvCore = wpsi.GetCoreCount();
            Console.WriteLine("PInv Cores: {0}", pinvCore);

#if !(NETCOREAPP2_0)
            Assert.AreEqual(wmiCore, pinvCore);
#endif
        }

        [Test]
        public void TestGetCoreCountData()
        {
#if !(NETCOREAPP2_0)
            var windowsSystemInfo = new WMISystemInfo();
            var wmiCore = windowsSystemInfo.GetCoreCount();
            Console.WriteLine("WMI Cores: {0}", wmiCore);
#endif

            var wpsi = new WindowsSystemInfo();
            Console.WriteLine("PInv Physical Cores: {0}", wpsi.GetCoreCount());
            Console.WriteLine("PInv Logical Cores: {0}", wpsi.GetLogicalCoreCount());
            Console.WriteLine("PInv Processor Packages: {0}", wpsi.GetProcessorPackageCount());
            Console.WriteLine("PInv NUMA Nodes: {0}", wpsi.GetNumaNodeCount());
        }
    }
}
