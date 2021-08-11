using System;
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
    internal class ConnectionStringTests
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: addr, datname, dms, pid, Postgres, usename, username

        // ReSharper restore CommentTypo
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax;Initial Catalog=dms5;integrated security=SSPI", "", "Data Source=gigasax;Initial Catalog=dms5;integrated security=SSPI", true)]
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax;Initial Catalog=dms5;integrated security=SSPI", "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True;Application Name=PRISMTest", true, 2)]
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax;Initial Catalog=dms5;integrated security=SSPI;", "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True;Application Name=PRISMTest", true, 2)]
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax;integrated security=SSPI;", "PRISMTest", "Data Source=gigasax;Integrated Security=True;Application Name=PRISMTest", true, 2)]
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax", "", "Data Source=gigasax", false)]
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax", "PRISMTest", "Data Source=gigasax;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax;Initial Catalog=dms5", "", "Data Source=gigasax;Initial Catalog=dms5", false)]
        [TestCase(DbServerTypes.MSSQLServer, "Data Source=gigasax;Initial Catalog=dms5", "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243;Password=SecretKey;Database=dms", "", "Host=prismdb1;Username=d3l243;Password=SecretKey;Database=dms", false)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243;Password=SecretKey;Database=dms", "PRISMTest", "Host=prismdb1;Username=d3l243;Password=SecretKey;Database=dms;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243;Password=SecretKey", "", "Host=prismdb1;Username=d3l243;Password=SecretKey", false)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243;Password=SecretKey", "PRISMTest", "Host=prismdb1;Username=d3l243;Password=SecretKey;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243;Password=SecretKey;", "PRISMTest", "Host=prismdb1;Username=d3l243;Password=SecretKey;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243", "", "Host=prismdb1;Username=d3l243", true)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243", "PRISMTest", "Host=prismdb1;Username=d3l243;Application Name=PRISMTest", true, 2)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243;Database=dms;", "", "Host=prismdb1;Username=d3l243;Database=dms;", true, 2)]
        [TestCase(DbServerTypes.PostgreSQL, "Host=prismdb1;Username=d3l243;Database=dms;", "PRISMTest", "Host=prismdb1;Username=d3l243;Database=dms;Application Name=PRISMTest", true, 2)]
        [Category("PNL_Domain")]
        public void TestAddApplicationName(
            DbServerTypes serverType,
            string connectionString,
            string applicationName,
            string expectedResult,
            bool testConnectionString,
            int secondsToStayConnected = 0,
            string sqlQuery = "Select Table_Catalog, Table_Schema, Table_Type, Table_Name From information_schema.tables")
        {
            var updatedConnectionString = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, applicationName);

            Console.WriteLine("{0}: {1}", serverType, updatedConnectionString);

            if (!string.IsNullOrWhiteSpace(expectedResult))
                Assert.AreEqual(expectedResult, updatedConnectionString);

            if (!testConnectionString)
                return;

            GetQueryResults(updatedConnectionString, sqlQuery, secondsToStayConnected);
        }

        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True")]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True;Application Name=PRISMTest")]
        public void TestGetConnectionString(DbServerTypes serverType, string serverName, string databaseName, string applicationName, string expectedResult)
        {
            var connectionString = DbToolsFactory.GetConnectionString(serverType, serverName, databaseName, applicationName);

            Console.WriteLine("{0}: {1}", serverType, connectionString);

            if (!string.IsNullOrWhiteSpace(expectedResult))
                Assert.AreEqual(expectedResult, connectionString);
        }

        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "", "", "", null, "", "Data Source=gigasax;Integrated Security=True", true)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "", "", null, "", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True", true)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "", "", true, "", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True", true)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "bob", "", null, "", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True;User ID=bob", true)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "d3l243", "", null, "", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True;User ID=d3l243", true)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "d3l243", "", true, "", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True;User ID=d3l243", true)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "d3l243", "", null, "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=True;User ID=d3l243;Application Name=PRISMTest", true)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "d3l243", "SecretKey", null, "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=False;User ID=d3l243;Password=SecretKey;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "d3l243", "SecretKey", false, "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=False;User ID=d3l243;Password=SecretKey;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.MSSQLServer, "gigasax", "dms5", "d3l243", "SecretKey", true, "PRISMTest", "Data Source=gigasax;Initial Catalog=dms5;Integrated Security=False;User ID=d3l243;Password=SecretKey;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "", "", "", null, "", "Host=prismdb1;Integrated Security=True", false)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "", "", null, "", "Host=prismdb1;Database=dms;Integrated Security=True", false)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "", "", false, "", "Host=prismdb1;Database=dms;Integrated Security=True", false)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "", "", true, "", "Host=prismdb1;Database=dms;Integrated Security=True", false)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "", "d3l243", "", null, "", "Host=prismdb1;Username=d3l243", true)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "d3l243", "", null, "", "Host=prismdb1;Database=dms;Username=d3l243", true)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "d3l243", "", true, "", "Host=prismdb1;Database=dms;Username=d3l243;Integrated Security=True", true)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "d3l243", "", null, "PRISMTest", "Host=prismdb1;Database=dms;Username=d3l243;Application Name=PRISMTest", true)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "d3l243", "SecretKey", null, "PRISMTest", "Host=prismdb1;Database=dms;Username=d3l243;Password=SecretKey;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "d3l243", "SecretKey", false, "PRISMTest", "Host=prismdb1;Database=dms;Username=d3l243;Password=SecretKey;Integrated Security=False;Application Name=PRISMTest", false)]
        [TestCase(DbServerTypes.PostgreSQL, "prismdb1", "dms", "d3l243", "SecretKey", true, "PRISMTest", "Host=prismdb1;Database=dms;Username=d3l243;Password=SecretKey;Integrated Security=False;Application Name=PRISMTest", false)]
        [Category("PNL_Domain")]
        public void TestGetConnectionStringForUser(
            DbServerTypes serverType,
            string serverName,
            string databaseName,
            string userName,
            string password,
            bool? useIntegratedSecurity,
            string applicationName,
            string expectedResult,
            bool testConnectionString,
            int secondsToStayConnected = 0,
            string sqlQuery = "Select Table_Catalog, Table_Schema, Table_Type, Table_Name From information_schema.tables")
        {
            var connectionString = DbToolsFactory.GetConnectionString(serverType, serverName, databaseName, userName, password, applicationName, useIntegratedSecurity);

            Console.WriteLine("{0}: {1}", serverType, connectionString);

            if (!string.IsNullOrWhiteSpace(expectedResult))
                Assert.AreEqual(expectedResult, connectionString);

            if (!testConnectionString)
                return;

            GetQueryResults(connectionString, sqlQuery, secondsToStayConnected);
        }

        private string GetPaddedList(List<string> items, string stringFormat)
        {
            var result = new StringBuilder();
            foreach (var item in items)
            {
                if (result.Length > 0)
                    result.Append(" ");

                result.AppendFormat(stringFormat, item);
            }

            return result.ToString();
        }

        private void GetQueryResults(string connectionString, string sqlQuery, int secondsToStayConnected, int rowsToShow = 10, int columnWidth = 15)
        {
            Console.WriteLine();

            // Retrieve data using the factory class

            var dbTools = DbToolsFactory.GetDBTools(connectionString);
            dbTools.GetQueryResults(sqlQuery, out var results);

            var stringFormat = "{0,-" + columnWidth + "}";

            foreach (var resultRow in results.Take(rowsToShow))
            {
                Console.WriteLine(GetPaddedList(resultRow, stringFormat));
            }

            Console.WriteLine();
            Console.WriteLine();

            // ReSharper disable CommentTypo

            // Retrieve data over a period of several seconds (provided secondsToStayConnected is positive)

            // View active connections in SQL Server using "Select * From sys.dm_exec_sessions"
            // View active connections in Postgres using   "select pid as process_id, usename as username, datname as database_name, client_addr as client_address, application_name, backend_start, state, state_change from pg_stat_activity;"

            // ReSharper restore CommentTypo

            var serverType = DbToolsFactory.GetServerTypeFromConnectionString(connectionString);

            var startTime = DateTime.UtcNow;

            var cmd = serverType == DbServerTypes.PostgreSQL ?
                GetPostgresDbCommand(connectionString, sqlQuery) :
                GetSqlServerDbCommand(connectionString, sqlQuery);

            using (cmd)
            {
                try
                {
                    using var reader = cmd.ExecuteReader();

                    if (!reader.HasRows)
                    {
                        return;
                    }

                    var schemaTable = reader.GetSchemaTable();
                    if (schemaTable != null)
                    {
                        var columnNames = schemaTable.Rows
                            .Cast<DataRow>()
                            .Select(item => (string)item["ColumnName"])
                            .ToList();

                        Console.WriteLine(GetPaddedList(columnNames, stringFormat));
                    }

                    var rowCount = 0;
                    while (reader.Read())
                    {
                        var currentRow = new List<string>();

                        for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                        {
                            var value = reader.GetValue(columnIndex);

                            if (DBNull.Value.Equals(value))
                            {
                                currentRow.Add(string.Empty);
                            }
                            else
                            {
                                currentRow.Add(value.ToString());
                            }
                        }

                        Console.WriteLine(GetPaddedList(currentRow, stringFormat));

                        if (secondsToStayConnected > 0)
                        {
                            PRISM.ProgRunner.SleepMilliseconds(250);
                        }

                        rowCount++;

                        if (rowsToShow > 0 && rowCount >= rowsToShow)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error querying the database: " + ex.Message);
                }

                while (secondsToStayConnected > 0 && DateTime.UtcNow.Subtract(startTime).TotalSeconds < secondsToStayConnected)
                {
                    PRISM.ProgRunner.SleepMilliseconds(250);
                }

                cmd.Connection.Close();
            }
        }

        private DbCommand GetPostgresDbCommand(string connectionString, string sqlQuery)
        {
            var dbConnection = new NpgsqlConnection(connectionString);
            dbConnection.Open();

            var sqlCmd = new NpgsqlCommand(sqlQuery) { CommandType = CommandType.Text };
            sqlCmd.Connection = dbConnection;

            return sqlCmd;
        }

        private DbCommand GetSqlServerDbCommand(string connectionString, string sqlQuery)
        {
            var dbConnection = new SqlConnection(connectionString);
            dbConnection.Open();

            var sqlCmd = new SqlCommand(sqlQuery) { CommandType = CommandType.Text };

            sqlCmd.Connection = dbConnection;

            return sqlCmd;
        }
    }
}
