using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using NUnit.Framework;
using PRISMDatabaseUtils;

namespace PRISMTest
{
    [TestFixture]
    public class StoredProcedureTests
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: dms, dmsdev, dmswebuser, mtuser, na, Postgres, PostgreSQL, ProteinSeqs, SQL, workdir

        // ReSharper restore CommentTypo

        private const string DMS_WEB_USER = "dmswebuser";

        private const string MTS_READER = "mtuser";
        private const string MTS_READER_PASSWORD = "mt4fun";

        /// <summary>
        /// Use stored procedure Find_Log_Entry to look for log entries
        /// </summary>
        /// <param name="server">Server</param>
        /// <param name="database">Database</param>
        [TestCase("Gigasax", "DMS5")]
        [Category("DatabaseIntegrated")]
        public void TestSearchLogsIntegrated(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestSearchLogs(connectionString, database, "Integrated");
        }

        /// <summary>
        /// Use stored procedure Find_Log_Entry to look for log entries
        /// </summary>
        /// <param name="server">Server</param>
        /// <param name="database">Database</param>
        [TestCase("Gigasax", "DMS5")]
        [Category("DatabaseNamedUser")]
        public void TestSearchLogsNamedUser(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestSearchLogs(connectionString, database, TestDBTools.DMS_READER);
        }

        /// <summary>
        /// Use procedure Find_Log_Entry to look for log entries
        /// </summary>
        /// <remarks>
        /// The procedure returns the results using a refcursor, which ExecuteSPData auto-converts into a result set
        /// </remarks>
        /// <param name="server">Server</param>
        /// <param name="database">Database</param>
        [TestCase("prismdb2.emsl.pnl.gov", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestSearchLogsPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, DMS_WEB_USER);
            TestSearchLogs(connectionString, database, DMS_WEB_USER);
        }

        private void TestSearchLogs(string connectionString, string database, string user)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand("Find_Log_Entry", CommandType.StoredProcedure);

            // On Postgres, the call to ExecuteSP will auto-change this parameter to _returnCode of type InputOutput
            var returnParam = dbTools.AddParameter(spCmd, "@Return", SqlType.Int, direction: ParameterDirection.ReturnValue);

            var entryTypeParam = dbTools.AddParameter(spCmd, "@entryType", SqlType.VarChar, 32);
            entryTypeParam.Value = "Normal";

            var messageTextParam = dbTools.AddParameter(spCmd, "@messageText", SqlType.VarChar, 500);
            messageTextParam.Value = "complete";

            dbTools.AddParameter(spCmd, "@maxRowCount", SqlType.Int).Value = 15;

            var messageParam = dbTools.AddParameter(spCmd, "@message", SqlType.VarChar, 255, ParameterDirection.InputOutput);

            Console.WriteLine("Running stored procedure " + spCmd.CommandText + " against " + database + " as user " + user);

            // On Postgres, procedure Find_Log_Entry returns the results using a refcursor, which ExecuteSPData auto-converts into a result set
            var resCode = dbTools.ExecuteSPData(spCmd, out var results, 1);

            if (resCode != 0)
            {
                Console.WriteLine("Procedure " + spCmd.CommandText + " returned a non-zero value: " + resCode);
            }

            if (!string.IsNullOrWhiteSpace(messageParam.Value.CastDBVal<string>()))
            {
                Console.WriteLine("Procedure " + spCmd.CommandText + " returned message: " + messageParam.Value.CastDBVal<string>());
            }

            var returnCodeValue = DBToolsBase.GetReturnCode(returnParam);

            Assert.That(returnCodeValue, Is.EqualTo(0), spCmd.CommandText + " Procedure did not return 0");

            var rowsDisplayed = 0;

