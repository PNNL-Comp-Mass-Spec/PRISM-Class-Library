using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using PRISM;

// ReSharper disable UnusedMember.Global
namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Methods for appending columns to a data table
    /// Also includes methods for retrieving data from a row of values, using a columnMap dictionary
    /// </summary>
    public static class DataTableUtils
    {
        #region "Properties"

        /// <summary>
        /// When using GetColumnValue, if an exact match is not found and this is true,
        /// look for columnName matching ColumnNameX in TableName.ColumnNameX
        /// </summary>
        public static bool GetColumnIndexAllowColumnNameMatchOnly { get; set; } = true;

        /// <summary>
        /// When using GetColumnValue, if an exact match is not found and this is true,
        /// look for columnName matching any part of a column name
        /// </summary>
        public static bool GetColumnIndexAllowFuzzyMatch { get; set; } = true;

        /// <summary>
        /// When using GetColumnValue, throw an exception if an invalid column name
        /// or if the column index is out of range vs. the actual number of columns
        /// </summary>
        public static bool GetColumnValueThrowExceptions { get; set; } = true;

        #endregion

        /// <summary>
        /// Append to a dictionary mapping a column identifier to the names supported for that column identifier
        /// Use this method to add a column with the same identifier and name
        /// Assumes case-insensitive column names
        /// </summary>
        /// <remarks>Use this method in conjunction with GetColumnMappingFromHeaderLine</remarks>
        /// <param name="columnNamesByIdentifier"></param>
        /// <param name="columnIdentifier"></param>
        public static void AddColumnIdentifier(
            Dictionary<string, SortedSet<string>> columnNamesByIdentifier,
            string columnIdentifier)
        {
            AddColumnNamesForIdentifier(columnNamesByIdentifier, columnIdentifier, false, columnIdentifier);
        }

        /// <summary>
        /// Append to a dictionary mapping a column identifier to the names supported for that column identifier
        /// Assumes case-insensitive column names
        /// </summary>
        /// <remarks>Use this method in conjunction with GetColumnMappingFromHeaderLine</remarks>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnNamesByIdentifier"></param>
        /// <param name="columnIdentifier"></param>
        /// <param name="columnNames">Comma separated list of column names</param>
        public static void AddColumnNamesForIdentifier<T>(
            Dictionary<T, SortedSet<string>> columnNamesByIdentifier,
            T columnIdentifier,
            params string[] columnNames)
        {
            AddColumnNamesForIdentifier(columnNamesByIdentifier, columnIdentifier, false, columnNames);
        }

        /// <summary>
        /// Append to a dictionary mapping a column identifier to the names supported for that column identifier
        /// </summary>
        /// <remarks>Use this method in conjunction with GetColumnMappingFromHeaderLine</remarks>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnNamesByIdentifier"></param>
        /// <param name="columnIdentifier"></param>
        /// <param name="caseSensitiveColumnNames"></param>
        /// <param name="columnNames"></param>
        public static void AddColumnNamesForIdentifier<T>(
            Dictionary<T, SortedSet<string>> columnNamesByIdentifier,
            T columnIdentifier,
            bool caseSensitiveColumnNames,
            params string[] columnNames)
        {
            StringComparer stringComparer;

            if (caseSensitiveColumnNames)
                stringComparer = StringComparer.Ordinal;
            else
                stringComparer = StringComparer.OrdinalIgnoreCase;

            var columnNameList = new SortedSet<string>(stringComparer);

            foreach (var columnName in columnNames)
            {
                columnNameList.Add(columnName);
            }

            columnNamesByIdentifier.Add(columnIdentifier, columnNameList);
        }

        /// <summary>
        /// Append a column of the given type to the DataTable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataTable"></param>
        /// <param name="columnName"></param>
        /// <param name="columnType"></param>
        /// <param name="defaultValue"></param>
        /// <param name="isReadOnly">True if the column value cannot be updated after a row is added to a table</param>
        /// <param name="isUnique"></param>
        /// <param name="autoIncrement"></param>
        private static bool AppendColumnToTable<T>(
            DataTable dataTable,
            string columnName,
            Type columnType,
            T defaultValue,
            bool isReadOnly,
            bool isUnique,
            bool autoIncrement = false)
        {
            try
            {
                var newColumn = dataTable.Columns.Add(columnName);
                newColumn.DataType = columnType;

                if (autoIncrement)
                {
                    newColumn.DefaultValue = null;
                    newColumn.AutoIncrement = true;
                    newColumn.Unique = true;
                }
                else
                {
                    newColumn.DefaultValue = defaultValue;
                    newColumn.AutoIncrement = false;
                    newColumn.Unique = isUnique;
                }

                newColumn.ReadOnly = isReadOnly;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Append a date column to the data table
        /// </summary>
        /// <param name="dataTable">Data table</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultDate">Default value</param>
        /// <param name="isReadOnly">True if the column value cannot be updated after a row is added to a table</param>
        /// <param name="isUnique">True if the value in each row of the column must be unique</param>
        public static bool AppendColumnDateToTable(DataTable dataTable, string columnName, DateTime defaultDate, bool isReadOnly = false, bool isUnique = false)
        {
            return AppendColumnToTable(dataTable, columnName, Type.GetType("System.DateTime"), defaultDate, isReadOnly, isUnique);
        }

        /// <summary>
        /// Append a double column to the data table
        /// </summary>
        /// <param name="dataTable">Data table</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="isReadOnly">True if the column value cannot be updated after a row is added to a table</param>
        /// <param name="isUnique"></param>
        public static bool AppendColumnDoubleToTable(DataTable dataTable, string columnName, double defaultValue = 0, bool isReadOnly = false, bool isUnique = false)
        {
            return AppendColumnToTable(dataTable, columnName, Type.GetType("System.Double"), defaultValue, isReadOnly, isUnique);
        }

        /// <summary>
        /// Append a float (single) column to the data table
        /// </summary>
        /// <param name="dataTable">Data table</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="isReadOnly">True if the column value cannot be updated after a row is added to a table</param>
        /// <param name="isUnique"></param>
        public static bool AppendColumnFloatToTable(DataTable dataTable, string columnName, float defaultValue = 0, bool isReadOnly = false, bool isUnique = false)
        {
            return AppendColumnToTable(dataTable, columnName, Type.GetType("System.Double"), defaultValue, isReadOnly, isUnique);
        }

        /// <summary>
        /// Append an integer column to the data table
        /// </summary>
        /// <param name="dataTable">Data table</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value (ignored if autoIncrement is true)</param>
        /// <param name="autoIncrement">True if this is an auto incremented value</param>
        /// <param name="isReadOnly">True if the column value cannot be updated after a row is added to a table (forced to true if autoIncrement is true)</param>
        /// <param name="isUnique">True if the value in each row of the column must be unique</param>
        public static bool AppendColumnIntegerToTable(DataTable dataTable, string columnName, int defaultValue = 0, bool autoIncrement = false, bool isReadOnly = false, bool isUnique = false)
        {
            return AppendColumnToTable(dataTable, columnName, Type.GetType("System.Int32"), defaultValue, isReadOnly, isUnique, autoIncrement);
        }

        /// <summary>
        /// Append a long integer column to the data table
        /// </summary>
        /// <param name="dataTable">Data table</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value (ignored if autoIncrement is true)</param>
        /// <param name="autoIncrement">True if this is an auto incremented value</param>
        /// <param name="isReadOnly">True if the column value cannot be updated after a row is added to a table (forced to true if autoIncrement is true)</param>
        /// <param name="isUnique">True if the value in each row of the column must be unique</param>
        public static bool AppendColumnLongToTable(DataTable dataTable, string columnName, long defaultValue = 0, bool autoIncrement = false, bool isReadOnly = false, bool isUnique = false)
        {
            return AppendColumnToTable(dataTable, columnName, Type.GetType("System.Int64"), defaultValue, isReadOnly, isUnique, autoIncrement);
        }

        /// <summary>
        /// Append a string column to the data table
        /// </summary>
        /// <param name="dataTable">Data table</param>
        /// <param name="columnName">Column name</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="isReadOnly">True if the column value cannot be updated after a row is added to a table</param>
        /// <param name="isUnique">True if the value in each row of the column must be unique</param>
        public static bool AppendColumnStringToTable(DataTable dataTable, string columnName, string defaultValue = null, bool isReadOnly = false, bool isUnique = false)
        {
            return AppendColumnToTable(dataTable, columnName, Type.GetType("System.String"), defaultValue, isReadOnly, isUnique);
        }

        /// <summary>
        /// Search the columnMap dictionary for the best match to columnIdentifier
        /// First looks for an exact match for columnIdentifier
        /// If no match, and columnIdentifier is a string, will try a partial match:
        ///   If property GetColumnIndexAllowColumnNameMatchOnly is true, looks for a match after the last period seen in each name
        ///   If property GetColumnIndexAllowFuzzyMatch is true, looks for a column that contains the desired column name
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnMap">Map of column identifier to column index, as returned by GetColumnMapping</param>
        /// <param name="columnIdentifier">Column identifier to find</param>
        /// <returns>The zero-based column index, or -1 if no match</returns>
        public static int GetColumnIndex<T>(
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier)
        {
            return GetColumnIndex(columnMap, columnIdentifier, GetColumnIndexAllowColumnNameMatchOnly, GetColumnIndexAllowFuzzyMatch);
        }

        /// <summary>
        /// Search the columnMap dictionary for the best match to columnIdentifier
        /// First looks for an exact match for columnIdentifier
        /// If no match, and columnIdentifier is a string, will try a partial match:
        ///   If property allowColumnNameMatchOnly is true, looks for a match after the last period seen in each name
        ///   If property allowFuzzyMatch is true, looks for a column that contains the desired column name
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnIdentifier">Column identifier to find</param>
        /// <param name="allowColumnNameMatchOnly">
        /// When true and an exact match is not found, look for columns in the dictionary matching
        /// the pattern Table.ColumnX, where ColumnX matches columnIdentifier
        /// </param>
        /// <param name="allowFuzzyMatch">
        /// When true and a clear match is not found, look for a column that contains the desired column
        /// </param>
        /// <returns>The zero-based column index, or -1 if no match</returns>
        public static int GetColumnIndex<T>(
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            bool allowColumnNameMatchOnly,
            bool allowFuzzyMatch = true)
        {
            if (columnMap.TryGetValue(columnIdentifier, out var columnIndex))
            {
                return columnIndex;
            }

            if (columnIdentifier is not string columnName)
            {
                return -1;
            }

            if (allowColumnNameMatchOnly)
            {
                var periodAndName = "." + columnName;

                foreach (var item in columnMap)
                {
                    if (item.Key is string keyName &&
                        keyName.EndsWith(periodAndName, StringComparison.OrdinalIgnoreCase))
                    {
                        return item.Value;
                    }
                }
            }

            // ReSharper disable once InvertIf
            if (allowFuzzyMatch)
            {
                foreach (var item in columnMap)
                {
                    if (item.Key is string keyName &&
                        keyName.IndexOf(columnName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return item.Value;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Get a mapping from column name to column index, based on column order
        /// </summary>
        /// <remarks>Use in conjunction with GetColumnValue, e.g. GetColumnValue(resultRow, columnMap, "ID")</remarks>
        /// <param name="columnNames"></param>
        /// <param name="caseSensitiveColumnNames"></param>
        /// <returns>Mapping from column name to column index</returns>
        public static Dictionary<string, int> GetColumnMapping(IReadOnlyList<string> columnNames, bool caseSensitiveColumnNames = true)
        {
            StringComparer stringComparer;

            if (caseSensitiveColumnNames)
                stringComparer = StringComparer.Ordinal;
            else
                stringComparer = StringComparer.OrdinalIgnoreCase;

            var columnMap = new Dictionary<string, int>(stringComparer);

            for (var i = 0; i < columnNames.Count; i++)
            {
                columnMap.Add(columnNames[i], i);
            }

            return columnMap;
        }

        /// <summary>
        /// Examine a tab-delimited list of column names (as read from the first line of a text file)
        /// Compare the column names to the names in the columnNamesByIdentifier dictionary to determine the column index of each column name
        /// If a name is not found for an identifier, the column index will be -1
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="headerLine">Tab-delimited list of column names</param>
        /// <param name="columnNamesByIdentifier">Dictionary of known column names for each column identifier</param>
        /// <returns>Mapping from column identifier of type T (either a string or an enum) to the index of the column in the header line</returns>
        public static Dictionary<T, int> GetColumnMappingFromHeaderLine<T>(
            string headerLine,
            Dictionary<T, SortedSet<string>> columnNamesByIdentifier)
        {
            var columnMap = new Dictionary<T, int>();
            GetColumnMappingFromHeaderLine(columnMap, headerLine, columnNamesByIdentifier);
            return columnMap;
        }

        /// <summary>
        /// Examine a tab-delimited list of column names (as read from the first line of a text file)
        /// Compare the column names to the names in the columnNamesByIdentifier dictionary to determine the column index of each column name
        /// If a name is not found for an identifier, the column index will be -1
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnMap">Mapping from column identifier of type T (either a string or an enum) to the index of the column in the header line</param>
        /// <param name="headerLine">Tab-delimited list of column names</param>
        /// <param name="columnNamesByIdentifier">Dictionary of known column names for each column identifier</param>
        /// <returns>True if at least one recognized column name is found, otherwise false</returns>
        public static bool GetColumnMappingFromHeaderLine<T>(
            IDictionary<T, int> columnMap,
            string headerLine,
            Dictionary<T, SortedSet<string>> columnNamesByIdentifier)
        {
            columnMap.Clear();

            foreach (var candidateColumn in columnNamesByIdentifier)
            {
                columnMap.Add(candidateColumn.Key, -1);
            }

            var columnNames = headerLine.Split('\t').ToList();

            if (columnNames.Count < 1)
            {
                ConsoleMsgUtils.ShowWarning("Invalid header line sent to GetColumnMappingFromHeaderLine; should be a tab-delimited list");
                return false;
            }

            var columnIndex = 0;
            var matchFound = false;

            foreach (var columnName in columnNames)
            {
                foreach (var candidateColumn in columnNamesByIdentifier)
                {
                    if (!candidateColumn.Value.Contains(columnName))
                        continue;

                    // Match found
                    columnMap[candidateColumn.Key] = columnIndex;
                    matchFound = true;
                    break;
                }
                columnIndex++;
            }

            return matchFound;
        }

        /// <summary>
        /// Get the string value for the specified column (of type T)
        /// Returns valueIfMissing if the column is not found
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnIdentifier">Column name or enum</param>
        /// <param name="valueIfMissing">Value to return if the column identifier is invalid or if resultRow does not have enough columns</param>
        /// <returns>String value</returns>
        public static string GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            string valueIfMissing = "")
        {
            var value = GetColumnValue(resultRow, columnMap, columnIdentifier, valueIfMissing, out _);
            return value;
        }

        /// <summary>
        /// Get the string value for the specified column (of type T)
        /// Returns valueIfMissing if the column is not found
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnIdentifier">Column name or enum</param>
        /// <param name="valueIfMissing">Value to return if the column identifier is invalid or if resultRow does not have enough columns</param>
        /// <param name="validColumn">Output: True if the column identifier is valid and if resultRow has enough columns</param>
        /// <returns>String value</returns>
        public static string GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            string valueIfMissing,
            out bool validColumn)
        {
            var columnIndex = GetColumnIndex(columnMap, columnIdentifier);

            if (columnIndex < 0 || columnIndex >= resultRow.Count)
            {
                if (GetColumnValueThrowExceptions)
                {
                    var exceptionMessage = GetInvalidColumnNameExceptionMessage(columnIndex, columnIdentifier);
                    throw new Exception(exceptionMessage);
                }

                validColumn = false;
                return valueIfMissing;
            }

            validColumn = true;
            var value = resultRow[columnIndex];

            return value;
        }

        /// <summary>
        /// Get the string value for the specified column (of type T)
        /// Returns valueIfMissing if the column is not found
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnIdentifier">Column name or enum</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="validNumber">Output: True if the column identifier is valid, the resultRow has enough columns, and the value is an integer</param>
        /// <returns>Integer value</returns>
        public static int GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            int defaultValue,
            out bool validNumber)
        {
            var valueText = GetColumnValue(resultRow, columnMap, columnIdentifier, string.Empty, out var validColumn);

            if (!validColumn)
            {
                validNumber = false;
                return defaultValue;
            }

            if (int.TryParse(valueText, out var value))
            {
                validNumber = true;
                return value;
            }

            validNumber = false;
            return defaultValue;
        }

        /// <summary>
        /// Get the string value for the specified column (of type T)
        /// Returns valueIfMissing if the column is not found
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnIdentifier">Column name or enum</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="validNumber">Output: True if the column identifier is valid, the resultRow has enough columns, and the value is a double</param>
        /// <returns>Double value</returns>
        public static double GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            double defaultValue,
            out bool validNumber)
        {
            var valueText = GetColumnValue(resultRow, columnMap, columnIdentifier, string.Empty, out var validColumn);

            if (!validColumn)
            {
                validNumber = false;
                return defaultValue;
            }

            if (double.TryParse(valueText, out var value))
            {
                validNumber = true;
                return value;
            }

            validNumber = false;
            return defaultValue;
        }

        /// <summary>
        /// Get the string value for the specified column (of type T)
        /// Returns valueIfMissing if the column is not found
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnIdentifier">Column name or enum</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="validNumber">Output: True if the column identifier is valid, the resultRow has enough columns, and the value is DateTime</param>
        /// <returns>DateTime value</returns>
        public static DateTime GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            DateTime defaultValue,
            out bool validNumber)
        {
            var valueText = GetColumnValue(resultRow, columnMap, columnIdentifier, string.Empty, out var validColumn);

            if (!validColumn)
            {
                validNumber = false;
                return defaultValue;
            }

            if (DateTime.TryParse(valueText, out var value))
            {
                validNumber = true;
                return value;
            }

            validNumber = false;
            return defaultValue;
        }

        /// <summary>
        /// Get the integer value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        public static int GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            int defaultValue)
        {
            return GetColumnValue(resultRow, columnMap, columnIdentifier, defaultValue, out _);
        }

        /// <summary>
        /// Get the double value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        public static double GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            double defaultValue)
        {
            return GetColumnValue(resultRow, columnMap, columnIdentifier, defaultValue, out _);
        }

        /// <summary>
        /// Get the date value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        public static DateTime GetColumnValue<T>(
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<T, int> columnMap,
            T columnIdentifier,
            DateTime defaultValue)
        {
            return GetColumnValue(resultRow, columnMap, columnIdentifier, defaultValue, out _);
        }

        /// <summary>
        /// Return a string of the expected column names in a header line for a tab-delimited text file
        /// The order of the columns will be based on the default sort of the identifier data type, T
        /// When T is an enum, the sort order will be by the integer value of each enum
        /// </summary>
        /// <remarks>If an identifier in columnNamesByIdentifier has multiple supported column names, uses the first one in the SortedSet</remarks>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnNamesByIdentifier">Column names, by identifier</param>
        /// <param name="columnDelimiter">Column delimiter, by default a tab</param>
        /// <returns>Delimited list of column names</returns>
        public static string GetExpectedHeaderLine<T>(
            IReadOnlyDictionary<T, SortedSet<string>> columnNamesByIdentifier,
            string columnDelimiter = "\t")
        {
            var columnIdentifierList = (from item in columnNamesByIdentifier.Keys orderby item select item).ToList();

            return GetExpectedHeaderLine(columnNamesByIdentifier, columnIdentifierList, columnDelimiter);
        }

        /// <summary>
        /// Return a string of the expected column names in a header line for a tab-delimited text file
        /// </summary>
        /// <remarks>If an identifier in columnNamesByIdentifier has multiple supported column names, uses the first one in the SortedSet</remarks>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnNamesByIdentifier">Column names, by identifier</param>
        /// <param name="columnIdentifierList">Ordered list of column identifiers (typically a string or an enum)</param>
        /// <param name="columnDelimiter">Column delimiter, by default a tab</param>
        /// <returns>Delimited list of column names</returns>
        public static string GetExpectedHeaderLine<T>(
            IReadOnlyDictionary<T, SortedSet<string>> columnNamesByIdentifier,
            IEnumerable<T> columnIdentifierList,
            string columnDelimiter = "\t")
        {
            var headerColumnNames = new List<string>();

            foreach (var identifier in columnIdentifierList)
            {
                if (!columnNamesByIdentifier.ContainsKey(identifier))
                {
                    throw new Exception("Error in GetExpectedHeaderLine; columnNamesByIdentifier does not contain enum " + identifier);
                }

                headerColumnNames.Add(columnNamesByIdentifier[identifier].First());
            }

            if (headerColumnNames.Count == 0 && columnNamesByIdentifier.Count > 0)
            {
                // columnIdentifierList was empty
                // Return the headers using the default order
                return GetExpectedHeaderLine(columnNamesByIdentifier, columnDelimiter);
            }

            return string.Join(columnDelimiter, headerColumnNames);
        }

        /// <summary>
        /// Get the error message for either an invalid column name or not enough columns
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnIndex"></param>
        /// <param name="columnIdentifier"></param>
        public static string GetInvalidColumnNameExceptionMessage<T>(int columnIndex, T columnIdentifier)
        {
            string errorReason;

            if (columnIndex < 0)
                errorReason = "invalid column name";
            else
                errorReason = "not enough columns in resultRow (index out of range)";

            return string.Format("Cannot retrieve value for column {0}; {1}", columnIdentifier, errorReason);
        }

        /// <summary>
        /// Get the error message for either an invalid column name or not enough columns
        /// </summary>
        /// <typeparam name="T">Column identifier type (typically string or an enum)</typeparam>
        /// <param name="columnMap"></param>
        /// <param name="columnIdentifier"></param>
        public static string GetInvalidColumnNameExceptionMessage<T>(IReadOnlyDictionary<T, int> columnMap, T columnIdentifier)
        {
            string errorReason;

            if (!columnMap.ContainsKey(columnIdentifier))
                errorReason = "invalid column name";
            else
                errorReason = "not enough columns in resultRow (index out of range)";

            return string.Format("Cannot retrieve value for column {0}; {1}", columnIdentifier, errorReason);
        }
    }
}
