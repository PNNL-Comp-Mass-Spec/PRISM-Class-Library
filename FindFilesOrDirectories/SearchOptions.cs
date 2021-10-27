using System;
using System.Collections.Generic;
using PRISM;

namespace FindFilesOrDirectories
{
    public class SearchOptions : EventNotifier
    {
        /// <summary>
        /// Input file path
        /// </summary>
        [Option("Input", "InputFile", "I",
            ArgPosition = 1, Required = true, IsInputFilePath = true,
            HelpText = "Input file path. " +
                       "When using /I at the command line, surround the filename with double quotes if it contains spaces")]
        public string InputFileOrDirectoryPath { get; set; }

        [Option("Extensions", "Ext",
            HelpText = "Comma separated list of known file extensions; ignored if ProcessDirectories is true")]
        public string KnownFileExtensions
        {
            get => string.Join(",", KnownFileExtensionList);
            set
            {
                var extensions = value.Split(',');

                KnownFileExtensionList.Clear();

                if (extensions.Length > 0)
                {
                    KnownFileExtensionList.AddRange(extensions);
                }
            }
        }

        public List<string> KnownFileExtensionList { get; private set; }

        /// <summary>
        /// Output directory path
        /// </summary>
        [Option("Output", "OutputDirectory", "O",
            HelpText = "Output directory name (or full path). " +
                       "If omitted, the output files will be created in the program directory")]
        public string OutputDirectoryPath { get; set; }

        [Option("Directories", "D",
            HelpText = "When true, search for directories to process")]
        public bool ProcessDirectories { get; set; }

        [Option("Recurse", "S",
            HelpText = "Search in subdirectories")]
        public bool RecurseDirectories { get; set; }

        [Option("RecurseDepth",
            HelpText = "When searching in subdirectories, the maximum depth to search. " +
                       "When 0 or negative, recurse infinitely. " +
                       "When 1, only process the current directory. " +
                       "When 2, process the current directory and files in its subdirectories.")]
        public int MaxLevelsToRecurse { get; set; }

        [Option("AlternateOutputDirectory", "A",
            HelpText = "Optionally provide a directory path to write results to when searching in subdirectories")]
        public string OutputDirectoryAlternatePath { get; set; }

        [Option("RecreateDirStructure", "R",
            HelpText = "When searching in subdirectories, if an alternate output directory has been defined, " +
                       "this can be set to true to re-create the input directory hierarchy in the alternate output directory")]
        public bool RecreateDirectoryHierarchyInAlternatePath { get; set; }

        public bool LogEnabled { get; set; }

        [Option("LogFile", "Log", "L",
            HelpText = "If specified, write to a log file. Can optionally provide a log file path", ArgExistsProperty = nameof(LogEnabled))]
        public string LogFilePath { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SearchOptions()
        {
            KnownFileExtensionList = new List<string>();

            InputFileOrDirectoryPath = string.Empty;

            OutputDirectoryPath = string.Empty;

            ProcessDirectories = false;

            RecurseDirectories = false;
            MaxLevelsToRecurse = 0;

            OutputDirectoryAlternatePath = string.Empty;
            RecreateDirectoryHierarchyInAlternatePath = false;

            LogEnabled = false;
            LogFilePath = string.Empty;
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(InputFileOrDirectoryPath))
            {
                ConsoleMsgUtils.ShowWarning("Input file or directory path not defined");
                return false;
            }

            return true;
        }
    }
}
