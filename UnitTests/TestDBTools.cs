using System;
using System.Linq;
using NUnit.Framework;
using PRISMDatabaseUtils;

namespace PRISMTest
{
    [TestFixture]
    class TestDBTools
    {
        public const string DMS_READER = "dmsreader";
        public const string DMS_READER_PASSWORD = "dms4fun";

        [TestCase(
            "Data Source=gigasax;Initial Catalog=DMS5;integrated security=SSPI",
            DbServerTypes.MSSQLServer)]
        [TestCase(
            "Data Source=gigasax;Initial Catalog=dms5;User=dmsreader;Password=dms4fun",
            DbServerTypes.MSSQLServer)]
        [TestCase(
            "DbServerType=SqlServer;Data Source=gigasax;Initial Catalog=DMS5;integrated security=SSPI",
            DbServerTypes.MSSQLServer)]
        [TestCase(
            "Host=prismweb3;Username=dmsreader;Database=dms",
            DbServerTypes.PostgreSQL)]
        [TestCase(
            "DbServerType=Postgres;Host=prismweb3;Username=dmsreader;Database=dms",
            DbServerTypes.PostgreSQL)]
        public void TestDbToolsInitialization(string connectionString, DbServerTypes expectedServerType)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                Console.WriteLine("Connection string was interpreted as PostgreSQL");
                Assert.AreEqual(expectedServerType, DbServerTypes.PostgreSQL);
                return;
            }

            if (dbTools.DbServerType == DbServerTypes.MSSQLServer)
            {
                Console.WriteLine("Connection string was interpreted as Microsoft SQL Server");
                Assert.AreEqual(expectedServerType, DbServerTypes.MSSQLServer);
                return;
            }

