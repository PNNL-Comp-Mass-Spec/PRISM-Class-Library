using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PRISM
{
    /// <summary>
    /// This class produces an easier-to read stack trace for an exception
    /// See the descriptions for functions GetExceptionStackTrace and
    /// GetExceptionStackTraceMultiLine for example text
    /// </summary>
    /// <remarks></remarks>
    public class clsStackTraceFormatter
    {

        public const string STACK_TRACE_TITLE = "Stack trace: ";
        public const string STACK_CHAIN_SEPARATOR = "-:-";

        public const string FINAL_FILE_PREFIX = " in ";

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a compact description of the current stack
        /// </summary>
        /// <param name="objException"></param>
        /// <returns>
        /// String of the form:
        /// "Stack trace: clsCodeTest.Test-:-clsCodeTest.TestException-:-clsCodeTest.InnerTestException in clsCodeTest.vb:line 86"
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTrace(Exception objException)
        {

            var stackTraceData = GetExceptionStackTraceData(objException).ToList();

            var sbStackTrace = new StringBuilder();
            for (var index = 0; index <= stackTraceData.Count - 1; index++) {
                if (index == stackTraceData.Count - 1 && stackTraceData[index].StartsWith(FINAL_FILE_PREFIX)) {
                    sbStackTrace.Append(stackTraceData[index]);
                    break;
                }

                if (index == 0) {
                    sbStackTrace.Append(STACK_TRACE_TITLE + stackTraceData[index]);
                } else {
                    sbStackTrace.Append(STACK_CHAIN_SEPARATOR + stackTraceData[index]);
                }
            }

            return sbStackTrace.ToString();
        }

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a cleaned up description of the current stack,
        /// with one line for each function in the call tree
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>
        /// Stack trace:
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTraceMultiLine(Exception ex)
        {

            var stackTraceData = GetExceptionStackTraceData(ex);

            var sbStackTrace = new StringBuilder();
            sbStackTrace.AppendLine(STACK_TRACE_TITLE);

            foreach (var traceItem in stackTraceData)
            {
                sbStackTrace.AppendLine("  " + traceItem);
            }

            return sbStackTrace.ToString();

        }

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a cleaned up description of the current stack
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>
        /// List of function names; for example:
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks></remarks>
        public static IEnumerable<string> GetExceptionStackTraceData(Exception ex)
        {
            return GetExceptionStackTraceData(ex.StackTrace);
        }

        /// <summary>
        /// Parses the given StackTrace text to return a cleaned up description of the current stack
        /// </summary>
        /// <param name="stackTraceText">Exception.StackTrace data</param>
        /// <returns>
        /// List of function names; for example:
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks></remarks>
        public static IEnumerable<string> GetExceptionStackTraceData(string stackTraceText)
        {
            const string REGEX_FUNCTION_NAME = @"at ([^(]+)\(";
            const string REGEX_FILE_NAME = @"in .+\\(.+)";

            const string CODE_LINE_PREFIX = ":line ";
            const string REGEX_LINE_IN_CODE = CODE_LINE_PREFIX + "\\d+";

            var lstFunctions = new List<string>();
            var strFinalFile = string.Empty;

            var reFunctionName = new Regex(REGEX_FUNCTION_NAME, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var reFileName = new Regex(REGEX_FILE_NAME, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var reLineInCode = new Regex(REGEX_LINE_IN_CODE, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            if (string.IsNullOrWhiteSpace(stackTraceText)) {
                var emptyStackTrace = new List<string> {
                    "Empty stack trace"
                };
                return emptyStackTrace;
            }

            // Process each line in objException.StackTrace
            // Populate strFunctions() with the function name of each line
            using (var trTextReader = new StringReader(stackTraceText)) {

                while (trTextReader.Peek() > -1) {
                    var strLine = trTextReader.ReadLine();

                    if (string.IsNullOrEmpty(strLine)) continue;

                    var strCurrentFunction = string.Empty;

                    var functionMatch = reFunctionName.Match(strLine);
                    var lineMatch = reLineInCode.Match(strLine);

                    // Also extract the file name where the Exception occurred
                    var fileMatch = reFileName.Match(strLine);
                    string currentFunctionFile;

                    if (fileMatch.Success) {
                        currentFunctionFile = fileMatch.Groups[1].Value;
                        if (strFinalFile.Length == 0) {
                            var lineMatchFinalFile = reLineInCode.Match(currentFunctionFile);
                            if (lineMatchFinalFile.Success) {
                                strFinalFile = currentFunctionFile.Substring(0, lineMatchFinalFile.Index);
                            } else {
                                strFinalFile = string.Copy(currentFunctionFile);
                            }
                        }
                    } else {
                        currentFunctionFile = string.Empty;
                    }

                    if (functionMatch.Success) {
                        strCurrentFunction = functionMatch.Groups[1].Value;
                    } else {
                        // Look for the word " in "
                        var intIndex = strLine.ToLower().IndexOf(" in ", StringComparison.Ordinal);
                        if (intIndex == 0) {
                            // " in" not found; look for the first space after startIndex 4
                            intIndex = strLine.IndexOf(" ", 4, StringComparison.Ordinal);
                        }

                        if (intIndex == 0) {
                            // Space not found; use the entire string
                            intIndex = strLine.Length - 1;
                        }

                        if (intIndex > 0) {
                            strCurrentFunction = strLine.Substring(0, intIndex);
                        }

                    }

                    var functionDescription = string.Copy(strCurrentFunction);

                    if (!string.IsNullOrEmpty(currentFunctionFile)) {
                        if (string.IsNullOrEmpty(strFinalFile) || !TrimLinePrefix(strFinalFile, CODE_LINE_PREFIX).Equals(TrimLinePrefix(currentFunctionFile, CODE_LINE_PREFIX), StringComparison.InvariantCulture)) {
                            functionDescription += FINAL_FILE_PREFIX + currentFunctionFile;
                        }
                    }

                    if (lineMatch.Success && !functionDescription.Contains(CODE_LINE_PREFIX)) {
                        functionDescription += lineMatch.Value;
                    }

                    lstFunctions.Add(functionDescription);
                }

            }

            var stackTraceData = new List<string>();
            stackTraceData.AddRange(lstFunctions);
            stackTraceData.Reverse();

            if (!string.IsNullOrWhiteSpace(strFinalFile)) {
                stackTraceData.Add(FINAL_FILE_PREFIX + strFinalFile);
            }

            return stackTraceData;

        }

        private static string TrimLinePrefix(string strFileDescription, string codeLinePrefix)
        {

            var matchIndex = strFileDescription.IndexOf(codeLinePrefix, StringComparison.Ordinal);
            if (matchIndex > 0) {
                return strFileDescription.Substring(0, matchIndex);
            }

            return strFileDescription;
        }

    }
}
