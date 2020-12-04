using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PRISM;

#pragma warning disable 618

namespace PRISMTest
{
    /// <summary>
    /// These unit tests apply to the log classes in LogClasses.cs
    /// Those classes are obsolete, and thus this entire file is marked with a pragma to disable warnings regarding the use of obsolete classes
    /// </summary>
    [TestFixture]
    internal class LoggerTests
    {
        // Ignore Spelling: pragma, MM-dd-yyyy, hh:mm:ss

        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", logMsgType.logNormal, 4, 500)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", logMsgType.logError, 2, 250)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log message", logMsgType.logNormal, 20, 50)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log error", logMsgType.logError, 2, 2000)]
        [TestCase(@"C:\Temp", "TestLogFile", "Test log warning", logMsgType.logWarning, 15, 100)]
        public void TestFileLogger(string logDirectory, string logFileNameBase, string message, logMsgType entryType, int logCount, int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logDirectory, logFileNameBase);

            var logger = new clsFileLogger(logFilePath);
            var randGenerator = new Random();

            string formatString;
            if (logCount < 10)
                formatString = "{0} {1}/{2}";
            else
                formatString = "{0} {1,2}/{2}";

            for (var i = 0; i < logCount; i++)
            {
                logger.PostEntry(string.Format(formatString, message, i + 1, logCount), entryType, true);
                ProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
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
        public void TestQueueLogger(string logDirectory, string logFileNameBase, string message, logMsgType entryType, int logCount, int logDelayMilliseconds)
        {
            var logFilePath = Path.Combine(logDirectory, logFileNameBase);

            var logger = new clsFileLogger(logFilePath);
            var randGenerator = new Random();

            var queueLogger = new clsQueLogger(logger);

            string formatString;
            if (logCount < 10)
                formatString = "{0} {1}/{2}";
            else
                formatString = "{0} {1,2}/{2}";

            for (var i = 0; i < logCount; i++)
            {
                queueLogger.PostEntry(string.Format(formatString, message, i + 1, logCount), entryType, true);
                ProgRunner.SleepMilliseconds(logDelayMilliseconds + randGenerator.Next(0, logDelayMilliseconds / 10));
            }

            if (logCount > 5)
            {
                var messages = new List<clsLogEntry>();
                for (var i = 0; i < logCount; i++)
                {
                    messages.Add(new clsLogEntry(string.Format(formatString, "Bulk " + message, i + 1, logCount), entryType));
                }
                queueLogger.PostEntries(messages);
            }

            // Sleep to give the Queue logger time to log the log entries
            ProgRunner.SleepMilliseconds(4000);

            var expectedName = logFileNameBase + "_" + DateTime.Now.ToString(clsFileLogger.FILENAME_DATE_STAMP);
            if (!logger.CurrentLogFilePath.Contains(expectedName))
            {
                Assert.Fail("Log file name was not in the expected format of " + expectedName + "; see " + logger.CurrentLogFilePath);
            }

            Console.WriteLine("Log entries written to " + logger.CurrentLogFilePath);
        }

        [TestCase("Gigasax", "DMS5", @"C:\Temp", "TestLogFileForDBLogging")]
        [TestCase("Gigasax", "DMS5", "", "")]
        [Category("DatabaseIntegrated")]
        public void TestDBLoggerIntegrated(string server, string database, string logDirectory, string logFileNameBase)
        {
            TestDBLogger(server, database, "Integrated", "", logDirectory, logFileNameBase);
        }

        [TestCase("Gigasax", "DMS5", @"C:\Temp", "TestLogFileForDBLogging")]
        [TestCase("Gigasax", "DMS5", "", "")]
        [Category("DatabaseNamedUser")]
        public void TestDBLoggerNamedUser(string server, string database, string logDirectory, string logFileNameBase)
        {
            TestDBLogger(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD, logDirectory, logFileNameBase);
        }

        private void TestDBLogger(string server, string database, string user, string password, string logDirectory, string logFileNameBase)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, user, password);

            var logFilePath = Path.Combine(logDirectory, logFileNameBase);
            var logger = new clsDBLogger(connectionString, logFilePath);

            Console.WriteLine("Calling logger.PostEntry using " + database + " as user " + user);

            // Call stored procedure PostLogEntry
            logger.PostEntry("Test log entry on " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), logMsgType.logDebug, false);
        }
    }
}
