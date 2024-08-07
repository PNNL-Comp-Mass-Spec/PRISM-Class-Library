﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// This class includes methods to be used when displaying messages at the console while monitoring a class that inherits EventNotifier
    /// </summary>
    public static class ConsoleMsgUtils
    {
        // Ignore Spelling: Utils

        private const string SEPARATOR = "------------------------------------------------------------------------------";

        private static bool mAutoCheckedDebugFontColor;

        private static readonly Regex mLeadingWhitespaceMatcher = new("^ +");

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
        /// <param name="emptyLineCount">Empty line count</param>
        public static void ConsoleWriteEmptyLines(int emptyLineCount)
        {
            for (var i = 1; i <= emptyLineCount; i++)
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to red)
        /// </summary>
        /// <param name="message">Message</param>
        public static string ShowError(string message)
        {
            return ShowErrorCustom(message);
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to red)
        /// </summary>
        /// <param name="format">Message format string</param>
        /// <param name="args">Arguments to use with formatString</param>
        /// <returns>Error message</returns>
        [StringFormatMethod("format")]
        public static string ShowError(string format, params object[] args)
        {
            return ShowErrorCustom(string.Format(format, args));
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to red)
        /// If an exception is included, the stack trace is shown using StackTraceFontColor
        /// </summary>
        /// <param name="ex">Exception (can be null)</param>
        /// <param name="format">Message format string (do not include ex.Message)</param>
        /// <param name="args">Arguments to use with formatString</param>
        [StringFormatMethod("format")]
        public static string ShowError(Exception ex, string format, params object[] args)
        {
            return ShowErrorCustom(string.Format(format, args), ex);
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to red)
        /// If an exception is included, the stack trace is shown using StackTraceFontColor
        /// </summary>
        /// <param name="message">Error message (do not include ex.Message)</param>
        /// <param name="ex">Exception (can be null)</param>
        /// <param name="writeToErrorStream">When true, also send the error to the standard error stream</param>
        public static string ShowError(string message, Exception ex, bool writeToErrorStream = false)
        {
            return ShowErrorCustom(message, ex, true, writeToErrorStream);
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to red)
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="includeSeparator">When true, add a separator line before and after the error</param>
        /// <param name="writeToErrorStream">When true, also send the error to the standard error stream</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        /// <returns>Error message, with the exception message appended, provided ex is not null and provided message does not end with ex.Message</returns>
        public static string ShowErrorCustom(string message, bool includeSeparator = true, bool writeToErrorStream = true, int emptyLinesBeforeMessage = 1)
        {
            return ShowErrorCustom(message, null, includeSeparator, writeToErrorStream, emptyLinesBeforeMessage);
        }

        /// <summary>
        /// Display an error message at the console with color ErrorFontColor (defaults to red)
        /// If an exception is included, the stack trace is shown using StackTraceFontColor
        /// </summary>
        /// <param name="message">Error message (do not include ex.Message)</param>
        /// <param name="ex">Exception (can be null)</param>
        /// <param name="includeSeparator">When true, add a separator line before and after the error</param>
        /// <param name="writeToErrorStream">When true, also send the error to the standard error stream</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        /// <returns>Error message, with the exception message appended, provided ex is not null and provided message does not end with ex.Message</returns>
        public static string ShowErrorCustom(
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

                if (ex != null)
                {
                    WriteToErrorStream(StackTraceFormatter.GetExceptionStackTrace(ex));
                }
            }

            return formattedError;
        }

        /// <summary>
        /// Display a set of error messages at the console with color ErrorFontColor (defaults to Red)
        /// </summary>
        /// <param name="title">Title text to be shown before the errors; can be null or blank</param>
        /// <param name="errorMessages">Error messages to show</param>
        /// <param name="writeToErrorStream">When true, also send the error to the standard error stream</param>
        /// <param name="indentChars">Characters to add before each error message; defaults to 3 spaces</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        /// <returns>The first error message</returns>
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
                indentChars = string.Empty;

            foreach (var item in errorMessages)
            {
                firstError ??= item;

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
        /// Display a debug message at the console with color DebugFontColor (defaults to dark gray)
        /// </summary>
        /// <param name="message">Message</param>
        public static void ShowDebug(string message)
        {
            ShowDebugCustom(message);
        }

        /// <summary>
        /// Display a debug message at the console with color DebugFontColor (defaults to dark gray)
        /// </summary>
        /// <param name="format">Message format string</param>
        /// <param name="args">Arguments to use with formatString</param>
        [StringFormatMethod("format")]
        public static void ShowDebug(string format, params object[] args)
        {
            ShowDebugCustom(string.Format(format, args));
        }

        /// <summary>
        /// Display a debug message at the console with color DebugFontColor (defaults to dark gray)
        /// </summary>
        /// <param name="message">Debug message</param>
        /// <param name="indentChars">Characters to use to indent the message</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        public static void ShowDebugCustom(string message, string indentChars = "  ", int emptyLinesBeforeMessage = 1)
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
        /// Display a warning message at the console with color WarningFontColor (defaults to yellow)
        /// </summary>
        /// <param name="message">Message</param>
        public static void ShowWarning(string message)
        {
            ShowWarningCustom(message);
        }

        /// <summary>
        /// Display a warning message at the console with color WarningFontColor (defaults to yellow)
        /// </summary>
        /// <param name="format">Message format string</param>
        /// <param name="args">Arguments to use with formatString</param>
        [StringFormatMethod("format")]
        public static void ShowWarning(string format, params object[] args)
        {
            ShowWarningCustom(string.Format(format, args));
        }

        /// <summary>
        /// Display a warning message at the console with color WarningFontColor (defaults to yellow)
        /// </summary>
        /// <param name="message">Warning message</param>
        /// <param name="emptyLinesBeforeMessage">Number of empty lines to display before showing the message</param>
        public static void ShowWarningCustom(string message, int emptyLinesBeforeMessage = 1)
        {
            ConsoleWriteEmptyLines(emptyLinesBeforeMessage);
            Console.ForegroundColor = WarningFontColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Sleep for the specified number of seconds
        /// </summary>
        /// <remarks>Sleeps for 10 second chunks until waitTimeSeconds has elapsed</remarks>
        /// <param name="waitTimeSeconds">Sleep time, in seconds</param>
        public static void SleepSeconds(double waitTimeSeconds)
        {
            var endTime = DateTime.UtcNow.AddSeconds(waitTimeSeconds);

            while (endTime.Subtract(DateTime.UtcNow).TotalMilliseconds > 10)
            {
                var remainingSeconds = endTime.Subtract(DateTime.UtcNow).TotalSeconds;

                if (remainingSeconds > 10)
                {
                    AppUtils.SleepMilliseconds(10000);
                }
                else
                {
                    var sleepTimeMsec = (int)Math.Ceiling(remainingSeconds * 1000);
                    AppUtils.SleepMilliseconds(sleepTimeMsec);
                }
            }
        }

        /// <summary>
        /// Wraps the words in textToWrap to the set width (where possible)
        /// </summary>
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph</returns>
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
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph as a list of strings</returns>
        public static List<string> WrapParagraphAsList(string textToWrap, int wrapWidth)
        {
            // Check for newline characters
            var textToWrapLfOnly = textToWrap.Replace("\r\n", "\n");

            var textLinesToWrap = textToWrapLfOnly.Split('\r', '\n');

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
        /// <param name="errorMessage">Error message</param>
        public static void WriteToErrorStream(string errorMessage)
        {
            try
            {
                using var errorStreamWriter = new StreamWriter(Console.OpenStandardError());
                errorStreamWriter.WriteLine(errorMessage);
            }
            catch
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Pause the program for the specified number of milliseconds, displaying a period at a set interval while paused
        /// </summary>
        /// <param name="millisecondsToPause">Milliseconds to pause; default 5 seconds</param>
        /// <param name="millisecondsBetweenDots">Seconds between each period; default 1 second</param>
        public static void PauseAtConsole(int millisecondsToPause = 5000, int millisecondsBetweenDots = 1000)
        {
            int totalIterations;

            Console.WriteLine();
            Console.Write("Continuing in " + (millisecondsToPause / 1000.0).ToString("0") + " seconds ");

            try
            {
                if (millisecondsBetweenDots == 0)
                    millisecondsBetweenDots = millisecondsToPause;

                totalIterations = (int)Math.Round(millisecondsToPause / (double)millisecondsBetweenDots, 0);
            }
            catch
            {
                // Ignore errors here
                totalIterations = 1;
            }

            var iteration = 0;
            do
            {
                Console.Write('.');

                AppUtils.SleepMilliseconds(millisecondsBetweenDots);

                iteration++;
            } while (iteration < totalIterations);

            Console.WriteLine();
        }
    }
}
