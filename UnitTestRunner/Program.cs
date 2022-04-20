using System;
using System.Collections.Generic;
using PRISM;
using PRISM.Logging;
using PRISMDatabaseUtils;
using PRISMTest;

// ReSharper disable UnusedMember.Local

namespace UnitTestRunner;

internal static class Program
{
    // Ignore Spelling: dmsdev, dmswebuser

    private const string DMS_WEB_USER = "dmswebuser";

    private const bool TRACE_MODE = true;

    static void Main()
    {
        var testsToRun = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TestPostLogEntryAsQueryWithParameters"
        };

        TestDbLoggerPostgres();

        TestDbLoggerSqlServer();

        TestStoredProcedures(testsToRun);

        LogTools.FlushPendingMessages();

        Console.WriteLine();
    }

    private static void TestDbLoggerPostgres(
        string host = "prismweb3",
        string database = "dmsdev",
        string user = DMS_WEB_USER)
    {
        LogTools.CreateFileLogger("UnitTestRunner", TRACE_MODE);

        LogTools.LogMessage("Test log message (console only)", false, false);

        LogTools.LogMessage("Test log message (file and console)");

        var connectionString = DbToolsFactory.GetConnectionString(
            DbServerTypes.PostgreSQL, host, database, user, string.Empty);

        PRISMDatabaseUtils.Logging.DbConfig.CreateDbLogger(connectionString, "UnitTestRunner", traceMode: TRACE_MODE);

        LogTools.LogMessage("Test log message (file and console)");

        LogTools.LogWarning("Test log warning 1", true);
        LogTools.LogWarning("Test log warning 2", true);
    }

    private static void TestDbLoggerSqlServer(
        string host = "gigasax",
        string database = "dms5")
    {
        LogTools.CreateFileLogger("UnitTestRunner", TRACE_MODE);

        LogTools.LogMessage("Test log message (console only)", false, false);

        LogTools.LogMessage("Test log message (file and console)");

        var connectionString = DbToolsFactory.GetConnectionString(
            DbServerTypes.MSSQLServer, host, database, "UnitTestRunner");

        PRISMDatabaseUtils.Logging.DbConfig.CreateDbLogger(connectionString, "UnitTestRunner", traceMode: TRACE_MODE);

        LogTools.LogMessage("Test log message (file and console)");

        LogTools.LogWarning("Test log warning 1", true);
        LogTools.LogWarning("Test log warning 2", true);
    }

    private static void TestStoredProcedures(
        ICollection<string> testsToRun,
        string host = "prismweb3",
        string database = "dmsdev",
        string user = TestDBTools.DMS_READER)
    {
        try
        {
            var testHarness = new StoredProcedureTests();

            if (testsToRun.Contains("TestPostLogEntryAsProcedure"))
            {
                testHarness.TestPostLogEntryAsProcedure(host, database);
            }

            if (testsToRun.Contains("TestPostLogEntryAsQuery"))
            {
                testHarness.TestPostLogEntryAsQuery(host, database, user, false);
                testHarness.TestPostLogEntryAsQuery(host, database, "NonExistentUser", false);
                testHarness.TestPostLogEntryAsQuery(host, database, DMS_WEB_USER, true);
            }

            if (testsToRun.Contains("TestPostLogEntryAsQueryWithParameters"))
            {
                testHarness.TestPostLogEntryAsQueryWithParameters(host, database, DMS_WEB_USER, true);
            }
        }
        catch (Exception ex)
        {
            ConsoleMsgUtils.ShowError("Error in TestStoredProcedures", ex);
        }
    }
}