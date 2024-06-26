﻿#if NETFRAMEWORK
using System;
#endif

namespace PRISMWin
{
    /// <summary>
    /// Methods for appending columns to a DataGrid table style
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public static class DataGridUtils
    {
        // Ignore Spelling: utils

        // Note: System.Windows.Forms.DataGrid is deprecated/gone in .NET 5.0, in favor of System.Windows.Forms.DataGridView
#if NETFRAMEWORK

        // ReSharper disable once UnusedMember.Global

        /// <summary>
        /// Append a column to a DataGrid table style
        /// </summary>
        /// <param name="tableStyle">Table style</param>
        /// <param name="mappingName">Mapping name</param>
        /// <param name="headerText">User-friendly column name</param>
        /// <param name="columnWidth">Column width</param>
        /// <param name="isReadOnly">True if readonly</param>
        /// <param name="isDateTime">When true, format the column as a date</param>
        /// <param name="decimalPlaces">
        /// If 0 or greater, a format string is constructed to show the specified number of decimal places
        /// (ignored if isDateTime is true)
        /// </param>
        public static void AppendColumnToTableStyle(
            System.Windows.Forms.DataGridTableStyle tableStyle,
            string mappingName,
            string headerText,
            int columnWidth = 75,
            bool isReadOnly = false,
            bool isDateTime = false,
            int decimalPlaces = -1)
        {
            var newColumn = new System.Windows.Forms.DataGridTextBoxColumn
            {
                MappingName = mappingName,
                HeaderText = headerText,
                Width = columnWidth,
                ReadOnly = isReadOnly
            };

            if (isDateTime)
            {
                newColumn.Format = "g";
            }
            else if (decimalPlaces >= 0)
            {
                newColumn.Format = "0.";
                for (var i = 0; i < decimalPlaces; i++)
                {
                    newColumn.Format += "0";
                }
            }

            tableStyle.GridColumnStyles.Add(newColumn);
        }

        // ReSharper disable once UnusedMember.Global

        /// <summary>
        /// Append a boolean column to a DataGrid table style
        /// </summary>
        /// <param name="tableStyle">Table style</param>
        /// <param name="mappingName">Mapping name</param>
        /// <param name="headerText">User-friendly column name</param>
        /// <param name="columnWidth">Column width</param>
        /// <param name="isReadOnly">True if readonly</param>
        /// <param name="sourceIsTrueFalse">
        /// True if the source data represents true and false using boolean values
        /// False if the source data represents true and false using 1 and 0
        /// </param>
        public static void AppendBoolColumnToTableStyle(
            System.Windows.Forms.DataGridTableStyle tableStyle,
            string mappingName,
            string headerText,
            int columnWidth = 75,
            bool isReadOnly = false,
            bool sourceIsTrueFalse = true)
        {
            var newColumn = new System.Windows.Forms.DataGridBoolColumn
            {
                MappingName = mappingName,
                HeaderText = headerText,
                Width = columnWidth,
                ReadOnly = isReadOnly
            };

            if (sourceIsTrueFalse)
            {
                newColumn.FalseValue = false;
                newColumn.TrueValue = true;
            }
            else
            {
                newColumn.FalseValue = 0;
                newColumn.TrueValue = 1;
            }

            newColumn.AllowNull = false;
            newColumn.NullValue = Convert.DBNull;

            tableStyle.GridColumnStyles.Add(newColumn);
        }
#endif
    }
}
