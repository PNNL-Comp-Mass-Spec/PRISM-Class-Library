using System;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class ProgRunnerTests
    {
        private const string UTILITIES_FOLDER = @"\\proto-2\unitTest_Files\PRISM\Utilities";

        [Test]
        [TestCase("sleep.exe", "3", false, false, 10, false)]
        [TestCase("sleep.exe", "3", true, false, 10, false)]
        [TestCase("sleep.exe", "20", false, true, 6, true)]
        [TestCase("ls.exe", @"-alF c:\", false, false, 10, false)]
        [TestCase("ls.exe", @"-alFR c:\", true, true, 10, true)]
        public void TestRunProgram(string exeName, string cmdArgs, bool createNoWindow, bool writeConsoleOutput, int maxRuntimeSeconds, bool programAbortExpected)
        {
            const int MONITOR_INTERVAL_MSEC = 500;

            var utilityExe = new FileInfo(Path.Combine(UTILITIES_FOLDER, exeName));
            if (!utilityExe.Exists)
            {
                Assert.Fail("Exe not found: " + utilityExe.FullName);
            }

            var workDir = @"C:\Temp";
            var tempFolder = new DirectoryInfo(workDir);
            if (!tempFolder.Exists)
                tempFolder.Create();

            var coreCount = PRISMWin.clsProcessStats.GetCoreCount();
            Console.WriteLine("Machine has {0} cores", coreCount);

            Assert.GreaterOrEqual(coreCount, 2, "Core count less than 2");

            var progRunner = new clsProgRunner
            {
                Arguments = cmdArgs,
                CreateNoWindow = createNoWindow,
                MonitoringInterval = MONITOR_INTERVAL_MSEC,
                Name = "ProgRunnerUnitTest",
                Program = utilityExe.FullName,
                Repeat = false,
                RepeatHoldOffTime = 0,
                WorkDir = workDir,
                CacheStandardOutput = false,
                EchoOutputToConsole = false,
                WriteConsoleOutputToFile = writeConsoleOutput,
                ConsoleOutputFileIncludesCommandLine = true
            };

            progRunner.StartAndMonitorProgram();

            var cachedProcessID = 0;
            var dtStartTime = DateTime.UtcNow;
            var abortProcess = false;

            while (progRunner.State != clsProgRunner.States.NotMonitoring)
            {
                if (cachedProcessID == 0)
                    cachedProcessID = progRunner.PID;

                clsProgRunner.SleepMilliseconds(MONITOR_INTERVAL_MSEC / 2);

                try
                {
                    if (cachedProcessID > 0)
                    {
                        var cpuUsage = PRISMWin.clsProcessStats.GetCoreUsageByProcessID(cachedProcessID);
                        Console.WriteLine("CPU Usage: " + cpuUsage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to get the core usage: {0}", ex.Message);
                }

                clsProgRunner.SleepMilliseconds(MONITOR_INTERVAL_MSEC / 2);

                if (maxRuntimeSeconds > 0)
                {
                    if (DateTime.UtcNow.Subtract(dtStartTime).TotalSeconds > maxRuntimeSeconds)
                    {
                        Console.WriteLine("Aborting ProcessID {0} since runtime has exceeded {1} seconds", cachedProcessID, maxRuntimeSeconds);
                        abortProcess = true;
                    }
                }

                if (abortProcess)
                    progRunner.StopMonitoringProgram(kill: true);
            }

            PRISMWin.clsProcessStats.ClearCachedPerformanceCounterForProcessID(cachedProcessID);

            if (writeConsoleOutput)
            {
                clsProgRunner.SleepMilliseconds(250);

                var consoleOutputFilePath = progRunner.ConsoleOutputFilePath;
                Assert.IsNotEmpty(consoleOutputFilePath, "Console output file path is empty");

                var consoleOutputFile = new FileInfo(consoleOutputFilePath);
                Assert.True(consoleOutputFile.Exists, "File not found: " + consoleOutputFilePath);

                var secondsSinceMidnight = (int)(DateTime.Now.Subtract(DateTime.Today).TotalSeconds);
                if (consoleOutputFile.DirectoryName == null)
                {
                    Assert.Fail("Unable to determine the parent folder of " + consoleOutputFilePath);
                }
                var newFilePath = Path.Combine(consoleOutputFile.DirectoryName,
                                               Path.GetFileNameWithoutExtension(consoleOutputFilePath) + "_" + secondsSinceMidnight + ".txt");
                consoleOutputFile.MoveTo(newFilePath);

                // Open the file and assure that the first line contains the .exe name
                using (var reader = new StreamReader(new FileStream(newFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    if (reader.EndOfStream)
                    {
                        Assert.Fail("The ConsoleOutput file is empty: " + newFilePath);
                    }
                    var dataLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(dataLine))
                    {
                        Assert.Fail("The first line of the ConsoleOutput file is empty: " + newFilePath);
                    }

                    if (!dataLine.ToLower().Contains(exeName))
                    {
                        Assert.Fail("The first line of the ConsoleOutput file does not contain " + exeName + ": " + newFilePath);
                    }
                }
            }

            if (programAbortExpected)
            {
                Assert.True(abortProcess, "Process was expected to be aborted due to excessive runtime, but was not");
            }
            else
            {
                Assert.False(abortProcess, "Process was aborted due to excessive runtime; this is unexpected");
            }
        }
    }
}
