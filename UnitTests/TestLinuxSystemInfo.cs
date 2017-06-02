using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class TestLinuxSystemInfo
    {
        private const string SHARE_PATH = @"\\proto-2\unitTest_Files\PRISM";

        [Test]
        [TestCase(@"LinuxTestFiles\Centos6\etc", @"lsb-release", "LSB_VERSION=base-4.0-amd64:base-4.0-noarch:core-4.0-amd64")]
        [TestCase(@"LinuxTestFiles\Centos6\etc", @"redhat-release", "Red Hat Enterprise Linux Workstation release 6.9 (Santiago)")]
        public void TestGetCentos6Version(string remoteVersionFolderPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new clsOSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionFolderPath, versionFileName));

            var versionText = osVersionInfo.GetFirstLineVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);
            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [Test]
        [TestCase(@"LinuxTestFiles\Ubuntu\etc", @"os-release", "Ubuntu 17.04 (Zesty Zapus)")]
        public void TestGetOSReleaseVersion(string remoteVersionFolderPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new clsOSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionFolderPath, versionFileName));

            var versionText = osVersionInfo.GetOSReleaseVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);
            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [Test]
        [TestCase(@"LinuxTestFiles\Solaris\etc", @"release", "Solaris 10 11/06 s10s_u3wos_10 SPARC")]
        public void TestGetSolarisVersion(string remoteVersionFolderPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new clsOSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionFolderPath, versionFileName));

            var versionText = osVersionInfo.GetFirstLineVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);
            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [Test]
        [TestCase(@"LinuxTestFiles\Ubuntu\etc", @"lsb-release", "Ubuntu 17.04")]
        [TestCase(@"LinuxTestFiles\Ubuntu\etc", @"os-release", "Ubuntu; 17.04 (Zesty Zapus)")]
        public void TestGetUbuntuVersion(string remoteVersionFolderPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new clsOSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionFolderPath, versionFileName));

            var versionText = osVersionInfo.GetUbuntuVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);

            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [Test]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 16)]
        [TestCase(@"LinuxTestFiles\Cygwin\proc", 4)]
        [TestCase(@"LinuxTestFiles\Ubuntu\proc", 2)]
        public void TestGetCoreCount(string sourceProcFolderPath, int expectedCoreCount)
        {
            var procFolder = new DirectoryInfo(clsLinuxSystemInfo.ROOT_PROC_DIRECTORY);
            if (!procFolder.Exists)
            {
                // Proc folder not found; try to make it
                try
                {
                    procFolder.Create();
                }
                catch (Exception)
                {
                    Assert.Ignore("Directory not found, and cannot be created: " + procFolder.FullName);
                }
            }

            // Update the cpuinfo file in the local proc folder using sourceProcFolderPath
            var sourceCpuInfoFile = VerifyTestFile(Path.Combine(sourceProcFolderPath, clsLinuxSystemInfo.CPUINFO_FILE));
            var targetCpuInfoFile = new FileInfo(Path.Combine(procFolder.FullName, clsLinuxSystemInfo.CPUINFO_FILE));

            if (targetCpuInfoFile.Exists)
            {
                try
                {
                    targetCpuInfoFile.Delete();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Could not replace the local CpuInfo file at " + targetCpuInfoFile.FullName + ": " + ex.Message);
                }
            }

            try
            {
                sourceCpuInfoFile.CopyTo(targetCpuInfoFile.FullName);
            }
            catch (Exception ex)
            {
                Assert.Fail("Could not copy the CpuInfo file to " + targetCpuInfoFile.FullName + ": " + ex.Message);
            }

            var linuxSystemInfo = new clsLinuxSystemInfo();

            var coreCount = linuxSystemInfo.GetCoreCount();

            Console.WriteLine("Core count: {0}", coreCount);

            Assert.AreEqual(expectedCoreCount, coreCount);

        }

        [Test]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98079, 0.48, 24)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98096, 0.92, 46)]
        public void TestGetCoreUsageByProcessID(string sourceProcFolderPath, int processID, double expectedCoreUsage, double expectedCpuUsageTotal)
        {
            var procFolder = new DirectoryInfo(clsLinuxSystemInfo.ROOT_PROC_DIRECTORY);
            if (!procFolder.Exists)
            {
                // Proc folder not found; try to make it
                try
                {
                    procFolder.Create();
                }
                catch (Exception)
                {
                    Assert.Ignore("Directory not found, and cannot be created: " + procFolder.FullName);
                }
            }

            // Update the cpu stat file in the local proc folder using sourceProcFolderPath
            var sourceCpuStatFile1 = VerifyTestFile(Path.Combine(sourceProcFolderPath, processID + @"\CpuStat1\stat"));
            var sourceCpuStatFile2 = VerifyTestFile(Path.Combine(sourceProcFolderPath, processID + @"\CpuStat2\stat"));
            var targetCpuStatFile = new FileInfo(Path.Combine(procFolder.FullName, @"stat"));

            if (targetCpuStatFile.Exists)
            {
                try
                {
                    targetCpuStatFile.Delete();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Could not replace the local cpu stat file at " + targetCpuStatFile.FullName + ": " + ex.Message);
                }
            }

            sourceCpuStatFile1.CopyTo(targetCpuStatFile.FullName);
            // Console.WriteLine("{0:HH:mm:ss.fff}: Copied stat file from {1} to {2}", DateTime.Now, sourceCpuStatFile1.FullName, targetCpuStatFile.FullName);

            // Update the process stat file in the local proc folder using sourceProcFolderPath
            var sourceStatFile1 = VerifyTestFile(Path.Combine(sourceProcFolderPath, processID + @"\ProcStat1\stat"));
            var sourceStatFile2 = VerifyTestFile(Path.Combine(sourceProcFolderPath, processID + @"\ProcStat2\stat"));
            var targetStatFile = new FileInfo(Path.Combine(procFolder.FullName, processID + @"\stat"));

            if (targetStatFile.Exists)
            {
                try
                {
                    targetStatFile.Delete();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Could not replace the local process stat file at " + targetStatFile.FullName + ": " + ex.Message);
                }
            }

            try
            {
                var parentFolder = targetStatFile.Directory;
                if (parentFolder == null)
                    Assert.Fail("Unable to determine the parent directory of " + targetStatFile.FullName);

                if (!parentFolder.Exists)
                    parentFolder.Create();

                sourceStatFile1.CopyTo(targetStatFile.FullName, true);
                // Console.WriteLine("{0:HH:mm:ss.fff}: Copied stat file from {1} to {2}", DateTime.Now, sourceStatFile1.FullName, targetStatFile.FullName);
            }
            catch (Exception ex)
            {
                Assert.Fail("Could not copy the stat file to " + targetStatFile.FullName + ": " + ex.Message);
            }

            var linuxSystemInfo = new clsLinuxSystemInfo();

            var filesToCopy = new List<clsTestFileCopyInfo>
            {
                new clsTestFileCopyInfo(sourceCpuStatFile2, targetCpuStatFile),
                new clsTestFileCopyInfo(sourceStatFile2, targetStatFile)
            };

            // Start a timer to replace the stat file in 2 seconds
            var fileReplacerTimer = new Timer(ReplaceFiles, filesToCopy, 2000, -1);

            var coreUsage = linuxSystemInfo.GetCoreUsageByProcessID(processID, out var cpuUsageTotal, 3);

            fileReplacerTimer.Dispose();

            // Delay 1 second to allow threads to finish up
            var startTime = DateTime.UtcNow;
            while (true)
            {
                Thread.Sleep(250);
                if (DateTime.UtcNow.Subtract(startTime).TotalMilliseconds >= 1000)
                    break;
            }

            Console.WriteLine("Core usage: {0}", coreUsage);
            Console.WriteLine("Total CPU usage: {0}%", cpuUsageTotal);

            Assert.AreEqual(expectedCoreUsage, coreUsage, 0.01, "Core usage mismatch");
            Assert.AreEqual(expectedCpuUsageTotal, cpuUsageTotal, 0.1, "Total CPU usage mismatch");

        }

        private void ReplaceFiles(object state)
        {
            var filesToCopy = state as List<clsTestFileCopyInfo>;

            if (filesToCopy == null)
                return;

            foreach (var fileToCopy in filesToCopy)
            {
                fileToCopy.CopyToTargetNow();                
            }

        }

        [Test]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 42128)]
        [TestCase(@"LinuxTestFiles\Cygwin\proc", 13050)]
        [TestCase(@"LinuxTestFiles\Ubuntu\proc", 1276)]
        public void TestGetFreeMemory(string sourceProcFolderPath, float expectedFreeMemoryMB)
        {
            var procFolder = new DirectoryInfo(clsLinuxSystemInfo.ROOT_PROC_DIRECTORY);
            if (!procFolder.Exists)
            {
                // Proc folder not found; try to make it
                try
                {
                    procFolder.Create();
                }
                catch (Exception)
                {
                    Assert.Ignore("Directory not found, and cannot be created: " + procFolder.FullName);
                }
            }

            // Update the meminfo file in the local proc folder using sourceProcFolderPath
            var sourceMemInfoFile = VerifyTestFile(Path.Combine(sourceProcFolderPath, clsLinuxSystemInfo.MEMINFO_FILE));
            var targetMemInfoFile = new FileInfo(Path.Combine(procFolder.FullName, clsLinuxSystemInfo.MEMINFO_FILE));

            if (targetMemInfoFile.Exists)
            {
                try
                {
                    targetMemInfoFile.Delete();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Could not replace the local meminfo file at " + targetMemInfoFile.FullName + ": " + ex.Message);
                }
            }

            try
            {
                sourceMemInfoFile.CopyTo(targetMemInfoFile.FullName);
            }
            catch (Exception ex)
            {
                Assert.Fail("Could not copy the meminfo file to " + targetMemInfoFile.FullName + ": " + ex.Message);
            }

            var linuxSystemInfo = new clsLinuxSystemInfo();

            var freeMemoryMB = linuxSystemInfo.GetFreeMemoryMB();

            Console.WriteLine("Free memory: {0:F0} MB", freeMemoryMB);

            Assert.AreEqual(expectedFreeMemoryMB, freeMemoryMB, 1);

        }

        private FileInfo VerifyTestFile(string filePath)
        {
            var testFile = new FileInfo(filePath);
            if (testFile.Exists)
                return testFile;

            string relativeDirectory;

            if (filePath.Length > testFile.Name.Length)
                relativeDirectory = filePath.Substring(0, filePath.Length - testFile.Name.Length);
            else
                relativeDirectory = string.Empty;

            var alternateFile = new FileInfo(Path.Combine(SHARE_PATH, relativeDirectory, testFile.Name));
            if (alternateFile.Exists)
                return alternateFile;

            Assert.Fail("File not found: " + testFile.FullName);
            return null;
        }
    }
}
