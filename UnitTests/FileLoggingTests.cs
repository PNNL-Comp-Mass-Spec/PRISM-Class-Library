﻿using System;
using System.IO;
using NUnit.Framework;
using PRISM;
using PRISM.Logging;

namespace PRISMTest
{
    [TestFixture]
    class FileLoggingTests
    {

        [TestCase("", "TestLogFile", true, "TestLogFile")]
        [TestCase(@"C:\Temp", "TestLogFile", true, @"C:\Temp\TestLogFile")]
        [TestCase(@"C:\Temp", "TestLogFile.log", true, @"C:\Temp\TestLogFile.log")]
        [TestCase("", "", true, @"PRISM_log")]
        [TestCase("", "TestLogFile", false, "TestLogFile.txt")]
        [TestCase(@"C:\Temp", "TestLogFile", false, @"C:\Temp\TestLogFile.txt")]
        [TestCase("", "TestLogFile.log", true, "TestLogFile.log")]
        [TestCase("", "", false, @"PRISM_log.txt")]
        public void TestLogFileName(
            string logFolder,
            string logFileNameBase,
            bool appendDateToBaseFileName,
            string expectedBaseName)
        {

            string logFilePath;
            if (string.IsNullOrWhiteSpace(logFileNameBase))
                logFilePath = String.Empty;
            else if (string.IsNullOrWhiteSpace(logFolder))
                logFilePath = logFileNameBase;
            else
                logFilePath = Path.Combine(logFolder, logFileNameBase);

            var logger = new FileLogger(logFilePath, BaseLogger.LogLevels.INFO, appendDateToBaseFileName);

            logger.Info("Test log message");

            string expectedName;

            if (appendDateToBaseFileName)
            {
                if (Path.HasExtension(expectedBaseName))
                {
                    var currentExtension = Path.GetExtension(expectedBaseName);
                    expectedName = Path.ChangeExtension(expectedBaseName, null) + "_" + DateTime.Now.ToString("MM-dd-yyyy") + currentExtension;
                }
                else
                {
                    expectedName = expectedBaseName + "_" + DateTime.Now.ToString("MM-dd-yyyy") + FileLogger.LOG_FILE_EXTENSION;
                }
            }
            else
                expectedName = expectedBaseName;

            if (!FileLogger.LogFilePath.EndsWith(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);

        }

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 20, 50)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 2000)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test fatal log message", BaseLogger.LogLevels.FATAL, 2, 350)]
        public void TestFileLogger(
            string logFolder,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {

            var logFilePath = Path.Combine(logFolder, logFileNameBase);

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

                clsProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            var expectedName = logFileNameBase + "_" + DateTime.Now.ToString("MM-dd-yyyy") + FileLogger.LOG_FILE_EXTENSION;
            if (!FileLogger.LogFilePath.EndsWith(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);

        }

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        public void TestFileLoggerStatic(
            string logFolder,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logFolder, logFileNameBase);

            const bool appendDateToBaseName = true;
            FileLogger.ChangeLogFileBaseName(logFilePath, appendDateToBaseName);

            TestStaticLogging(
                message, entryType, logCount, logDelayMilliseconds,
                logFileNameBase + "_" + DateTime.Now.ToString("MM-dd-yyyy") + FileLogger.LOG_FILE_EXTENSION);

        }

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        public void TestFileLoggerFixedLogFileName(
            string logFolder,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logFolder, logFileNameBase);

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
            string logFolder,
            string logFileNameBase,
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logFolder, logFileNameBase);

            const bool appendDateToBaseName = false;
            FileLogger.ChangeLogFileBaseName(logFilePath, appendDateToBaseName);

            TestStaticLogging(
                message, entryType, logCount, logDelayMilliseconds,
                logFileNameBase);

        }

        private void TestStaticLogging(
            string message,
            BaseLogger.LogLevels entryType,
            int logCount,
            int logDelayMilliseconds,
            string expectedLogFileName)
        {

            var randGenerator = new Random();

            for (var i = 0; i < logCount; i++)
            {
                FileLogger.WriteLog(entryType, message + " " + i);
                clsProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            if (!FileLogger.LogFilePath.EndsWith(expectedLogFileName))
            {
                var errMsg = "Log file name was not in the expected format of " + expectedLogFileName + "; see " + FileLogger.LogFilePath;
                Assert.Fail(errMsg);
            }

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);

        }

    }
}