using System;
using System.Collections.Generic;
using PRISM;
using PRISM.Logging;

namespace FindFilesOrFolders
{
    internal static class Program
    {

        public const string PROGRAM_DATE = "February 12, 2020";

        private static string mInputFileOrDirectoryPath;
        private static string mOutputFileOrDirectoryPath;

        private static string mOutputFolderAlternatePath;

        private static bool mAssumeNoWildcards;
        private static List<string> mKnownExtensions;
        private static bool mProcessFolders;
        private static bool mRecurse;
        private static int mRecurseDepth;

        public static int Main(string[] args)
        {

            var objParseCommandLine = new clsParseCommandLine();

            mInputFileOrDirectoryPath = string.Empty;
            mOutputFileOrDirectoryPath = string.Empty;
            mOutputFolderAlternatePath = string.Empty;

            mAssumeNoWildcards = false;
            mKnownExtensions = new List<string>();
            mProcessFolders = false;
            mRecurse = false;
            mRecurseDepth = 0;

            try
            {
                var success = false;

                if (objParseCommandLine.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(objParseCommandLine))
                        success = true;
                }

                if (!success ||
                    objParseCommandLine.NeedToShowHelp ||
                    string.IsNullOrWhiteSpace(mInputFileOrDirectoryPath))
                {
                    ShowProgramHelp();
                    return -1;

                }

                const string PARAM_FILE_PATH = "";

                if (mProcessFolders)
                {
                    var folderProcessor = new FolderProcessor();
                    RegisterEvents(folderProcessor);
                    folderProcessor.SkipConsoleWriteIfNoProgressListener = true;

                    if (mRecurse)
                    {
                        ConsoleMsgUtils.ShowDebug("Calling folderProcessor.ProcessAndRecurseFolders");
                        success = folderProcessor.ProcessAndRecurseDirectories(mInputFileOrDirectoryPath, mOutputFileOrDirectoryPath, PARAM_FILE_PATH, mRecurseDepth);
                    }
                    else if (mAssumeNoWildcards)
                    {
                        ConsoleMsgUtils.ShowDebug("Calling folderProcessor.ProcessFolder");
                        success = folderProcessor.ProcessDirectory(mInputFileOrDirectoryPath, mOutputFileOrDirectoryPath, PARAM_FILE_PATH);
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowDebug("Calling folderProcessor.ProcessFoldersWildcard");
                        success = folderProcessor.ProcessDirectoriesWildcard(mInputFileOrDirectoryPath, mOutputFileOrDirectoryPath);
                    }

                }
                else
                {
                    var fileProcessor = new FileProcessor();
                    RegisterEvents(fileProcessor);
                    fileProcessor.SkipConsoleWriteIfNoProgressListener = true;

                    if (mRecurse)
                    {
                        const bool RECREATE_FOLDER_HIERARCHY = true;

                        if (mKnownExtensions.Count > 0)
                        {
                            ConsoleMsgUtils.ShowDebug(
                                "Calling fileProcessor.ProcessFilesAndRecurseFolders with user-defined extensions: " +
                                string.Join(", ", mKnownExtensions));

                            success = fileProcessor.ProcessFilesAndRecurseDirectories(
                                mInputFileOrDirectoryPath, mOutputFileOrDirectoryPath, mOutputFolderAlternatePath,
                                RECREATE_FOLDER_HIERARCHY, PARAM_FILE_PATH, mRecurseDepth, mKnownExtensions);
                        }
                        else
                        {
                            ConsoleMsgUtils.ShowDebug("Calling fileProcessor.ProcessFilesAndRecurseFolders with " +
                                                      "input file [" + mInputFileOrDirectoryPath + "], output folder [" + mOutputFileOrDirectoryPath + "]" +
                                                      " and extensions: " + string.Join(", ", fileProcessor.GetDefaultExtensionsToParse()));

                            success = fileProcessor.ProcessFilesAndRecurseDirectories(
                                mInputFileOrDirectoryPath, mOutputFileOrDirectoryPath, mOutputFolderAlternatePath,
                                RECREATE_FOLDER_HIERARCHY, PARAM_FILE_PATH, mRecurseDepth);

                        }

                    }
                    else if (mAssumeNoWildcards)
                    {
                        ConsoleMsgUtils.ShowDebug("Calling fileProcessor.ProcessFile with " +
                                                  "input file [" + mInputFileOrDirectoryPath + "] and output folder [" + mOutputFileOrDirectoryPath + "]");

                        success = fileProcessor.ProcessFile(mInputFileOrDirectoryPath, mOutputFileOrDirectoryPath);
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowDebug("Calling fileProcessor.ProcessFilesWildcard with " +
                                                  "input file [" + mInputFileOrDirectoryPath + "] and output folder [" + mOutputFileOrDirectoryPath + "]");

                        success = fileProcessor.ProcessFilesWildcard(mInputFileOrDirectoryPath, mOutputFileOrDirectoryPath);
                    }
                }

                if (!success)
                {
                    System.Threading.Thread.Sleep(1500);
                    return -3;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred in Program->Main: " + Environment.NewLine + ex.Message);
                Console.WriteLine(ex.StackTrace);
                System.Threading.Thread.Sleep(1500);
                return -1;
            }

            return 0;

        }

