using System;
using NUnit.Framework;
using PRISM;
using PRISMWin;

namespace PRISMTest
{
    [TestFixture]
    public class TestWindowsSystemInfo
    {
        [Test]
        public void TestGetTotalMemory()
        {
            var windowsSystemInfo = new WMISystemInfo();
            var wmiMem = windowsSystemInfo.GetTotalMemoryMB();
            Console.WriteLine("WMI Total memory: {0}", wmiMem);

            var wpsi = new WindowsSystemInfo();
            var pinvMem = wpsi.GetTotalMemoryMB();
            Console.WriteLine("PInv Total memory: {0}", pinvMem);

            Assert.AreEqual(wmiMem, pinvMem, 0.001);
        }

        [Test]
        public void TestGetFreeMemory()
        {
            var windowsSystemInfo = new WMISystemInfo();
            var wmiMem = windowsSystemInfo.GetFreeMemoryMB();
            Console.WriteLine("WMI Free memory: {0}", wmiMem);

            var wpsi = new WindowsSystemInfo();
            var pinvMem = wpsi.GetFreeMemoryMB();
            Console.WriteLine("PInv Free memory: {0}", pinvMem);

            Assert.AreEqual(wmiMem, pinvMem, 0.1);
        }

        [Test]
        public void TestGetCoreCount()
        {
            var windowsSystemInfo = new WMISystemInfo();
            var wmiCore = windowsSystemInfo.GetCoreCount();
            Console.WriteLine("WMI Cores: {0}", wmiCore);

            var wpsi = new WindowsSystemInfo();
            var pinvCore = wpsi.GetCoreCount();
            Console.WriteLine("PInv Cores: {0}", pinvCore);

            Assert.AreEqual(wmiCore, pinvCore);
        }

        [Test]
        public void TestGetCoreCountData()
        {
            var windowsSystemInfo = new WMISystemInfo();
            var wmiCore = windowsSystemInfo.GetCoreCount();
            Console.WriteLine("WMI Cores: {0}", wmiCore);

            var wpsi = new WindowsSystemInfo();
            Console.WriteLine("PInv Physical Cores: {0}", wpsi.GetCoreCount());
            Console.WriteLine("PInv Logical Cores: {0}", wpsi.GetLogicalCoreCount());
            Console.WriteLine("PInv Processor Packages: {0}", wpsi.GetProcessorPackageCount());
            Console.WriteLine("PInv NUMA Nodes: {0}", wpsi.GetNumaNodeCount());
        }
    }
}
