using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using PRISM;

// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace PRISMTest
{
    [TestFixture]
    internal class TestLinuxSystemInfo
    {
        // Ignore Spelling: lsb, redhat, ubuntu, noarch, Zapus, os, proc, Solaris, cygwin, cpuinfo, cpu, cpuload, cpuloadtest
        // Ignore Spelling: args, cmdline, meminfo

        internal const bool SHOW_TRACE_MESSAGES = false;

        [TestCase(@"LinuxTestFiles\Centos6\etc", "lsb-release", "LSB_VERSION=base-4.0-amd64:base-4.0-noarch:core-4.0-amd64")]
        [TestCase(@"LinuxTestFiles\Centos6\etc", "redhat-release", "Red Hat Enterprise Linux Workstation release 6.9 (Santiago)")]
        public void TestGetCentos6Version(string remoteVersionDirectoryPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new OSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionDirectoryPath, versionFileName));

            var versionText = osVersionInfo.GetFirstLineVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);
            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [TestCase(@"LinuxTestFiles\Ubuntu\etc", "os-release", "Ubuntu 17.04 (Zesty Zapus)")]
        public void TestGetOSReleaseVersion(string remoteVersionDirectoryPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new OSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionDirectoryPath, versionFileName));

            var versionText = osVersionInfo.GetOSReleaseVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);
            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [TestCase(@"LinuxTestFiles\Solaris\etc", "release", "Solaris 10 11/06 s10s_u3wos_10 SPARC")]
        public void TestGetSolarisVersion(string remoteVersionDirectoryPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new OSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionDirectoryPath, versionFileName));

            var versionText = osVersionInfo.GetFirstLineVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);
            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [TestCase(@"LinuxTestFiles\Ubuntu\etc", "lsb-release", "Ubuntu 17.04")]
        [TestCase(@"LinuxTestFiles\Ubuntu\etc", "os-release", "Ubuntu; 17.04 (Zesty Zapus)")]
        public void TestGetUbuntuVersion(string remoteVersionDirectoryPath, string versionFileName, string expectedVersionTextStart)
        {
            var osVersionInfo = new OSVersionInfo();

            var versionInfoFile = VerifyTestFile(Path.Combine(remoteVersionDirectoryPath, versionFileName));

            var versionText = osVersionInfo.GetUbuntuVersion(versionInfoFile.FullName);

            Console.WriteLine(versionText);

            Assert.True(versionText.StartsWith(expectedVersionTextStart));
        }

        [TestCase(@"LinuxTestFiles\Centos6\proc", 16, 2)]
        [TestCase(@"LinuxTestFiles\Cygwin\proc", 4, 1)]
        [TestCase(@"LinuxTestFiles\Ubuntu\proc", 2, 1)]
        public void TestGetCoreCount(string sourceProcDirectoryPath, int expectedCoreCount, int expectedProcessorPackages)
        {
            var procDirectory = ValidateLocalProcDirectory();

            // Update the cpuinfo file in the local proc directory
            CopyCPUInfoFile(procDirectory, sourceProcDirectoryPath);

            var linuxSystemInfo = new LinuxSystemInfo();

            var coreCount = linuxSystemInfo.GetCoreCount();

            Console.WriteLine("Core count: {0}", coreCount);

            Assert.AreEqual(expectedCoreCount, coreCount);

            Console.WriteLine("Processor Packages: {0}", linuxSystemInfo.GetProcessorPackageCount());

            Assert.AreEqual(expectedProcessorPackages, linuxSystemInfo.GetProcessorPackageCount());
        }

        [TestCase(@"LinuxTestFiles\Centos6\proc", 98079, "mono", "", 3.841)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98079, "mono", "cpuloadtest", 3.841)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98096, "mono", "cpuloadtest", 7.408)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98096, "mono", "InvalidArgs", -1)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98096, "InvalidProgram", "", -1)]
        public void TestGetCoreUsageByProcessName(string sourceProcDirectoryPath, int processID, string processName, string arguments, double expectedCoreUsageTotal)
        {
            const int SAMPLING_TIME_SECONDS = 3;

            var procDirectory = ValidateLocalProcDirectory();

            // Update the cpuinfo file in the local proc directory
            CopyCPUInfoFile(procDirectory, sourceProcDirectoryPath);

            // Update the cpu stat file in the local proc directory using sourceProcDirectoryPath
            var sourceCpuStatFile1 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\CpuStat1\stat"));
            var sourceCpuStatFile2 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\CpuStat2\stat"));
            var targetCpuStatFile = CopyCPUStatFile(procDirectory, sourceCpuStatFile1);

            // Update the process stat file in the local proc directory using sourceProcDirectoryPath
            var sourceStatFile1 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\ProcStat1\stat"));
            var sourceStatFile2 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\ProcStat2\stat"));
            var targetStatFile = CopyProcessStatFile(procDirectory, processID, sourceStatFile1);

            // Update the process cmdline file in the local proc directory using sourceProcDirectoryPath
            var sourceCmdLineFile = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\ProcStat1\cmdline"));
            var targetCmdLineFile = new FileInfo(Path.Combine(procDirectory.FullName, processID + @"\cmdline"));

            if (targetCmdLineFile.Exists)
            {
                try
                {
                    targetCmdLineFile.Delete();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Could not replace the local process cmdline file at " + targetCmdLineFile.FullName + ": " + ex.Message);
                }
            }

            try
            {
                sourceCmdLineFile.CopyTo(targetCmdLineFile.FullName, true);
            }
            catch (Exception ex)
            {
                Assert.Fail("Could not copy the cmdline file to " + targetCmdLineFile.FullName + ": " + ex.Message);
            }

            var linuxSystemInfo = new LinuxSystemInfo();

            var filesToCopy = new List<TestFileCopyInfo>
            {
                new(sourceCpuStatFile2, targetCpuStatFile),
                new(sourceStatFile2, targetStatFile)
            };

            // Start a timer to replace the stat file in 2 seconds
            var fileReplacerTimer = new Timer(ReplaceFiles, filesToCopy, (SAMPLING_TIME_SECONDS - 1) * 1000, -1);

            var coreUsage = linuxSystemInfo.GetCoreUsageByProcessName(processName, arguments, out var processIDs, SAMPLING_TIME_SECONDS);

            fileReplacerTimer.Dispose();

            // Delay 1 second to allow threads to finish up
            var startTime = DateTime.UtcNow;
            do
            {
                Thread.Sleep(250);
            }
            while (DateTime.UtcNow.Subtract(startTime).TotalMilliseconds < 1000);

            Console.WriteLine("Core usage: {0:F2}", coreUsage);

            Assert.AreEqual(expectedCoreUsageTotal, coreUsage, 0.1, "Core usage mismatch");

            if (processIDs.Count == 1)
                Console.WriteLine("Process ID: " + processIDs.First());
            else
                Console.WriteLine("Process IDs: " + string.Join(", ", processIDs));
        }

        [TestCase(@"LinuxTestFiles\Centos6\proc", 34304, 7.88, 49.3)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98079, 3.841, 24)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98096, 7.408, 46.3)]
        public void TestGetCoreUsageByProcessID(string sourceProcDirectoryPath, int processID, double expectedCoreUsage, double expectedCpuUsageTotal)
        {
            const int SAMPLING_TIME_SECONDS = 3;

            var procDirectory = ValidateLocalProcDirectory();

            // Update the cpuinfo file in the local proc directory
            CopyCPUInfoFile(procDirectory, sourceProcDirectoryPath);

            // Update the cpu stat file in the local proc directory using sourceProcDirectoryPath
            var sourceCpuStatFile1 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\CpuStat1\stat"));
            var sourceCpuStatFile2 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\CpuStat2\stat"));
            var targetCpuStatFile = CopyCPUStatFile(procDirectory, sourceCpuStatFile1);

            // Update the process stat file in the local proc directory using sourceProcDirectoryPath
            var sourceStatFile1 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\ProcStat1\stat"));
            var sourceStatFile2 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\ProcStat2\stat"));
            var targetStatFile = CopyProcessStatFile(procDirectory, processID, sourceStatFile1);

            var linuxSystemInfo = new LinuxSystemInfo {
                TraceEnabled = true
            };

            linuxSystemInfo.DebugEvent += LinuxSystemInfo_DebugEvent;
            linuxSystemInfo.ErrorEvent += LinuxSystemInfo_ErrorEvent;

            var filesToCopy = new List<TestFileCopyInfo>
            {
                new(sourceCpuStatFile2, targetCpuStatFile),
                new(sourceStatFile2, targetStatFile)
            };

            // Start a timer to replace the stat file in 2 seconds
            var fileReplacerTimer = new Timer(ReplaceFiles, filesToCopy, (SAMPLING_TIME_SECONDS - 1) * 1000, -1);

            var coreUsage = linuxSystemInfo.GetCoreUsageByProcessID(processID, out var cpuUsageTotal, SAMPLING_TIME_SECONDS);

            fileReplacerTimer.Dispose();

            // Delay 1 second to allow threads to finish up
            var startTime = DateTime.UtcNow;
            do
            {
                Thread.Sleep(250);
            }
            while (DateTime.UtcNow.Subtract(startTime).TotalMilliseconds < 1000);

            Console.WriteLine("Core usage: {0:F2}", coreUsage);
            Console.WriteLine("Total CPU usage: {0:F1}%", cpuUsageTotal);

            Assert.AreEqual(expectedCoreUsage, coreUsage, 0.01, "Core usage mismatch");
            Assert.AreEqual(expectedCpuUsageTotal, cpuUsageTotal, 0.1, "Total CPU usage mismatch");
        }

        private void CopyCPUInfoFile(FileSystemInfo procDirectory, string sourceProcDirectoryPath)
        {
            // Update the cpuinfo file in the local proc directory using sourceProcDirectoryPath
            var sourceCpuInfoFile = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, LinuxSystemInfo.CPUINFO_FILE));
            var targetCpuInfoFile = new FileInfo(Path.Combine(procDirectory.FullName, LinuxSystemInfo.CPUINFO_FILE));

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
                // Uncomment to see file paths at the console
                // Console.WriteLine("Copying {0} to {1}", sourceCpuInfoFile.FullName, targetCpuInfoFile.FullName);
                sourceCpuInfoFile.CopyTo(targetCpuInfoFile.FullName);
            }
            catch (Exception ex)
            {
                Assert.Fail("Could not copy the CpuInfo file to " + targetCpuInfoFile.FullName + ": " + ex.Message);
            }
        }

        private FileInfo CopyCPUStatFile(FileSystemInfo procDirectory, FileInfo sourceCpuStatFile)
        {
            var targetCpuStatFile = new FileInfo(Path.Combine(procDirectory.FullName, "stat"));

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

            sourceCpuStatFile.CopyTo(targetCpuStatFile.FullName);

            ShowTraceMessage(string.Format("Copied CPU stat file from {0} to {1}", sourceCpuStatFile.FullName, targetCpuStatFile.FullName));

            return targetCpuStatFile;
        }

        private FileInfo CopyProcessStatFile(FileSystemInfo procDirectory, int processID, FileInfo sourceStatFile)
        {
            var targetStatFile = new FileInfo(Path.Combine(procDirectory.FullName, processID + @"\stat"));

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
                var parentDirectory = targetStatFile.Directory;
                if (parentDirectory == null)
                    Assert.Fail("Unable to determine the parent directory of " + targetStatFile.FullName);

                if (!parentDirectory.Exists)
                    parentDirectory.Create();

                sourceStatFile.CopyTo(targetStatFile.FullName, true);

                ShowTraceMessage(string.Format("Copied process stat file from {0} to {1}", sourceStatFile.FullName, targetStatFile.FullName));
            }
            catch (Exception ex)
            {
                Assert.Fail("Could not copy the stat file to " + targetStatFile.FullName + ": " + ex.Message);
            }

            return targetStatFile;
        }

        /// <summary>
        /// Copy files from a source location to a target location
        /// </summary>
        /// <remarks>The state parameter is an object because this method is a callback for a timer</remarks>
        /// <param name="state">List of TestFileCopyInfo</param>
        private void ReplaceFiles(object state)
        {
            if (state is not List<TestFileCopyInfo> filesToCopy)
                return;

            foreach (var fileToCopy in filesToCopy)
            {
                fileToCopy.CopyToTargetNow();
            }
        }

        [TestCase(@"LinuxTestFiles\Centos6\proc", 98079, 24.100)]
        [TestCase(@"LinuxTestFiles\Centos6\proc", 98096, 46.366)]
        public void TestGetCPUUtilization(string sourceProcDirectoryPath, int processID, double expectedCpuUsageTotal)
        {
            const int SAMPLING_TIME_SECONDS = 3;

            var procDirectory = ValidateLocalProcDirectory();

            // Update the cpuinfo file in the local proc directory
            CopyCPUInfoFile(procDirectory, sourceProcDirectoryPath);

            // Update the cpu stat file in the local proc directory using sourceProcDirectoryPath
            var sourceCpuStatFile1 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\CpuStat1\stat"));
            var sourceCpuStatFile2 = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, processID + @"\CpuStat2\stat"));
            var targetCpuStatFile = CopyCPUStatFile(procDirectory, sourceCpuStatFile1);

            var linuxSystemInfo = new LinuxSystemInfo();

            var filesToCopy = new List<TestFileCopyInfo>
            {
                new(sourceCpuStatFile2, targetCpuStatFile),
            };

            // Start a timer to replace the stat file in 2 seconds
            var fileReplacerTimer = new Timer(ReplaceFiles, filesToCopy, (SAMPLING_TIME_SECONDS - 1) * 1000, -1);

            var cpuUsageTotal = linuxSystemInfo.GetCPUUtilization(SAMPLING_TIME_SECONDS);

            fileReplacerTimer.Dispose();

            // Delay 1 second to allow threads to finish up
            var startTime = DateTime.UtcNow;
            do
            {
                Thread.Sleep(250);
            }
            while (DateTime.UtcNow.Subtract(startTime).TotalMilliseconds < 1000);

            Console.WriteLine("Overall CPU usage: {0:F1}%", cpuUsageTotal);

            Assert.AreEqual(expectedCpuUsageTotal, cpuUsageTotal, 0.01, "CPU usage mismatch");
        }

        [TestCase(@"LinuxTestFiles\Centos6\proc", 42128)]
        [TestCase(@"LinuxTestFiles\Cygwin\proc", 13050)]
        [TestCase(@"LinuxTestFiles\Ubuntu\proc", 1276)]
        public void TestGetFreeMemory(string sourceProcDirectoryPath, float expectedFreeMemoryMB)
        {
            var procDirectory = ValidateLocalProcDirectory();

            // Update the meminfo file in the local proc directory using sourceProcDirectoryPath
            var sourceMemInfoFile = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, LinuxSystemInfo.MEMINFO_FILE));
            var targetMemInfoFile = new FileInfo(Path.Combine(procDirectory.FullName, LinuxSystemInfo.MEMINFO_FILE));

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

            var linuxSystemInfo = new LinuxSystemInfo();

            var freeMemoryMB = linuxSystemInfo.GetFreeMemoryMB();

            Console.WriteLine("Free memory: {0:F0} MB", freeMemoryMB);

            Assert.AreEqual(expectedFreeMemoryMB, freeMemoryMB, 1);
        }

        [TestCase(@"LinuxTestFiles\Centos6\proc", 64183)]
        [TestCase(@"LinuxTestFiles\Cygwin\proc", 32672)]
        [TestCase(@"LinuxTestFiles\Ubuntu\proc", 3938)]
        public void TestGetTotalMemory(string sourceProcDirectoryPath, float expectedTotalMemoryMB)
        {
            var procDirectory = ValidateLocalProcDirectory();

            // Update the meminfo file in the local proc directory using sourceProcDirectoryPath
            var sourceMemInfoFile = VerifyTestFile(Path.Combine(sourceProcDirectoryPath, LinuxSystemInfo.MEMINFO_FILE));
            var targetMemInfoFile = new FileInfo(Path.Combine(procDirectory.FullName, LinuxSystemInfo.MEMINFO_FILE));

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

            var linuxSystemInfo = new LinuxSystemInfo();

            var totalMemoryMB = linuxSystemInfo.GetTotalMemoryMB();

            Console.WriteLine("Total memory: {0:F0} MB", totalMemoryMB);

            Assert.AreEqual(expectedTotalMemoryMB, totalMemoryMB, 1);
        }

        private FileInfo VerifyTestFile(string filePath)
        {
            return FileRefs.GetTestFile(filePath);
        }

        private void ShowTraceMessage(string message)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (SHOW_TRACE_MESSAGES)
#pragma warning disable 162
                Console.WriteLine("{0:HH:mm:ss.fff}: {1}", DateTime.Now, message);
#pragma warning restore 162
        }

        private DirectoryInfo ValidateLocalProcDirectory()
        {
            var procDirectory = new DirectoryInfo(LinuxSystemInfo.ROOT_PROC_DIRECTORY);
            if (procDirectory.Exists)
                return procDirectory;

            // Proc directory not found; try to make it
            try
            {
                procDirectory.Create();
            }
            catch (Exception)
            {
                Assert.Ignore("Directory not found, and cannot be created: " + procDirectory.FullName);
            }

            return procDirectory;
        }

        #region "Event Handlers"

        private void LinuxSystemInfo_DebugEvent(string message)
        {
            ShowTraceMessage(message);
        }

        private void LinuxSystemInfo_ErrorEvent(string message, Exception ex)
        {
            Console.WriteLine("Error: " + message);
            Console.WriteLine(StackTraceFormatter.GetExceptionStackTraceMultiLine(ex));
        }

        #endregion

    }
}
