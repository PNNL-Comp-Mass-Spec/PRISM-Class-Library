using System;
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
    class StoredProcedureTests
    {
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
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

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

            var returnCode = dbTools.ExecuteSPData(spCmd, out var lstResults);

            Assert.AreEqual(0, returnCode, spCmd.CommandText + " Procedure did not return 0");

            var rowsDisplayed = 0;
            foreach (var result in lstResults)
            {
                Assert.GreaterOrEqual(result.Count, 5, "Result row has fewer than 5 columns");

                for (var colIndex = 0; colIndex < result.Count; colIndex++)
                {
                    Console.Write(result[colIndex] + "  ");

                    if (colIndex == 5)
                    {
                        var completeFound = result[colIndex].ToLower().Contains("complete");

                        Assert.True(completeFound, "Result row does not have complete in the Message column");
                        break;
                    }
                }
                Console.WriteLine();
                rowsDisplayed++;

                if (rowsDisplayed > 10)
                    break;
            }

            Console.WriteLine("Rows returned: " + lstResults.Count);
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
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            var spCmd = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "GetAllPeptideDatabases"
            };

            spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;
            spCmd.Parameters.Add(new SqlParameter("@IncludeUnused", SqlDbType.Int)).Value = 0;
            spCmd.Parameters.Add(new SqlParameter("@IncludeDeleted", SqlDbType.Int)).Value = 0;

            Console.WriteLine("Running stored procedure " + spCmd.CommandText + " against " + database + " as user " + user);

            var returnCode = dbTools.ExecuteSPData(spCmd, out var lstResults);

            Assert.AreEqual(0, returnCode, spCmd.CommandText + " Procedure did not return 0");

            var rowsDisplayed = 0;
            foreach (var result in lstResults)
            {
                Assert.GreaterOrEqual(result.Count, 9, "Result row has fewer than 9 columns");

                var dbName = result[0];
                var showData = dbName.ToLower().Contains("human");

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
                    var humanFound = organism.ToLower().Contains("homo_sapiens");

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

            Console.WriteLine("Rows returned: " + lstResults.Count);
        }

        /// <summary>
        /// Retrieve values from PostgreSQL function mc.GetManagerParameters()
        /// </summary>
        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestGetManagerParametersPostgresFunction(string server, string database)
        {
            var user = TestDBTools.DMS_READER;

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user);
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            var spCmd = new NpgsqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = "SELECT * FROM mc.GetManagerParameters('Pub-12-1, Pub-12-2', 0, 50)"
            };

            Console.WriteLine("Querying function mc.GetManagerParameters in " + database + " as user " + user);

            var success = dbTools.GetQueryResults(spCmd, out var lstResults, 1);

            Assert.IsTrue(success, "GetQueryResults return false");

            ExamineManagerParams(lstResults);
        }

        /// <summary>
        /// Retrieve values from PostgreSQL function mc.GetManagerParameters()
        /// </summary>
        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestGetManagerParametersPostgresFunctionWithParameters(string server, string database)
        {
            var user = TestDBTools.DMS_READER;

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user);
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            var spCmd = new NpgsqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = "SELECT * FROM mc.GetManagerParameters(@managerNameList, @sortMode, @maxRecursion)"
            };

            dbTools.AddParameter(spCmd, "managerNameList", SqlType.Text).Value = "Pub-12-1, Pub-12-2";
            dbTools.AddParameter(spCmd, "sortMode", SqlType.Int).Value = 0;
            dbTools.AddParameter(spCmd, "maxRecursion", SqlType.Int).Value = 50;

            Console.WriteLine("Querying function mc.GetManagerParameters in " + database + " as user " + user);

            var success = dbTools.GetQueryResults(spCmd, out var lstResults, 1);

            Assert.IsTrue(success, "GetQueryResults return false");

            ExamineManagerParams(lstResults);
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
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

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

            var returnCode = dbTools.ExecuteSPData(spCmd, out var lstResults, 0);

            Assert.AreEqual(0, returnCode, spCmd.CommandText + " Procedure did not return 0");

            ExamineManagerParams(lstResults);
        }

        private void ExamineManagerParams(IReadOnlyCollection<List<string>> lstResults)
        {

            var rowsDisplayed = 0;
            foreach (var result in lstResults)
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

            Console.WriteLine("Rows returned: " + lstResults.Count);
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestEnableDisableManagersSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestEnableDisableManagers(connectionString, "EnableDisableManagers");
        }

        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestEnableDisableManagersPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);
            TestEnableDisableManagers(connectionString, "mc.EnableDisableManagers");
        }


        // ToDo: Make a PostgreSQL version of EnableDisableManagers that returns a cursor instead of using _infoHead and _infoData


        /// <summary>
        /// Invoke stored procedure EnableDisableManagers and examine output parameter @message (or _message)
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="procedureNameWithSchema"></param>
        private void TestEnableDisableManagers(string connectionString, string procedureNameWithSchema)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString);

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

            // The call to ExecuteSP will auto-change this parameter to _returnCode of type InputOutput
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


        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestPostLogEntryAsProcedure(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);

            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            var spCmd = dbTools.CreateCommand("PostLogEntry", CommandType.StoredProcedure);

            dbTools.AddParameter(spCmd, "@type", SqlType.Text).Value = "Info";
            dbTools.AddParameter(spCmd, "@message", SqlType.Text).Value = "Test message 1";
            dbTools.AddParameter(spCmd, "@postedBy", SqlType.Text).Value = "Test caller";

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "@targetSchema", SqlType.Text).Value = "public";
            }

            dbTools.ExecuteSP(spCmd, 1);
        }

        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestPostLogEntryAsQuery(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);

            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            var query = "call PostLogEntry(_postedBy => 'Test caller', _type =>'Info', _message => 'Test message 2')";
            var spCmd = dbTools.CreateCommand(query);

            dbTools.GetQueryScalar(spCmd, out _, 1);
        }

        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestPostLogEntryAsQueryWithParameters(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);

            var dbTools = DbToolsFactory.GetDBTools(connectionString);

            var query = "call PostLogEntry(_postedBy => @_postedBy, _type => @_type, _message => @_message)";
            var spCmd = dbTools.CreateCommand(query);

            dbTools.AddParameter(spCmd, "_type", SqlType.Text).Value = "Info";
            var messageParam = dbTools.AddParameter(spCmd, "_message", SqlType.Text);
            dbTools.AddParameter(spCmd, "_postedBy", SqlType.Text).Value = "Test caller";

            messageParam.Value = "Test message 3";
            dbTools.GetQueryScalar(spCmd, out _, 1);
        }

    }
}
