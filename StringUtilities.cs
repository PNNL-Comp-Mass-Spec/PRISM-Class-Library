using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PRISM
{
    public class StringUtilities
    {
        private const string SCIENTIFIC_NOTATION_CLEANUP_REGEX = "0+E";
        private static readonly Regex m_scientificNotationTrim = new Regex(SCIENTIFIC_NOTATION_CLEANUP_REGEX, RegexOptions.Compiled);

        /// <summary>
        /// Dictionary that tracks the format string used for each digitsOfPrecision value
        /// </summary>
        /// <remarks>
        /// Keys are the number of digits of precision
        /// Values are strings like "0.0", "0.0#", "0.0##", etc.
        /// </remarks>
        private static readonly ConcurrentDictionary<int, string> mFormatStrings = new ConcurrentDictionary<int, string>();

        /// <summary>
        /// Dictionary that tracks the format string used for each digitsOfPrecision value displayed with scientific notation
        /// </summary>
        /// <remarks>
        /// Keys are the number of digits of precision and
        ///   "false" if the format string is of the form 0.00E+00 or
        ///   "true"  if the format string is of the form 0.00E+000
        /// Values are strings like "0.0E+00", "0.0#E+00", "0.0##E+00", "0.0#E+000", or "0.0##E+000"
        /// </remarks>
        private static readonly ConcurrentDictionary<string, string> mFormatStringsScientific = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Get the format string for the given number of digits after the decimal
        /// </summary>
        /// <param name="digitsAfterDecimal"></param>
        /// <returns>Strings like "0.0", "0.0#", "0.0##"</returns>
        private static string GetFormatString(int digitsAfterDecimal)
        {
            try
            {
                if (mFormatStrings.TryGetValue(digitsAfterDecimal, out var formatString))
                {
                    return formatString;
                }

                if (digitsAfterDecimal <= 1)
                {
                    var newFormatString = "0.0";
                    mFormatStrings.TryAdd(digitsAfterDecimal, newFormatString);
                    return newFormatString;
                }

                // Update format string to be of the form "0.0#######"
                var paddedFormatString = "0.0" + new string('#', digitsAfterDecimal - 1);

                mFormatStrings.TryAdd(digitsAfterDecimal, paddedFormatString);
                return paddedFormatString;
            }
            catch
            {
                return "0.0";
            }

        }

        /// <summary>
        /// Get the format string for the given number of digits after the decimal
        /// </summary>
        /// <param name="value">Value being formatted</param>
        /// <param name="digitsAfterDecimal"></param>
        /// <returns>Strings like "0.0E+00", "0.0#E+00", "0.0##E+00", "0.0#E+000", or "0.0##E+000"</returns>
        private static string GetFormatStringScientific(double value, int digitsAfterDecimal)
        {
            try
            {
                if (digitsAfterDecimal < 1)
                    digitsAfterDecimal = 1;

                var tinyNumber = Math.Log10(Math.Abs(value)) <= -99;

                if (mFormatStringsScientific.TryGetValue(digitsAfterDecimal + tinyNumber.ToString(), out var formatString))
                {
                    return formatString;
                }

                var newFormatString = "0.0";

                if (digitsAfterDecimal > 1)
                {
                    // Update format string to be of the form "0.0#######"
                    newFormatString += new string('#', digitsAfterDecimal - 1);
                }

                if (tinyNumber)
                {
                    newFormatString += "E+000";
                }
                else
                {
                    newFormatString += "E+00";
                }

                mFormatStringsScientific.TryAdd(digitsAfterDecimal + tinyNumber.ToString(), newFormatString);
                return newFormatString;
            }
            catch
            {
                return "0.00E+00";
            }

        }

        /// <summary>
        /// Convert value to a string with 5 total digits of precision
        /// </summary>
        /// <param name="value">Number to convert to text</param>
        /// <returns>Number as text</returns>
        /// <remarks>Numbers larger than 1000000 or smaller than 0.000001 will be in scientific notation</remarks>
        public static string ValueToString(double value)
        {
            return ValueToString(value, 5, 1000000);
        }

        /// <summary>
        /// Convert value to a string with the specified total digits of precision
        /// </summary>
        /// <param name="value">Number to convert to text</param>
        /// <param name="digitsOfPrecision">Total digits of precision (before and after the decimal point)</param>
        /// <returns>Number as text</returns>
        /// <remarks>Numbers larger than 1000000 or smaller than 0.000001 will be in scientific notation</remarks>
        public static string ValueToString(double value, byte digitsOfPrecision)
        {
            return ValueToString(value, digitsOfPrecision, 1000000);
        }

        /// <summary>
        /// Convert value to a string with the specified total digits of precision and customized scientific notation threshold
        /// </summary>
        /// <param name="value">Number to convert to text</param>
        /// <param name="digitsOfPrecision">Total digits of precision (before and after the decimal point)</param>
        /// <param name="scientificNotationThreshold">
        /// Values larger than this threshold (positive or negative) will be converted to scientific notation
        /// Also, values less than "1 / scientificNotationThreshold" will be converted to scientific notation
        /// Thus, if this threshold is 1000000, numbers larger than 1000000 or smaller than 0.000001 will be in scientific notation
        /// </param>
        /// <returns>Number as text</returns>
        /// <remarks>This function differs from DblToString in that here digitsOfPrecision is the total digits while DblToString focuses on the number of digits after the decimal point</remarks>
        public static string ValueToString(
            double value,
            byte digitsOfPrecision,
            double scientificNotationThreshold)
        {
            byte totalDigitsOfPrecision;

            if (digitsOfPrecision < 1)
                totalDigitsOfPrecision = 1;
            else
                totalDigitsOfPrecision = digitsOfPrecision;

            double effectiveScientificNotationThreshold;
            if (Math.Abs(scientificNotationThreshold) < 10)
                effectiveScientificNotationThreshold = 10;
            else
                effectiveScientificNotationThreshold = Math.Abs(scientificNotationThreshold);

            try
            {
                var strMantissa = GetFormatStringScientific(value, totalDigitsOfPrecision - 1);
                string strValue;

                if (Math.Abs(value) < double.Epsilon)
                {
                    return "0";
                }

                if (Math.Abs(value) <= 1 / effectiveScientificNotationThreshold ||
                    Math.Abs(value) >= effectiveScientificNotationThreshold)
                {
                    // Use scientific notation
                    strValue = value.ToString(strMantissa);
                }
                else if (Math.Abs(value) < 1)
                {
                    var digitsAfterDecimal = (int)Math.Floor(-Math.Log10(Math.Abs(value))) + totalDigitsOfPrecision;
                    var strFormatString = GetFormatString(digitsAfterDecimal);


                    strValue = value.ToString(strFormatString);
                    if (Math.Abs(double.Parse(strValue)) < double.Epsilon)
                    {
                        // Value was converted to 0; use scientific notation
                        strValue = value.ToString(strMantissa);
                    }
                    else
                    {
                        strValue = strValue.TrimEnd('0').TrimEnd('.');
                    }
                }
                else
                {
                    var digitsAfterDecimal = totalDigitsOfPrecision - (int)Math.Ceiling(Math.Log10(Math.Abs(value)));

                    if (digitsAfterDecimal > 0)
                    {
                        var strFormatString = GetFormatString(digitsAfterDecimal);
                        strValue = value.ToString(strFormatString);
                        strValue = strValue.TrimEnd('0').TrimEnd('.');
                    }
                    else
                    {
                        strValue = value.ToString("0");
                    }
                }

                if (totalDigitsOfPrecision <= 1)
                {
                    return strValue;
                }

                // Look for numbers in scientific notation with a series of zeroes before the E
                if (!m_scientificNotationTrim.IsMatch(strValue))
                {
                    return strValue;
                }

                // Match found, for example 1.5000E-43
                // Change it to instead be  1.5E-43

                var updatedValue = m_scientificNotationTrim.Replace(strValue, "E");

                // The number may now look like 1.E+43
                // If it does, then re-insert a zero after the decimal point
                return updatedValue.Replace(".E", ".0E");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in ValueToString: " + ex.Message);
                return value.ToString(CultureInfo.InvariantCulture);
            }

        }

    }
}
