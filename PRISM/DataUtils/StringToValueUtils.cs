using System;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable UnusedMember.Global

namespace PRISM.DataUtils
{
    /// <summary>
    /// Utilities for parsing values from strings
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class StringToValueUtils
    {
        /// <summary>
        /// Converts a string value of True or False to a boolean equivalent
        /// </summary>
        /// <remarks>Returns false if unable to convert</remarks>
        /// <param name="value"></param>
        /// <param name="defaultValue">Boolean value to return if value is empty or cannot be converted</param>
        public static bool CBoolSafe(string value, bool defaultValue = false)
        {
            try
            {
                if (bool.TryParse(value, out var parsedValue))
                    return parsedValue;
            }
            catch (Exception)
            {
                // ignored
            }

            return defaultValue;
        }

        /// <summary>
        /// Converts value to an integer
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue">Double to return if value is not numeric</param>
        public static double CDoubleSafe(string value, double defaultValue)
        {
            try
            {
                if (double.TryParse(value, out var parsedValue))
                    return parsedValue;
            }
            catch (Exception)
            {
                // ignored
            }

            return defaultValue;
        }

        /// <summary>
        /// Converts value to a float
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue">Float to return if value is not numeric</param>
        public static float CFloatSafe(string value, float defaultValue)
        {
            try
            {
                if (float.TryParse(value, out var parsedValue))
                    return parsedValue;
            }
            catch (Exception)
            {
                // ignored
            }

            return defaultValue;
        }

        /// <summary>
        /// Converts value to an integer
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue">Integer to return if value is not numeric</param>
        public static int CIntSafe(string value, int defaultValue)
        {
            try
            {
                if (int.TryParse(value, out var parsedValue))
                    return parsedValue;
            }
            catch (Exception)
            {
                // ignored
            }

            return defaultValue;
        }

        /// <summary>
        /// Converts value to a short integer
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defaultValue">Short to return if value is not numeric</param>
        public static short CShortSafe(string value, short defaultValue)
        {
            try
            {
                if (short.TryParse(value, out var parsedValue))
                    return parsedValue;
            }
            catch (Exception)
            {
                // ignored
            }

            return defaultValue;
        }

        /// <summary>
        /// Check whether a string can be converted to a double
        /// </summary>
        /// <param name="value"></param>
        /// <returns>True if successful, otherwise false</returns>
        public static bool IsNumber(string value)
        {
            try
            {
                return double.TryParse(value, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Tries to retrieve the string value at index columnIndex in dataColumns[]
        /// </summary>
        /// <param name="dataColumns">Array of strings</param>
        /// <param name="columnIndex">Column index</param>
        /// <param name="value">Output: string in the given column; empty string if columnIndex is out of range</param>
        /// <returns>
        /// True if success; False if columnIndex is less than 0 or columnIndex is out of range for the dataColumns array
        /// </returns>
        public static bool TryGetValue(string[] dataColumns, int columnIndex, out string value)
        {
            if (columnIndex >= 0 && columnIndex < dataColumns.Length)
            {
                value = dataColumns[columnIndex];
                if (string.IsNullOrEmpty(value))
                    value = string.Empty;
                return true;
            }

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// Tries to convert the text at index columnIndex of dataColumns[] to an integer
        /// </summary>
        /// <param name="dataColumns">Array of strings</param>
        /// <param name="columnIndex">Column index</param>
        /// <param name="value">Output: integer in the given column; 0 if columnIndex is out of range or cannot be converted to an integer</param>
        /// <returns>
        /// True if success; False if columnIndex is less than 0, columnIndex is out of range for the dataColumns array,
        /// or the text cannot be converted to an integer
        /// </returns>
        public static bool TryGetValueInt(string[] dataColumns, int columnIndex, out int value)
        {
            if (columnIndex >= 0 && columnIndex < dataColumns.Length)
            {
                if (int.TryParse(dataColumns[columnIndex], out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Tries to convert the text at index columnIndex of dataColumns[] to a float
        /// </summary>
        /// <param name="dataColumns">Array of strings</param>
        /// <param name="columnIndex">Column index</param>
        /// <param name="value">Output: float in the given column; 0 if columnIndex is out of range or cannot be converted to a float</param>
        /// <returns>
        /// True if success; False if columnIndex is less than 0, columnIndex is out of range for the dataColumns array,
        /// or the text cannot be converted to a float
        /// </returns>
        public static bool TryGetValueFloat(string[] dataColumns, int columnIndex, out float value)
        {
            if (columnIndex >= 0 && columnIndex < dataColumns.Length)
            {
                if (float.TryParse(dataColumns[columnIndex], out value))
                {
                    return true;
                }
            }

            value = 0;
            return false;
        }
    }
}