            Assert.Fail("The dbTools instance returned by DbToolsFactory.GetDBTool is not a recognized class");
        }

        [TestCase("Gigasax", "dms5")]
        [Category("DatabaseIntegrated")]
        public void TestGetRecentLogEntriesSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestGetRecentLogEntries(connectionString);
        }

        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestGetRecentLogEntriesPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);
            TestGetRecentLogEntries(connectionString);
        }

        public void TestGetRecentLogEntries(string connectionString)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            string query;
            string tableName;

            if (dbTools.DbServerType == DbServerTypes.MSSQLServer)
            {
                tableName = "t_log_entries";

                query = string.Format("SELECT * FROM (" +
                                      "   SELECT TOP 5 * FROM {0}" +
                                      "   Order By entry_id Desc) LookupQ " +
                                      "Order By entry_id", tableName);
            }
            else
            {
                tableName = "public.t_log_entries";

                query = string.Format("SELECT * FROM (" +
                                      "   SELECT * FROM {0}" +
                                      "   Order By entry_id Desc Limit 5) LookupQ " +
                                      "Order By entry_id", tableName);
            }

            var spCmd = dbTools.CreateCommand(query);

            var success = dbTools.GetQueryResults(spCmd, out var results, 1);

            Assert.IsTrue(success, "GetQueryResults returned false");

            Assert.Greater(results.Count, 0, "Row count in {0} should be non-zero, but was not", tableName);

            Console.WriteLine("{0} most recent entries in table {1}:", results.Count, tableName);

            foreach (var item in results)
            {
                Console.WriteLine(string.Join(", ", item));
            }

        }

        [TestCase("Gigasax", "dms5", "T_Log_Entries")]
        [Category("DatabaseIntegrated")]
        public void TestGetTableRowCountSqlServer(string server, string database, string tableName)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestGetTableRowCount(connectionString, tableName);
        }

        [TestCase("prismweb3", "dms", "public.t_log_entries")]
        [Category("DatabaseNamedUser")]
        public void TestGetTableRowCountPostgres(string server, string database, string tableName)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);
            TestGetTableRowCount(connectionString, tableName);
        }

        public void TestGetTableRowCount(string connectionString, string tableName)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            var query = "SELECT COUNT(*) FROM " + tableName;

            var spCmd = dbTools.CreateCommand(query);

            var success = dbTools.GetQueryScalar(spCmd, out var queryResult, 1);

            Assert.IsTrue(success, "GetQueryScalar returned false");

            var tableRowCount = queryResult.CastDBVal<int>();
            Console.WriteLine("RowCount in table {0} is {1:N0}", tableName, tableRowCount);

            Assert.Greater(tableRowCount, 0, "Row count in {0} should be non-zero, but was not", tableName);
        }

        [TestCase("Gigasax", "DMS5",
            "SELECT U_PRN, U_Name, U_HID FROM T_Users WHERE U_Name = 'AutoUser'", 1, "H09090911,AutoUser,H09090911")]
        [TestCase("Gigasax", "DMS5",
            "SELECT [Num C], [Num H], [Num N], [Num O], [Num S] FROM V_Residue_List_Report WHERE (Symbol IN ('K', 'R')) ORDER BY Symbol", 2,
            "6, 12, 2, 1, 0")]
        [Category("DatabaseIntegrated")]
        public void TestQueryTableIntegrated(string server, string database, string query, int expectedRowCount, string expectedValueList)
        {
            TestQueryTable(server, database, "Integrated", "", query, expectedRowCount, expectedValueList);
        }

        [TestCase("Gigasax", "DMS5",
            "SELECT U_PRN, U_Name, U_HID FROM T_Users WHERE U_Name = 'AutoUser'", 1, "H09090911,AutoUser,H09090911")]
        [TestCase("Gigasax", "DMS5",
            "SELECT [Num C], [Num H], [Num N], [Num O], [Num S] FROM V_Residue_List_Report WHERE (Symbol IN ('K', 'R')) ORDER BY Symbol", 2,
            "6, 12, 2, 1, 0")]
        [Category("PNL_Domain")]
        public void TestQueryTableNamedUser(string server, string database, string query, int expectedRowCount, string expectedValueList)
        {
            TestQueryTable(server, database, DMS_READER, DMS_READER_PASSWORD, query, expectedRowCount, expectedValueList);
        }

        [TestCase(
            "Data Source=gigasax;Initial Catalog=DMS5;integrated security=SSPI",
            "SELECT U_PRN, U_Name, U_HID FROM T_Users WHERE U_Name = 'AutoUser'",
            1, "H09090911,AutoUser,H09090911")]
        [TestCase(
            "Data Source=gigasax;Initial Catalog=dms5;User=dmsreader;Password=dms4fun",
            "SELECT [Num C], [Num H], [Num N], [Num O], [Num S] FROM V_Residue_List_Report WHERE (Symbol IN ('K', 'R')) ORDER BY Symbol",
            2,
            "6, 12, 2, 1, 0")]
        [TestCase(
            "DbServerType=SqlServer;Data Source=gigasax;Initial Catalog=DMS5;integrated security=SSPI",
            "SELECT U_PRN, U_Name, U_HID FROM T_Users WHERE U_Name = 'AutoUser'",
            1, "H09090911,AutoUser,H09090911")]
        [TestCase(
            "DbServerType=Postgres;Host=prismweb3;Username=dmsreader;Database=dms",
            "select mgr_name from mc.t_mgrs where mgr_name similar to 'pub-12-[1-4]';",
            4, "Pub-12-1,Pub-12-2,Pub-12-3,Pub-12-4")]
        [Category("PNL_Domain")]
        [Category("DatabaseIntegrated")]
        public void TestQueryTableCustomConnectionString(string connectionString, string query, int expectedRowCount, string expectedValueList)
        {
            var database = "Defined via connection string";
            var user = "Defined via connection string";

            TestQueryTableWork(connectionString, database, user, query, expectedRowCount, expectedValueList);
        }

        private void TestQueryTable(string server, string database, string user, string password, string query, int expectedRowCount, string expectedValueList)
        {
            var connectionString = GetConnectionStringSqlServer(server, database, user, password);
            TestQueryTableWork(connectionString, database, user, query, expectedRowCount, expectedValueList);
        }

        private void TestQueryTableWork(string connectionString, string database, string user, string query, int expectedRowCount, string expectedValueList)
        {

            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            Console.WriteLine("Running query " + query + " against " + database + " as user " + user);

            dbTools.GetQueryResults(query, out var lstResults);

            var expectedValues = expectedValueList.Split(',');

            Assert.AreEqual(lstResults.Count, expectedRowCount, "RowCount mismatch");

            var firstRow = lstResults.First();
            for (var colIndex = 0; colIndex < firstRow.Count; colIndex++)
            {
                if (colIndex >= expectedValues.Length)
                    break;

                Assert.AreEqual(firstRow[colIndex], expectedValues[colIndex].Trim(),
                    "Data value mismatch, column {0}, expected {1} but actually {2}",
                    colIndex + 1, expectedValues[colIndex], firstRow[colIndex]);
            }

            Console.WriteLine("Rows returned: " + lstResults.Count);
        }

        [TestCase("Gigasax", "DMS5",
            "SELECT U_PRN, U_Name, U_HID FROM T_FakeTable WHERE U_Name = 'AutoUser'", 0, "")]
        [TestCase("Gigasax", "DMS5",
            "SELECT FakeColumn FROM T_Log_Entries WHERE ID = 5", 0, "")]
        [TestCase("Gigasax", "DMS5",
            "SELECT * FROM T_LogEntries WHERE ID = 5", 0, "")]
        [TestCase("Gigasax", "NonExistentDatabase",
            "SELECT * FROM T_FakeTable", 0, "")]
        [Category("DatabaseIntegrated")]
        public void TestQueryFailuresIntegrated(
            string server, string database,
            string query, int expectedRowCount, string expectedValueList)
        {
            TestQueryFailures(server, database, "Integrated", "", query, expectedRowCount, expectedValueList);
        }

        [TestCase("Gigasax", "DMS5", "dmsreader", "dms4fun",
            "SELECT U_PRN, U_Name, U_HID FROM T_FakeTable WHERE U_Name = 'AutoUser'", 0, "")]
        [TestCase("Gigasax", "DMS5", "dmsreader", "dms4fun",
            "SELECT FakeColumn FROM T_Log_Entries WHERE ID = 5", 0, "")]
        [TestCase("Gigasax", "DMS5", "dmsreader", "dms4fun",
            "SELECT * FROM T_LogEntries WHERE ID = 5", 0, "")]
        [TestCase("Gigasax", "NonExistentDatabase", "dmsreader", "dms4fun",
            "SELECT * FROM T_FakeTable", 0, "")]
        [TestCase("Gigasax", "DMS5", "dmsreader", "WrongPassword",
            "SELECT * FROM T_Log_Entries WHERE ID = 5", 0, "")]
        [TestCase("Gigasax", "Ontology_Lookup", "dmsreader", "dms4fun",
            "SELECT * FROM T_Permissions_Test_Table", 0, "")]
        [Category("PNL_Domain")]
        public void TestQueryFailuresNamedUser(
            string server, string database, string user, string password,
            string query, int expectedRowCount, string expectedValueList)
        {
            TestQueryFailures(server, database, user, password, query, expectedRowCount, expectedValueList);
        }

        private void TestQueryFailures(string server, string database, string user, string password,
            string query, int expectedRowCount, string expectedValueList)
        {
            var connectionString = GetConnectionStringSqlServer(server, database, user, password);
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            Console.WriteLine("Running query " + query + " against " + database + " as user " + user);

            dbTools.GetQueryResults(query, out var lstResults);

            var expectedValues = expectedValueList.Split(',');

            if (lstResults == null || lstResults.Count == 0 && expectedRowCount == 0)
            {
                if (expectedRowCount == 0)
                    Console.WriteLine("No results found; this was expected");
                return;
            }

            Assert.AreEqual(lstResults.Count, expectedRowCount, "RowCount mismatch");

            var firstRow = lstResults.First();
            for (var colIndex = 0; colIndex < firstRow.Count; colIndex++)
            {
                if (colIndex >= expectedValues.Length)
                    break;

                Assert.AreEqual(firstRow[colIndex], expectedValues[colIndex].Trim(),
                    "Data value mismatch, column {0}, expected {1} but actually {2}",
                    colIndex + 1, expectedValues[colIndex], firstRow[colIndex]);
            }

            Console.WriteLine("Rows returned: " + lstResults.Count);
        }

        /// <summary>
        /// Get a PostgreSQL compatible connection string
        /// </summary>
        /// <param name="server"></param>
        /// <param name="database"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static string GetConnectionStringPostgres(string server, string database, string user)
        {
            return string.Format("Host={0};Username={1};Database={2}", server, user, database);
        }

        /// <summary>
        /// Get a SQL Server compatible connection string
        /// </summary>
        /// <param name="server"></param>
        /// <param name="database"></param>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetConnectionStringSqlServer(string server, string database, string user = "Integrated", string password = "")
        {
            if (string.Equals(user, "Integrated", StringComparison.OrdinalIgnoreCase))
                return string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;", server, database);

            return string.Format("Data Source={0};Initial Catalog={1};User={2};Password={3};", server, database, user, password);
        }
    }
}