            foreach (var result in results)
            {
                Assert.That(result, Has.Count.GreaterThanOrEqualTo(5), "Result row has fewer than 5 columns");

                for (var colIndex = 0; colIndex < result.Count; colIndex++)
                {
                    Console.Write(result[colIndex] + "  ");

                    if (colIndex == 5)
                    {
                        var completeFound = result[colIndex].IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0;

                        Assert.That(completeFound, Is.True, "Result row does not have complete in the Message column");
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

            Assert.That(returnCode, Is.EqualTo(0), spCmd.CommandText + " Procedure did not return 0");

            var rowsDisplayed = 0;

            foreach (var result in results)
            {
                Assert.That(result, Has.Count.GreaterThanOrEqualTo(9), "Result row has fewer than 9 columns");

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

                    Assert.That(humanFound, Is.True, "Human PT database does not have organism Homo_Sapiens");
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
        /// Retrieve values from PostgreSQL function mc.get_manager_parameters()
        /// </summary>
        [TestCase("prismdb2.emsl.pnl.gov", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestGetManagerParametersPostgresFunction(string server, string database)
        {
            var user = TestDBTools.DMS_READER;

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user, TestDBTools.DMS_READER_PASSWORD);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new NpgsqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = "SELECT * FROM mc.get_manager_parameters('Pub-12-1, Pub-12-2', 0, 50)"
            };

            Console.WriteLine("Querying function mc.get_manager_parameters in " + database + " as user " + user);

            var success = dbTools.GetQueryResults(spCmd, out var results, 1);

            Assert.That(success, Is.True, "GetQueryResults return false");

            ExamineManagerParams(results);
        }

        /// <summary>
        /// Retrieve values from PostgreSQL function mc.get_manager_parameters()
        /// </summary>
        [TestCase("prismdb2.emsl.pnl.gov", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestGetManagerParametersPostgresFunctionWithParameters(string server, string database)
        {
            var user = TestDBTools.DMS_READER;

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, user, TestDBTools.DMS_READER_PASSWORD);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new NpgsqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = "SELECT * FROM mc.get_manager_parameters(@managerNameList, @sortMode, @maxRecursion)"
            };

            dbTools.AddParameter(spCmd, "managerNameList", SqlType.Text).Value = "Pub-12-1, Pub-12-2";
            dbTools.AddParameter(spCmd, "sortMode", SqlType.Int).Value = 0;
            dbTools.AddParameter(spCmd, "maxRecursion", SqlType.Int).Value = 50;

            Console.WriteLine("Querying function mc.get_manager_parameters in " + database + " as user " + user);

            var success = dbTools.GetQueryResults(spCmd, out var results, 1);

            Assert.That(success, Is.True, "GetQueryResults return false");

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
        /// Retrieve values from SQL Server stored procedure Get_Manager_Parameters
        /// </summary>
        /// <param name="server">Server</param>
        /// <param name="database">Database</param>
        /// <param name="user">User</param>
        /// <param name="password">Password</param>
        private void TestGetManagerParametersSP(string server, string database, string user, string password)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, user, password);
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "Get_Manager_Parameters"
            };

            spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;
            spCmd.Parameters.Add(new SqlParameter("@managerNameList", SqlDbType.VarChar, 4000)).Value = "Pub-12-1, Pub-12-2";
            spCmd.Parameters.Add(new SqlParameter("@sortMode", SqlDbType.TinyInt)).Value = 0;
            spCmd.Parameters.Add(new SqlParameter("@maxRecursion", SqlDbType.TinyInt)).Value = 50;

            Console.WriteLine("Running stored procedure " + spCmd.CommandText + " against " + database + " as user " + user);

            var returnCode = dbTools.ExecuteSPData(spCmd, out var results, 0);

            Assert.That(returnCode, Is.EqualTo(0), spCmd.CommandText + " Procedure did not return 0");

            ExamineManagerParams(results);
        }

        private void ExamineManagerParams(IReadOnlyCollection<List<string>> results)
        {
            var rowsDisplayed = 0;

            foreach (var result in results)
            {
                Assert.That(result, Has.Count.GreaterThanOrEqualTo(12), "Result row has fewer than 12 columns");

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
                    Assert.That(workDir, Is.EqualTo(@"C:\DMS_WorkDir1"));
                }
                else if (managerName.Equals("Pub-12-2", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.That(workDir, Is.EqualTo(@"C:\DMS_WorkDir2"));
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
            TestEnableDisableManagers(connectionString, "enable_disable_managers");
        }

        [TestCase("prismdb2.emsl.pnl.gov", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestEnableDisableManagersPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestEnableDisableManagers(connectionString, "mc.enable_disable_managers");
        }

        /// <summary>
        /// Invoke stored procedure EnableDisableManagers (or enable_disable_managers) and examine output parameter @message (or _message)
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="procedureNameWithSchema">Procedure name</param>
        private void TestEnableDisableManagers(string connectionString, string procedureNameWithSchema)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand(procedureNameWithSchema, CommandType.StoredProcedure);

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_enable", SqlType.Boolean).Value = true;
            }
            else
            {
                dbTools.AddParameter(spCmd, "@Enable", SqlType.Int).Value = 1;
            }

            dbTools.AddParameter(spCmd, "@ManagerTypeID", SqlType.Int).Value = 11;
            dbTools.AddParameter(spCmd, "@managerNameList", SqlType.VarChar, 4000).Value = "Pub-12-1, Pub-12-2";

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_infoOnly", SqlType.Boolean).Value = true;
            }
            else
            {
                dbTools.AddParameter(spCmd, "@infoOnly", SqlType.Int).Value = 1;
            }

            DbParameter messageParam;

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_includeDisabled", SqlType.Boolean).Value = false;
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

            Assert.Multiple(() =>
            {
                Assert.That(returnCode, Is.EqualTo(0), procedureNameWithSchema + " Procedure did not return 0");
                Assert.That(returnParam.Value, Is.EqualTo(0), procedureNameWithSchema + " @Return (or _returnCode) is not 0");
            });
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestEnableDisableManagersDataSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestEnableDisableManagersData(connectionString, "enable_disable_managers");
        }

        [TestCase("prismdb2.emsl.pnl.gov", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestEnableDisableManagersDataPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestEnableDisableManagersData(connectionString, "mc.enable_disable_managers");
        }

        /// <summary>
        /// Invoke stored procedure enable_disable_managers and examine output parameter @message (or _message)
        /// </summary>
        /// <remarks>
        /// On PostgreSQL, procedure mc.enable_disable_managers returns query results using a reference cursor (_results refcursor)
        /// ExecuteSPData looks for parameters of type refcursor and retrieves the results
        /// </remarks>
        /// <param name="connectionString">Connection string</param>
        /// <param name="procedureNameWithSchema">Procedure name</param>
        private void TestEnableDisableManagersData(string connectionString, string procedureNameWithSchema)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand(procedureNameWithSchema, CommandType.StoredProcedure);

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_enable", SqlType.Boolean).Value = true;
            }
            else
            {
                dbTools.AddParameter(spCmd, "@Enable", SqlType.Int).Value = 1;
            }

            dbTools.AddParameter(spCmd, "@ManagerTypeID", SqlType.Int).Value = 11;
            dbTools.AddParameter(spCmd, "@managerNameList", SqlType.VarChar, 4000).Value = "Pub-12-1, Pub-12-2";

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_infoOnly", SqlType.Boolean).Value = true;
            }
            else
            {
                dbTools.AddParameter(spCmd, "@infoOnly", SqlType.Int).Value = 1;
            }

            DbParameter messageParam;

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_includeDisabled", SqlType.Boolean).Value = false;
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

            Assert.Multiple(() =>
            {
                Assert.That(returnCode, Is.EqualTo(0), procedureNameWithSchema + " Procedure did not return 0");
                Assert.That(returnParam.Value, Is.EqualTo(0), procedureNameWithSchema + " @Return (or _returnCode) is not 0");
            });
        }

