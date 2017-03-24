using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class StoredProcedureTests
    {
        [TestCase("Gigasax", "DMS5")]
        public void TestSearchLogs(string server, string database)
        {
            var connectionString = GetConnectionString(server, database);
            var dbTools = new clsExecuteDatabaseSP(connectionString);

            var spCmd = new SqlCommand()
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "FindLogEntry"
            };

            spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;

            var entryTypeParam = spCmd.Parameters.Add(new SqlParameter("@EntryType", SqlDbType.VarChar, 32));
            entryTypeParam.Value = "Normal";

            var messageTextParam = spCmd.Parameters.Add(new SqlParameter("@MessageText", SqlDbType.VarChar, 500));
            messageTextParam.Value = "complete";

            Console.WriteLine("Running stored procedure " + spCmd.CommandText);

            List<List<string>> lstResults;
            var returnCode = dbTools.ExecuteSP(spCmd, out lstResults);

            Assert.AreEqual(returnCode, 0, spCmd.CommandText + "Procedure did not return 0");

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
        public void TestGetAllPeptideDatabases(string server, string database)
        {
            var connectionString = GetConnectionString(server, database);
            var dbTools = new clsExecuteDatabaseSP(connectionString);

            var spCmd = new SqlCommand()
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "GetAllPeptideDatabases"
            };

            spCmd.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int)).Direction = ParameterDirection.ReturnValue;
            spCmd.Parameters.Add(new SqlParameter("@IncludeUnused", SqlDbType.Int)).Value = 0;
            spCmd.Parameters.Add(new SqlParameter("@IncludeDeleted", SqlDbType.Int)).Value = 0;

            Console.WriteLine("Running stored procedure " + spCmd.CommandText);

            List<List<string>> lstResults;
            var returnCode = dbTools.ExecuteSP(spCmd, out lstResults);

            Assert.AreEqual(returnCode, 0, spCmd.CommandText + "Procedure did not return 0");

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

        private string GetConnectionString(string server, string database)
        {
            return string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;", server, database);
        }

    }
}
