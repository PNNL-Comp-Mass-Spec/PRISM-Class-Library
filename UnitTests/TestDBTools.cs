﻿using System;
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
            DbServerTypes.PostgresSQL)]
        [TestCase(
            "DbServerType=Postgres;Host=prismweb3;Username=dmsreader;Database=dms",
            DbServerTypes.PostgresSQL)]
        public void TestDbToolsInitialization(string connectionString, DbServerTypes expectedServerType)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            if (dbTools.DbServerType == DbServerTypes.PostgresSQL)
            {
                Console.WriteLine("Connection string was interpreted as PostgreSQL");
                Assert.AreEqual(expectedServerType, DbServerTypes.PostgresSQL);
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
            var connectionString = GetConnectionString(server, database, user, password);
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
            var connectionString = GetConnectionString(server, database, user, password);
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
        /// Get a SQL Server compatible connection string
        /// </summary>
        /// <param name="server"></param>
        /// <param name="database"></param>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetConnectionString(string server, string database, string user = "Integrated", string password = "")
        {
            if (string.Equals(user, "Integrated", StringComparison.OrdinalIgnoreCase))
                return string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;", server, database);

            return string.Format("Data Source={0};Initial Catalog={1};User={2};Password={3};", server, database, user, password);
        }
    }
}