        [TestCase("Gigasax", "dms5", "find_log_entry", false, 0)]
        public void TestGetReturnCodeSqlServer(string server, string database, string procedureName, bool skipProcedureCall, int expectedReturnCode)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestGetReturnCode(connectionString, procedureName, skipProcedureCall, expectedReturnCode);
        }

        [TestCase("prismdb2.emsl.pnl.gov", "dms", "find_log_entry", true, 0, "")]
        [TestCase("prismdb2.emsl.pnl.gov", "dms", "find_log_entry", true, 2200, "2200L")]
        [TestCase("prismdb2.emsl.pnl.gov", "dms", "find_log_entry", true, 2, "2F005")]
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

        /// <summary>
        /// Call the specified stored procedure and examine the return code
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="procedureName">Procedure name</param>
        /// <param name="skipProcedureCall">If true, skip the procedure call</param>
        /// <param name="expectedReturnCode">Expected return code</param>
        /// <param name="returnCodeOverride">If not an empty string, auto-change returnParam to be named _returnCode and to have this argument's text</param>
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

            Assert.That(returnCodeValue, Is.EqualTo(expectedReturnCode), "Return code mismatch");
        }

        [TestCase("Gigasax", "dms5", 0)]
        [Category("DatabaseIntegrated")]
        public void TestGetNamedReturnCodeSqlServer(string server, string database, int expectedReturnCode)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database);
            TestGetNamedReturnCode(connectionString, expectedReturnCode);
        }

        // Note that this test will only work if the computer has a .pgpass file defining the password for user dmswebuser
        // On Windows, the file is at C:\users\username\AppData\Roaming\postgresql\pgpass.conf

        // On Proto-2, the Jenkins service runs under the NETWORK SERVICE account
        // The required location for the PgPass file is: C:\Windows\ServiceProfiles\NetworkService\AppData\Roaming\postgresql\pgpass.conf

        [TestCase("prismdb2.emsl.pnl.gov", "dms", 0)]
        [Category("DatabaseNamedUser")]
        public void TestGetNamedReturnCodePostgres(string server, string database, int expectedReturnCode)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, "dmsreader", "dms4fun");
            TestGetNamedReturnCode(connectionString, expectedReturnCode);
        }

        private void TestGetNamedReturnCode(string connectionString, int expectedReturnCode)
        {
            const string SP_NAME_GET_PARAM_FILE_MOD_INFO = "get_param_file_mod_info";

            try
            {
                var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, "PRISMTest");

                var dbTools = DbToolsFactory.GetDBTools(connectionStringToUse, debugMode: true);

                var dbServerType = DbToolsFactory.GetServerTypeFromConnectionString(dbTools.ConnectStr);

                var cmd = dbTools.CreateCommand(SP_NAME_GET_PARAM_FILE_MOD_INFO, CommandType.StoredProcedure);

                // ReSharper disable once StringLiteralTypo
                dbTools.AddParameter(cmd, "@parameterFileName", SqlType.VarChar, 255).Value = "MSGFPlus_Tryp_DynSTYPhos_Stat_CysAlk_TMT_16Plex_Protocol1_20ppmParTol.txt";

                var paramFileId = dbTools.AddParameter(cmd, "@paramFileID", SqlType.Int, ParameterDirection.InputOutput);

                DbParameter paramFileFound;
                DbParameter targetSymbolList;
                DbParameter pmMassCorrectionTagList;
                DbParameter npMassCorrectionTagList;

                if (dbServerType == DbServerTypes.PostgreSQL)
                {
                    paramFileFound = dbTools.AddParameter(cmd, "@paramFileFound", SqlType.Boolean, ParameterDirection.InputOutput);
                    targetSymbolList = dbTools.AddParameter(cmd, "@pmTargetSymbolList", SqlType.VarChar, 128, ParameterDirection.InputOutput);
                    pmMassCorrectionTagList = dbTools.AddParameter(cmd, "@pmMassCorrectionTagList", SqlType.VarChar, 512, ParameterDirection.InputOutput);
                    npMassCorrectionTagList = dbTools.AddParameter(cmd, "@npMassCorrectionTagList", SqlType.VarChar, 512, ParameterDirection.InputOutput);
                }
                else
                {
                    paramFileFound = dbTools.AddParameter(cmd, "@paramFileFound", SqlType.TinyInt, ParameterDirection.InputOutput);
                    targetSymbolList = dbTools.AddParameter(cmd, "@pm_TargetSymbolList", SqlType.VarChar, 128, ParameterDirection.InputOutput);
                    pmMassCorrectionTagList = dbTools.AddParameter(cmd, "@pm_MassCorrectionTagList", SqlType.VarChar, 512, ParameterDirection.InputOutput);
                    npMassCorrectionTagList = dbTools.AddParameter(cmd, "@np_MassCorrectionTagList", SqlType.VarChar, 512, ParameterDirection.InputOutput);
                }

                var messageParam = dbTools.AddParameter(cmd, "@message", SqlType.VarChar, 255, ParameterDirection.InputOutput);

                DbParameter returnCodeParam;

                if (dbServerType == DbServerTypes.PostgreSQL)
                {
                    returnCodeParam = dbTools.AddParameter(cmd, "@returnCode", SqlType.VarChar, 64, ParameterDirection.InputOutput);
                    returnCodeParam.Value = string.Empty;
                }
                else
                {
                    returnCodeParam = new SqlParameter("@returnCode", SqlDbType.VarChar, 64);
                }

                // Initialize the output parameter values (required for PostgreSQL)
                paramFileId.Value = 0;
                paramFileFound.Value = false;
                targetSymbolList.Value = string.Empty;
                pmMassCorrectionTagList.Value = string.Empty;
                npMassCorrectionTagList.Value = string.Empty;

                messageParam.Value = string.Empty;

                // Execute the SP
                var returnCode = dbTools.ExecuteSP(cmd, out var errorMessage, 0);

                var success = returnCode == 0;

                if (!success)
                {
                    var errorMsg = string.Format(
                        "Procedure {0} returned error code {1}{2}",
                        SP_NAME_GET_PARAM_FILE_MOD_INFO, returnCode,
                        string.IsNullOrWhiteSpace(errorMessage)
                            ? string.Empty
                            : ": " + errorMessage);

                    if (returnCode == DbUtilsConstants.RET_VAL_EXCESSIVE_RETRIES)
                    {
                        // Return code was -5
                        Console.WriteLine("A return code of {0} indicates that the stored procedure call failed or permission was denied", DbUtilsConstants.RET_VAL_EXCESSIVE_RETRIES);
                    }
                    else
                    {
                        Console.WriteLine("Error: " + errorMsg);
                    }
                }

                // ReSharper disable CommentTypo

                // Expected values:

                // Param file ID                 3539
                // Param file found              true
                // Modification symbols          *,<,C,K
                // Static and dynamic mod names  Phosph,TMT16Tag,IodoAcet,TMT16Tag
                // isotopic mod names:           ''

                // ReSharper restore CommentTypo

                Console.WriteLine();
                Console.WriteLine("Message:                      {0}", messageParam.Value);

                if (dbServerType == DbServerTypes.PostgreSQL)
                {
                    Console.WriteLine("Return code:                  {0}", returnCodeParam.Value);
                }

                Console.WriteLine("Param file ID:                {0}", paramFileId.Value);
                Console.WriteLine("Param file found:             {0}", paramFileFound.Value);
                Console.WriteLine("Modification symbols:         {0}", targetSymbolList.Value);
                Console.WriteLine("Static and dynamic mod names: {0}", pmMassCorrectionTagList.Value);
                Console.WriteLine("isotopic mod names:           {0}", npMassCorrectionTagList.Value);

                Assert.That(returnCode, Is.EqualTo(expectedReturnCode), "Return code mismatch");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calling {0}: {1}", SP_NAME_GET_PARAM_FILE_MOD_INFO, ex.Message);
            }
        }

        [TestCase("prismweb3", "dmsdev")]
        [Category("DatabaseNamedUser")]
        public void TestPostLogEntryAsProcedure(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, DMS_WEB_USER);

            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand("post_log_entry", CommandType.StoredProcedure);

            dbTools.AddParameter(spCmd, "@type", SqlType.Text).Value = "Info";
            dbTools.AddParameter(spCmd, "@message", SqlType.Text).Value = "Test message 1";
            dbTools.AddParameter(spCmd, "@postedBy", SqlType.Text).Value = "Test caller";

            if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
            {
                dbTools.AddParameter(spCmd, "_targetSchema", SqlType.Text).Value = "public";
            }

            dbTools.ExecuteSP(spCmd, 1);

            Console.WriteLine("Complete: " + spCmd.CommandText);
            Console.WriteLine();
            Console.WriteLine("Selecting recent rows from t_log_entries");
            Console.WriteLine();

            var spSelectCmd = dbTools.CreateCommand("Select * from t_log_entries where posting_time >= current_timestamp - Interval '20 seconds'");

            var success = dbTools.GetQueryResults(spSelectCmd, out var queryResults, out var columnNames, 1);

            Assert.That(success, Is.True, "GetQueryResults returned false while querying t_log_entries");

            TestDBTools.ShowRowsFromTLogEntries(queryResults, columnNames);
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

            var query = "call post_log_entry(_postedBy => 'Test caller', _type =>'Info', _message => 'Test message 2')";
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

            var query = "call post_log_entry(_postedBy => @_postedBy, _type => @_type, _message => @_message)";
            var spCmd = dbTools.CreateCommand(query);

            dbTools.AddParameter(spCmd, "_type", SqlType.Text).Value = "Info";
            var messageParam = dbTools.AddParameter(spCmd, "_message", SqlType.Text);
            dbTools.AddParameter(spCmd, "_postedBy", SqlType.Text).Value = "Test caller";

            messageParam.Value = "Test message 3";
            var postSuccess = dbTools.GetQueryScalar(spCmd, out _, 1);

            VerifyTestPostLogEntry(dbTools, user, expectedPostSuccess, postSuccess);
        }

        [TestCase("Gigasax", "DMS_Capture")]
        [Category("DatabaseIntegrated")]
        public void TestRequestCaptureStepTaskSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestRequestCaptureStepTask(connectionString, "request_ctm_step_task");
        }

        /// <summary>
        /// Test calling procedure cap.request_ctm_step_task with _infoLevel = 1
        /// </summary>
        /// <remarks>
        /// Assigned category "SkipJenkins" since the unit test reports two errors when Jenkins runs this unit test:
        /// 1) cap.request_ctm_step_task Procedure did not return 0; expected 0 but actually -5
        /// 2) cap.request_ctm_step_task @Return (or _returnCode) is not 0; expected 0 but actually string.Empty
        /// </remarks>
        /// <param name="server">Server name</param>
        /// <param name="database">Database name</param>
        [TestCase("prismdb2.emsl.pnl.gov", "dms")]
        [Category("SkipJenkins")]
        public void TestRequestCaptureStepTaskPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, "svc-dms");
            TestRequestCaptureStepTask(connectionString, "cap.request_ctm_step_task");
        }

        /// <summary>
        /// Invoke stored procedure request_ctm_step_task and examine the results
        /// </summary>
        /// <remarks>
        /// On PostgreSQL, procedure mc.request_ctm_step_task returns query results using a reference cursor (_results refcursor)
        /// ExecuteSPData looks for parameters of type refcursor and retrieves the results
        /// If not tasks are available, the cursor will not be open
        /// </remarks>
        /// <param name="connectionString">Connection string</param>
        /// <param name="procedureNameWithSchema">Procedure name</param>
        private void TestRequestCaptureStepTask(string connectionString, string procedureNameWithSchema)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            var spCmd = dbTools.CreateCommand("request_ctm_step_task", CommandType.StoredProcedure);

            dbTools.AddParameter(spCmd, "@processorName", SqlType.VarChar, 128, "Monroe_CTM");
            var jobParam = dbTools.AddParameter(spCmd, "@job", SqlType.Int, ParameterDirection.InputOutput);
            var messageParam = dbTools.AddParameter(spCmd, "@message", SqlType.VarChar, 512, ParameterDirection.InputOutput);
            dbTools.AddTypedParameter(spCmd, "@infoLevel", SqlType.TinyInt, value: 1);
            dbTools.AddParameter(spCmd, "@managerVersion", SqlType.VarChar, 128, "(unknown version)");
            dbTools.AddTypedParameter(spCmd, "@jobCountToPreview", SqlType.Int, value: 10);

            jobParam.Value = 0;

            // Uncomment if required (this class never assigned a value to this parameter, though the procedure supports limiting assigned capture jobs if manager parameter "perspective" is "server")
            // dbTools.AddTypedParameter(spCmd, "@serverPerspectiveEnabled", SqlType.TinyInt, value: serverPerspectiveEnabled);

            var returnParam = dbTools.AddParameter(spCmd, "@returnCode", SqlType.VarChar, 64, ParameterDirection.InputOutput);

            Console.WriteLine("Running stored procedure " + procedureNameWithSchema + " using dbTools of type " + dbTools.DbServerType);

            var returnCode = dbTools.ExecuteSPData(spCmd, out var results, 1);

            Console.WriteLine();
            Console.WriteLine("Message: " + messageParam.Value);
            Console.WriteLine("Return:  " + returnParam.Value);

            if (returnCode is 53000 or 5301)
            {
                Console.WriteLine("ReturnCode is {0}, No tasks are available", returnCode);
                return;
            }

            foreach (var row in results)
            {
                Console.WriteLine(string.Join(", ", row));
            }

            Assert.Multiple(() =>
            {
                Assert.That(returnCode, Is.EqualTo(0), procedureNameWithSchema + " Procedure did not return 0");
                Assert.That(returnParam.Value, Is.EqualTo(0), procedureNameWithSchema + " @Return (or _returnCode) is not 0");
            });
        }

        private void VerifyTestPostLogEntry(IDBTools dbTools, string user, bool expectedPostSuccess, bool actualPostSuccess)
        {
            if (expectedPostSuccess)
                Assert.That(actualPostSuccess, Is.True, $"Call to post_log_entry failed for user {user}; it should have succeeded");
            else
                Assert.That(actualPostSuccess, Is.False, $"Call to post_log_entry succeeded for user {user}; it should have failed");

            if (!actualPostSuccess)
                return;

            Console.WriteLine();
            Console.WriteLine("Selecting recent rows from t_log_entries");
            Console.WriteLine();

            var spSelectCmd = dbTools.CreateCommand("Select * from t_log_entries where posting_time >= current_timestamp - Interval '20 seconds'");

            var querySuccess = dbTools.GetQueryResults(spSelectCmd, out var queryResults, out var columnNames, 1);

            Assert.That(querySuccess, Is.True, "GetQueryResults returned false while querying t_log_entries");

            TestDBTools.ShowRowsFromTLogEntries(queryResults, columnNames);
        }
    }
}
