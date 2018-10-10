using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// This class demonstrates how to decorate properties in a class so that the CommandLineParser can use them to match command line arguments
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal class GenericParserOptions
    {
        public GenericParserOptions()
        {
            StartID = 0;
            EndID = int.MaxValue;

            OutputDirectoryPath = string.Empty;
            AppendToOutput = true;

            Preview = false;
        }

        [Option("start", Required = true, HelpText = "First ID to process")]
        public int StartID { get; set; }

        [Option("end", HelpText = "Last ID to process", HelpShowsDefault = true)]
        public int EndID { get; set; }

        [Option("output", "o", ArgPosition = 1, HelpText = "Output directory path for creating the results file")]
        public string OutputDirectoryPath { get; set; }

        [Option("append", HelpText = "Append results to the output file", HelpShowsDefault = true)]
        public bool AppendToOutput { get; set; }

        [Option("preview", HelpText = "Preview changes")]
        public bool Preview { get; set; }

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
        }

        public bool ValidateArgs()
        {
            if (string.IsNullOrWhiteSpace(OutputDirectoryPath))
            {
                var currentDirectory = new DirectoryInfo(".");
                OutputDirectoryPath = currentDirectory.FullName;
            }

            return true;
        }

    }
}
