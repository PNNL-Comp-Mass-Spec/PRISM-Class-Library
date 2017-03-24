using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class LoggerTests
    {

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", logMsgType.logNormal, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", logMsgType.logError, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", logMsgType.logNormal, 20, 50)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", logMsgType.logError, 2, 2000)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", logMsgType.logWarning, 15, 100)]
        public void TestFileLogger(string logFolder, string logFileNameBase, string message, logMsgType entryType, int logCount, int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logFolder, logFileNameBase);

            var logger = new clsFileLogger(logFilePath);
            var randGenerator = new Random();

            for (var i = 0; i < logCount; i++)
            {
                logger.PostEntry(message + " " + i, entryType, true);
                clsProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            var expectedName = logFileNameBase + "_" + DateTime.Now.ToString("MM-dd-yyyy");
            if (!logger.CurrentLogFilePath.Contains(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + logger.CurrentLogFilePath);
            }

            Console.WriteLine("Log entries written to " + logger.CurrentLogFilePath);

        }

        [TestCase(@"C:\Temp", "TestQueuedLogFile", "Test log message", logMsgType.logNormal, 4, 500)]
        [TestCase(@"C:\Temp", "TestQueuedLogFile", "Test log error", logMsgType.logError, 2, 250)]
        [TestCase(@"C:\Temp", "TestQueuedLogFile", "Test log message", logMsgType.logNormal, 20, 50)]
        [TestCase(@"C:\Temp", "TestQueuedLogFile", "Test log error", logMsgType.logError, 2, 2000)]
        [TestCase(@"C:\Temp", "TestQueuedLogFile", "Test log warning", logMsgType.logWarning, 15, 330)]
        public void TestQueueLogger(string logFolder, string logFileNameBase, string message, logMsgType entryType, int logCount, int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logFolder, logFileNameBase);

            var logger = new clsFileLogger(logFilePath);
            var randGenerator = new Random();

            var queueLogger = new clsQueLogger(logger);

            for (var i = 0; i < logCount; i++)
            {
                queueLogger.PostEntry(message + " " + i, entryType, true);
                clsProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            if (logCount > 5)
            {
                var messages = new List<clsLogEntry>();
                for (var i = 0; i < logCount; i++)
                {
                    messages.Add(new clsLogEntry("Bulk " + message + " " + i, entryType, true));
                }
                queueLogger.PostEntries(messages);
            }

            // Sleep to give the Queue logger time to log the log entries
            clsProgRunner.SleepMilliseconds(4000);

            var expectedName = logFileNameBase + "_" + DateTime.Now.ToString(clsFileLogger.FILENAME_DATESTAMP);
            if (!logger.CurrentLogFilePath.Contains(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + logger.CurrentLogFilePath);
            }

            Console.WriteLine("Log entries written to " + logger.CurrentLogFilePath);

        }

        [TestCase(@"Gigasax", "DMS5", @"C:\Temp", "TestLogFileForDBLogging")]
        [TestCase(@"Gigasax", "DMS5", "", "")]
        public void TestDBLogger(string server, string database, string logFolder, string logFileNameBase)
        {
            var connectionString = GetConnectionString(server, database);

            var logFilePath = Path.Combine(logFolder, logFileNameBase);
            var logger = new clsDBLogger(connectionString, logFilePath);

            // Call stored procedure PostLogEntry
            logger.PostEntry("Test log entry on " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), logMsgType.logDebug, false);
        }

        private string GetConnectionString(string server, string database)
        {
            return string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;", server, database);
        }

    }
}
