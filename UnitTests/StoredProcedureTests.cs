﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using Npgsql;
using NUnit.Framework;
using PRISMDatabaseUtils;

namespace PRISMTest
{
    [TestFixture]
    public class StoredProcedureTests
    {
        // Ignore Spelling: dmsdev, dmswebuser, mtuser, workdir, PostgreSQL, ProteinSeqs

        private const string DMS_WEB_USER = "dmswebuser";

        private const string MTS_READER = "mtuser";
        private const string MTS_READER_PASSWORD = "mt4fun";

        [TestCase("Gigasax", "DMS5")]
        [Category("DatabaseIntegrated")]
        public void TestSearchLogsIntegrated(string server, string database)
        {
            TestSearchLogs(server, database, "Integrated", string.Empty);
        }

        [TestCase("Gigasax", "DMS5")]
        [Category("DatabaseNamedUser")]
        public void TestSearchLogsNamedUser(string server, string database)
        {
            TestSearchLogs(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
        }

        private void TestSearchLogs(string server, string database, string user, string password)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, user, password);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "FindLogEntry"
            };

            spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;

            var entryTypeParam = spCmd.Parameters.Add(new SqlParameter("@EntryType", SqlDbType.VarChar, 32));
            entryTypeParam.Value = "Normal";

            var messageTextParam = spCmd.Parameters.Add(new SqlParameter("@MessageText", SqlDbType.VarChar, 500));
            messageTextParam.Value = "complete";

            Console.WriteLine("Running stored procedure " + spCmd.CommandText + " against " + database + " as user " + user);

            var returnCode = dbTools.ExecuteSPData(spCmd, out var results);

            Assert.AreEqual(0, returnCode, spCmd.CommandText + " Procedure did not return 0");

            var rowsDisplayed = 0;
            foreach (var result in results)
            {
                Assert.GreaterOrEqual(result.Count, 5, "Result row has fewer than 5 columns");

                for (var colIndex = 0; colIndex < result.Count; colIndex++)
                {
                    Console.Write(result[colIndex] + "  ");

                    if (colIndex == 5)
                    {
                        var completeFound = result[colIndex].IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0;

                        Assert.True(completeFound, "Result row does not have complete in the Message column");
                        break;
                    }
                }
                Console.WriteLine();
                rowsDisplayed++;

                if (rowsDisplayed > 10)
                    break;
            }

