﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class ProgRunnerTests
    {
#if !(NETCOREAPP2_0)
        private const string UTILITIES_FOLDER = @"\\proto-2\unitTest_Files\PRISM\Utilities";

        /// <summary>
        /// Start long running processes then force them to be aborted by setting maxRuntimeSeconds to a small value
        /// </summary>
        /// <param name="exeName"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="createNoWindow"></param>
        /// <param name="writeConsoleOutput"></param>
        /// <param name="maxRuntimeSeconds"></param>
        /// <remarks>
        /// These tests work when run as a normal user but can fail when run on our Jenkins server under the NETWORK SERVICE account; thus the SkipNetworkService category
        /// Category PNL_Domain is included here because these tests do not work on AppVeyor
        /// </remarks>
        [TestCase("sleep.exe", "20", false, true, 6)]
        [TestCase("ls.exe", @"-alFR c:\", true, true, 3)]
        [Category("SkipNetworkService")]
        [Category("PNL_Domain")]
        public void TestAbortRunningProgram(string exeName, string cmdArgs, bool createNoWindow, bool writeConsoleOutput, int maxRuntimeSeconds)
        {
            TestRunProgram(exeName, cmdArgs, createNoWindow, writeConsoleOutput, maxRuntimeSeconds, programAbortExpected: true);
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

        private void TestGarbageCollectionWork(int iterations, int gcEvents, int dictionaryCount, int dictionarySize)
        {
            // This method will create 2.5 million FileInfo objects (if iterations is 2500000) and store those in random locations in various dictionaries
            // It will remove half of the dictionaries gcEvents times, calling GarbageCollectNow after each removal

            var dictionaries = new List<Dictionary<int, List<FileInfo>>>();
            var rand = new Random();

            var keysRemoved = 0;
            var valuesRemoved = 0;

            var lastGC = DateTime.UtcNow;

            var gcInterval = (int)Math.Floor(iterations / (double)gcEvents);

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

                clsProgRunner.GarbageCollectNow();

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
        /// Test starting a process
        /// </summary>
        /// <param name="exeName"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="createNoWindow"></param>
        /// <param name="writeConsoleOutput"></param>
        /// <param name="maxRuntimeSeconds"></param>
        /// <remarks>Category PNL_Domain is included here because these tests do not work on AppVeyor</remarks>
        [TestCase("sleep.exe", "3", false, false, 10)]
        [TestCase("sleep.exe", "3", true, false, 10)]
        [TestCase("ls.exe", @"-alF c:\", false, false, 10)]
        [Category("PNL_Domain")]
        public void TestRunProgram(string exeName, string cmdArgs, bool createNoWindow, bool writeConsoleOutput, int maxRuntimeSeconds)
        {
            TestRunProgram(exeName, cmdArgs, createNoWindow, writeConsoleOutput, maxRuntimeSeconds, programAbortExpected: false);
        }

        private void TestRunProgram(string exeName, string cmdArgs, bool createNoWindow, bool writeConsoleOutput, int maxRuntimeSeconds, bool programAbortExpected)
        {
            const int MONITOR_INTERVAL_MSEC = 500;

            var utilityExe = new FileInfo(Path.Combine(UTILITIES_FOLDER, exeName));
            if (!utilityExe.Exists)
            {
                Assert.Fail("Exe not found: " + utilityExe.FullName);
            }

            var processStats = new PRISMWin.clsProcessStats();

            var workDir = @"C:\Temp";
            var tempFolder = new DirectoryInfo(workDir);
            if (!tempFolder.Exists)
                tempFolder.Create();

            var coreCount = processStats.GetCoreCount();
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
                        var cpuUsage = processStats.GetCoreUsageByProcessID(cachedProcessID);
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

            processStats.ClearCachedPerformanceCounterForProcessID(cachedProcessID);

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
#endif
    }
}