        static void RegisterEvents(IEventNotifier processor)
        {
            processor.DebugEvent += Processor_DebugEvent;
            processor.ErrorEvent += Processor_ErrorEvent;
            processor.StatusEvent += Processor_StatusEvent;
            processor.WarningEvent += Processor_WarningEvent;
        }

        static void Processor_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        static void Processor_ErrorEvent(string message, Exception ex)
        {
            ShowErrorMessage(message, ex);
        }

        static void Processor_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        static void Processor_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine objParseCommandLine)
        {
            // Returns True if no problems; otherwise, returns false
            var lstValidParameters = new List<string> {
                "I", "O", "AltOutput", "Folders", "S", "NoWild", "Ext"};

            try
            {
                // Make sure no invalid parameters are present
                if (objParseCommandLine.InvalidParametersPresent(lstValidParameters))
                {
                    var badArguments = new List<string>();
                    foreach (var item in objParseCommandLine.InvalidParameters(lstValidParameters))
                    {
                        badArguments.Add("/" + item);
                    }

                    ShowErrorMessage("Invalid command line parameters", badArguments);

                    return false;
                }

                // Query objParseCommandLine to see if various parameters are present
                if (objParseCommandLine.NonSwitchParameterCount > 0)
                {
                    mInputFileOrDirectoryPath = objParseCommandLine.RetrieveNonSwitchParameter(0);
                }

                if (objParseCommandLine.NonSwitchParameterCount > 1)
                {
                    mOutputFileOrDirectoryPath = objParseCommandLine.RetrieveNonSwitchParameter(1);
                }

                if (objParseCommandLine.RetrieveValueForParameter("I", out var paramValue))
                {
                    mInputFileOrDirectoryPath = string.Copy(paramValue);
                }

                if (objParseCommandLine.RetrieveValueForParameter("O", out paramValue))
                {
                    mOutputFileOrDirectoryPath = string.Copy(paramValue);
                }

                if (objParseCommandLine.RetrieveValueForParameter("AltOutput", out paramValue))
                {
                    mOutputFolderAlternatePath = string.Copy(paramValue);
                }

                if (objParseCommandLine.IsParameterPresent("Folders"))
                    mProcessFolders = true;

                mRecurse = objParseCommandLine.IsParameterPresent("S");

                if (mRecurse && objParseCommandLine.RetrieveValueForParameter("S", out paramValue))
                {
                    if (int.TryParse(paramValue, out var recurseDepth))
                        mRecurseDepth = recurseDepth;
                }

                mAssumeNoWildcards = objParseCommandLine.IsParameterPresent("NoWild");

                if (mRecurse && objParseCommandLine.RetrieveValueForParameter("Ext", out paramValue))
                {
                    var extensions = paramValue.Split(',');
                    if (extensions.Length > 0)
                    {
                        mKnownExtensions.Clear();
                        mKnownExtensions.AddRange(extensions);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message, ex);
            }

            return false;
        }

        private static void ShowErrorMessage(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void ShowErrorMessage(string title, IEnumerable<string> messages)
        {
            ConsoleMsgUtils.ShowErrors(title, messages);
        }

        private static void ShowProgramHelp()
        {
            var exeName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            try
            {
                Console.WriteLine();
                Console.WriteLine("This is a test program for FileProcessor and clsProcessFoldersBaseClass");
                Console.WriteLine();
                Console.WriteLine("Program syntax:" + Environment.NewLine + exeName);

                Console.WriteLine(" InputFileOrDirectoryPath [/Folders] [/O:OutputFileOrFolder] [/AltOutput] " +
                                  "[/S:MaxDepth] [/NoWild] [/Ext:KnownExtensionList");

                Console.WriteLine();
                Console.WriteLine("By default finds files; use /Folders to find folders");
                Console.WriteLine();
                Console.WriteLine("Use /O to specify the output file or folder path");
                Console.WriteLine();
                Console.WriteLine("Use /AltOutput to create the results in an alternate output folder (will retain the folder hierarchy if /S is provided)");
                Console.WriteLine();
                Console.WriteLine("Use /S to recurse into subdirectories.  Optionally append a number for the max depth");
                Console.WriteLine();
                Console.WriteLine("Use /NoWild to not check for wildcards in the input path (ignored if /S is used)");
                Console.WriteLine();
                Console.WriteLine("Use /Ext define the list of known extensions to match; only valid for /Files and only valid if /S is used");
                Console.WriteLine("For example, /Ext:.txt,.png");

                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2017");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com");
                Console.WriteLine("Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov or http://www.sysbio.org/resources/staff/");

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying the program syntax: " + ex.Message);
            }

        }


    }
}
