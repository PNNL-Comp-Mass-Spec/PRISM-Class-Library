using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// This class includes methods to be used when displaying messages at the console while monitoring a class that inherits EventNotifier
    /// </summary>
    public static class ConsoleMsgUtils
    {
        private const string SEPARATOR = "------------------------------------------------------------------------------";

        private static bool mAutoCheckedDebugFontColor;

        private static readonly Regex mLeadingWhitespaceMatcher = new Regex("^ +");

        /// <summary>
        /// Debug message font color
        /// </summary>
        public static ConsoleColor DebugFontColor { get; set; } = ConsoleColor.DarkGray;

        /// <summary>
        /// Error message font color
        /// </summary>
        public static ConsoleColor ErrorFontColor { get; set; } = ConsoleColor.Red;

        /// <summary>
        /// Stack trace font color
        /// </summary>
        public static ConsoleColor StackTraceFontColor { get; set; } = ConsoleColor.Cyan;

        /// <summary>
        /// Warning message font color
        /// </summary>
        public static ConsoleColor WarningFontColor { get; set; } = ConsoleColor.Yellow;

        /// <summary>
        /// Call Console.WriteLine() the specified number of times
        /// </summary>
        /// <param name="emptyLineCount"></param>
        public static void ConsoleWriteEmptyLines(int emptyLineCount)
        {
            for (var i = 1; i <= emptyLineCount; i++)
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to Red)
        /// If an exception is included, the stack trace is shown using StackTraceFontColor
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="includeSeparator">When true, add a separator line before and after the error</param>
        /// <param name="writeToErrorStream">When true, also send the error to the the standard error stream</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        /// <returns>Error message, with the exception message appended, provided ex is not null and provided message does not end with ex.message</returns>
        public static string ShowError(string message, bool includeSeparator = true, bool writeToErrorStream = true, int emptyLinesBeforeMessage = 1)
        {
            return ShowError(message, null, includeSeparator, writeToErrorStream, emptyLinesBeforeMessage);
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to Red)
        /// If an exception is included, the stack trace is shown using StackTraceFontColor
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="ex">Exception (can be null)</param>
        /// <param name="includeSeparator">When true, add a separator line before and after the error</param>
        /// <param name="writeToErrorStream">When true, also send the error to the the standard error stream</param>
        /// <returns>Error message, with the exception message appended, provided ex is not null and provided message does not end with ex.message</returns>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        public static string ShowError(
            string message,
            Exception ex,
            bool includeSeparator = true,
            bool writeToErrorStream = true,
            int emptyLinesBeforeMessage = 1)
        {
            ConsoleWriteEmptyLines(emptyLinesBeforeMessage);

            if (includeSeparator)
            {
                Console.WriteLine(SEPARATOR);
            }

            string formattedError;
            if (ex == null || message.Contains(ex.Message))
            {
                formattedError = message;
            }
            else
            {
                formattedError = message + ": " + ex.Message;
            }

            Console.ForegroundColor = ErrorFontColor;
            Console.WriteLine(formattedError);

            if (ex != null)
            {
                Console.ForegroundColor = StackTraceFontColor;
                Console.WriteLine();
                Console.WriteLine(StackTraceFormatter.GetExceptionStackTraceMultiLine(ex));
            }

            Console.ResetColor();

            if (includeSeparator)
            {
                Console.WriteLine(SEPARATOR);
            }

            if (emptyLinesBeforeMessage > 0)
            {
                Console.WriteLine();
            }

            if (writeToErrorStream)
            {
                WriteToErrorStream(formattedError);
            }

            return formattedError;
        }

        /// <summary>
        /// Display a set of error messages at the console with color ErrorFontColor (defaults to Red)
        /// </summary>
        /// <param name="title">Title text to be shown before the errors; can be null or blank</param>
        /// <param name="errorMessages">Error messages to show</param>
        /// <param name="writeToErrorStream">When true, also send the error to the the standard error stream</param>
        /// <param name="indentChars">Characters to add before each error message; defaults to 3 spaces</param>
        /// <returns>The first error message</returns>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        // ReSharper disable once UnusedMember.Global
        public static string ShowErrors(
            string title,
            IEnumerable<string> errorMessages,
            bool writeToErrorStream = true,
            string indentChars = "  ",
            int emptyLinesBeforeMessage = 1)
        {
            string firstError = null;

            ConsoleWriteEmptyLines(emptyLinesBeforeMessage);
            Console.WriteLine(SEPARATOR);

            if (!string.IsNullOrWhiteSpace(title))
                Console.WriteLine(title);

            if (string.IsNullOrEmpty(indentChars))
                indentChars = "";

            foreach (var item in errorMessages)
            {
                if (firstError == null)
                    firstError = item;

                ShowError(indentChars + item, false, writeToErrorStream);
            }

            Console.WriteLine(SEPARATOR);
            if (emptyLinesBeforeMessage > 0)
            {
                Console.WriteLine();
            }

            return firstError;
        }

        /// <summary>
        /// Display a debug message at the console with color DebugFontColor (defaults to DarkGray)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="indentChars">Characters to use to indent the message</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        public static void ShowDebug(string message, string indentChars = "  ", int emptyLinesBeforeMessage = 1)
        {
            if (Path.DirectorySeparatorChar == '/' && !mAutoCheckedDebugFontColor)
            {
                // Running on Linux
                // Dark gray appears as black when using Putty; use blue instead
                if (DebugFontColor == ConsoleColor.DarkGray)
                {
                    DebugFontColor = ConsoleColor.Blue;
                }
                mAutoCheckedDebugFontColor = true;
            }

            ConsoleWriteEmptyLines(emptyLinesBeforeMessage);

            Console.ForegroundColor = DebugFontColor;
            if (string.IsNullOrEmpty(indentChars))
            {
                Console.WriteLine(indentChars + message);
            }
            else
            {
                Console.WriteLine(indentChars + message);
            }
            Console.ResetColor();
        }

        /// <summary>
        /// Display a warning message at the console with color WarningFontColor (defaults to Yellow)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        public static void ShowWarning(string message, int emptyLinesBeforeMessage = 1)
        {
            ConsoleWriteEmptyLines(emptyLinesBeforeMessage);
            Console.ForegroundColor = WarningFontColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Sleep for the specified number of seconds
        /// </summary>
        /// <param name="waitTimeSeconds"></param>
        /// <remarks>Sleeps for 10 second chunks until waitTimeSeconds has elapsed</remarks>
        public static void SleepSeconds(double waitTimeSeconds)
        {
            var endTime = DateTime.UtcNow.AddSeconds(waitTimeSeconds);
            while (endTime.Subtract(DateTime.UtcNow).TotalMilliseconds > 10)
            {
                var remainingSeconds = endTime.Subtract(DateTime.UtcNow).TotalSeconds;
                if (remainingSeconds > 10)
                {
                    ProgRunner.SleepMilliseconds(10000);
                }
                else
                {
                    var sleepTimeMsec = (int)Math.Ceiling(remainingSeconds * 1000);
                    ProgRunner.SleepMilliseconds(sleepTimeMsec);
                }
            }
        }

        /// <summary>
        /// Wraps the words in textToWrap to the set width (where possible)
        /// </summary>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph</returns>
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        public static string WrapParagraph(string textToWrap, int wrapWidth = 80)
        {
            var wrappedText = new StringBuilder();
            foreach (var line in WrapParagraphAsList(textToWrap, wrapWidth))
            {
                if (wrappedText.Length > 0)
                    wrappedText.AppendLine();

                wrappedText.Append(line);
            }

            return wrappedText.ToString();
        }

        /// <summary>
        /// Wraps the words in textToWrap to the set width (where possible)
        /// </summary>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph as a list of strings</returns>
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        public static List<string> WrapParagraphAsList(string textToWrap, int wrapWidth)
        {
            // Check for newline characters
            var textLinesToWrap = textToWrap.Split('\r', '\n');

            // List of wrapped text lines
            var wrappedText = new List<string>();

            foreach (var lineToWrap in textLinesToWrap)
            {
                var reMatch = mLeadingWhitespaceMatcher.Match(lineToWrap);

                var leadingWhitespace = reMatch.Success ? reMatch.Value : string.Empty;

                var split = lineToWrap.Split(' ');

                var line = leadingWhitespace;

                foreach (var key in split)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        if (key.Length + line.Length > wrapWidth)
                        {
                            wrappedText.Add(line);
                            line = leadingWhitespace;
                        }
                        else
                        {
                            line += " ";
                        }
                    }
                    line += key.Replace('\a', ' ');
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    wrappedText.Add(string.Empty);
                }
                else
                {
                    wrappedText.Add(line);
                }
            }

            return wrappedText;
        }

        /// <summary>
        /// Write a message to the error stream
        /// </summary>
        /// <param name="errorMessage"></param>
        public static void WriteToErrorStream(string errorMessage)
        {
            try
            {
                using (var errorStreamWriter = new StreamWriter(Console.OpenStandardError()))
                {
                    errorStreamWriter.WriteLine(errorMessage);
                }
            }
            catch
            {
                // Ignore errors here
            }
        }
    }
}
