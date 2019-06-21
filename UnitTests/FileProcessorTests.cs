using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using PRISM;
using PRISM.FileProcessor;

namespace PRISMTest
{
    [TestFixture]
    class FileProcessorTests
    {

        [TestCase(@"C:\Temp", "", @"C:\Temp\PRISM_log", 0)]
        [TestCase(@"C:\Temp", "TestLogFile", @"C:\Temp\TestLogFile_log", 0)]
        [TestCase(@"C:\Temp", "TestLogFile", @"C:\Temp\TestLogFile_log", 2)]
        [TestCase("", "", "PRISM_log", 0)]
        [TestCase("", "", "PRISM_log", 2)]
        [TestCase("", "TestLogFile", "TestLogFile_log", 0)]
        public void TestLogFileName(
            string logDirectory,
            string logFileNameBase,
            string expectedBaseName,
            int messagesToLog)
        {

            var fileStatsLogger = new SimpleFileStatsLogger
            {
                LogFileBaseName = logFileNameBase,
                LogDirectoryPath = logDirectory,
                LogMessagesToFile = true,
                MessagesToLog = messagesToLog,
                UseLogFilePath = false
            };

            var fileToFind = ProcessFilesOrDirectoriesBase.GetAppPath();

            fileStatsLogger.ProcessFile(fileToFind);

            Console.WriteLine();
            Console.WriteLine("Log file path: " + fileStatsLogger.LogFilePath);

            var logFileSuffix = expectedBaseName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";

            if (!fileStatsLogger.LogFilePath.EndsWith(logFileSuffix))
            {
                Assert.Fail("Unexpected log file name; does not end with " + logFileSuffix);
            }

        }

        [TestCase(@"C:\Temp\PRISM_Custom_log_Job1000.txt", 0)]
        [TestCase(@"C:\Temp\PRISM_Custom_log_Job1001.txt", 1)]
        [TestCase(@"C:\Temp\PRISM_Custom_log_Job1002.txt", 2)]
        public void TestLogFileNameFullPath(
            string logFilePath,
            int messagesToLog)
        {
            var fileStatsLogger = new SimpleFileStatsLogger
            {
                LogFilePath = logFilePath,
                LogMessagesToFile = true,
                MessagesToLog = messagesToLog,
                UseLogFilePath = true
            };

            var fileToFind = ProcessFilesOrDirectoriesBase.GetAppPath();

            fileStatsLogger.ProcessFile(fileToFind);

            Console.WriteLine();
            Console.WriteLine("Log file path: " + fileStatsLogger.LogFilePath);

            Assert.AreEqual(logFilePath, fileStatsLogger.LogFilePath);
        }

        [TestCase(@"C:\Program Files", "msvcp*.dll", 5)]
        [TestCase(@"C:\Users", "msvcp*.dll", 5)]
        public void TestRecurseDirectories(
            string startingDirectory,
            string fileMatchSpec,
            int maxLevelsToRecurse)
        {
            var fileFinder = new SimpleFileFinder {
                LogMessagesToFile = false,
                SkipConsoleWriteIfNoStatusListener = true,
                SkipConsoleWriteIfNoDebugListener = true,
                SkipConsoleWriteIfNoProgressListener = true
            };

            var startingDirectoryAndFileSpec = Path.Combine(startingDirectory, fileMatchSpec);

            fileFinder.ProcessFilesAndRecurseDirectories(startingDirectoryAndFileSpec, "", "", false, "", maxLevelsToRecurse);

            Console.WriteLine();
            Console.WriteLine("Processed {0} files", fileFinder.FilesProcessed);

            if (fileFinder.FileProcessErrors > 0)
            {
                Console.WriteLine("Error processing {0} files", fileFinder.FileProcessErrors);
            }
        }

    }

    /// <summary>
    /// Simple class that derives from ProcessFilesBase
    /// It looks for files in the given directory and subdirectories
    /// Matching file names are shown at the console
    /// </summary>
    class SimpleFileFinder : ProcessFilesBase
    {

        public override string GetErrorMessage()
        {
            return string.Empty;
        }

        public override bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath, bool resetErrorCode)
        {
            CleanupFilePaths(ref inputFilePath, ref outputDirectoryPath);

            var fileInfo = new FileInfo(inputFilePath);

            if (fileInfo.Exists)
            {
                Console.WriteLine(fileInfo.FullName);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Simple class that derives from ProcessFilesBase
    /// It looks for a file in the given directory and logs the file size and last write time to a log file
    /// </summary>
    class SimpleFileStatsLogger : ProcessFilesBase
    {

        public string LogFileBaseName { get; set; }

        public int MessagesToLog { get; set; }

        /// <summary>
        /// When true, define the log file using LogFilePath
        /// </summary>
        public bool UseLogFilePath { get; set; }

        public override string GetErrorMessage()
        {
            return string.Empty;
        }

        public override bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath, bool resetErrorCode)
        {
            CleanupFilePaths(ref inputFilePath, ref outputDirectoryPath);

            if (LogMessagesToFile)
            {
                if (UseLogFilePath && string.IsNullOrWhiteSpace(LogFilePath) && !string.IsNullOrWhiteSpace(LogFileBaseName))
                {
                    LogFilePath = LogFileBaseName;
                }
                else if (!string.IsNullOrWhiteSpace(LogDirectoryPath) || !string.IsNullOrWhiteSpace(LogFileBaseName))
                {
                    UpdateAutoDefinedLogFilePath(LogDirectoryPath, LogFileBaseName);
                }
            }

            var fileInfo = new FileInfo(inputFilePath);

            if (fileInfo.Exists)
            {
                LogMessage("File found: " + fileInfo.FullName);
                Console.WriteLine("Stats for file: " + fileInfo.FullName);
                Console.WriteLine("Size: " + fileInfo.Length + " bytes");
                Console.WriteLine("Last write time: " + fileInfo.LastWriteTime.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                LogMessage("File not found: " + fileInfo.FullName);
            }

            for (var i = 1; i <= MessagesToLog; i++)
            {
                var secondsToSleep = Math.Min(i, 5);
                ConsoleMsgUtils.SleepSeconds(secondsToSleep);

                LogMessage(string.Format("Placeholder message {0}", i));
            }

            return false;


        }
    }
}
