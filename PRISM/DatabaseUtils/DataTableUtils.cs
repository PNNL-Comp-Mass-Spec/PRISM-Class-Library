using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable UnusedMember.Global

namespace PRISM.DatabaseUtils
{
    /// <summary>
    /// Methods for appending columns to a data table
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [Obsolete("Use PRISMDatabaseUtils.DataTableUtils instead", true)]
    public static class DataTableUtils
    {
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
        private static bool AppendColumnToTable<T>(DataTable dataTable, string columnName, Type columnType, T defaultValue, bool isReadOnly, bool isUnique, bool autoIncrement = false)
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
    }
}
