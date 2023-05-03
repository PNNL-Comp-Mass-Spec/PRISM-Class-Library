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
    public class TestDBTools
    {
        // Ignore Spelling: acq, Citext, Desc, dms, dmsreader, mgrs, Num, pgpass, Postgres, PostgreSQL, Sql, Username, yyyy-MM-dd hh:mm tt

        public const string DMS_READER = "dmsreader";
        public const string DMS_READER_PASSWORD = "dms4fun";

        public enum TestTableColumnNames
        {
            ShapeName = 0,
            Sides = 1,
            Color = 2,
            Perimeter = 3
        }

        private static Dictionary<TestTableColumnNames, SortedSet<string>> GetShapeTableColumnNamesByIdentifier()
        {
            var columnNamesByIdentifier = new Dictionary<TestTableColumnNames, SortedSet<string>>();

            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.ShapeName, "Shape", "ShapeName", "Shape_Name");
            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.Sides, "Sides", "SideCount", "Side_Count");
            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.Color, "Color");
            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.Perimeter, "Perimeter");

            return columnNamesByIdentifier;
        }

        /// <summary>
        /// This method tests using the same name for each column identifier and column name
        /// </summary>
        /// <param name="columnIdentifierList"></param>
        [TestCase("Shape,Sides,Color,Perimeter")]
        [TestCase("ShapeName,SideCount,Color,Perimeter")]
        public void TestAddColumnIdentifiers(string columnIdentifierList)
        {
            var columnIdentifiers = columnIdentifierList.Split(',');

            var columnNamesByIdentifier = new Dictionary<string, SortedSet<string>>();

            foreach (var columnIdentifier in columnIdentifiers)
            {
                DataTableUtils.AddColumnIdentifier(columnNamesByIdentifier, columnIdentifier);
            }

            var columnMap = DataTableUtils.GetColumnMappingFromHeaderLine(string.Join("\t", columnIdentifiers), columnNamesByIdentifier);

            TestResultRowRoundTrip(columnMap, columnIdentifiers);
        }

        /// <summary>
        /// This method tests using different strings for column identifier and column name
        /// </summary>
        /// <param name="columnIdentifierList"></param>
        /// <param name="columnNameList"></param>
        [TestCase("Shape,Sides,Color,Perimeter", "")]
        [TestCase("ShapeName,SideCount,Color,Perimeter", "Shape,Sides,Color,Perimeter")]
        public void TestAddColumnIdentifiersString(string columnIdentifierList, string columnNameList)
        {
            var columnIdentifiers = columnIdentifierList.Split(',');
            var columnNames = string.IsNullOrEmpty(columnNameList) ? columnIdentifierList.Split(',') : columnNameList.Split(',');

            Assert.AreEqual(columnIdentifiers.Length, columnNames.Length,
                "columnIdentifierList.Count does not match columnNameList.Count: {0} vs. {1}", columnIdentifiers.Length, columnNames.Length);

            var columnNamesByIdentifier = new Dictionary<string, SortedSet<string>>();
            for (var i = 0; i < columnIdentifiers.Length; i++)
            {
                DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, columnIdentifiers[i], columnNames[i]);
            }

            var columnMap = DataTableUtils.GetColumnMappingFromHeaderLine(string.Join("\t", columnNames), columnNamesByIdentifier);

            TestResultRowRoundTrip(columnMap, columnIdentifiers);
        }

        private void TestResultRowRoundTrip(IReadOnlyDictionary<string, int> columnMap, IReadOnlyList<string> columnIdentifiers)
        {
            var resultRow = "Square,4,Yellow,16".Split(',');

            var shape = DataTableUtils.GetColumnValue(resultRow, columnMap, columnIdentifiers[0]);        // Shape
            var sides = DataTableUtils.GetColumnValue(resultRow, columnMap, columnIdentifiers[1], 0);     // Sides
            var color = DataTableUtils.GetColumnValue(resultRow, columnMap, columnIdentifiers[2]);        // Color
            var perimeter = DataTableUtils.GetColumnValue(resultRow, columnMap, columnIdentifiers[3], 0); // Perimeter

            Console.WriteLine("Validating {0} {1}, side count {2}, perimeter {3}", color, shape, sides, perimeter);

            Assert.AreEqual("Square", shape, "Shape name mismatch");
            Assert.AreEqual(4, sides, "Side count mismatch");
            Assert.AreEqual("Yellow", color, "Color mismatch");
            Assert.AreEqual(16, perimeter, "Perimeter mismatch");
        }

        /// <summary>
        /// This method tests allowing multiple column names for the same column identifier
        /// </summary>
        /// <param name="columnNameList">List of valid column names, both semicolon and comma separated</param>
        [TestCase("ShapeName;SideCount;Color;Perimeter")]
        [TestCase("Shape;Sides;Color;Perimeter")]
        [TestCase("ShapeName,Shape;Sides,SideCount;Color;Perimeter")]
        public void TestAddColumnIdentifiersEnum(string columnNameList)
        {
            var columnNames = columnNameList.Split(';');

            var columnNamesByIdentifier = new Dictionary<TestTableColumnNames, SortedSet<string>>();
            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.ShapeName, columnNames[0]);
            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.Sides, columnNames[1]);
            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.Color, columnNames[2]);
            DataTableUtils.AddColumnNamesForIdentifier(columnNamesByIdentifier, TestTableColumnNames.Perimeter, columnNames[3]);

            var columnMap = DataTableUtils.GetColumnMappingFromHeaderLine(string.Join("\t", columnNames), columnNamesByIdentifier);

            var resultRow = "Square,4,Yellow,16".Split(',');

            var shape = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.ShapeName);
            var sides = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.Sides, 0);
            var color = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.Color);
            var perimeter = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.Perimeter, 0);

            Console.WriteLine("Validating {0} {1}, side count {2}, perimeter {3}", color, shape, sides, perimeter);

            Assert.AreEqual("Square", shape, "Shape name mismatch");
            Assert.AreEqual(4, sides, "Side count mismatch");
            Assert.AreEqual("Yellow", color, "Color mismatch");
            Assert.AreEqual(16, perimeter, "Perimeter mismatch");
        }

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
            "Host=prismdb1;Username=dmsreader;Database=dms",
            DbServerTypes.PostgreSQL)]
        [TestCase(
            "DbServerType=Postgres;Host=prismdb1;Username=dmsreader;Database=dms",
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

            var connectionString = usePostgres
                ? GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD)
                : GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);

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

            var connectionString = usePostgres
                ? GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD)
                : GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);

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

        [TestCase("prismdb1", "dms", "T_Event_Log", 15)]
        [TestCase("prismdb1", "dms", "T_Event_Target", 15)]
        [Category("DatabaseNamedUser")]
        public void TestGetColumnValuePostgres(string server, string database, string tableName, int rowCountToRetrieve)
        {
            var connectionString = GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD);
            TestGetColumnValue(connectionString, tableName, rowCountToRetrieve);
        }

        private void TestGetColumnValue(string connectionString, string tableName, int rowCountToRetrieve)
        {
            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);

            string query;
            var columnNames = new List<string>();

            if (dbTools.DbServerType == DbServerTypes.MSSQLServer)
            {
                if (tableName.Equals("T_Event_Log", StringComparison.OrdinalIgnoreCase))
                {
                    columnNames.Add("Event_ID");
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

                query = string.Format("SELECT " + string.Join(", ", columnNames) +
                                      " FROM (" +
                                      "   SELECT TOP {0} * FROM {1}" +
                                      "   ORDER BY {2} Desc) LookupQ " +
                                      "ORDER BY {2}", rowCountToRetrieve, tableName, columnNames.FirstOrDefault());
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

                query = string.Format("SELECT  " + string.Join(", ", columnNames) +
                                      " FROM (" +
                                      "   SELECT * FROM {1}" +
                                      "   ORDER BY {2} DESC LIMIT {0}) LookupQ " +
                                      "ORDER BY {2}", rowCountToRetrieve, tableName, columnNames.FirstOrDefault());
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
                dataLine.AppendFormat("{0,-15}", column.Split('.').Last());
            }
            Console.WriteLine(dataLine.ToString());

            var rowNumber = 0;

            foreach (var resultRow in queryResults)
            {
                rowNumber++;

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
                            var integerValue = dbTools.GetColumnValue(resultRow, columnMapping, currentColumnName, 0);
                            dataLine.AppendFormat("{0,-15}", integerValue);
                            break;

                        case SqlType.Date:
                        case SqlType.DateTime:
                            var dateValue = dbTools.GetColumnValue(resultRow, columnMapping, currentColumnName, DateTime.MinValue);
                            dataLine.AppendFormat("{0,-15}", dateValue);
                            break;

                        default:
                            var value = dbTools.GetColumnValue(resultRow, columnMapping, currentColumnName);
                            dataLine.AppendFormat("{0,-15}", value);
                            break;
                    }
                }
                Console.WriteLine(dataLine.ToString());
            }

            Console.WriteLine();
        }

        [TestCase(false, "Shape", "Sides", "Color")]
        [TestCase(false, "ShapeName", "SideCount", "Color")]
        [TestCase(false, "Shape", "Side_Count", "Color")]
        [TestCase(false, "Shape", "Sides_Count", "Color")]
        [TestCase(false, "Shape", "Side_Count", "Color", "Perimeter")]
        [TestCase(true, "Shape", "Sides", "Color")]
        [TestCase(true, "ShapeName", "SideCount", "Color")]
        [TestCase(true, "Shape", "Sides_Count", "Color")]
        [TestCase(true, "Shape", "Side_Count", "Color")]
        [TestCase(true, "Shape", "Side_Count", "Color", "Perimeter")]
        public void TestGetColumnValueEnum(bool throwExceptions, params string[] headerNames)
        {
            DataTableUtils.GetColumnValueThrowExceptions = throwExceptions;

            try
            {
                var columnNamesByIdentifier = GetShapeTableColumnNamesByIdentifier();

                var columnMap = DataTableUtils.GetColumnMappingFromHeaderLine(string.Join("\t", headerNames), columnNamesByIdentifier);

                var results = new List<List<string>>();

                if (headerNames.Contains("Perimeter"))
                {
                    results.Add(new List<string> { "Square", "4", "Red", "25" });
                    results.Add(new List<string> { "Square", "4", "Blue", "35" });
                    results.Add(new List<string> { "Circle", "1", "Green", string.Empty });
                    results.Add(new List<string> { "Triangle", "3", "Yellow" });
                }
                else
                {
                    results.Add(new List<string> { "Square", "4", "Red" });
                    results.Add(new List<string> { "Square", "4", "Blue" });
                    results.Add(new List<string> { "Circle", "1" });
                    results.Add(new List<string> { "Triangle", "3", "Yellow" });
                }

                var perimeterColumnName = headerNames.Length > 3 ? headerNames[3] : string.Empty;

                Console.WriteLine("Header names");
                Console.WriteLine("{0,-15} {1,-12} {2,-10} {3,-12}", headerNames[0], headerNames[1], headerNames[2], perimeterColumnName);

                Console.WriteLine();
                Console.WriteLine("{0,-15} {1,-12} {2,-10} {3,-12} {4,-15}", "Shape", "Sides", "Color", "Perimeter", "Perimeter_Value");

                foreach (var resultRow in results)
                {
                    try
                    {
                        var shapeName = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.ShapeName);
                        var sideCount = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.Sides, 0);
                        var colorName = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.Color, "No color", out var colorDefined);
                        var perimeterText = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.Perimeter, "Undefined", out var perimeterDefined);
                        var perimeter = DataTableUtils.GetColumnValue(resultRow, columnMap, TestTableColumnNames.Perimeter, 0);

                        Console.WriteLine("{0,-15} {1,-12} {2,-10} {3,-12} {4,-15}", shapeName, sideCount, colorName, perimeterText, perimeter);

                        if (colorName.Equals("No color"))
                            Assert.False(colorDefined, "Color column found unexpectedly");
                        else
                            Assert.True(colorDefined, "Color column not found");

                        if (headerNames.Contains("Perimeter"))
                        {
                            if (perimeterText.Equals("Undefined"))
                                Assert.False(perimeterDefined, "Perimeter column found unexpectedly");
                            else
                                Assert.True(perimeterDefined, "Perimeter column not found");
                        }
                        else
                        {
                            Assert.False(perimeterDefined, "perimeterDefined should be false");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (throwExceptions)
                        {
                            Console.WriteLine("Exception caught (this is allowed): " + ex.Message);
                        }
                        else
                        {
                            Console.WriteLine("Exception caught (this is unexpected): " + ex.Message);
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (throwExceptions)
                {
                    Console.WriteLine("Exception caught (this is allowed): " + ex.Message);
                }
                else
                {
                    Console.WriteLine("Exception caught (this is unexpected): " + ex.Message);
                    throw;
                }
            }
        }

        [TestCase(true, "Shape", "Sides", "Color")]
        [TestCase(false, "Shape", "Sides", "Color")]
        [TestCase(true, "Shape", "Sides", "Color", "Perimeter")]
        [TestCase(false, "Shape", "Sides", "Color", "Perimeter")]
        public void TestGetColumnValueGeneric(bool throwExceptions, params string[] headerNames)
        {
            DataTableUtils.GetColumnValueThrowExceptions = throwExceptions;

            try
            {
                var columnMap = DataTableUtils.GetColumnMapping(headerNames);

                var results = new List<List<string>>();

                if (headerNames.Contains("Perimeter"))
                {
                    results.Add(new List<string> { "Square", "4", "Red", "25" });
                    results.Add(new List<string> { "Square", "4", "Blue", "35" });
                    results.Add(new List<string> { "Circle", "1", "Green", string.Empty });
                    results.Add(new List<string> { "Triangle", "3", "Yellow" });
                }
                else
                {
                    results.Add(new List<string> { "Square", "4", "Red" });
                    results.Add(new List<string> { "Square", "4", "Blue" });
                    results.Add(new List<string> { "Circle", "1" });
                    results.Add(new List<string> { "Triangle", "3", "Yellow" });
                }

                var perimeterColumnName = headerNames.Length > 3 ? headerNames[3] : string.Empty;

                Console.WriteLine("Header names");
                Console.WriteLine("{0,-15} {1,-6} {2,-10} {3,-12}", headerNames[0], headerNames[1], headerNames[2], perimeterColumnName);

                Console.WriteLine();
                Console.WriteLine("{0,-15} {1,-6} {2,-10} {3,-12} {4,-15}", "Shape", "Sides", "Color", "Perimeter", "Perimeter_Value");

                foreach (var resultRow in results)
                {
                    try
                    {
                        var shapeName = DataTableUtils.GetColumnValue(resultRow, columnMap, "Shape");
                        var sideCount = DataTableUtils.GetColumnValue(resultRow, columnMap, "Sides", 0);
                        var colorName = DataTableUtils.GetColumnValue(resultRow, columnMap, "Color", "No color");

                        var perimeterText = DataTableUtils.GetColumnValue(resultRow, columnMap, "Perimeter", "Undefined");
                        var perimeter = DataTableUtils.GetColumnValue(resultRow, columnMap, "Perimeter", 0);

                        Console.WriteLine("{0,-15} {1,-6} {2,-10} {3,-12} {4,-15}", shapeName, sideCount, colorName, perimeterText, perimeter);
                    }
                    catch (Exception ex)
                    {
                        if (throwExceptions)
                        {
                            Console.WriteLine("Exception caught (this is allowed): " + ex.Message);
                        }
                        else
                        {
                            Console.WriteLine("Exception caught (this is unexpected): " + ex.Message);
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (throwExceptions)
                {
                    Console.WriteLine("Exception caught (this is allowed): " + ex.Message);
                }
                else
                {
                    Console.WriteLine("Exception caught (this is unexpected): " + ex.Message);
                    throw;
                }
            }
        }

        [TestCase(
            "Shape  SideCount  Color  Perimeter")]
        [TestCase(
            "Shape  SideCount  Color",
            TestTableColumnNames.ShapeName, TestTableColumnNames.Sides, TestTableColumnNames.Color)]
        [TestCase(
            "Shape  SideCount  Perimeter",
            TestTableColumnNames.ShapeName, TestTableColumnNames.Sides, TestTableColumnNames.Perimeter)]
        [TestCase(
            "SideCount  Perimeter  Color  Shape",
            TestTableColumnNames.Sides, TestTableColumnNames.Perimeter, TestTableColumnNames.Color, TestTableColumnNames.ShapeName)]
        public void TestGetExpectedHeaderLine(string expectedHeaderNames, params TestTableColumnNames[] customHeaderOrder)
        {
            var columnNamesByIdentifier = GetShapeTableColumnNamesByIdentifier();

            if (customHeaderOrder.Length == 0)
            {
                var defaultHeaderNames = DataTableUtils.GetExpectedHeaderLine(columnNamesByIdentifier, "  ");

                Console.WriteLine("Header names in default sort order");
                Console.WriteLine(defaultHeaderNames);
                Console.WriteLine();

                Assert.AreEqual(expectedHeaderNames, defaultHeaderNames);
            }

            if (customHeaderOrder.Length == 0)
                Console.WriteLine("Header names calling overloaded DataTableUtils.GetExpectedHeaderLine");
            else
                Console.WriteLine("Header names given order: \n{0}\n", string.Join("  ", customHeaderOrder));

            var customHeaderNames = DataTableUtils.GetExpectedHeaderLine(columnNamesByIdentifier, customHeaderOrder, "  ");
            Console.WriteLine(customHeaderNames);

            Assert.AreEqual(expectedHeaderNames, customHeaderNames);
        }

        [Test]
        public void TestGetExpectedHeaderLineEmptyLists()
        {
            var columnNamesByIdentifier = new Dictionary<TestTableColumnNames, SortedSet<string>>();

            var emptyHeaderListA = DataTableUtils.GetExpectedHeaderLine(columnNamesByIdentifier, "  ");

            var emptyHeaderListB = DataTableUtils.GetExpectedHeaderLine(columnNamesByIdentifier, new List<TestTableColumnNames>(), "  ");

            Assert.IsEmpty(emptyHeaderListA);
            Assert.IsEmpty(emptyHeaderListB);

            Console.WriteLine("Header lines were empty, as expected");
        }

        [Test]
        public void TestGetExpectedHeaderLineInvalidDictionary()
        {
            var columnNamesByIdentifier = GetShapeTableColumnNamesByIdentifier();

            // Get a copy of the existing items; without ToList() this test does not behave properly
            var columnIdentifierList = columnNamesByIdentifier.Keys.ToList();

            columnNamesByIdentifier.Remove(TestTableColumnNames.Color);
            columnNamesByIdentifier.Remove(TestTableColumnNames.Perimeter);

            try
            {
                DataTableUtils.GetExpectedHeaderLine(columnNamesByIdentifier, columnIdentifierList, "  ");
                Assert.Fail("GetExpectedHeaderLine did not throw an exception, but it should have");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Expected exception has been thrown: " + ex.Message);
            }
        }

        [TestCase("prismdb1", "dms", "SELECT * FROM t_log_entries LIMIT 5", "entry_id, posted_by, entered, type, message, entered_by", false)]
        [TestCase("prismdb1", "dms", "SELECT Entry_ID, Posted_By, Entered, Type, Message FROM t_log_entries LIMIT 5", "entry_id, posted_by, entered, type, message", false)]
        [TestCase("prismdb1", "dms", "SELECT Entry_ID, Posted_By, Entered, Type, Message FROM t_log_entries LIMIT 5", "Entry_ID, Posted_By, Entered, Type, Message", true)]
        [TestCase("prismdb1", "dms", "SELECT entry_id, posted_by, entered, type, message FROM t_log_entries LIMIT 5", "entry_id, posted_by, entered, type, message", true)]
        [TestCase("prismdb1", "dms", "SELECT Entry_ID, \"posted_by\", Entered, type, Message FROM t_log_entries LIMIT 5", "Entry_ID, posted_by, Entered, type, Message", true)]
        [TestCase("prismdb1", "dms", "SELECT Tool_Name, \"HMS\", \"HMS-MSn\" FROM v_analysis_tool_dataset_type_crosstab LIMIT 6", "Tool_Name, HMS, HMS-MSn", true)]
        [TestCase("prismdb1", "dms", "SELECT Entry_ID, Posted_By AS \"Posted By\", Entered, Type, Message FROM public.t_log_entries limit 6", "Entry_ID, Posted By, Entered, Type, Message", true)]
        [TestCase("prismdb1", "dms", "SELECT Entry_ID, Posted_By, Entered, Type, Message FROM t_log_entries LIMIT 5", "Entry_ID, Posted_By, Entered, Type, Message", true)]
        [TestCase("prismdb1", "dms", "SELECT t.Param_File_Type, pf.Param_File_ID, pf.Param_File_Name, pf.param_file_type_id AS \"Type ID\" FROM t_param_files PF INNER JOIN t_param_file_types T ON PF.param_file_type_id = T.param_file_type_id WHERE t.param_file_type Like 'MSGF%' Limit 6;", "param_file_type, param_file_id, param_file_name, Type ID", false)]
        [TestCase("prismdb1", "dms", "SELECT t.Param_File_Type, pf.Param_File_ID, pf.Param_File_Name, pf.param_file_type_id AS \"Type ID\" FROM t_param_files PF INNER JOIN t_param_file_types T ON PF.param_file_type_id = T.param_file_type_id WHERE t.param_file_type Like 'MSGF%' Limit 6;", "Param_File_Type, Param_File_ID, Param_File_Name, Type ID", true)]
        [TestCase("prismdb1", "dms", "SELECT \"Param-File-Types\".Param_File_Type, pf.Param_File_ID, pf.Param_File_Name, pf.param_file_type_id AS \"Type ID\" FROM t_param_files PF INNER JOIN t_param_file_types \"Param-File-Types\" ON PF.param_file_type_id = \"Param-File-Types\".param_file_type_id WHERE \"Param-File-Types\".param_file_type Like 'MSGF%' Limit 6;", "Param_File_Type, Param_File_ID, Param_File_Name, Type ID", true)]
        [Category("DatabaseNamedUser")]
        public void TestGetExpectedCapitalizedColumnNames(string server, string database, string query, string expectedColumnNames, bool autoCapitalize)
        {
            var connectionString = GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD);

            var dbTools = DbToolsFactory.GetDBTools(connectionString, debugMode: true);
            dbTools.CapitalizeColumnNamesInResults = autoCapitalize;

            Console.WriteLine("Create command at {0:yyyy-MM-dd hh:mm:ss tt}", DateTime.Now);

            var spCmd = dbTools.CreateCommand(query);

            var success = dbTools.GetQueryResults(spCmd, out var queryResults, out var columnNames, 1);

            Console.WriteLine();

            Assert.IsTrue(success, "GetQueryResults returned false");

            Assert.Greater(queryResults.Count, 0, "Row count should be non-zero, but was not");

            var expectedNames = expectedColumnNames.Split(',');

            if (expectedNames.Length != columnNames.Count)
            {
                Assert.Fail("Expected column name list has {0} columns, but the query results have {1} columns", expectedNames.Length, columnNames.Count);
            }

            for (var i = 0; i < expectedNames.Length; i++)
            {
                var expectedName = expectedNames[i].Trim();
                var actualName = columnNames[i];

                Assert.AreEqual(expectedName, actualName,
                    "Actual column name does not match the expected column name: {0} vs. {1}", actualName, expectedName);
            }

            Console.WriteLine("All {0} column names matched the expected capitalization: {1}", expectedNames.Length, expectedColumnNames);
        }

        [TestCase("Gigasax", "dms5", 5, 1, false)]
        [TestCase("Gigasax", "dms5", 5, 1, true)]
        [TestCase("Gigasax", "dms5", 10, 2, true)]
        [Category("DatabaseIntegrated")]
        public void TestGetRecentLogEntriesSqlServer(string server, string database, int rowCountToRetrieve, int iterations, bool specifyColumnNames)
        {
            var connectionString = GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestGetRecentLogEntries(connectionString, rowCountToRetrieve, iterations, specifyColumnNames);
        }

        [TestCase("prismdb1", "dms", 5, 1, false)]
        [TestCase("prismdb1", "dms", 5, 1, true)]
        [TestCase("prismdb1", "dms", 10, 2, true)]
        [Category("DatabaseNamedUser")]
        public void TestGetRecentLogEntriesPostgres(string server, string database, int rowCountToRetrieve, int iterations, bool specifyColumnNames)
        {
            var connectionString = GetConnectionStringPostgres(server, database, DMS_READER, DMS_READER_PASSWORD);
            TestGetRecentLogEntries(connectionString, rowCountToRetrieve, iterations, specifyColumnNames);
        }

        /// <summary>
        /// Test connecting to a Postgres database using integrated authentication
        /// </summary>
        /// <remarks>
        /// If the Postgres instance on the target server is not integrated with LDAP, the following exception occurs:
        /// LDAP authentication failed for user "d3l243"
        /// </remarks>
        /// <param name="server"></param>
        /// <param name="database"></param>
        /// <param name="rowCountToRetrieve"></param>
        /// <param name="iterations"></param>
        /// <param name="specifyColumnNames">When true, use explicit column names instead of *</param>
        [TestCase("prismdb1", "dms", 5, 2, false)]
        [TestCase("prismdb1", "dms", 5, 2, true)]
        [Category("DatabaseIntegrated")]
        public void TestGetRecentLogEntriesPostgresIntegrated(string server, string database, int rowCountToRetrieve, int iterations, bool specifyColumnNames)
        {
            var connectionString = string.Format("Host={0};Database={1};Integrated Security=true;Username=d3l243", server, database);
            TestGetRecentLogEntries(connectionString, rowCountToRetrieve, iterations, specifyColumnNames);
        }

        public void TestGetRecentLogEntries(string connectionString, int rowCountToRetrieve, int iterations, bool specifyColumnNames)
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

                var isPostgres = (dbTools.DbServerType == DbServerTypes.PostgreSQL);

                // Note that the SQL Server version of DMS does not have column Entered_BY in table T_Log_Entries
                // In contrast, the Postgres version of DMS does have column entered_by
                var columnList = specifyColumnNames
                    ? string.Format("Entry_ID, Posted_By, Entered, Type, Message{0}", isPostgres ? ", Entered_By" : "")
                    : "*";

                if (isPostgres)
                {
                    tableName = "public.t_log_entries";

                    query = string.Format("SELECT {0} FROM (" +
                                          "   SELECT {0} FROM {2}" +
                                          "   Order By entry_id Desc Limit {1}) LookupQ " +
                                          "ORDER BY entry_id",
                        columnList, rowCountToRetrieve + i * 2, tableName);
                }
                else
                {
                    tableName = "t_log_entries";

                    query = string.Format("SELECT {0} FROM (" +
                                          "   SELECT TOP {1} {0} FROM {2}" +
                                          "   ORDER BY entry_id Desc) LookupQ " +
                                          "ORDER BY entry_id",
                        columnList, rowCountToRetrieve + i * 2, tableName);
                }

                Console.WriteLine("Create command at {0:yyyy-MM-dd hh:mm:ss tt}", DateTime.Now);

                var spCmd = dbTools.CreateCommand(query);

                var success = dbTools.GetQueryResults(spCmd, out var queryResults, out var columnNames, 1);

                Assert.IsTrue(success, "GetQueryResults returned false");

                Assert.Greater(queryResults.Count, 0, "Row count in {0} should be non-zero, but was not", tableName);

                Console.WriteLine("{0} most recent entries in table {1}:", queryResults.Count, tableName);
                ShowRowsFromTLogEntries(queryResults, columnNames);

                if (i < iterations - 1)
                {
                    System.Threading.Thread.Sleep(1250);
                }

                Console.WriteLine();
            }
        }

        public static void ShowRowsFromTLogEntries(List<List<string>> results, List<string> columnNames)
        {
            if (columnNames.Count == 0)
                Console.WriteLine("{0,-10} {1,-21} {2,-20} {3}", "Entry_ID", "Date", "Posted_By", "Message");
            else
                Console.WriteLine("{0,-10} {1,-21} {2,-20} {3}", columnNames[0], columnNames[2], columnNames[1], columnNames[4]);

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

        [TestCase("prismdb1", "dms", "public.t_log_entries")]
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
            "SELECT username, name, hanford_id FROM v_users_export WHERE name = 'AutoUser'", 1, "H09090911,AutoUser,H09090911")]
        [TestCase("Gigasax", "DMS5",
            "SELECT Num_C, Num_H, Num_N, Num_O, Num_S FROM V_Residue_List_Report WHERE (Symbol IN ('K', 'R')) ORDER BY Symbol", 2,
            "6, 12, 2, 1, 0")]
        [Category("DatabaseIntegrated")]
        public void TestQueryTableIntegrated(string server, string database, string query, int expectedRowCount, string expectedValueList)
        {
            TestQueryTable(server, database, "Integrated", "", query, expectedRowCount, expectedValueList);
        }

        [TestCase("Gigasax", "DMS5",
            "SELECT username, name, hanford_id FROM v_users_export WHERE name = 'AutoUser'", 1, "H09090911,AutoUser,H09090911")]
        [TestCase("Gigasax", "DMS5",
            "SELECT Num_C, Num_H, Num_N, Num_O, Num_S FROM V_Residue_List_Report WHERE (Symbol IN ('K', 'R')) ORDER BY Symbol", 2,
            "6, 12, 2, 1, 0")]
        [Category("PNL_Domain")]
        public void TestQueryTableNamedUser(string server, string database, string query, int expectedRowCount, string expectedValueList)
        {
            TestQueryTable(server, database, DMS_READER, DMS_READER_PASSWORD, query, expectedRowCount, expectedValueList);
        }

        [TestCase(
            "Data Source=gigasax;Initial Catalog=DMS5;integrated security=SSPI",
            "SELECT username, name, hanford_id FROM v_users_export WHERE name = 'AutoUser'",
            1, "H09090911,AutoUser,H09090911")]
        [TestCase(
            "Data Source=gigasax;Initial Catalog=dms5;User=dmsreader;Password=dms4fun",
            "SELECT Num_C, Num_H, Num_N, Num_O, Num_S FROM V_Residue_List_Report WHERE (Symbol IN ('K', 'R')) ORDER BY Symbol",
            2, "6, 12, 2, 1, 0")]
        [TestCase(
            "DbServerType=SqlServer;Data Source=gigasax;Initial Catalog=DMS5;integrated security=SSPI",
            "SELECT username, name, hanford_id FROM v_users_export WHERE name = 'AutoUser'",
            1, "H09090911,AutoUser,H09090911")]
        [TestCase(
            "DbServerType=Postgres;Host=prismdb1;Username=dmsreader;Database=dms",
            "select mgr_name from mc.t_mgrs where mgr_name similar to 'pub-12-[1-4]' order by mgr_name;",
            4, "Pub-12-1,Pub-12-2,Pub-12-3,Pub-12-4")]
        [TestCase(
            "DbServerType=Postgres;Server=prismdb1;Username=dmsreader;Database=dms",
            "select mgr_name from mc.t_mgrs where mgr_name similar to 'pub-12-[1-4]' order by mgr_name;",
            4, "Pub-12-1,Pub-12-2,Pub-12-3,Pub-12-4")]
        [TestCase(
            "DbServerType=Postgres;Host=prismdb1;Username=dmsreader;Database=dms",
             "SELECT username, name, hanford_id FROM v_users_export WHERE name = 'AutoUser'",
            1, "H09090911,AutoUser,H09090911")]
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
            var connectionWorks = dbTools.TestDatabaseConnection(out var version);

            Console.WriteLine("Running query " + query + " against " + database + " as user " + user);
            Console.WriteLine("Connection string: {0} (connection test {1}, version {2})", connectionString, connectionWorks ? "successful" : "failed", version);

            dbTools.GetQueryResults(query, out var results);

            var expectedValues = expectedValueList.Split(',');

            Assert.AreEqual(results.Count, expectedRowCount, "RowCount mismatch");

            if (expectedRowCount < 1)
            {
                Console.WriteLine("Rows returned: " + results.Count);
                return;
            }

            var firstRow = results[0];

            for (var colIndex = 0; colIndex < firstRow.Count; colIndex++)
            {
                if (colIndex >= expectedValues.Length)
                    break;

                Assert.AreEqual(firstRow[colIndex], expectedValues[colIndex].Trim(),
                    "Data value mismatch, column {0}, expected {1} but actually {2}",
                    colIndex + 1, expectedValues[colIndex], firstRow[colIndex]);
            }

            Console.WriteLine("Rows returned: " + results.Count);
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

            dbTools.GetQueryResults(query, out var results);

            var expectedValues = expectedValueList.Split(',');

            if (results == null || results.Count == 0 && expectedRowCount == 0)
            {
                if (expectedRowCount == 0)
                    Console.WriteLine("No results found; this was expected");
                return;
            }

            Assert.AreEqual(results.Count, expectedRowCount, "RowCount mismatch");

            if (expectedRowCount < 1)
            {
                Console.WriteLine("Rows returned: " + results.Count);
                return;
            }

            var firstRow = results[0];

            for (var colIndex = 0; colIndex < firstRow.Count; colIndex++)
            {
                if (colIndex >= expectedValues.Length)
                    break;

                Assert.AreEqual(firstRow[colIndex], expectedValues[colIndex].Trim(),
                    "Data value mismatch, column {0}, expected {1} but actually {2}",
                    colIndex + 1, expectedValues[colIndex], firstRow[colIndex]);
            }

            Console.WriteLine("Rows returned: " + results.Count);
        }

        /// <summary>
        /// Get a PostgreSQL compatible connection string
        /// </summary>
        /// <remarks>
        /// Instead of providing an explicit password, create a pgpass file
        /// Linux:   ~/.pgpass
        /// Windows: C:\users\username\AppData\Roaming\postgresql\pgpass.conf
        /// Proto-2: C:\Windows\ServiceProfiles\NetworkService\AppData\Roaming\postgresql\pgpass.conf
        /// </remarks>
        /// <param name="server">Server (aka host) name</param>
        /// <param name="database">Database name</param>
        /// <param name="user">Username</param>
        /// <param name="password">Password (if empty, use .pgpass or pgpass.conf)</param>
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
        public static string GetConnectionStringSqlServer(string server, string database, string user = "Integrated", string password = "")
        {
            if (string.Equals(user, "Integrated", StringComparison.OrdinalIgnoreCase))
                return string.Format("Data Source={0};Initial Catalog={1};Integrated Security=SSPI;", server, database);

            return string.Format("Data Source={0};Initial Catalog={1};User={2};Password={3};", server, database, user, password);
        }
    }
}
