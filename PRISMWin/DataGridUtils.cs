using System;
using System.Diagnostics.CodeAnalysis;

namespace PRISMWin
{
    /// <summary>
    /// Methods for appending columns to a DataGrid table style
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class DataGridUtils
    {
        /// <summary>
        /// Append a column to a DataGrid table style
        /// </summary>
        /// <param name="tableStyle"></param>
        /// <param name="mappingName"></param>
        /// <param name="headerText">User-friendly column name</param>
        /// <param name="columnWidth"></param>
        /// <param name="isReadOnly"></param>
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

        /// <summary>
        /// Append a boolean column to a DataGrid table style
        /// </summary>
        /// <param name="tableStyle"></param>
        /// <param name="mappingName"></param>
        /// <param name="headerText">User-friendly column name</param>
        /// <param name="columnWidth"></param>
        /// <param name="isReadOnly"></param>
        /// <param name="sourceIsTrueFalse">
        /// True if the source data represents true and false using a bool.
        /// False if the source data represents true and false using 1 and 0.
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
    }
}
