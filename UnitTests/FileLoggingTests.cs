using System;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;
using NUnit.Framework;
using PRISM;
using PRISM.Logging;
using ZipFile = System.IO.Compression.ZipFile;

namespace PRISMTest
{
    [TestFixture]
    internal class FileLoggingTests
    {
        // Ignore Spelling: MM-dd-yyyy, hh:mm:ss, tt

        private const string LOGFILE_BASENAME = "FileLoggingTester";

        [TestCase(@"C:\Temp", 5)]
        public void TestArchiveOldLogFiles(string logDirectory, int yearsToSimulate)
        {
            const int FILES_PER_YEAR = 40;

            var logDir = new DirectoryInfo(logDirectory);

            if (yearsToSimulate > 20)
                yearsToSimulate = 20;

            if (!logDir.Exists)
            {
                try
                {
                    logDir.Create();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Unable to create missing directory " + logDir.FullName + ": " + ex.Message);
                }
            }

            // Create 10 dummy log files using the MM-dd-yyyy format, starting 52 days before today
            var logFilesCreated1 = CreateLogFiles(logDir, DateTime.Now.AddDays(-52), 10, true);
            Assert.IsTrue(logFilesCreated1.Count > 0, "CreateLogFiles returned an empty list for logFilesCreated1");

            // Create 40 dummy log files using the yyyy-MM-dd format, starting 42 days before today
            var logFilesCreated2 = CreateLogFiles(logDir, DateTime.Now.AddDays(-42), 40, false);
            Assert.IsTrue(logFilesCreated2.Count > 0, "CreateLogFiles returned an empty list for logFilesCreated2");

            // Delete old dummy log files from the previous year
            var previousYearDir = new DirectoryInfo(Path.Combine(logDir.FullName, (DateTime.Now.Year - 1).ToString()));

            if (previousYearDir.Exists)
            {
                var filesToFind = new List<FileInfo>();
                filesToFind.AddRange(logFilesCreated1);
                filesToFind.AddRange(logFilesCreated2);

                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (var logFile in filesToFind)
                {
                    var oldLogFile = new FileInfo(Path.Combine(previousYearDir.FullName, logFile.Name));
                    if (oldLogFile.Exists)
                        oldLogFile.Delete();
                }
            }

            // Cache the list of old log file directories so we can examine the .zip file for each
            var logFileDirs = new List<DirectoryInfo>();

            // Create dummy log files for previous years
            // In addition, delete any old .zip files
            for (var i = 0; i < yearsToSimulate; i++)
            {
                var startDate = DateTime.Now.AddDays(-230 - 365 * i);

                var logFileStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day);

                var oldLogFilesDir = new DirectoryInfo(Path.Combine(logDir.FullName, startDate.Year.ToString()));
                var oldLogDirZipFile = new FileInfo(Path.Combine(logDir.FullName, startDate.Year + ".zip"));
                var oldLogDirZipFileArchived = new FileInfo(Path.Combine(logDir.FullName, FileLogger.ARCHIVED_LOG_FILES_DIRECTORY_NAME, startDate.Year + ".zip"));

                CreateLogFiles(oldLogFilesDir, logFileStartDate, FILES_PER_YEAR, true);

                if (oldLogDirZipFile.Exists)
                    oldLogDirZipFile.Delete();

                if (oldLogDirZipFileArchived.Exists)
                    oldLogDirZipFileArchived.Delete();

                logFileDirs.Add(oldLogFilesDir);

                System.Threading.Thread.Sleep(250);
            }

            var oldZippedLogFiles = new List<FileInfo>();

