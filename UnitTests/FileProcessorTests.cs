using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using PRISM.FileProcessor;

namespace PRISMTest
{
    [TestFixture]
    class FileProcessorTests
    {

        [TestCase(@"C:\Temp", "", @"C:\Temp\PRISM_log")]
        [TestCase(@"C:\Temp", "TestLogFile", @"C:\Temp\TestLogFile_log")]
        [TestCase("", "", "PRISM_log")]
        [TestCase("", "TestLogFile", "TestLogFile_log")]
        public void TestLogFileName(
            string logFolder,
            string logFileNameBase,
            string expectedBaseName)
        {
            var fileStatsLogger = new SimpleFileStatsLogger
            {
                LogFileBaseName = logFileNameBase,
                LogFolderPath = logFolder,
                LogMessagesToFile = true
            };

            var fileToFind = ProcessFilesOrFoldersBase.GetAppPath();

            fileStatsLogger.ProcessFile(fileToFind);

            Console.WriteLine();
            Console.WriteLine("Log file path: " + fileStatsLogger.LogFilePath);

            var logFileSuffix = expectedBaseName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";

            if (!fileStatsLogger.LogFilePath.EndsWith(logFileSuffix))
            {
                Assert.Fail("Unexpected log file name; does not end with " + logFileSuffix);
            }

        }

    }

    /// <summary>
    /// Simple class that derives from ProcessFilesBase
    /// It looks for a file in the given folder and logs the file size and last write time to a log file
    /// </summary>
    class SimpleFileStatsLogger : ProcessFilesBase
    {

        public string LogFileBaseName { get; set; }

        public override string GetErrorMessage()
        {
            return string.Empty;
        }

        public override bool ProcessFile(string inputFilePath, string outputFolderPath, string parameterFilePath, bool resetErrorCode)
        {
            CleanupFilePaths(ref inputFilePath, ref outputFolderPath);

            if (LogMessagesToFile && (!string.IsNullOrWhiteSpace(LogFolderPath) || !string.IsNullOrWhiteSpace(LogFileBaseName)))
            {
                UpdateAutoDefinedLogFilePath(LogFolderPath, LogFileBaseName);
            }

            var fileInfo = new FileInfo(inputFilePath);

            if (fileInfo.Exists)
            {
                LogMessage("File found: " + fileInfo.FullName);
                Console.WriteLine("Stats for file: " + fileInfo.FullName);
                Console.WriteLine("Size: " + fileInfo.Length + " bytes");
                Console.WriteLine("Last write time: " + fileInfo.LastWriteTime.ToString(CultureInfo.InvariantCulture));
                return true;
            }

            LogMessage("File not found: " + fileInfo.FullName);

            return false;


        }
    }
}
