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
        [TestCase("prismdb2", "dms")]
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
                Assert.That(result.Count, Is.GreaterThanOrEqualTo(5), "Result row has fewer than 5 columns");

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

            Assert.That(returnCode, Is.EqualTo(0), spCmd.CommandText + " Procedure did not return 0");

            var rowsDisplayed = 0;

            foreach (var result in results)
            {
                Assert.That(result.Count, Is.GreaterThanOrEqualTo(9), "Result row has fewer than 9 columns");

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
        /// Retrieve values from PostgreSQL function mc.get_manager_parameters()
        /// </summary>
        [TestCase("prismdb2", "dms")]
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

            Assert.IsTrue(success, "GetQueryResults return false");

            ExamineManagerParams(results);
        }

        /// <summary>
        /// Retrieve values from PostgreSQL function mc.get_manager_parameters()
        /// </summary>
        [TestCase("prismdb2", "dms")]
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
                Assert.That(result.Count, Is.GreaterThanOrEqualTo(12), "Result row has fewer than 12 columns");

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

        [TestCase("prismdb2", "dms")]
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

            Assert.That(returnCode, Is.EqualTo(0), procedureNameWithSchema + " Procedure did not return 0");
            Assert.That(returnParam.Value, Is.EqualTo(0), procedureNameWithSchema + " @Return (or _returnCode) is not 0");
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestEnableDisableManagersDataSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestEnableDisableManagersData(connectionString, "enable_disable_managers");
        }

        [TestCase("prismdb2", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestEnableDisableManagersDataPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestEnableDisableManagersData(connectionString, "mc.enable_disable_managers");
        }

        /// <summary>
        /// Invoke stored procedure EnableDisableManagers (or enable_disable_managers) and examine output parameter @message (or _message)
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

            Assert.That(returnCode, Is.EqualTo(0), procedureNameWithSchema + " Procedure did not return 0");
            Assert.That(returnParam.Value, Is.EqualTo(0), procedureNameWithSchema + " @Return (or _returnCode) is not 0");
        }

        [TestCase("Gigasax", "dms5", "find_log_entry", false, 0)]
        public void TestGetReturnCodeSqlServer(string server, string database, string procedureName, bool skipProcedureCall, int expectedReturnCode)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestGetReturnCode(connectionString, procedureName, skipProcedureCall, expectedReturnCode);
        }

        [TestCase("prismdb2", "dms", "find_log_entry", true, 0, "")]
        [TestCase("prismdb2", "dms", "find_log_entry", true, 2200, "2200L")]
        [TestCase("prismdb2", "dms", "find_log_entry", true, 2, "2F005")]
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

        [TestCase("prismdb2", "dms", 0)]
        [Category("DatabaseNamedUser")]
        public void TestGetNamedReturnCodePostgres(string server, string database, int expectedReturnCode)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, DMS_WEB_USER);
            TestGetNamedReturnCode(connectionString, expectedReturnCode);
        }

        private void TestGetNamedReturnCode(string connectionString, int expectedReturnCode)
        {
            const string SP_NAME_REPORT_GET_SPECTRAL_LIBRARY_ID = "get_spectral_library_id";

            try
            {
                var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, "PRISMTest");

                var dbTools = DbToolsFactory.GetDBTools(connectionStringToUse, debugMode: true);

                var dbServerType = DbToolsFactory.GetServerTypeFromConnectionString(dbTools.ConnectStr);

                var cmd = dbTools.CreateCommand(SP_NAME_REPORT_GET_SPECTRAL_LIBRARY_ID, CommandType.StoredProcedure);

                if (dbServerType == DbServerTypes.PostgreSQL)
                    dbTools.AddParameter(cmd, "@allowAddNew", SqlType.Boolean).Value = true;
                else
                    dbTools.AddParameter(cmd, "@allowAddNew", SqlType.TinyInt).Value = 1;

                dbTools.AddParameter(cmd, "@dmsSourceJob", SqlType.Int).Value = 0;
                // ReSharper disable once StringLiteralTypo
                dbTools.AddParameter(cmd, "@proteinCollectionList", SqlType.VarChar, 2000).Value = "H_sapiens_UniProt_SPROT_2021-06-20,Tryp_Pig_Bov";
                dbTools.AddParameter(cmd, "@organismDbFile", SqlType.VarChar, 128).Value = "na";
                dbTools.AddParameter(cmd, "@fragmentIonMzMin", SqlType.Real).Value = 200f;
                dbTools.AddParameter(cmd, "@fragmentIonMzMax", SqlType.Real).Value = 1800f;

                if (dbServerType == DbServerTypes.PostgreSQL)
                    dbTools.AddParameter(cmd, "@trimNTerminalMet", SqlType.Boolean).Value = true;
                else
                    dbTools.AddParameter(cmd, "@trimNTerminalMet", SqlType.TinyInt).Value = 1;

                dbTools.AddParameter(cmd, "@cleavageSpecificity", SqlType.VarChar, 64).Value = "K*,R*";
                dbTools.AddParameter(cmd, "@missedCleavages", SqlType.Int).Value = 2;
                dbTools.AddParameter(cmd, "@peptideLengthMin", SqlType.Int).Value = 7;
                dbTools.AddParameter(cmd, "@peptideLengthMax", SqlType.Int).Value = 30;
                dbTools.AddParameter(cmd, "@precursorMzMin", SqlType.Real).Value = 350f;
                dbTools.AddParameter(cmd, "@precursorMzMax", SqlType.Real).Value = 1800f;
                dbTools.AddParameter(cmd, "@precursorChargeMin", SqlType.Int).Value = 2;
                dbTools.AddParameter(cmd, "@precursorChargeMax", SqlType.Int).Value = 4;

                if (dbServerType == DbServerTypes.PostgreSQL)
                    dbTools.AddParameter(cmd, "@staticCysCarbamidomethyl", SqlType.Boolean).Value = true;
                else
                    dbTools.AddParameter(cmd, "@staticCysCarbamidomethyl", SqlType.TinyInt).Value = 1;

                dbTools.AddParameter(cmd, "@staticMods", SqlType.VarChar, 512).Value = string.Empty;
                dbTools.AddParameter(cmd, "@dynamicMods", SqlType.VarChar, 512).Value = "UniMod:35,   15.994915,  M";
                dbTools.AddParameter(cmd, "@maxDynamicMods", SqlType.Int).Value = 3;

                // Append the output parameters

                var libraryIdParam = dbTools.AddParameter(cmd, "@libraryId", SqlType.Int, ParameterDirection.InputOutput);
                var libraryStateIdParam = dbTools.AddParameter(cmd, "@libraryStateId", SqlType.Int, ParameterDirection.InputOutput);
                var libraryNameParam = dbTools.AddParameter(cmd, "@libraryName", SqlType.VarChar, 255, ParameterDirection.InputOutput);
                var storagePathParam = dbTools.AddParameter(cmd, "@storagePath", SqlType.VarChar, 255, ParameterDirection.InputOutput);

                var shouldMakeLibraryParam = dbTools.AddParameter(cmd, "@sourceJobShouldMakeLibrary", dbServerType == DbServerTypes.PostgreSQL ? SqlType.Boolean : SqlType.TinyInt, ParameterDirection.InputOutput);

                var messageParam = dbTools.AddParameter(cmd, "@message", SqlType.VarChar, 255, ParameterDirection.InputOutput);
                var returnCodeParam = dbTools.AddParameter(cmd, "@returnCode", SqlType.VarChar, 64, ParameterDirection.InputOutput);

                // Initialize the output parameter values (required for PostgreSQL)
                libraryIdParam.Value = 0;
                libraryStateIdParam.Value = 0;
                libraryNameParam.Value = string.Empty;
                storagePathParam.Value = string.Empty;

                shouldMakeLibraryParam.Value = dbServerType == DbServerTypes.PostgreSQL ? false : 0;

                messageParam.Value = string.Empty;
                returnCodeParam.Value = string.Empty;

                // Execute the SP
                var returnCode = dbTools.ExecuteSP(cmd, out var errorMessage, 0);

                var success = returnCode == 0;

                if (!success)
                {
                    var errorMsg = string.Format(
                        "Procedure {0} returned error code {1}{2}",
                        SP_NAME_REPORT_GET_SPECTRAL_LIBRARY_ID, returnCode,
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

                // Expected messages:

                // If the spectral library does not exist, and allowAddNew is false:
                //   Would create a new spectral library named H_sapiens_UniProt_SPROT_2021-06-20_Tryp_Pig_Bov_7D7D8EC4.predicted.speclib

                // If the spectral library does not exist, and allowAddNew is true:
                //   Created spectral library ID 1002: H_sapiens_UniProt_SPROT_2021-06-20_Tryp_Pig_Bov_7D7D8EC4.predicted.speclib

                // If the spectral library already exists:
                //   Found existing spectral library ID 1002 with state 2: H_sapiens_UniProt_SPROT_2021-06-20_Tryp_Pig_Bov_7D7D8EC4.predicted.speclib

                Console.WriteLine();
                Console.WriteLine("Message:             " + messageParam.Value);
                Console.WriteLine("Return code:         " + returnCodeParam.Value);

                Console.WriteLine("Library ID:          " + libraryIdParam.Value);
                Console.WriteLine("Library State ID:    " + libraryStateIdParam.Value);
                Console.WriteLine("Library Name:        " + libraryNameParam.Value);
                Console.WriteLine("Storage Path:        " + storagePathParam.Value);
                Console.WriteLine("Should Make New Lib: " + shouldMakeLibraryParam.Value);

                Assert.That(returnCode, Is.EqualTo(expectedReturnCode), "Return code mismatch");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calling {0}: {1}", SP_NAME_REPORT_GET_SPECTRAL_LIBRARY_ID, ex.Message);
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

            Assert.IsTrue(success, "GetQueryResults returned false while querying t_log_entries");

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

        private void VerifyTestPostLogEntry(IDBTools dbTools, string user, bool expectedPostSuccess, bool actualPostSuccess)
        {
            if (expectedPostSuccess)
                Assert.IsTrue(actualPostSuccess, $"Call to post_log_entry failed for user {user}; it should have succeeded");
            else
                Assert.IsFalse(actualPostSuccess, $"Call to post_log_entry succeeded for user {user}; it should have failed");

            if (!actualPostSuccess)
                return;

            Console.WriteLine();
            Console.WriteLine("Selecting recent rows from t_log_entries");
            Console.WriteLine();

            var spSelectCmd = dbTools.CreateCommand("Select * from t_log_entries where posting_time >= current_timestamp - Interval '20 seconds'");

            var querySuccess = dbTools.GetQueryResults(spSelectCmd, out var queryResults, out var columnNames, 1);

            Assert.IsTrue(querySuccess, "GetQueryResults returned false while querying t_log_entries");

            TestDBTools.ShowRowsFromTLogEntries(queryResults, columnNames);
        }
    }
}