            // Create three placeholder zipped log files for prior years
            // This is done to validate that they are moved into the log archive directory
            for (var i = yearsToSimulate; i < yearsToSimulate + 3; i++)
            {
                var startDate = DateTime.Now.AddDays(-230 - 365 * i);

                var infoFile = new FileInfo(Path.Combine(logDir.FullName, string.Format("FileLoggingTester_01-01-{0}.txt", startDate.Year)));

                var oldLogDirZipFile = new FileInfo(Path.Combine(logDir.FullName, startDate.Year + ".zip"));

                if (oldLogDirZipFile.Exists)
                    oldLogDirZipFile.Delete();

                using (var writer = new StreamWriter(new FileStream(infoFile.FullName, FileMode.Create, FileAccess.Write)))
                {
                    writer.WriteLine("Placeholder log file for {0}", startDate.Year);
                }

                // Ionic.Zip.ZipFile
                using var zipper = new Ionic.Zip.ZipFile(oldLogDirZipFile.FullName)
                {
                    UseZip64WhenSaving = Zip64Option.AsNecessary
                };

                zipper.AddItem(infoFile.FullName, string.Empty);
                zipper.Save();

                oldZippedLogFiles.Add(oldLogDirZipFile);

                var oldLogDirZipFileArchived = new FileInfo(Path.Combine(logDir.FullName, FileLogger.ARCHIVED_LOG_FILES_DIRECTORY_NAME, oldLogDirZipFile.Name));

                if (oldLogDirZipFileArchived.Exists)
                    oldLogDirZipFileArchived.Delete();

                infoFile.Delete();
            }

            FileLogger.ChangeLogFileBaseName(Path.Combine(logDirectory, LOGFILE_BASENAME));
            FileLogger.ZipOldLogDirectories = true;

            System.Threading.Thread.Sleep(250);

            // Instruct the logger to create .zip files for previous years
            FileLogger.ArchiveOldLogFilesNow();

            // Assure that the zip files were created and have data
            foreach (var oldLogFilesDir in logFileDirs)
            {
                var zipFilePath = Path.Combine(logDirectory, FileLogger.ARCHIVED_LOG_FILES_DIRECTORY_NAME, oldLogFilesDir.Name + ".zip");

                var zipFileToCheck = new FileInfo(zipFilePath);

                if (!zipFileToCheck.Exists)
                {
                    // The previous year won't have a zip file created until after March 1
                    // Determine whether or not one should be expected

                    if (!int.TryParse(oldLogFilesDir.Name, out var subDirYear))
                    {
                        Assert.Fail("Log file directory name is not a year: " + oldLogFilesDir.Name);
                    }

                    var dateThresholdForZippingPreviousYearFiles = new DateTime(DateTime.Now.Year, 1, 1).AddDays(FileLogger.OLD_LOG_DIRECTORY_AGE_THRESHOLD_DAYS);

                    if (subDirYear == DateTime.Now.Year ||
                        subDirYear == DateTime.Now.Year - 1 && DateTime.Now < dateThresholdForZippingPreviousYearFiles)
                    {
                        Console.WriteLine(
                            "The previous year is not 90 days in the past, thus files in {0} were not zipped; this is expected",
                            oldLogFilesDir.FullName);

                        continue;
                    }

                    Assert.Fail("Expected .zip file not found: " + zipFileToCheck.FullName);
                }

                using var archive = ZipFile.OpenRead(zipFileToCheck.FullName);

                var fileCountInZip = archive.Entries.Count;

                Assert.GreaterOrEqual(fileCountInZip, FILES_PER_YEAR, "Zip file {0} has fewer than {1} files", zipFileToCheck.FullName, FILES_PER_YEAR);

                Console.WriteLine("{0} has {1} entries", zipFileToCheck.FullName, fileCountInZip);
            }

            // Assure that the placeholder .zip files were also archived
            foreach (var zipFile in oldZippedLogFiles)
            {
                var zipFilePath = Path.Combine(logDirectory, FileLogger.ARCHIVED_LOG_FILES_DIRECTORY_NAME, zipFile.Name);

                var zipFileToCheck = new FileInfo(zipFilePath);

                if (!zipFileToCheck.Exists)
                {
                    Assert.Fail("Expected .zip file not found: " + zipFileToCheck.FullName);
                }
            }

            FileLogger.FlushPendingMessages();
        }

