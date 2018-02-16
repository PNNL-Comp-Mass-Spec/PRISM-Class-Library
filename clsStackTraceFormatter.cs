using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        /// <summary>
        /// Stack trace label string
        /// </summary>
        public const string STACK_TRACE_TITLE = "Stack trace: ";

        /// <summary>
        /// String interpolated between parts of the stack trace
        /// </summary>
        public const string STACK_CHAIN_SEPARATOR = "-:-";

        /// <summary>
        /// Prefix added before the final file is listed in the stacktrace
        /// </summary>
        public const string FINAL_FILE_PREFIX = " in ";

        /// <summary>
        /// Get a string listing the methods leading to the calling method
        /// </summary>
        /// <returns>
        /// String of the form:
        /// Stack trace: TestApp.exe.InnerMethod-:-TestApp.exe.TestMethod-:-TestApp.exe.Main
        /// </returns>
        public static string GetCurrentStackTrace()
        {
            var parentMethods = GetStackTraceMethods(includeParamTypes: false);

            return STACK_TRACE_TITLE + string.Join(STACK_CHAIN_SEPARATOR, parentMethods);
        }

        /// <summary>
        /// Get a multiline string listing the methods leading to the calling method
        /// </summary>
        /// <returns>
        /// String of the form:
        /// Stack trace:
        ///     TestApp.exe: Bool InnerMethod(string, int ByRef)
        ///     TestApp.exe: Void TestMethod()
        ///     TestApp.exe: Int32 Main()
        /// </returns>
        public static string GetCurrentStackTraceMultiLine()
        {
            var parentMethods = GetStackTraceMethods(includeParamTypes: true);

            var stackTraceLines = new List<string> {
                STACK_TRACE_TITLE
            };

            foreach (var methodName in parentMethods)
            {
                stackTraceLines.Add("    " + methodName);
            }

            return string.Join("\n", stackTraceLines);
        }

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a compact description of the current stack
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <param name="includeInnerExceptionMessages">When true, also append details of any inner exceptions</param>
        /// <returns>
        /// String of the form:
        /// Stack trace: clsCodeTest.Test-:-clsCodeTest.TestException-:-clsCodeTest.InnerTestException in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTrace(Exception ex, bool includeInnerExceptionMessages = true)
        {

            var stackTraceData = GetExceptionStackTraceData(ex).ToList();

            var stackTraceLines = new List<string>();

            for (var index = 0; index <= stackTraceData.Count - 1; index++)
            {
                if (index == stackTraceData.Count - 1 && stackTraceData[index].StartsWith(FINAL_FILE_PREFIX))
                {
                    stackTraceLines.Add(stackTraceData[index]);
                    break;
                }

                if (index == 0)
                {
                    stackTraceLines.Add(STACK_TRACE_TITLE + stackTraceData[index]);
                }
                else
                {
                    stackTraceLines.Add(STACK_CHAIN_SEPARATOR + stackTraceData[index]);
                }
            }

            if (!includeInnerExceptionMessages || ex.InnerException == null)
                return string.Join("", stackTraceLines);

            AppendInnerExceptions(ex, stackTraceLines, STACK_CHAIN_SEPARATOR);

            return string.Join("", stackTraceLines);

        }

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a cleaned up description of the current stack,
        /// with one line for each function in the call tree
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <param name="includeInnerExceptionMessages">When true, also append details of any inner exceptions</param>
        /// <param name="includeMethodParams">When true, also include the parameters of each method</param>
        /// <returns>
        /// Stack trace:
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTraceMultiLine(
            Exception ex,
            bool includeInnerExceptionMessages = true,
            bool includeMethodParams = false)
        {

            var stackTraceData = GetExceptionStackTraceData(ex, includeMethodParams);

            var stackTraceLines = new List<string> {
                STACK_TRACE_TITLE
            };


            foreach (var traceItem in stackTraceData)
            {
                stackTraceLines.Add("  " + traceItem);
            }

            if (!includeInnerExceptionMessages || ex.InnerException == null)
                return string.Join("\n", stackTraceLines);

            AppendInnerExceptions(ex, stackTraceLines, string.Empty);

            return string.Join("\n", stackTraceLines);

        }

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a cleaned up description of the current stack
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <param name="includeMethodParams">When true, also include the parameters of each method</param>
        /// <returns>
        /// List of function names; for example:
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks></remarks>
        public static IEnumerable<string> GetExceptionStackTraceData(Exception ex, bool includeMethodParams = false)
        {
            return GetExceptionStackTraceData(ex.StackTrace, includeMethodParams);
        }

        /// <summary>
        /// Parses the given StackTrace text to return a cleaned up description of the current stack
        /// </summary>
        /// <param name="stackTraceText">Exception.StackTrace data</param>
        /// <param name="includeMethodParams">When true, also include the parameters of each method</param>
        /// <returns>
        /// List of function names; for example:
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks></remarks>
        public static IEnumerable<string> GetExceptionStackTraceData(string stackTraceText, bool includeMethodParams = false)
        {

            const string CODE_LINE_PREFIX = ":line ";
            const string REGEX_LINE_IN_CODE = CODE_LINE_PREFIX + "\\d+";

            var methods = new List<string>();
            var finalFile = string.Empty;

            var reMethodInfo = new Regex(@"at (?<MethodName>[^(]+)\((?<MethodArgs>[^)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var reFileName = new Regex(@"in .+\\(?<FileName>.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var reLineInCode = new Regex(REGEX_LINE_IN_CODE, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            if (string.IsNullOrWhiteSpace(stackTraceText))
            {
                var emptyStackTrace = new List<string> {
                    "Empty stack trace"
                };
                return emptyStackTrace;
            }

            // Process each line in the exception stack track
            // Populate methods with the method name of each line, optionally including method arguments
            using (var reader = new StringReader(stackTraceText))
            {

                while (reader.Peek() > -1)
                {
                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrEmpty(dataLine))
                        continue;

                    string currentMethod;
                    string currentMethodArgs;

                    var methodMatch = reMethodInfo.Match(dataLine);
                    var lineMatch = reLineInCode.Match(dataLine);

                    // Also extract the file name where the Exception occurred
                    var fileMatch = reFileName.Match(dataLine);
                    string currentMethodFile;

                    if (fileMatch.Success)
                    {
                        currentMethodFile = fileMatch.Groups["FileName"].Value;
                        if (finalFile.Length == 0)
                        {
                            var lineMatchFinalFile = reLineInCode.Match(currentMethodFile);
                            if (lineMatchFinalFile.Success)
                            {
                                finalFile = currentMethodFile.Substring(0, lineMatchFinalFile.Index);
                            }
                            else
                            {
                                finalFile = currentMethodFile;
                            }
                        }
                    }
                    else
                    {
                        currentMethodFile = string.Empty;
                    }

                    if (methodMatch.Success)
                    {
                        currentMethod = methodMatch.Groups["MethodName"].Value;
                        currentMethodArgs = methodMatch.Groups["MethodArgs"].Value;
                    }
                    else
                    {
                        // Look for the word " in "
                        var charIndex = dataLine.ToLower().IndexOf(" in ", StringComparison.Ordinal);
                        if (charIndex == 0)
                        {
                            // " in" not found; look for the first space after startIndex 4
                            charIndex = dataLine.IndexOf(" ", 4, StringComparison.Ordinal);
                        }

                        if (charIndex == 0)
                        {
                            // Space not found; use the entire string
                            charIndex = dataLine.Length;
                        }

                        if (charIndex > 0)
                        {
                            if (includeMethodParams)
                            {
                                currentMethod = dataLine.Substring(0, charIndex);
                            }
                            else
                            {
                                var openParenthIndex = dataLine.IndexOf("(", StringComparison.Ordinal);
                                if (openParenthIndex > 0)
                                    currentMethod = dataLine.Substring(0, Math.Min(openParenthIndex, charIndex));
                                else
                                    currentMethod = dataLine.Substring(0, charIndex);
                            }

                            currentMethodArgs = string.Empty;
                        }
                        else
                        {
                            currentMethod = string.Empty;
                            currentMethodArgs = string.Empty;
                        }

                    }

                    string methodDescription;

                    if (includeMethodParams && !string.IsNullOrWhiteSpace(currentMethodArgs))
                        methodDescription = currentMethod + "(" + currentMethodArgs + ")";
                    else
                        methodDescription = currentMethod;

                    if (!string.IsNullOrEmpty(currentMethodFile))
                    {
                        if (string.IsNullOrEmpty(finalFile) ||
                            !TrimLinePrefix(finalFile, CODE_LINE_PREFIX).Equals(
                                TrimLinePrefix(currentMethodFile, CODE_LINE_PREFIX), StringComparison.OrdinalIgnoreCase))
                        {
                            methodDescription += FINAL_FILE_PREFIX + currentMethodFile;
                        }
                    }

                    if (lineMatch.Success && !methodDescription.Contains(CODE_LINE_PREFIX))
                    {
                        methodDescription += lineMatch.Value;
                    }

                    methods.Add(methodDescription);
                }

            }

            var stackTraceData = new List<string>();
            stackTraceData.AddRange(methods);
            stackTraceData.Reverse();

            if (!string.IsNullOrWhiteSpace(finalFile))
            {
                stackTraceData.Add(FINAL_FILE_PREFIX + finalFile);
            }

            return stackTraceData;

        }

        private static void AppendInnerExceptions(Exception ex, ICollection<string> stackTraceLines, string messagePrefix)
        {

            var innerException = ex.InnerException;
            while (innerException != null)
            {
                var skipMessage = false;

                foreach (var item in stackTraceLines)
                {
                    if (item.Contains(innerException.Message))
                    {
                        skipMessage = true;
                        break;
                    }
                }

                if (skipMessage)
                    continue;

                stackTraceLines.Add(messagePrefix + innerException.Message);
                innerException = innerException.InnerException;
            }
        }

        private static IEnumerable<string> GetStackTraceMethods(bool includeParamTypes, int levelsToIgnore = 2)
        {
            var stackTrace = new StackTrace();

            var parentMethods = new List<string>();
            for (var i = levelsToIgnore; i < stackTrace.FrameCount; i++)
            {
                var parentMethod = stackTrace.GetFrame(i).GetMethod();
                if (includeParamTypes)
                    parentMethods.Add(parentMethod.Module + ": " + parentMethod);
                else
                    parentMethods.Add(parentMethod.Module + "." + parentMethod.Name);
            }

            return parentMethods;
        }

        private static string TrimLinePrefix(string fileDescription, string codeLinePrefix)
        {

            var matchIndex = fileDescription.IndexOf(codeLinePrefix, StringComparison.Ordinal);
            if (matchIndex > 0)
            {
                return fileDescription.Substring(0, matchIndex);
            }

            return fileDescription;
        }

    }
}
