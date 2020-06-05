﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Npgsql;
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

        [TestCase(false, "")]
        [TestCase(true, "")]
        [TestCase(false, "_acqDate:Date,_acqTime:DateTime,_acqInfo:XML,_status:TinyInt")]
        [TestCase(true, "_acqDate:Date,_acqTime:DateTime,_acqInfo:XML,_status:TinyInt")]
        public void TestAddParameter(bool usePostgres, string parameterList)
        {
            var server = "DbServer";
            var database = "TestDB";

            if (string.IsNullOrWhiteSpace(parameterList))
            {
                // Use the default parameter list
                parameterList = "@message:VarChar,@eventID:Int,@eventDate:DateTime,@success:TinyInt";
            }

            var connectionString = usePostgres ?
                                       GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD) :
                                       GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);

            var parameters = ParseParameterList(parameterList);

            TestAddParameter(connectionString, parameters);
        }

        public void TestAddParameter(string connectionString, List<KeyValuePair<string, SqlType>> parameters)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var cmd = dbTools.CreateCommand("TestProcedure", CommandType.StoredProcedure);

            var addedParameters = new List<DbParameter>();

            foreach (var parameter in parameters)
            {
                var newParam = dbTools.AddParameter(cmd, parameter.Key, parameter.Value);
                addedParameters.Add(newParam);
            }

            DisplayParameters(addedParameters);
        }

        [TestCase(false, "")]
        [TestCase(true, "")]
        [TestCase(true, "_acqDate:Date,_acqTime:Time,_acqInfo:JSON,_status:Bit")]
        public void TestAddPgSqlParameter(bool usePostgres, string parameterList)
        {
            var server = "DbServer";
            var database = "TestDB";

            if (string.IsNullOrWhiteSpace(parameterList))
            {
                // Use the default parameter list
                parameterList = "@message:VarChar,@eventID:Integer,@eventDate:Timestamp,@maxValue:Double,@success:Boolean,@source:Citext";
            }

            var connectionString = usePostgres ?
                                       GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD) :
                                       GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);

            var parameters = ParseParameterList(parameterList);

            TestAddPgSqlParameter(connectionString, parameters);
        }

        public void TestAddPgSqlParameter(string connectionString, List<KeyValuePair<string, SqlType>> parameters)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var cmd = dbTools.CreateCommand("TestProcedure", CommandType.StoredProcedure);

            var addedParameters = new List<DbParameter>();

            foreach (var parameter in parameters)
            {
                var newParam = dbTools.AddParameter(cmd, parameter.Key, parameter.Value);
                addedParameters.Add(newParam);
            }

            DisplayParameters(addedParameters);
        }

        private void DisplayParameters(IEnumerable<DbParameter> addedParameters)
        {

            Console.WriteLine("{0,-15} {1,-15} {2}", "Param Name", "SqlType", "Db Type");
            foreach (var item in addedParameters)
            {
                string dbType;
                if (item is NpgsqlParameter npgSqlParam)
                {
                    dbType = npgSqlParam.NpgsqlDbType.ToString();
                }
                else if (item is SqlParameter sqlParam)
                {
                    dbType = sqlParam.SqlDbType.ToString();
                }
                else
                {
                    dbType = string.Empty;
                }

                Console.WriteLine("{0,-15} {1,-15} {2}", item.ParameterName, item.DbType, dbType);
            }
        }

        private List<KeyValuePair<string, SqlType>> ParseParameterList(string parameterList)
        {
            var parameters = new List<KeyValuePair<string, SqlType>>();

            foreach (var item in parameterList.Split(','))
            {
                var colonIndex = item.IndexOf(':');
                if (colonIndex < 0)
                    throw new Exception("Colon not found in the parameter list for " + item);

                var argName = item.Substring(0, colonIndex);
                var argType = item.Substring(colonIndex + 1);

                if (Enum.TryParse<SqlType>(argType, out var parsedType))
                {
                    parameters.Add(new KeyValuePair<string, SqlType>(argName, parsedType));
                    continue;
                }

                throw new Exception(string.Format("Cannot convert {0} to enum SqlType", argType));
            }

            return parameters;
        }


        [TestCase("Gigasax", "dms5", "T_Event_Log", 15)]
        [TestCase("Gigasax", "dms5", "T_Event_Target", 15)]
        [Category("DatabaseIntegrated")]
        public void TestGetColumnValueSqlServer(string server, string database, string tableName, int rowCountToRetrieve)
        {
            var connectionString = GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestGetColumnValue(connectionString, tableName, rowCountToRetrieve);
        }

        [TestCase("prismweb3", "dms", "T_Event_Log", 15)]
        [TestCase("prismweb3", "dms", "T_Event_Target", 15)]
        [Category("DatabaseNamedUser")]
        public void TestGetColumnValuePostgres(string server, string database, string tableName, int rowCountToRetrieve)
        {
            var connectionString = GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD);
            TestGetColumnValue(connectionString, tableName, rowCountToRetrieve);
        }

        public void TestGetColumnValue(string connectionString, string tableName, int rowCountToRetrieve)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            string query;
            var columnNames = new List<string>();

            if (dbTools.DbServerType == DbServerTypes.MSSQLServer)
            {
                if (tableName.Equals("T_Event_Log", StringComparison.OrdinalIgnoreCase))
                {
                    columnNames.Add("[Index]");
                    columnNames.Add("LookupQ.Target_Type");
                    columnNames.Add("LookupQ.Target_ID");
                    columnNames.Add("Target_State");
                    columnNames.Add("Entered");
                    columnNames.Add("Entered_By");
                }
                else if (tableName.Equals("T_Event_Target", StringComparison.OrdinalIgnoreCase))
                {
                    columnNames.Add("[ID]");
                    columnNames.Add("Name");
                    columnNames.Add("Target_Table");
                    columnNames.Add("LookupQ.Target_ID_Column");
                    columnNames.Add("Target_State_Column");
                }
                else
                {
                    throw new Exception("Invalid table name: " + tableName);
                }

                query = string.Format("SELECT " + string.Join(", ", columnNames) + " FROM (" +
                                      "   SELECT TOP {0} * FROM {1}" +
                                      "   Order By {2} Desc) LookupQ " +
                                      "Order By {2}", rowCountToRetrieve, tableName, columnNames.First());

            }
            else
            {
                if (tableName.Equals("T_Event_Log", StringComparison.OrdinalIgnoreCase))
                {
                    tableName = "mc.t_event_log";
                    columnNames.Add("event_id");
                    columnNames.Add("LookupQ.target_type");
                    columnNames.Add("LookupQ.target_id");
                    columnNames.Add("target_state");
                    columnNames.Add("entered");
                    columnNames.Add("entered_by");
                }
                else if (tableName.Equals("T_Event_Target", StringComparison.OrdinalIgnoreCase))
                {
                    tableName = "mc.t_event_target";
                    columnNames.Add("id");
                    columnNames.Add("name");
                    columnNames.Add("target_table");
                    columnNames.Add("LookupQ.target_id_column");
                    columnNames.Add("target_state_column");
                }
                else
                {
                    throw new Exception("Invalid table name: " + tableName);
                }

                query = string.Format("SELECT  " + string.Join(", ", columnNames) + " FROM (" +
                                      "   SELECT * FROM {1}" +
                                      "   Order By {2} Desc Limit {0}) LookupQ " +
                                      "Order By {2}", rowCountToRetrieve, tableName, columnNames.First());


            }

            var columnDataTypes = new List<SqlType>();

            if (tableName.Equals("T_Event_Log", StringComparison.OrdinalIgnoreCase) || tableName.Equals("mc.t_event_log"))
            {
                columnDataTypes.Add(SqlType.Int);
                columnDataTypes.Add(SqlType.Int);
                columnDataTypes.Add(SqlType.Int);
                columnDataTypes.Add(SqlType.SmallInt);
                columnDataTypes.Add(SqlType.DateTime);
                columnDataTypes.Add(SqlType.VarChar);
            }
            else if (tableName.Equals("T_Event_Target") || tableName.Equals("mc.t_event_target"))
            {
                columnDataTypes.Add(SqlType.Int);
                columnDataTypes.Add(SqlType.VarChar);
                columnDataTypes.Add(SqlType.VarChar);
                columnDataTypes.Add(SqlType.VarChar);
                columnDataTypes.Add(SqlType.VarChar);
            }

            Console.WriteLine("Create command at {0:yyyy-MM-dd hh:mm:ss tt}", DateTime.Now);

            var spCmd = dbTools.CreateCommand(query);

            var success = dbTools.GetQueryResults(spCmd, out var queryResults, 1);

            Assert.IsTrue(success, "GetQueryResults returned false");

            Assert.Greater(queryResults.Count, 0, "Row count in {0} should be non-zero, but was not", tableName);

            var columnMapping = dbTools.GetColumnMapping(columnNames);

            Console.WriteLine();
            Console.WriteLine("{0} most recent entries in table {1}:", queryResults.Count, tableName);
            Console.WriteLine();

            var dataLine = new StringBuilder();
            foreach (var column in columnNames)
            {
                if (dataLine.Length > 0)
                    dataLine.Append("   ");
                dataLine.Append(string.Format("{0,-15}", column.Split('.').Last()));
            }
            Console.WriteLine(dataLine.ToString());

            var rowNumber = 0;
            foreach (var resultRow in queryResults)
            {
                rowNumber += 1;

                dataLine.Clear();
                for (var i = 0; i < columnNames.Count; i++)
                {
                    if (dataLine.Length > 0)
                        dataLine.Append("   ");

                    string currentColumnName;
                    if (rowNumber % 3 == 0)
                    {
                        // Force a fuzzy name match
                        currentColumnName = columnNames[i].Substring(0, columnNames[i].Length - 1);
                    }
                    else if (rowNumber % 3 == 1 && columnNames[i].IndexOf('.') >= 0)
                    {
                        // Match the name after the period
                        currentColumnName = columnNames[i].Split('.').Last();
                    }
                    else
                    {
                        // Match the entire name
                        currentColumnName = columnNames[i];
                    }

                    switch (columnDataTypes[i])
                    {
                        case SqlType.Int:
                        case SqlType.SmallInt:
                            var intValue = dbTools.GetColumnValue(resultRow, columnMapping, currentColumnName, 0);
                            dataLine.Append(string.Format("{0,-15}", intValue));
                            break;

                        case SqlType.Date:
                        case SqlType.DateTime:
                            var dateValue = dbTools.GetColumnValue(resultRow, columnMapping, currentColumnName, DateTime.MinValue);
                            dataLine.Append(string.Format("{0,-15}", dateValue));
                            break;

                        default:
                            var value = dbTools.GetColumnValue(resultRow, columnMapping, currentColumnName);
                            dataLine.Append(string.Format("{0,-15}", value));
                            break;
                    }
                }
                Console.WriteLine(dataLine.ToString());
            }

            Console.WriteLine();

        }

        [TestCase("Gigasax", "dms5", 5, 1)]
        [TestCase("Gigasax", "dms5", 10, 2)]
        [Category("DatabaseIntegrated")]
        public void TestGetRecentLogEntriesSqlServer(string server, string database, int rowCountToRetrieve, int iterations)
        {
            var connectionString = GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestGetRecentLogEntries(connectionString, rowCountToRetrieve, iterations);
        }

        [TestCase("prismweb3", "dms", 5, 1)]
        [TestCase("prismweb3", "dms", 10, 2)]
        [Category("DatabaseNamedUser")]
        public void TestGetRecentLogEntriesPostgres(string server, string database, int rowCountToRetrieve, int iterations)
        {
            var connectionString = GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD);
            TestGetRecentLogEntries(connectionString, rowCountToRetrieve, iterations);
        }

        public void TestGetRecentLogEntries(string connectionString, int rowCountToRetrieve, int iterations)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            if (iterations < 1)
                iterations = 1;
            if (iterations > 5)
                iterations = 5;

            for (var i = 0; i < iterations; i++)
            {
                string query;
                string tableName;
                if (dbTools.DbServerType == DbServerTypes.MSSQLServer)
                {
                    tableName = "t_log_entries";

                    query = string.Format("SELECT * FROM (" +
                                          "   SELECT TOP {0} * FROM {1}" +
                                          "   Order By entry_id Desc) LookupQ " +
                                          "Order By entry_id", rowCountToRetrieve + i * 2, tableName);
                }
                else
                {
                    tableName = "public.t_log_entries";

                    query = string.Format("SELECT * FROM (" +
                                          "   SELECT * FROM {1}" +
                                          "   Order By entry_id Desc Limit {0}) LookupQ " +
                                          "Order By entry_id", rowCountToRetrieve + i * 2, tableName);
                }

                Console.WriteLine("Create command at {0:yyyy-MM-dd hh:mm:ss tt}", DateTime.Now);

                var spCmd = dbTools.CreateCommand(query);

                var success = dbTools.GetQueryResults(spCmd, out var queryResults, 1);

                Assert.IsTrue(success, "GetQueryResults returned false");

                Assert.Greater(queryResults.Count, 0, "Row count in {0} should be non-zero, but was not", tableName);

                Console.WriteLine("{0} most recent entries in table {1}:", queryResults.Count, tableName);
                ShowRowsFromTLogEntries(queryResults);

                if (i < iterations - 1)
                {
                    System.Threading.Thread.Sleep(1250);
                }

                Console.WriteLine();
            }
        }

        public static void ShowRowsFromTLogEntries(List<List<string>> results)
        {
            Console.WriteLine("{0,-10} {1,-21} {2,-20} {3}", "Entry_ID", "Date", "Posted_By", "Message");
            foreach (var item in results)
            {
                var postingTime = DateTime.Parse(item[2]);
                Console.WriteLine("{0,-10} {1,-21:yyyy-MM-dd hh:mm tt} {2,-20} {3}", item[0], postingTime, item[1], item[4]);
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
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD);
            TestGetTableRowCount(connectionString, tableName);
        }

        public void TestGetTableRowCount(string connectionString, string tableName)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

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
        /// <param name="password">password</param>
        /// <returns></returns>
        /// <remarks>
        /// Instead of providing an explicit password, create a pgpass file
        /// Linux:   ~/.pgpass
        /// Windows: c:\users\username\AppData\Roaming\postgresql\pgpass.conf
        /// </remarks>
        public static string GetConnectionStringPostgres(string server, string database, string user, string password = "")
        {
            string optionalPassword;
            if (string.IsNullOrWhiteSpace(password))
                optionalPassword = string.Empty;
            else
                optionalPassword = ";Password=" + password;

            return string.Format("Host={0};Username={1};Database={2}{3}", server, user, database, optionalPassword);
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
