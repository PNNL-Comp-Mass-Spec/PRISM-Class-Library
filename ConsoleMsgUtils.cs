using System;
using System.Collections.Generic;

namespace PRISM
{
    /// <summary>
    /// This class includes methods to be used when displaying messages at the console while monitoring a class that inherits clsEventNotifier
    /// </summary>
    public static class ConsoleMsgUtils
    {
        private const string SEPARATOR = "------------------------------------------------------------------------------";

        private static bool mAutoCheckedDebugFontColor;

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
        /// Display an error message at the console with color ErrorFontColor (defaults to Red)
        /// If an exception is included, the stack trace is shown using StackTraceFontColor
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="includeSeparator">When true, add a separator line before and after the error</param>
        /// <param name="writeToErrorStream">When true, also send the error to the the standard error stream</param>
        /// <returns>Error message, with the exception message appended, provided ex is not null and provided message does not end with ex.message</returns>
        public static string ShowError(string message, bool includeSeparator = true, bool writeToErrorStream = true)
        {
            return ShowError(message, null, includeSeparator, writeToErrorStream);
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
        public static string ShowError(string message, Exception ex, bool includeSeparator = true, bool writeToErrorStream = true)
        {

            Console.WriteLine();
            if (includeSeparator)
            {
                Console.WriteLine(SEPARATOR);
            }

            string formattedError;
            if (ex == null || message.EndsWith(ex.Message))
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
                Console.WriteLine(clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex));
            }

            Console.ResetColor();

            if (includeSeparator)
            {
                Console.WriteLine(SEPARATOR);
            }
            Console.WriteLine();

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
        public static string ShowErrors(string title, IEnumerable<string> errorMessages, bool writeToErrorStream = true, string indentChars = "   ")
        {
            string firstError = null;

            Console.WriteLine();
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
            Console.WriteLine();

            return firstError;
        }

        /// <summary>
        /// Display a debug message at the console with color DebugFontColor (defaults to DarkGray)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="indentChars">Characters to use to indent the message</param>
        public static void ShowDebug(string message, string indentChars = "  ")
        {
            if (System.IO.Path.DirectorySeparatorChar == '/' && !mAutoCheckedDebugFontColor)
            {
                // Running on Linux
                // Dark gray appears as black when using Putty; use blue instead
                if (DebugFontColor == ConsoleColor.DarkGray)
                {
                    DebugFontColor = ConsoleColor.Blue;
                }
                mAutoCheckedDebugFontColor = true;
            }

            Console.WriteLine();
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
        public static void ShowWarning(string message)
        {
            Console.WriteLine();
            Console.ForegroundColor = WarningFontColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Write a message to the error stream
        /// </summary>
        /// <param name="errorMessage"></param>
        public static void WriteToErrorStream(string errorMessage)
        {
            try
            {
                using (var swErrorStream = new System.IO.StreamWriter(Console.OpenStandardError()))
                {
                    swErrorStream.WriteLine(errorMessage);
                }
            }
            catch
            {
                // Ignore errors here
            }
        }

    }
}
