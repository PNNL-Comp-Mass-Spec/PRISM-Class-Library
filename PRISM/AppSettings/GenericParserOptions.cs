using System;
using System.IO;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// This class demonstrates how to decorate properties in a class so that the CommandLineParser can use them to match command line arguments
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    internal class GenericParserOptions
    {
        public GenericParserOptions()
        {
            StartID = 0;
            EndID = int.MaxValue;

            InputFilePath = string.Empty;
            OutputDirectoryPath = string.Empty;
            AppendToOutput = true;

            Preview = false;

            LogEnabled = false;
            LogFilePath = "log.txt";
        }

        [Option("Start", Required = true, HelpText = "First ID to process")]
        public int StartID { get; set; }

        [Option("End", HelpText = "Last ID to process", HelpShowsDefault = true)]
        public int EndID { get; set; }

        [Option("Input", "I", ArgPosition = 1, HelpText = "File to process", IsInputFilePath = true)]
        public string InputFilePath { get; set; }

        [Option("Output", "O", ArgPosition = 2, HelpText = "Output directory path for creating the results file")]
        public string OutputDirectoryPath { get; set; }

        [Option("Append", HelpText = "Append results to the output file", HelpShowsDefault = true)]
        public bool AppendToOutput { get; set; }

        [Option("Preview", HelpText = "Preview changes")]
        public bool Preview { get; set; }

        public bool LogEnabled { get; set; }

        [Option("Log", HelpText = "If specified, write to a log file. Can optionally provide a log file path", ArgExistsProperty = nameof(LogEnabled))]
        public string LogFilePath { get; set; }

        /// <summary>
        /// Display the current options at the console
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public void OutputSetOptions()
        {
            Console.WriteLine("Using options:");

            Console.WriteLine("First ID: {0}", StartID);

            if (EndID < int.MaxValue)
                Console.WriteLine("Last ID: {0}", EndID);

            Console.WriteLine("Output directory path: {0}", OutputDirectoryPath);
            Console.WriteLine("Append to output: {0}", AppendToOutput);

            if (Preview)
                Console.WriteLine("Previewing changes");

            if (LogEnabled)
            {
                Console.WriteLine("Logging to file: {0}", LogFilePath);
            }
        }

        /// <summary>
        /// Validate the options
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        // ReSharper disable once UnusedMember.Global
        public bool ValidateArgs(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(InputFilePath))
            {
                errorMessage = "Use /I to specify the input file";
                return false;
            }

            if (string.IsNullOrWhiteSpace(OutputDirectoryPath))
            {
                var currentDirectory = new DirectoryInfo(".");
                OutputDirectoryPath = currentDirectory.FullName;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
