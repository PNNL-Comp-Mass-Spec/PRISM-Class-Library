using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Some extension methods that are useful when reading from databases
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class DatabaseUtilsExtensions
    {

        /// <summary>
        /// When using GetColumnValue, if an exact match is not found and this is true,
        /// look for columnName matching ColumnNameX in TableName.ColumnNameX
        /// </summary>
        public static bool GetColumnIndexAllowColumnNameMatchOnly => DataTableUtils.GetColumnIndexAllowColumnNameMatchOnly;

        /// <summary>
        /// When using GetColumnValue, if an exact match is not found and this is true,
        /// look for columnName matching any part of a column name
        /// </summary>
        public static bool GetColumnIndexAllowFuzzyMatch => DataTableUtils.GetColumnIndexAllowFuzzyMatch;

        /// <summary>
        /// Simple conversion that handles DBNull for parsing database fields
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns>If value is DBNull, then returns default(t) (string.Empty for string); otherwise casts value to T</returns>
        /// <remarks>
        /// This method does not with VB.NET when Option Strict is enabled.
        /// As an alternative, use GetInteger, GetString, etc. (in this class)
        /// </remarks>
        public static T CastDBVal<T>(this object value)
        {
            if (value == null || value == DBNull.Value)
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)string.Empty;
                }

                // ReSharper disable once RedundantTypeSpecificationInDefaultExpression
                return default(T);
            }

            // Simplistic version, which only works if you don't need the string representation of a number
            // return (T)value;

            // More robust version:
            return (T)Convert.ChangeType(value, typeof(T));
        }

        /// <summary>
        /// Simple conversion that handles DBNull for parsing database fields
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="valueIfNull">The value to return if the value is null or DBNull.</param>
        public static T CastDBVal<T>(this object value, T valueIfNull)
        {
            if (value == null || value == DBNull.Value)
            {
                return valueIfNull;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }


        /// <summary>
        /// Converts an database field value to a string, checking for null values
        /// This is intended to be used with DataSet objects retrieved via a SqlDataAdapter
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="dbValue">Value from database</param>
        /// <returns>If dbValue is DBNull, returns "", otherwise returns the string representation of dbValue</returns>
        /// <remarks></remarks>
        public static string GetString(this IDBTools dbTools, object dbValue)
        {
            if (ReferenceEquals(dbValue, DBNull.Value))
            {
                return string.Empty;
            }

            return Convert.ToString(dbValue);
        }

        /// <summary>
        /// Converts an database field value to a float (single), checking for null values
        /// This is intended to be used with DataSet objects retrieved via a SqlDataAdapter
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="dbValue">Value from database</param>
        /// <returns>If dbValue is DBNull, returns 0.0, otherwise returns the string representation of dbValue</returns>
        /// <remarks>An exception will be thrown if the value is not numeric</remarks>
        public static float GetFloat(this IDBTools dbTools, object dbValue)
        {
            if (ReferenceEquals(dbValue, DBNull.Value))
            {
                return (float)0.0;
            }

            return Convert.ToSingle(dbValue);
        }

        /// <summary>
        /// Converts an database field value to a double, checking for null values
        /// This is intended to be used with DataSet objects retrieved via a SqlDataAdapter
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="dbValue">Value from database</param>
        /// <returns>If dbValue is DBNull, returns 0.0, otherwise returns the string representation of dbValue</returns>
        /// <remarks>An exception will be thrown if the value is not numeric</remarks>
        public static double GetDouble(this IDBTools dbTools, object dbValue)
        {
            if (ReferenceEquals(dbValue, DBNull.Value))
            {
                return 0.0;
            }

            return Convert.ToDouble(dbValue);
        }

        /// <summary>
        /// Converts an database field value to an integer (Int32), checking for null values
        /// This is intended to be used with DataSet objects retrieved via a SqlDataAdapter
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="dbValue">Value from database</param>
        /// <returns>If dbValue is DBNull, returns 0, otherwise returns the string representation of dbValue</returns>
        /// <remarks>An exception will be thrown if the value is not numeric</remarks>
        public static int GetInteger(this IDBTools dbTools, object dbValue)
        {
            if (ReferenceEquals(dbValue, DBNull.Value))
            {
                return 0;
            }

            return Convert.ToInt32(dbValue);
        }

        /// <summary>
        /// Converts an database field value to a long integer (Int64), checking for null values
        /// This is intended to be used with DataSet objects retrieved via a SqlDataAdapter
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="dbValue">Value from database</param>
        /// <returns>If dbValue is DBNull, returns 0, otherwise returns the string representation of dbValue</returns>
        /// <remarks>An exception will be thrown if the value is not numeric</remarks>
        public static long GetLong(this IDBTools dbTools, object dbValue)
        {
            if (ReferenceEquals(dbValue, DBNull.Value))
            {
                return 0;
            }

            return Convert.ToInt64(dbValue);
        }

        /// <summary>
        /// Search the columnMap dictionary for the best match to columnName
        /// First looks for an exact match for columnName
        /// If no match, and if property GetColumnIndexAllowColumnNameMatchOnly is true, looks for a match after the last period seen in each name
        /// If still no match, and if property GetColumnIndexAllowFuzzyMatch is true, looks for a column that contains the desired column name
        /// </summary>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnName">Column name to find</param>
        /// <returns>The zero-based column index, or -1 if no match</returns>
        public static int GetColumnIndex(
            IReadOnlyDictionary<string, int> columnMap,
            string columnName)
        {
            return GetColumnIndex(columnMap, columnName, GetColumnIndexAllowColumnNameMatchOnly, GetColumnIndexAllowFuzzyMatch);
        }

        /// <summary>
        /// Search the columnMap dictionary for the best match to columnName
        /// First looks for an exact match for columnName
        /// If no match, and if allowColumnNameMatchOnly is true, looks for a match after the last period seen in each name
        /// If still no match, and if allowFuzzyMatch is true, looks for a column that contains the desired column name
        /// </summary>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnName">Column name to find</param>
        /// <param name="allowColumnNameMatchOnly">
        /// When true and an exact match is not found, look for columns in the dictionary matching
        /// the pattern Table.ColumnX, where ColumnX matches columnName
        /// </param>
        /// <param name="allowFuzzyMatch">
        /// When true and a clear match is not found, look for a column that contains the desired column
        /// </param>
        /// <returns>The zero-based column index, or -1 if no match</returns>
        public static int GetColumnIndex(
            IReadOnlyDictionary<string, int> columnMap,
            string columnName,
            bool allowColumnNameMatchOnly,
            bool allowFuzzyMatch = true)
        {
            if (columnMap.TryGetValue(columnName, out var columnIndex))
            {
                return columnIndex;
            }

            if (allowColumnNameMatchOnly)
            {
                var periodAndName = "." + columnName;
                foreach (var item in columnMap)
                {
                    if (item.Key.EndsWith(periodAndName, StringComparison.OrdinalIgnoreCase))
                        return item.Value;
                }
            }

            // ReSharper disable once InvertIf
            if (allowFuzzyMatch)
            {
                foreach (var item in columnMap)
                {
                    if (item.Key.IndexOf(columnName, StringComparison.OrdinalIgnoreCase) >= 0)
                        return item.Value;
                }
            }

            return -1;
        }

        /// <summary>
        /// Get a mapping from column name to column index, based on column order
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="columns"></param>
        /// <returns>Mapping from column name to column index</returns>
        /// <remarks>Use in conjunction with GetColumnValue, e.g. GetColumnValue(resultRow, columnMap, "ID")</remarks>
        public static Dictionary<string, int> GetColumnMapping(this IDBTools dbTools, IReadOnlyList<string> columns)
        {
            var columnMap = new Dictionary<string, int>();

            for (var i = 0; i < columns.Count; i++)
            {
                columnMap.Add(columns[i], i);
            }

            return columnMap;
        }

        /// <summary>
        /// Get the string value for the specified column
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnName">Column Name</param>
        /// <returns>String value</returns>
        /// <remarks>The returned value could be null, but note that GetQueryResults converts all Null strings to string.Empty</remarks>
        public static string GetColumnValue(
            this IDBTools dbTools,
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<string, int> columnMap,
            string columnName)
        {
            var columnIndex = GetColumnIndex(columnMap, columnName);
            if (columnIndex < 0)
                throw new Exception("Invalid column name: " + columnName);

            var value = resultRow[columnIndex];

            return value;
        }

        /// <summary>
        /// Get the integer value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        public static int GetColumnValue(
            this IDBTools dbTools,
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<string, int> columnMap,
            string columnName,
            int defaultValue)
        {
            return GetColumnValue(dbTools, resultRow, columnMap, columnName, defaultValue, out _);
        }

        /// <summary>
        /// Get the integer value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnName">Column Name</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="validNumber">Output: set to true if the column contains an integer</param>
        /// <returns>Integer value</returns>
        public static int GetColumnValue(
            this IDBTools dbTools,
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<string, int> columnMap,
            string columnName,
            int defaultValue,
            out bool validNumber)
        {
            var columnIndex = GetColumnIndex(columnMap, columnName);
            if (columnIndex < 0)
                throw new Exception("Invalid column name: " + columnName);

            var valueText = resultRow[columnIndex];

            if (int.TryParse(valueText, out var value))
            {
                validNumber = true;
                return value;
            }

            validNumber = false;
            return defaultValue;
        }

        /// <summary>
        /// Get the double value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        public static double GetColumnValue(
            this IDBTools dbTools,
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<string, int> columnMap,
            string columnName,
            double defaultValue)
        {
            return GetColumnValue(dbTools, resultRow, columnMap, columnName, defaultValue, out _);
        }

        /// <summary>
        /// Get the double value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnName">Column Name</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="validNumber">Output: set to true if the column contains a double (or integer)</param>
        /// <returns>Double value</returns>
        public static double GetColumnValue(
            this IDBTools dbTools,
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<string, int> columnMap,
            string columnName,
            double defaultValue,
            out bool validNumber)
        {
            var columnIndex = GetColumnIndex(columnMap, columnName);
            if (columnIndex < 0)
                throw new Exception("Invalid column name: " + columnName);

            var valueText = resultRow[columnIndex];

            if (double.TryParse(valueText, out var value))
            {
                validNumber = true;
                return value;
            }

            validNumber = false;
            return defaultValue;
        }

        /// <summary>
        /// Get the date value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        public static DateTime GetColumnValue(
            this IDBTools dbTools,
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<string, int> columnMap,
            string columnName,
            DateTime defaultValue)
        {
            return GetColumnValue(dbTools, resultRow, columnMap, columnName, defaultValue, out _);
        }

        /// <summary>
        /// Get the date value for the specified column, or the default value if the value is empty or non-numeric
        /// </summary>
        /// <param name="dbTools">Reference to dbTools so this can resolve as an extension method</param>
        /// <param name="resultRow">Row of results, as returned by GetQueryResults</param>
        /// <param name="columnMap">Map of column name to column index, as returned by GetColumnMapping</param>
        /// <param name="columnName">Column Name</param>
        /// <param name="defaultValue">Default value</param>
        /// <param name="validNumber">Output: set to true if the column contains a valid date</param>
        /// <returns>True or false</returns>
        public static DateTime GetColumnValue(
            this IDBTools dbTools,
            IReadOnlyList<string> resultRow,
            IReadOnlyDictionary<string, int> columnMap,
            string columnName,
            DateTime defaultValue,
            out bool validNumber)
        {
            var columnIndex = GetColumnIndex(columnMap, columnName);
            if (columnIndex < 0)
                throw new Exception("Invalid column name: " + columnName);

            var valueText = resultRow[columnIndex];

            if (DateTime.TryParse(valueText, out var value))
            {
                validNumber = true;
                return value;
            }

            validNumber = false;
            return defaultValue;
        }
    }
}
