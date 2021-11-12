using System;
using System.IO;
using System.Threading;
using PRISM;
using PRISM.FileProcessor;
using PRISM.Logging;

namespace FindFilesOrDirectories
{
    internal static class Program
    {
        // Ignore Spelling: Conf

        public const string PROGRAM_DATE = "November 11, 2021";

        public static int Main(string[] args)
        {
            try
            {
                var programName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
                var exePath = ProcessFilesOrDirectoriesBase.GetAppPath();
                var exeName = Path.GetFileName(exePath);

                var parser = new CommandLineParser<SearchOptions>(programName, GetAppVersion())
                {
                    ProgramInfo = "This is an example application demonstrating the use of the CommandLineParser " +
                                  "and of classes that inherit ProcessDirectoriesBase and ProcessFilesBase.",
                    ContactInfo = "Program written by Matthew Monroe for PNNL (Richland, WA)" + Environment.NewLine +
                                  "E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov" + Environment.NewLine +
                                  "Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics"
                };

                parser.UsageExamples.Add(exeName + " *.txt");
                parser.UsageExamples.Add(exeName + " *.txt /O:OutputDirectoryPath");
                parser.UsageExamples.Add(exeName + " *.D /Directories /P:ParameterFilePath");

                // The default argument name for parameter files is /ParamFile or -ParamFile
                // Also allow /Conf or /P
                parser.AddParamFileKey("Conf");
                parser.AddParamFileKey("P");

                var result = parser.ParseArgs(args);
                var options = result.ParsedResults;
                RegisterEvents(options);

                if (!result.Success || !options.Validate())
                {
                    if (parser.CreateParamFileProvided)
                    {
                        return 0;
                    }

                    // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                    Thread.Sleep(750);
                    return -1;
                }

                bool success;

                if (options.ProcessDirectories)
                {
                    var processor = new DirectoryProcessor(options);

                    RegisterEvents(processor);
                    processor.SkipConsoleWriteIfNoProgressListener = true;

                    if (options.RecurseDirectories)
                    {
                        ConsoleMsgUtils.ShowDebug("Calling processor.ProcessAndRecurseDirectories");
                        success = processor.ProcessAndRecurseDirectories(
                            options.InputFileOrDirectoryPath,
                            options.OutputDirectoryPath,
                            string.Empty,
                            options.MaxLevelsToRecurse);
                    }
                    else if (PathHasWildcard(options.InputFileOrDirectoryPath))
                    {
                        ConsoleMsgUtils.ShowDebug("Calling processor.ProcessDirectoriesWildcard");
                        success = processor.ProcessDirectoriesWildcard(
                            options.InputFileOrDirectoryPath, options.OutputDirectoryPath);
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowDebug("Calling processor.ProcessDirectory");
                        success = processor.ProcessDirectory(
                            options.InputFileOrDirectoryPath,
                            options.OutputDirectoryPath, string.Empty);
                    }
                }
                else
                {
                    var fileProcessor = new FileProcessor(options);

                    RegisterEvents(fileProcessor);
                    fileProcessor.SkipConsoleWriteIfNoProgressListener = true;

                    if (options.RecurseDirectories)
                    {
                        const bool RECREATE_DIRECTORY_HIERARCHY = true;

                        if (options.KnownFileExtensions.Length > 0)
                        {
                            ConsoleMsgUtils.ShowDebug(
                                "Calling fileProcessor.ProcessFilesAndRecurseDirectories with user-defined extensions: " +
                                string.Join(", ", options.KnownFileExtensions));

                            success = fileProcessor.ProcessFilesAndRecurseDirectories(
                                options.InputFileOrDirectoryPath,
                                options.OutputDirectoryPath,
                                options.OutputDirectoryAlternatePath,
                                RECREATE_DIRECTORY_HIERARCHY,
                                string.Empty,
                                options.MaxLevelsToRecurse,
                                options.KnownFileExtensionList);
                        }
                        else
                        {
                            ConsoleMsgUtils.ShowDebug(
                                "Calling fileProcessor.ProcessFilesAndRecurseDirectories with " +
                                "input file [" + options.InputFileOrDirectoryPath + "], " +
                                "output directory [" + options.OutputDirectoryPath + "]" +
                                " and extensions: " + string.Join(", ", fileProcessor.GetDefaultExtensionsToParse()));

                            success = fileProcessor.ProcessFilesAndRecurseDirectories(
                                options.InputFileOrDirectoryPath,
                                options.OutputDirectoryPath,
                                options.OutputDirectoryAlternatePath,
                                RECREATE_DIRECTORY_HIERARCHY,
                                string.Empty,
                                options.MaxLevelsToRecurse);
                        }
                    }
                    else if (PathHasWildcard(options.InputFileOrDirectoryPath))
                    {
                        ConsoleMsgUtils.ShowDebug(
                            "Calling fileProcessor.ProcessFilesWildcard with " +
                            "input file [" + options.InputFileOrDirectoryPath + "] and " +
                            "output directory [" + options.OutputDirectoryPath + "]");

                        success = fileProcessor.ProcessFilesWildcard(options.InputFileOrDirectoryPath, options.OutputDirectoryPath);
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowDebug(
                            "Calling fileProcessor.ProcessFile with " +
                            "input file [" + options.InputFileOrDirectoryPath + "] and " +
                            "output directory [" + options.OutputDirectoryPath + "]");

                        success = fileProcessor.ProcessFile(options.InputFileOrDirectoryPath, options.OutputDirectoryPath);
                    }
                }

                if (!success)
                {
                    Thread.Sleep(1500);
                    return -3;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred in Program->Main: " + Environment.NewLine + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Thread.Sleep(1500);
                return -1;
            }

            return 0;
        }

        private static bool PathHasWildcard(string fileOrDirectoryPath)
        {
            return fileOrDirectoryPath.Contains("*") || fileOrDirectoryPath.Contains("?");
        }

        private static void RegisterEvents(IEventNotifier processor)
        {
            processor.DebugEvent += Processor_DebugEvent;
            processor.ErrorEvent += Processor_ErrorEvent;
            processor.StatusEvent += Processor_StatusEvent;
            processor.WarningEvent += Processor_WarningEvent;
        }

        private static void Processor_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private static void Processor_ErrorEvent(string message, Exception ex)
        {
            ShowErrorMessage(message, ex);
        }

        private static void Processor_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void Processor_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static void ShowErrorMessage(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }
    }
}
