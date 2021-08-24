using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    internal class ProgRunnerTests
    {
        // Ignore Spelling: AppVeyor, alF

#if !NETCOREAPP2_0
        private const string UTILITIES_DIRECTORY = @"\\proto-2\unitTest_Files\PRISM\Utilities";

        /// <summary>
        /// Start long running processes then force them to be aborted by setting maxRuntimeSeconds to a small value
        /// </summary>
        /// <remarks>
        /// These tests work when run as a normal user but can fail when run on our Jenkins server under the NETWORK SERVICE account; thus the DesktopInteraction category
        /// Category PNL_Domain is included here because these tests do not work on AppVeyor
        /// </remarks>
        /// <param name="exeName"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="writeConsoleOutput"></param>
        /// <param name="maxRuntimeSeconds"></param>
        [TestCase("sleep.exe", "20",  true, 6)]
        [TestCase("ls.exe", @"-alFR c:\",  true, 3)]
        [Category("DesktopInteraction")]
        [Category("PNL_Domain")]
        public void TestAbortRunningProgramCreateWindow(string exeName, string cmdArgs, bool writeConsoleOutput, int maxRuntimeSeconds)
        {
            TestRunProgram(exeName, cmdArgs, createNoWindow: false, writeConsoleOutput, maxRuntimeSeconds, programAbortExpected: true);
        }

        /// <summary>
        /// Start long running processes then force them to be aborted by setting maxRuntimeSeconds to a small value
        /// </summary>
        /// <remarks>
        /// Category PNL_Domain is included here because these tests do not work on AppVeyor
        /// </remarks>
        /// <param name="exeName"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="writeConsoleOutput"></param>
        /// <param name="maxRuntimeSeconds"></param>
        [TestCase("sleep.exe", "20",  true, 6)]
        [TestCase("ls.exe", @"-alFR c:\",  true, 3)]
        [Category("PNL_Domain")]
        public void TestAbortRunningProgramNoWindow(string exeName, string cmdArgs, bool writeConsoleOutput, int maxRuntimeSeconds)
        {
            TestRunProgram(exeName, cmdArgs, createNoWindow: true, writeConsoleOutput, maxRuntimeSeconds, programAbortExpected: true);
        }

        [TestCase(1000000, 5, 250, 2500)]
        [Category("PNL_Domain")]
        public void TestGarbageCollectionLarge(int iterations, int gcEvents, int dictionaryCount, int dictionarySize)
        {
            TestGarbageCollectionWork(iterations, gcEvents, dictionaryCount, dictionarySize);
        }

        [TestCase(100000, 5, 250, 2500)]
        public void TestGarbageCollection(int iterations, int gcEvents, int dictionaryCount, int dictionarySize)
        {
            TestGarbageCollectionWork(iterations, gcEvents, dictionaryCount, dictionarySize);
        }

        /// <summary>
        /// This method will create 2.5 million FileInfo objects (if iterations is 2500000) and store those in random locations in various dictionaries
        /// It will remove half of the dictionaries garbageCollectionEvents times, calling GarbageCollectNow after each removal
        /// </summary>
        /// <param name="iterations"></param>
        /// <param name="garbageCollectionEvents"></param>
        /// <param name="dictionaryCount"></param>
        /// <param name="dictionarySize"></param>
        private void TestGarbageCollectionWork(int iterations, int garbageCollectionEvents, int dictionaryCount, int dictionarySize)
        {
            var dictionaries = new List<Dictionary<int, List<FileInfo>>>();
            var rand = new Random();

            var keysRemoved = 0;
            var valuesRemoved = 0;

            var lastGC = DateTime.UtcNow;

            var gcInterval = (int)Math.Floor(iterations / (double)garbageCollectionEvents);

            for (var i = 0; i < iterations; i++)
            {
                // Pick a dictionary at random (add new dictionaries if required)
                var dictionaryIndex = rand.Next(0, dictionaryCount - 1);
                while (dictionaries.Count < dictionaryIndex + 1)
                {
                    dictionaries.Add(new Dictionary<int, List<FileInfo>>());
                }

                var currentDictionary = dictionaries[dictionaryIndex];

                // Create a random filename, 100 characters long, composed of upper and lowercase letters
                var randomName = new StringBuilder();
                for (var charIndex = 0; charIndex < 100; charIndex++)
                {
                    if (rand.NextDouble() > 0.85)
                        randomName.Append((char)rand.Next(65, 91));
                    else
                        randomName.Append((char)rand.Next(97, 123));
                }

                // Create a FileInfo object
                var randomFile = new FileInfo(randomName + ".txt");

                // Pick a random key for selecting a list to append the FileInfo object to
                var key = rand.Next(0, dictionarySize);
                if (currentDictionary.TryGetValue(key, out var values))
                {
                    values.Add(randomFile);
                }
                else
                {
                    var newValues = new List<FileInfo> { randomFile };
                    currentDictionary.Add(key, newValues);
                }

                if (i == 0 || i % gcInterval != 0) continue;

                // Remove half of the dictionaries, at random
                var targetCount = dictionaries.Count / 2;

                for (var j = 0; j < targetCount; j++)
                {
                    var dictionaryIndexToRemove = rand.Next(0, dictionaries.Count - 1);

                    var dictionaryToRemove = dictionaries[dictionaryIndexToRemove];

                    keysRemoved = dictionaryToRemove.Keys.Count;

                    foreach (var keyToRemove in dictionaryToRemove.Keys)
                    {
                        valuesRemoved += dictionaryToRemove[keyToRemove].Count;
                    }

                    dictionaries.RemoveAt(dictionaryIndexToRemove);
                }

                Console.WriteLine("Garbage collect at {0:yyyy-MM-dd hh:mm:ss tt} ({1:F1} seconds elapsed)",
                    DateTime.Now, DateTime.UtcNow.Subtract(lastGC).TotalSeconds);

                ProgRunner.GarbageCollectNow();

                lastGC = DateTime.UtcNow;
            }

            Console.WriteLine("{0} dictionaries", dictionaries.Count);

            var totalKeys = 0;
            var totalValues = 0;

            foreach (var dictionary in dictionaries)
            {
                totalKeys += dictionary.Keys.Count;

                foreach (var key in dictionary.Keys)
                {
                    totalValues += dictionary[key].Count;
                }
            }

            Console.WriteLine("{0} total keys", totalKeys);
            Console.WriteLine("{0} total values", totalValues);

            Console.WriteLine("{0} keys removed", keysRemoved);
            Console.WriteLine("{0} values removed", valuesRemoved);
        }

        /// <summary>
        /// Test starting a process, showing a window
        /// </summary>
        /// <remarks>Category DesktopInteraction is included here because these tests do not work on AppVeyor or on Jenkins</remarks>
        /// <param name="exeName"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="writeConsoleOutput"></param>
        /// <param name="maxRuntimeSeconds"></param>
        [TestCase("sleep.exe", "3",  false, 15)]
        [TestCase("sleep.exe", "3",  false, 15)]
        [TestCase("ls.exe", @"-alF c:\",  false, 15)]
        [Category("DesktopInteraction")]
        public void TestRunProgramCreateWindow(string exeName, string cmdArgs,bool writeConsoleOutput, int maxRuntimeSeconds)
        {
            TestRunProgram(exeName, cmdArgs, createNoWindow: false, writeConsoleOutput, maxRuntimeSeconds, programAbortExpected: false);
        }

        /// <summary>
        /// Test starting a process, no window
        /// </summary>
        /// <remarks>Category PNL_Domain is included here because these tests do not work on AppVeyor</remarks>
        /// <param name="exeName"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="writeConsoleOutput"></param>
        /// <param name="maxRuntimeSeconds"></param>
        [TestCase("sleep.exe", "3",  false, 15)]
        [TestCase("sleep.exe", "3",  false, 15)]
        [TestCase("ls.exe", @"-alF c:\",  false, 15)]
        [Category("PNL_Domain")]
        public void TestRunProgramNoWindow(string exeName, string cmdArgs, bool writeConsoleOutput, int maxRuntimeSeconds)
        {
            TestRunProgram(exeName, cmdArgs, createNoWindow: true, writeConsoleOutput, maxRuntimeSeconds, programAbortExpected: false);
        }

        private void TestRunProgram(string exeName, string cmdArgs, bool createNoWindow, bool writeConsoleOutput, int maxRuntimeSeconds, bool programAbortExpected)
        {
            const int MONITOR_INTERVAL_MSEC = 500;

            var utilityExe = new FileInfo(Path.Combine(UTILITIES_DIRECTORY, exeName));
            if (!utilityExe.Exists)
            {
                Assert.Fail("Exe not found: " + utilityExe.FullName);
            }

            var processStats = new PRISMWin.ProcessStats();

            var workDir = @"C:\Temp";
            var tempDir = new DirectoryInfo(workDir);
            if (!tempDir.Exists)
                tempDir.Create();

            var coreCount = processStats.GetCoreCount();
            Console.WriteLine("Machine has {0} cores", coreCount);

            Assert.GreaterOrEqual(coreCount, 2, "Core count less than 2");

            var progRunner = new ProgRunner
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
                ConsoleOutputFilePath = Path.Combine(workDir, Path.GetFileNameWithoutExtension(utilityExe.Name) + "_ConsoleOutput.txt"),
                ConsoleOutputFileIncludesCommandLine = true
            };

            Console.WriteLine("Running {0} using ProgRunner", utilityExe.FullName);
            progRunner.StartAndMonitorProgram();

            var cachedProcessID = 0;
            var startTime = DateTime.UtcNow;
            var abortProcess = false;

            while (progRunner.State != ProgRunner.States.NotMonitoring)
            {
                if (cachedProcessID == 0 && progRunner.PID != 0)
                {
                    Console.WriteLine("Program ID is {0}", progRunner.PID);
                    cachedProcessID = progRunner.PID;
                }

                ProgRunner.SleepMilliseconds(MONITOR_INTERVAL_MSEC / 2);

                try
                {
                    if (cachedProcessID > 0)
                    {
                        var cpuUsage = processStats.GetCoreUsageByProcessID(cachedProcessID);
                        Console.WriteLine("CPU Usage: " + cpuUsage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to get the core usage: {0}", ex.Message);
                }

                ProgRunner.SleepMilliseconds(MONITOR_INTERVAL_MSEC / 2);

                if (maxRuntimeSeconds > 0)
                {
                    if (DateTime.UtcNow.Subtract(startTime).TotalSeconds > maxRuntimeSeconds)
                    {
                        Console.WriteLine("Aborting ProcessID {0} since runtime has exceeded {1} seconds", cachedProcessID, maxRuntimeSeconds);
                        abortProcess = true;
                    }
                }

                if (abortProcess)
                    progRunner.StopMonitoringProgram(kill: true);
            }

            processStats.ClearCachedPerformanceCounterForProcessID(cachedProcessID);

            if (writeConsoleOutput)
            {
                ProgRunner.SleepMilliseconds(250);

                var consoleOutputFilePath = progRunner.ConsoleOutputFilePath;
                Assert.IsNotEmpty(consoleOutputFilePath, "Console output file path is empty");

                var consoleOutputFile = new FileInfo(consoleOutputFilePath);
                Assert.True(consoleOutputFile.Exists, "File not found: " + consoleOutputFilePath);

                var secondsSinceMidnight = (int)(DateTime.Now.Subtract(DateTime.Today).TotalSeconds);
                if (consoleOutputFile.DirectoryName == null)
                {
                    Assert.Fail("Unable to determine the parent directory of " + consoleOutputFilePath);
                }
                var newFilePath = Path.Combine(consoleOutputFile.DirectoryName,
                                               Path.GetFileNameWithoutExtension(consoleOutputFilePath) + "_" + secondsSinceMidnight + ".txt");
                consoleOutputFile.MoveTo(newFilePath);

                // Open the file and assure that the first line contains the .exe name
                using var reader = new StreamReader(new FileStream(newFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

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

            if (programAbortExpected)
            {
                Assert.True(abortProcess, "Process was expected to be aborted due to excessive runtime, but was not");
            }
            else
            {
                Assert.False(abortProcess, "Process was aborted due to excessive runtime; this is unexpected");
            }
        }
#endif
    }
}