            Console.WriteLine("Rows returned: " + results.Count);
        }

        [TestCase("Pogo", "MTS_Master")]
        [Category("DatabaseIntegrated")]
        public void TestGetAllPeptideDatabasesIntegrated(string server, string database)
        {
            TestGetAllPeptideDatabases(server, database, "Integrated", string.Empty);
        }

        [TestCase("Pogo", "MTS_Master")]
        [Category("DatabaseNamedUser")]
        public void TestGetAllPeptideDatabasesNamedUser(string server, string database)
        {
            TestGetAllPeptideDatabases(server, database, MTS_READER, MTS_READER_PASSWORD);
        }

        private void TestGetAllPeptideDatabases(string server, string database, string user, string password)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, user, password);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "GetAllPeptideDatabases"
            };

            spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;

            // Test adding parameters using dbTools
            dbTools.AddParameter(spCmd, "@IncludeUnused", SqlType.Int).Value = 0;
            dbTools.AddTypedParameter(spCmd, "@IncludeDeleted", SqlType.Int, 0, 0);

            Console.WriteLine("Running stored procedure " + spCmd.CommandText + " against " + database + " as user " + user);

            var returnCode = dbTools.ExecuteSPData(spCmd, out var results);

            Assert.AreEqual(0, returnCode, spCmd.CommandText + " Procedure did not return 0");

            var rowsDisplayed = 0;
            foreach (var result in results)
            {
                Assert.GreaterOrEqual(result.Count, 9, "Result row has fewer than 9 columns");

                var dbName = result[0];
                var showData = dbName.IndexOf("human", StringComparison.OrdinalIgnoreCase) >= 0;

                var organism = "??";

                for (var colIndex = 0; colIndex < result.Count; colIndex++)
                {
                    if (showData && colIndex < 4)
                        Console.Write(result[colIndex] + "  ");

                    if (colIndex == 2)
                    {
                        organism = result[colIndex];
                    }
                }

                if (dbName.StartsWith("PT_Human", StringComparison.OrdinalIgnoreCase))
                {
                    var humanFound = organism.IndexOf("homo_sapiens", StringComparison.OrdinalIgnoreCase) >= 0;

                    Assert.True(humanFound, "Human PT database does not have organism Homo_Sapiens");
                }

                if (showData)
                {
                    Console.WriteLine();
                    rowsDisplayed++;

                    if (rowsDisplayed > 10)
                        break;
                }
            }

            Console.WriteLine("Rows returned: " + results.Count);
        }

        /// <summary>
        /// Retrieve values from PostgreSQL function mc.GetManagerParameters()
        /// </summary>
        [TestCase("prismdb1", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestGetManagerParametersPostgresFunction(string server, string database)
        {
            var user = TestDBTools.DMS_READER;

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user, TestDBTools.DMS_READER_PASSWORD);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new NpgsqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = "SELECT * FROM mc.GetManagerParameters('Pub-12-1, Pub-12-2', 0, 50)"
            };

            Console.WriteLine("Querying function mc.GetManagerParameters in " + database + " as user " + user);

            var success = dbTools.GetQueryResults(spCmd, out var results, 1);

            Assert.IsTrue(success, "GetQueryResults return false");

            ExamineManagerParams(results);
        }

        /// <summary>
        /// Retrieve values from PostgreSQL function mc.GetManagerParameters()
        /// </summary>
        [TestCase("prismdb1", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestGetManagerParametersPostgresFunctionWithParameters(string server, string database)
        {
            var user = TestDBTools.DMS_READER;

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user, TestDBTools.DMS_READER_PASSWORD);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new NpgsqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = "SELECT * FROM mc.GetManagerParameters(@managerNameList, @sortMode, @maxRecursion)"
            };

            dbTools.AddParameter(spCmd, "managerNameList", SqlType.Text).Value = "Pub-12-1, Pub-12-2";
            dbTools.AddParameter(spCmd, "sortMode", SqlType.Int).Value = 0;
            dbTools.AddParameter(spCmd, "maxRecursion", SqlType.Int).Value = 50;

            Console.WriteLine("Querying function mc.GetManagerParameters in " + database + " as user " + user);

            var success = dbTools.GetQueryResults(spCmd, out var results, 1);

            Assert.IsTrue(success, "GetQueryResults return false");

            ExamineManagerParams(results);
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestGetManagerParametersIntegrated(string server, string database)
        {
            TestGetManagerParametersSP(server, database, "Integrated", string.Empty);
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseNamedUser")]
        public void TestGetManagerParametersNamedUser(string server, string database)
        {
            TestGetManagerParametersSP(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
        }

        /// <summary>
        /// Retrieve values from SQL Server stored procedure GetManagerParameters
        /// </summary>
        /// <param name="server"></param>
        /// <param name="database"></param>
        /// <param name="user"></param>
        /// <param name="password"></param>
        private void TestGetManagerParametersSP(string server, string database, string user, string password)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, user, password);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "GetManagerParameters"
            };

            spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;
            spCmd.Parameters.Add(new SqlParameter("@ManagerNameList", SqlDbType.VarChar, 4000)).Value = "Pub-12-1, Pub-12-2";
            spCmd.Parameters.Add(new SqlParameter("@SortMode", SqlDbType.TinyInt)).Value = 0;
            spCmd.Parameters.Add(new SqlParameter("@MaxRecursion", SqlDbType.TinyInt)).Value = 50;

            Console.WriteLine("Running stored procedure " + spCmd.CommandText + " against " + database + " as user " + user);

            var returnCode = dbTools.ExecuteSPData(spCmd, out var results, 0);

            Assert.AreEqual(0, returnCode, spCmd.CommandText + " Procedure did not return 0");

            ExamineManagerParams(results);
        }

        private void ExamineManagerParams(IReadOnlyCollection<List<string>> results)
        {
            var rowsDisplayed = 0;
            foreach (var result in results)
            {
                Assert.GreaterOrEqual(result.Count, 12, "Result row has fewer than 12 columns");

                var managerName = result[0];
                var paramName = result[1];
                var workDirParam = paramName.Equals("workdir", StringComparison.OrdinalIgnoreCase);

                var workDir = "??";

                for (var colIndex = 0; colIndex < result.Count; colIndex++)
                {
                    if (workDirParam && colIndex < 7)
                        Console.Write(result[colIndex] + "  ");

                    if (colIndex == 4)
                    {
                        workDir = result[colIndex];
                    }
                }

                if (!workDirParam)
                    continue;

                if (managerName.Equals("Pub-12-1", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.AreEqual(@"C:\DMS_WorkDir1", workDir);
                }
                else if (managerName.Equals("Pub-12-2", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.AreEqual(@"C:\DMS_WorkDir2", workDir);
                }

                Console.WriteLine();
                rowsDisplayed++;

                if (rowsDisplayed > 10)
                    break;
            }

            Console.WriteLine("Rows returned: " + results.Count);
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestEnableDisableManagersSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestEnableDisableManagers(connectionString, "EnableDisableManagers");
        }

        [TestCase("prismdb1", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestEnableDisableManagersPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestEnableDisableManagers(connectionString, "mc.EnableDisableManagers");
        }

        /// <summary>
        /// Invoke stored procedure EnableDisableManagers and examine output parameter @message (or _message)
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="procedureNameWithSchema"></param>
        private void TestEnableDisableManagers(string connectionString, string procedureNameWithSchema)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand(procedureNameWithSchema, CommandType.StoredProcedure);

            dbTools.AddParameter(spCmd, "@Enable", SqlType.Int).Value = 1;
            dbTools.AddParameter(spCmd, "@ManagerTypeID", SqlType.Int).Value = 11;
            dbTools.AddParameter(spCmd, "@managerNameList", SqlType.VarChar, 4000).Value = "Pub-12-1, Pub-12-2";
            dbTools.AddParameter(spCmd, "@infoOnly", SqlType.Int).Value = 1;

            DbParameter messageParam;

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_includeDisabled", SqlType.Int).Value = 0;
                messageParam = dbTools.AddParameter(spCmd, "_message", SqlType.Text, ParameterDirection.InputOutput);
            }
            else
            {
                messageParam = dbTools.AddParameter(spCmd, "@message", SqlType.VarChar, 4000, ParameterDirection.InputOutput);
            }

            // On Postgres, the call to ExecuteSP will auto-change this parameter to _returnCode of type InputOutput
            var returnParam = dbTools.AddParameter(spCmd, "@Return", SqlType.Int, direction: ParameterDirection.ReturnValue);

            Console.WriteLine("Running stored procedure " + procedureNameWithSchema + " using dbTools of type " + dbTools.DbServerType);

            var returnCode = dbTools.ExecuteSP(spCmd, out var errorMessage, 1);

            Console.WriteLine();
            Console.WriteLine("Message: " + messageParam.Value);
            Console.WriteLine("Return:  " + returnParam.Value);
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                Console.WriteLine("Error:   " + errorMessage);
            }

            Assert.AreEqual(0, returnCode, procedureNameWithSchema + " Procedure did not return 0");
            Assert.AreEqual(0, returnParam.Value, procedureNameWithSchema + " @Return (or _returnCode) is not 0");
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestEnableDisableManagersDataSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestEnableDisableManagersData(connectionString, "EnableDisableManagers");
        }

        [TestCase("prismdb1", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestEnableDisableManagersDataPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestEnableDisableManagersData(connectionString, "mc.EnableDisableManagers");
        }

        /// <summary>
        /// Invoke stored procedure EnableDisableManagers and examine output parameter @message (or _message)
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="procedureNameWithSchema"></param>
        private void TestEnableDisableManagersData(string connectionString, string procedureNameWithSchema)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand(procedureNameWithSchema, CommandType.StoredProcedure);

            dbTools.AddParameter(spCmd, "@Enable", SqlType.Int).Value = 1;
            dbTools.AddParameter(spCmd, "@ManagerTypeID", SqlType.Int).Value = 11;
            dbTools.AddParameter(spCmd, "@managerNameList", SqlType.VarChar, 4000).Value = "Pub-12-1, Pub-12-2";
            dbTools.AddParameter(spCmd, "@infoOnly", SqlType.Int).Value = 1;

            DbParameter messageParam;

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_includeDisabled", SqlType.Int).Value = 0;
                messageParam = dbTools.AddParameter(spCmd, "_message", SqlType.Text, ParameterDirection.InputOutput);
            }
            else
            {
                messageParam = dbTools.AddParameter(spCmd, "@message", SqlType.VarChar, 4000, ParameterDirection.InputOutput);
            }

            // On Postgres, the call to ExecuteSPData will auto-change this parameter to _returnCode of type InputOutput
            var returnParam = dbTools.AddParameter(spCmd, "@Return", SqlType.Int, direction: ParameterDirection.ReturnValue);

            Console.WriteLine("Running stored procedure " + procedureNameWithSchema + " using dbTools of type " + dbTools.DbServerType);

            var returnCode = dbTools.ExecuteSPData(spCmd, out var results, 1);

            Console.WriteLine();
            Console.WriteLine("Message: " + messageParam.Value);
            Console.WriteLine("Return:  " + returnParam.Value);
            foreach (var row in results)
            {
                Console.WriteLine(string.Join(", ", row));
            }

            Assert.AreEqual(0, returnCode, procedureNameWithSchema + " Procedure did not return 0");
            Assert.AreEqual(0, returnParam.Value, procedureNameWithSchema + " @Return (or _returnCode) is not 0");
        }

        [TestCase("Gigasax", "dms5", "FindLogEntry", false, 0)]
        public void TestGetReturnCodeSqlServer(string server, string database, string procedureName, bool skipProcedureCall, int expectedReturnCode)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestGetReturnCode(connectionString, procedureName, skipProcedureCall, expectedReturnCode);
        }

        [TestCase("prismweb3", "dmsdev", "FindLogEntry", true, 0, "")]
        [TestCase("prismweb3", "dmsdev", "FindLogEntry", true, 2200, "2200L")]
        [TestCase("prismweb3", "dmsdev", "FindLogEntry", true, 2, "2F005")]
        public void TestGetReturnCodePostgres(
            string server,
            string database,
            string procedureName,
            bool skipProcedureCall,
            int expectedReturnCode,
            string returnCodeOverride
            )
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, DMS_WEB_USER);
            TestGetReturnCode(connectionString, procedureName, skipProcedureCall, expectedReturnCode, returnCodeOverride);
        }

        private void TestGetReturnCode(
            string connectionString,
            string procedureName,
            bool skipProcedureCall,
            int expectedReturnCode,
            string returnCodeOverride = "")
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand(procedureName, CommandType.StoredProcedure);

            // On Postgres, the call to ExecuteSP will auto-change this parameter to _returnCode of type InputOutput
            var returnParam = dbTools.AddParameter(spCmd, "@Return", SqlType.Int, direction: ParameterDirection.ReturnValue);

            if (!skipProcedureCall)
            {
                dbTools.ExecuteSPData(spCmd, out var results);
                var rowsDisplayed = 0;
                foreach (var result in results)
                {
                    for (var colIndex = 0; colIndex < result.Count; colIndex++)
                    {
                        string valueToShow;
                        if (result[colIndex].Length > 20)
                            valueToShow = result[colIndex].Substring(0, 20) + " ...";
                        else
                            valueToShow = result[colIndex];

                        Console.Write(valueToShow + "  ");
                        if (colIndex > 3)
                            break;
                    }

                    Console.WriteLine();
                    rowsDisplayed++;

                    if (rowsDisplayed > 10)
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(returnCodeOverride))
            {
                // Auto-change returnParam to be named _returnCode of type text
                returnParam.ParameterName = "_returnCode";
                returnParam.DbType = DbType.String;
                returnParam.Direction = ParameterDirection.InputOutput;
                returnParam.Value = returnCodeOverride;
            }

            var returnCodeValue = DBToolsBase.GetReturnCode(returnParam);

            if (string.IsNullOrWhiteSpace(returnCodeOverride))
                Console.WriteLine("Return value: {0}", returnCodeValue);
            else
                Console.WriteLine("_returnCode {0} evaluates to return value {1}", returnCodeOverride, returnCodeValue);

            Assert.AreEqual(expectedReturnCode, returnCodeValue, "Return code mismatch");
        }

        [TestCase("prismweb3", "dmsdev")]
        [Category("DatabaseNamedUser")]
        public void TestPostLogEntryAsProcedure(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, DMS_WEB_USER);

            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand("PostLogEntry", CommandType.StoredProcedure);

            dbTools.AddParameter(spCmd, "@type", SqlType.Text).Value = "Info";
            dbTools.AddParameter(spCmd, "@message", SqlType.Text).Value = "Test message 1";
            dbTools.AddParameter(spCmd, "@postedBy", SqlType.Text).Value = "Test caller";

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "@targetSchema", SqlType.Text).Value = "public";
            }

            dbTools.ExecuteSP(spCmd, 1);

            Console.WriteLine("Complete: " + spCmd.CommandText);
            Console.WriteLine();
            Console.WriteLine("Selecting recent rows from t_log_entries");
            Console.WriteLine();

            var spSelectCmd = dbTools.CreateCommand("Select * from t_log_entries where posting_time >= current_timestamp - Interval '10 seconds'");

            var success = dbTools.GetQueryResults(spSelectCmd, out var queryResults, 1);

            Assert.IsTrue(success, "GetQueryResults returned false while querying t_log_entries");

            TestDBTools.ShowRowsFromTLogEntries(queryResults);
        }

        [TestCase("prismweb3", "dmsdev", TestDBTools.DMS_READER, false)]
        [TestCase("prismweb3", "dmsdev", "NonExistentUser", false)]
        [TestCase("prismweb3", "dmsdev", DMS_WEB_USER, true)]
        [Category("DatabaseNamedUser")]
        public void TestPostLogEntryAsQuery(string server, string database, string user, bool expectedPostSuccess)
        {
            string connectionString;
            if (user.Equals(TestDBTools.DMS_READER))
            {
                connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user, TestDBTools.DMS_READER_PASSWORD);
            }
            else
            {
                connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user);
            }

            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var query = "call PostLogEntry(_postedBy => 'Test caller', _type =>'Info', _message => 'Test message 2')";
            var spCmd = dbTools.CreateCommand(query);

            var postSuccess = dbTools.GetQueryScalar(spCmd, out _, 1);

            VerifyTestPostLogEntry(dbTools, user, expectedPostSuccess, postSuccess);
        }

        [TestCase("prismweb3", "dmsdev", DMS_WEB_USER, true)]
        [Category("DatabaseNamedUser")]
        public void TestPostLogEntryAsQueryWithParameters(string server, string database, string user, bool expectedPostSuccess)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user);

            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var query = "call PostLogEntry(_postedBy => @_postedBy, _type => @_type, _message => @_message)";
            var spCmd = dbTools.CreateCommand(query);

            dbTools.AddParameter(spCmd, "_type", SqlType.Text).Value = "Info";
            var messageParam = dbTools.AddParameter(spCmd, "_message", SqlType.Text);
            dbTools.AddParameter(spCmd, "_postedBy", SqlType.Text).Value = "Test caller";

            messageParam.Value = "Test message 3";
            var postSuccess = dbTools.GetQueryScalar(spCmd, out _, 1);

            VerifyTestPostLogEntry(dbTools, user, expectedPostSuccess, postSuccess);
        }

        private void VerifyTestPostLogEntry(IDBTools dbTools, string user, bool expectedPostSuccess, bool actualPostSuccess)
        {
            if (expectedPostSuccess)
                Assert.IsTrue(actualPostSuccess, "Call to PostLogEntry failed for user {0}; it should have succeeded", user);
            else
                Assert.IsFalse(actualPostSuccess, "Call to PostLogEntry succeeded for user {0}; it should have failed", user);

            if (!actualPostSuccess)
                return;

            Console.WriteLine();
            Console.WriteLine("Selecting recent rows from t_log_entries");
            Console.WriteLine();

            var spSelectCmd = dbTools.CreateCommand("Select * from t_log_entries where posting_time >= current_timestamp - Interval '10 seconds'");

            var querySuccess = dbTools.GetQueryResults(spSelectCmd, out var queryResults, 1);

            Assert.IsTrue(querySuccess, "GetQueryResults returned false while querying t_log_entries");

            TestDBTools.ShowRowsFromTLogEntries(queryResults);
        }
    }
}
