using System;
using System.IO;
using NUnit.Framework;
using PRISM;
using PRISM.Logging;

namespace PRISMTest
{
    [TestFixture]
    class FileLoggingTests
    {

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 20, 50)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 2000)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test fatal log message", BaseLogger.LogLevels.FATAL, 2, 350)]
        public void TestFileLogger(string logFolder, string logFileNameBase, string message, BaseLogger.LogLevels entryType, int logCount, int logDelayMilliseconds)
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

            var expectedName = logFileNameBase + "_" + DateTime.Now.ToString("MM-dd-yyyy");
            if (!FileLogger.LogFilePath.Contains(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);

        }

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", BaseLogger.LogLevels.INFO, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", BaseLogger.LogLevels.ERROR, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", BaseLogger.LogLevels.WARN, 15, 100)]
        public void TestFileLoggerStatic(string logFolder, string logFileNameBase, string message, BaseLogger.LogLevels entryType, int logCount, int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logFolder, logFileNameBase);

            FileLogger.ChangeLogFileBaseName(logFilePath);
            var randGenerator = new Random();

            for (var i = 0; i < logCount; i++)
            {
                FileLogger.WriteLog(entryType, message + " " + i);
                clsProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            var expectedName = logFileNameBase + "_" + DateTime.Now.ToString("MM-dd-yyyy");
            if (!FileLogger.LogFilePath.Contains(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + FileLogger.LogFilePath);
            }

            Console.WriteLine("Log entries written to " + FileLogger.LogFilePath);

        }
    }
}