        [TestCase("", "TestLogFile", true, "TestLogFile")]
        [TestCase(@"C:\Temp", "TestLogFile", true, @"C:\Temp\TestLogFile")]
        [TestCase(@"C:\Temp", "TestLogFile.log", true, @"C:\Temp\TestLogFile.log")]
        [TestCase("", "", true, "PRISM_log")]
        [TestCase("", "TestLogFile", false, "TestLogFile.txt")]
        [TestCase(@"C:\Temp", "TestLogFile", false, @"C:\Temp\TestLogFile.txt")]
        [TestCase("", "TestLogFile.log", true, "TestLogFile.log")]
        [TestCase("", "", false, "PRISM_log.txt")]
        public void TestLogFileName(
            string logDirectory,
            string logFileNameBase,
            bool appendDateToBaseFileName,
            string expectedBaseName)
        {
            FileLogger.ResetLogFileName();

            string logFilePath;
            if (string.IsNullOrWhiteSpace(logFileNameBase))
                logFilePath = string.Empty;
            else if (string.IsNullOrWhiteSpace(logDirectory))
                logFilePath = logFileNameBase;
            else
                logFilePath = Path.Combine(logDirectory, logFileNameBase);

            var logger = new FileLogger(logFilePath, BaseLogger.LogLevels.INFO, appendDateToBaseFileName);

            logger.Info("Test log message");

            string expectedName;

            if (appendDateToBaseFileName)
            {
                if (Path.HasExtension(expectedBaseName))
                {
                    var currentExtension = Path.GetExtension(expectedBaseName);
                    expectedName = Path.ChangeExtension(expectedBaseName, null) + "_" + DateTime.Now.ToString("yyyy-MM-dd") + currentExtension;
                }
                else
                {
                    expectedName = expectedBaseName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + FileLogger.LOG_FILE_EXTENSION;
                }
            }
            else
            {
                expectedName = expectedBaseName;
            }

            if (!FileLogger.LogFilePath.EndsWith(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);
        }

        [TestCase("", "PRISM_log")]
        [TestCase(@"Logs\EmptyFileLoggerConstructor", @"Logs\EmptyFileLoggerConstructor")]
        public void TestEmptyFileLoggerConstructor(string logFileNameBase, string expectedBaseName)
        {
            FileLogger.ResetLogFileName();

            var logger = new FileLogger
            {
                LogLevel = BaseLogger.LogLevels.INFO
            };

            if (!string.IsNullOrWhiteSpace(logFileNameBase))
            {
                FileLogger.ChangeLogFileBaseName(logFileNameBase);
            }

            logger.Info("Info message");

            // This debug message won't appear in the log file because the LogLevel is INFO
            logger.Debug("Debug message");

            if (string.IsNullOrWhiteSpace(logFileNameBase))
            {
                ProgRunner.SleepMilliseconds(500);
                FileLogger.FlushPendingMessages();
                ProgRunner.SleepMilliseconds(500);
            }

            Console.WriteLine();
            Console.WriteLine("Log file path: " + FileLogger.LogFilePath);

            var expectedName = expectedBaseName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + FileLogger.LOG_FILE_EXTENSION;
            if (!FileLogger.LogFilePath.EndsWith(expectedName))
            {
                if (string.IsNullOrWhiteSpace(FileLogger.LogFilePath))
                    Console.WriteLine("Actual log file name is empty; this is not a critical error");
                else
                    Assert.Fail("Actual log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }
        }

        [TestCase("", "PRISM_log")]
        [TestCase(@"Logs\DefinedFileLoggerConstructor", @"Logs\DefinedFileLoggerConstructor")]
        public void TestFileLoggerConstructor(string logFileNameBase, string expectedBaseName)
        {
            FileLogger.ResetLogFileName();

            var logger = new FileLogger(logFileNameBase, BaseLogger.LogLevels.INFO);

            logger.Info("Info message");

            // This debug message won't appear in the log file because the LogLevel is INFO
            logger.Debug("Debug message");

            if (string.IsNullOrWhiteSpace(logFileNameBase))
            {
                ProgRunner.SleepMilliseconds(500);
                FileLogger.FlushPendingMessages();
            }

            Console.WriteLine();
            Console.WriteLine("Log file path: " + FileLogger.LogFilePath);

            var expectedName = expectedBaseName + "_" + DateTime.Now.ToString("yyyy-MM-dd") + FileLogger.LOG_FILE_EXTENSION;
            if (!FileLogger.LogFilePath.EndsWith(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }
        }

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 20, 50)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 2000)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test fatal log message", BaseLogger.LogLevels.FATAL, 2, 350)]
        public void TestFileLogger(
            string logDirectory,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            FileLogger.ResetLogFileName();

            var logFilePath = Path.Combine(logDirectory, logFileNameBase);

            var logger = new FileLogger(logFilePath);
            var randGenerator = new Random();

            for (var i = 0; i < logCount; i++)
            {
                var logMessage = message + " " + i;

                switch (entryType)
                {
                    case BaseLogger.LogLevels.DEBUG:
                        logger.Debug(logMessage);
                        break;
                    case BaseLogger.LogLevels.INFO:
                        logger.Info(logMessage);
                        break;
                    case BaseLogger.LogLevels.WARN:
                        logger.Warn(logMessage);
                        break;
                    case BaseLogger.LogLevels.ERROR:
                        logger.Error(logMessage);
                        break;
                    case BaseLogger.LogLevels.FATAL:
                        logger.Fatal(logMessage);
                        break;
                    default:
                        logger.Fatal("Unrecognized log type: " + entryType);
                        break;
                }

                ProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            var expectedName = logFileNameBase + "_" + DateTime.Now.ToString("yyyy-MM-dd") + FileLogger.LOG_FILE_EXTENSION;
            if (!FileLogger.LogFilePath.EndsWith(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);
        }

        [TestCase("Test log message", BaseLogger.LogLevels.INFO, 4, 125)]
        [TestCase("Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase("Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        public void TestFileLoggerStaticDefaultName(
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            var logFileNameBase = "PRISM_log";

            FileLogger.ResetLogFileName();

            TestStaticLogging(
                message, entryType, logCount, logDelayMilliseconds,
                logFileNameBase + "_" + DateTime.Now.ToString("yyyy-MM-dd") + FileLogger.LOG_FILE_EXTENSION);
        }

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        public void TestFileLoggerStatic(
            string logDirectory,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logDirectory, logFileNameBase);

            const bool appendDateToBaseName = true;
            FileLogger.ChangeLogFileBaseName(logFilePath, appendDateToBaseName);

            TestStaticLogging(
                message, entryType, logCount, logDelayMilliseconds,
                logFileNameBase + "_" + DateTime.Now.ToString("yyyy-MM-dd") + FileLogger.LOG_FILE_EXTENSION);
        }

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        public void TestFileLoggerFixedLogFileName(
            string logDirectory,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logDirectory, logFileNameBase);

            const bool appendDateToBaseName = false;
            FileLogger.ChangeLogFileBaseName(logFilePath, appendDateToBaseName);

            TestStaticLogging(
                message, entryType, logCount, logDelayMilliseconds,
                logFileNameBase + FileLogger.LOG_FILE_EXTENSION);
        }

        [TestCase(@"C:\Temp", "TestLogFile.log", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile.log", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile.log", "Test log warning", BaseLogger.LogLevels.WARN, 3, 275)]
        public void TestFileLoggerFixedLogFileNameWithExtension(
            string logDirectory,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logDirectory, logFileNameBase);

            const bool appendDateToBaseName = false;
            FileLogger.ChangeLogFileBaseName(logFilePath, appendDateToBaseName);

            TestStaticLogging(
                message, entryType, logCount, logDelayMilliseconds,
                logFileNameBase);
        }

        [TestCase("TestLogFile", "Test log message via FileLogger.WriteLog", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase("TestLogFile", "Test log error via FileLogger.WriteLog", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase("TestLogFile", "Test log warning via FileLogger.WriteLog", BaseLogger.LogLevels.WARN, 15, 100)]
        public void TestFileLoggerRelativePath(
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            const bool appendDateToBaseName = true;
            FileLogger.ChangeLogFileBaseName(logFileNameBase, appendDateToBaseName);

            TestStaticLogging(
                message, entryType, logCount, logDelayMilliseconds,
                logFileNameBase + "_" + DateTime.Now.ToString("yyyy-MM-dd") + FileLogger.LOG_FILE_EXTENSION);
        }

        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.INFO, BaseLogger.LogLevels.INFO)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.INFO, BaseLogger.LogLevels.DEBUG)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.INFO, BaseLogger.LogLevels.ERROR)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.DEBUG, BaseLogger.LogLevels.INFO)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.ERROR, BaseLogger.LogLevels.DEBUG)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.ERROR, BaseLogger.LogLevels.ERROR)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.ERROR, BaseLogger.LogLevels.FATAL)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.FATAL, BaseLogger.LogLevels.FATAL)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.FATAL, BaseLogger.LogLevels.INFO)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.FATAL, BaseLogger.LogLevels.DEBUG)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.WARN, BaseLogger.LogLevels.WARN)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.WARN, BaseLogger.LogLevels.INFO)]
        [TestCase(@"C:\Temp", "TestLogFile", BaseLogger.LogLevels.WARN, BaseLogger.LogLevels.FATAL)]
        [TestCase("Logs", "TestLogFile", BaseLogger.LogLevels.INFO, BaseLogger.LogLevels.INFO)]
        [TestCase("Logs", "TestLogFile", BaseLogger.LogLevels.INFO, BaseLogger.LogLevels.DEBUG)]
        [TestCase("Logs", "TestLogFile", BaseLogger.LogLevels.INFO, BaseLogger.LogLevels.ERROR)]
        public void TestLogTools(string logDirectory, string logFileNameBase, BaseLogger.LogLevels entryType, BaseLogger.LogLevels logThresholdLevel)
        {
            var logFilePath = Path.Combine(logDirectory, logFileNameBase);

            LogTools.CreateFileLogger(logFilePath, logThresholdLevel);
            Console.WriteLine("Log file; " + LogTools.CurrentLogFilePath);

            var message = "Test log " + entryType + " via LogTools (log threshold is " + logThresholdLevel + ")";

            switch (entryType)
            {
                case BaseLogger.LogLevels.DEBUG:
                    LogTools.LogDebug(message);
                    break;
                case BaseLogger.LogLevels.INFO:
                    LogTools.LogMessage(message);
                    break;
                case BaseLogger.LogLevels.WARN:
                    LogTools.LogWarning(message);
                    break;
                case BaseLogger.LogLevels.ERROR:
                    LogTools.LogError(message);
                    break;
                case BaseLogger.LogLevels.FATAL:
                    LogTools.LogFatalError(message);
                    break;
                default:
                    LogTools.LogError("Unrecognized log type: " + entryType);
                    break;
            }

            ProgRunner.SleepMilliseconds(100);

            LogTools.FlushPendingMessages();
        }

        [TestCase(LogMessage.TimestampFormatMode.MonthDayYear24hr, "MM/dd/yyyy HH:mm:ss")]
        [TestCase(LogMessage.TimestampFormatMode.MonthDayYear12hr, "MM/dd/yyyy hh:mm:ss tt")]
        [TestCase(LogMessage.TimestampFormatMode.YearMonthDay24hr, "yyyy-MM-dd HH:mm:ss")]
        [TestCase(LogMessage.TimestampFormatMode.YearMonthDay12hr, "yyyy-MM-dd hh:mm:ss tt")]
        public void TestTimestampFormatting(LogMessage.TimestampFormatMode timestampFormat, string expectedFormatString)
        {
            var testMessage = new LogMessage(BaseLogger.LogLevels.INFO, "Test message");

            var formattedMessage = testMessage.GetFormattedMessage(timestampFormat);

            EvaluateFormattedMessageTimestamp(formattedMessage, expectedFormatString, true);
        }

        [TestCase(LogMessage.TimestampFormatMode.MonthDayYear24hr, "MM/dd/yyyy HH:mm:ss", true)]
        [TestCase(LogMessage.TimestampFormatMode.MonthDayYear24hr, "MM/dd/yyyy HH:mm:ss", false)]
        [TestCase(LogMessage.TimestampFormatMode.MonthDayYear12hr, "MM/dd/yyyy hh:mm:ss tt", true)]
        [TestCase(LogMessage.TimestampFormatMode.MonthDayYear12hr, "MM/dd/yyyy hh:mm:ss tt", false)]
        [TestCase(LogMessage.TimestampFormatMode.YearMonthDay24hr, "yyyy-MM-dd HH:mm:ss", true)]
        [TestCase(LogMessage.TimestampFormatMode.YearMonthDay24hr, "yyyy-MM-dd HH:mm:ss", false)]
        [TestCase(LogMessage.TimestampFormatMode.YearMonthDay12hr, "yyyy-MM-dd hh:mm:ss tt", true)]
        [TestCase(LogMessage.TimestampFormatMode.YearMonthDay12hr, "yyyy-MM-dd hh:mm:ss tt", false)]
        public void TestTimestampFormattingLocalVsUtc(LogMessage.TimestampFormatMode timestampFormat, string expectedFormatString, bool useLocalTime)
        {
            var testMessage = new LogMessage(BaseLogger.LogLevels.INFO, "Test message");

            var formattedMessage = testMessage.GetFormattedMessage(useLocalTime, timestampFormat);

            EvaluateFormattedMessageTimestamp(formattedMessage, expectedFormatString, useLocalTime);
        }

        private List<FileInfo> CreateLogFiles(DirectoryInfo logDir, DateTime logFileDate, int filesPerYear, bool useLegacyDateFormat)
        {
            const string LOG_FILE_DATE_CODE_LEGACY = "MM-dd-yyyy";

            var logFilesCreated = new List<FileInfo>();

            if (!logDir.Exists)
                logDir.Create();

            for (var i = 0; i < filesPerYear; i++)
            {
                var timestampFormat = useLegacyDateFormat ? LOG_FILE_DATE_CODE_LEGACY : FileLogger.LOG_FILE_DATE_CODE;
                var formatString = "{0}_{1:" + timestampFormat + "}.txt";

                // formatString will be either
                // {0}_{1:yyyy-MM-dd}.txt or
                // {0}_{1:MM-dd-yyyy}.txt

                var logFilePath = Path.Combine(logDir.FullName, string.Format(formatString, LOGFILE_BASENAME, logFileDate));

                using (var writer = new StreamWriter(new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.WriteLine("Test log file, created {0}", DateTime.Now.ToString(LogMessage.DATE_TIME_FORMAT_YEAR_MONTH_DAY_12H));
                }

                var logFile = new FileInfo(logFilePath)
                {
                    LastWriteTime = logFileDate.AddHours(23)
                };

                logFilesCreated.Add(logFile);

                logFileDate = logFileDate.AddDays(1);
            }

            return logFilesCreated;
        }

        private void EvaluateFormattedMessageTimestamp(string formattedMessage, string expectedFormatString, bool useLocalTime)
        {
            Console.WriteLine(formattedMessage);

            if (!formattedMessage.Contains(","))
            {
                Assert.Fail("Formatted message is not comma separated");
            }

            var messageParts = formattedMessage.Split(',');
            var messageTimestampText = messageParts[0];

            string expectedTimestampText;
            if (useLocalTime)
                expectedTimestampText = DateTime.Now.ToString(expectedFormatString);
            else
                expectedTimestampText = DateTime.UtcNow.ToString(expectedFormatString);

            if (!DateTime.TryParse(messageTimestampText, out var messageTimestamp))
            {
                Assert.Fail("Could not parse date/time from message timestamp: " + messageTimestampText);
            }

            if (!DateTime.TryParse(expectedTimestampText, out var expectedTimestamp))
            {
                Assert.Fail("Could not parse date/time from expected timestamp: " + expectedTimestampText);
            }

            var timeDiffSeconds = expectedTimestamp.Subtract(messageTimestamp).TotalSeconds;

            Assert.LessOrEqual(Math.Abs(timeDiffSeconds), 2,
                               "Message timestamp does not agree with system clock: {0} vs. {1}",
                               messageTimestampText, expectedTimestampText);
        }

        private void TestStaticLogging(
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds,
            string expectedLogFileName)
        {
            var randGenerator = new Random();

            string formatString;
            if (logCount < 10)
                formatString = "{0} {1}/{2}";
            else
                formatString = "{0} {1,2}/{2}";

            for (var i = 0; i < logCount; i++)
            {
                FileLogger.WriteLog(entryType, string.Format(formatString, message, i + 1, logCount));
                ProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            if (!FileLogger.LogFilePath.EndsWith(expectedLogFileName))
            {
                var errMsg = "Log file name was not in the expected format of " + expectedLogFileName + "; see " + FileLogger.LogFilePath;
                Assert.Fail(errMsg);
            }

            FileLogger.FlushPendingMessages();

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);
        }
    }
}
