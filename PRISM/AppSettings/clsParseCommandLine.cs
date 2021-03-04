using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

// NET Standard does not have Environment.CommandLine, which means making this functional under NET Standard is non-trivial and API-breaking.
// Instead use the CommandLineParser class.

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// This class can be used to parse the text following the program name when a
    /// program is started from the command line
    /// </summary>
    /// <remarks>Superseded by CommandLineParser (but not marked obsolete since used in numerous applications)</remarks>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class clsParseCommandLine
    {
        /// <summary>
        /// Default switch char
        /// </summary>
        public const char DEFAULT_SWITCH_CHAR = '/';

        /// <summary>
        /// Alternate switch char
        /// </summary>
        public const char ALTERNATE_SWITCH_CHAR = '-';

        /// <summary>
        /// Default character between the switch name and a value to associate with the parameter
        /// </summary>
        public const char DEFAULT_SWITCH_PARAM_CHAR = ':';

        private readonly Dictionary<string, string> mSwitches = new Dictionary<string, string>();

        private readonly List<string> mNonSwitchParameters = new List<string>();

        /// <summary>
        /// If true, we need to show the syntax to the user due to a switch error, invalid switch, or the presence of /? or /help
        /// </summary>
        public bool NeedToShowHelp { get; private set; }

        /// <summary>
        /// This will be true after calling ParseCommandLine if the command line has no arguments (either non-switch or switch based)
        /// </summary>
        public bool NoParameters { get; private set; }

        /// <summary>
        /// Number of switches
        /// </summary>
        public int ParameterCount => mSwitches.Count;

        /// <summary>
        /// Number of parameters that are not preceded by a switch
        /// </summary>
        public int NonSwitchParameterCount => mNonSwitchParameters.Count;

        /// <summary>
        /// Set to true to see extra debug information
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Compares the parameter names in parameterList with the parameters at the command line
        /// </summary>
        /// <param name="parameterList">Parameter list</param>
        /// <returns>True if any of the parameters are not present in parameterList()</returns>
        public bool InvalidParametersPresent(List<string> parameterList)
        {
            const bool caseSensitive = false;
            return InvalidParametersPresent(parameterList, caseSensitive);
        }

        /// <summary>
        /// Compares the parameter names in parameterList with the parameters at the command line
        /// </summary>
        /// <param name="parameterList">Parameter list</param>
        /// <returns>True if any of the parameters are not present in parameterList()</returns>
        public bool InvalidParametersPresent(IEnumerable<string> parameterList)
        {
            const bool caseSensitive = false;
            return InvalidParametersPresent(parameterList, caseSensitive);
        }

        /// <summary>
        /// Compares the parameter names in parameterList with the parameters at the command line
        /// </summary>
        /// <param name="parameterList">Parameter list</param>
        /// <param name="caseSensitive">True to perform case-sensitive matching of the parameter name</param>
        /// <returns>True if any of the parameters are not present in parameterList()</returns>
        public bool InvalidParametersPresent(IEnumerable<string> parameterList, bool caseSensitive)
        {
            return InvalidParameters(parameterList.ToList(), caseSensitive).Count > 0;
        }

        /// <summary>
        /// Validate that the user-provided parameters are in the validParameters list
        /// </summary>
        /// <param name="validParameters"></param>
        /// <param name="caseSensitive"></param>
        public bool InvalidParametersPresent(List<string> validParameters, bool caseSensitive)
        {
            return InvalidParameters(validParameters, caseSensitive).Count > 0;
        }

        /// <summary>
        /// Retrieve a list of the user-provided parameters that are not in validParameters
        /// </summary>
        /// <param name="validParameters"></param>
        public List<string> InvalidParameters(List<string> validParameters)
        {
            const bool caseSensitive = false;
            return InvalidParameters(validParameters, caseSensitive);
        }

        /// <summary>
        /// Retrieve a list of the user-provided parameters that are not in validParameters
        /// </summary>
        /// <param name="validParameters"></param>
        /// <param name="caseSensitive"></param>
        public List<string> InvalidParameters(List<string> validParameters, bool caseSensitive)
        {
            var invalidParameters = new List<string>();

            try
            {
                // Find items in mSwitches whose keys are not in validParameters)

                foreach (var item in mSwitches)
                {
                    var itemKey = item.Key;
                    int matchCount;

                    if (caseSensitive)
                    {
                        matchCount = (from validItem in validParameters
                                      where validItem == itemKey
                                      select validItem).Count();
                    }
                    else
                    {
                        matchCount = (from validItem in validParameters
                                      where string.Equals(validItem, itemKey, StringComparison.OrdinalIgnoreCase)
                                      select validItem).Count();
                    }

                    if (matchCount == 0)
                    {
                        invalidParameters.Add(item.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in InvalidParameters", ex);
            }

            return invalidParameters;
        }

        /// <summary>
        /// Look for parameter on the command line
        /// </summary>
        /// <param name="paramName">Parameter name</param>
        /// <returns>True if present, otherwise false</returns>
        public bool IsParameterPresent(string paramName)
        {
            const bool caseSensitive = false;
            return RetrieveValueForParameter(paramName, out _, caseSensitive);
        }

        /// <summary>
        /// Parse the parameters and switches at the command line; uses / for the switch character and : for the switch parameter character
        /// </summary>
        /// <returns>Returns True if any command line parameters were found; otherwise false</returns>
        /// <remarks>
        /// If /? or /help is found, returns False and sets mShowHelp to True
        /// If there are no arguments at the command line, returns false, but sets NoArgumentsProvided to true
        /// </remarks>
        public bool ParseCommandLine()
        {
            return ParseCommandLine(DEFAULT_SWITCH_CHAR, DEFAULT_SWITCH_PARAM_CHAR);
        }

        /// <summary>
        /// Parse the parameters and switches at the command line; uses : for the switch parameter character
        /// </summary>
        /// <returns>Returns True if any command line parameters were found; otherwise false</returns>
        /// <remarks>
        /// If /? or /help is found, returns False and sets mShowHelp to True
        /// If there are no arguments at the command line, returns false, but sets NoArgumentsProvided to true
        /// </remarks>
        public bool ParseCommandLine(char switchStartChar)
        {
            return ParseCommandLine(switchStartChar, DEFAULT_SWITCH_PARAM_CHAR);
        }

        /// <summary>
        /// Parse the parameters and switches at the command line
        /// </summary>
        /// <param name="switchStartChar"></param>
        /// <param name="switchParameterChar"></param>
        /// <returns>Returns True if any command line parameters were found; otherwise false</returns>
        /// <remarks>
        /// If /? or /help is found, returns False and sets mShowHelp to True
        /// If there are no arguments at the command line, returns false, but sets NoArgumentsProvided to true
        /// </remarks>
        public bool ParseCommandLine(char switchStartChar, char switchParameterChar)
        {
            mSwitches.Clear();
            mNonSwitchParameters.Clear();

            try
            {
                string commandLine;
                try
                {
                    // .CommandLine() returns the full command line
                    commandLine = Environment.CommandLine;

                    // .GetCommandLineArgs splits the command line at spaces, though it keeps text between double quotes together
                    // Note that .NET will strip out the starting and ending double quote if the user provides a parameter like this:
                    // MyProgram.exe "C:\Program Files\FileToProcess"
                    //
                    // In this case, paramList[1] will not have a double quote at the start but it will have a double quote at the end:
                    //  paramList[1] = C:\Program Files\FileToProcess"

                    // One very odd feature of Environment.GetCommandLineArgs() is that if the command line looks like this:
                    //    MyProgram.exe "D:\WorkDir\SubDir\" /O:D:\OutputDir
                    // Then paramList will have:
                    //    paramList[1] = D:\WorkDir\SubDir" /O:D:\OutputDir
                    //
                    // To avoid this problem instead specify the command line as:
                    //    MyProgram.exe "D:\WorkDir\SubDir" /O:D:\OutputDir
                    // which gives:
                    //    paramList[1] = D:\WorkDir\SubDir
                    //    paramList[2] = /O:D:\OutputDir
                    //
                    // Due to the idiosyncrasies of .GetCommandLineArgs, we will instead use SplitCommandLineParams to do the splitting
                    // paramList = Environment.GetCommandLineArgs()

                }
                catch (Exception ex)
                {
                    // In .NET 1.x, programs would fail if called from a network share
                    // This appears to be fixed in .NET 2.0 and above
                    // If an exception does occur here, we'll show the error message at the console, then sleep for 5 seconds

                    ConsoleMsgUtils.ShowWarning("------------------------------------------------------------------------------");
                    ConsoleMsgUtils.ShowWarning(ConsoleMsgUtils.WrapParagraph(
                                                    "This program cannot be run from a network share.  Please map a drive to the " +
                                                    "network share you are currently accessing or copy the program files and " +
                                                    "required DLLs to your local computer."));
                    ConsoleMsgUtils.ShowWarning("Exception: " + ex.Message);
                    ConsoleMsgUtils.ShowWarning("------------------------------------------------------------------------------");

                    ConsoleMsgUtils.PauseAtConsole();

                    NeedToShowHelp = true;
                    return false;
                }

                if (DebugMode)
                {
                    Console.WriteLine();
                    Console.WriteLine("Debugging command line parsing");
                    Console.WriteLine();
                }

                var paramList = SplitCommandLineParams(commandLine);

                if (DebugMode)
                {
                    Console.WriteLine();
                }

                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    if (DebugMode)
                    {
                        Console.WriteLine("Command line is empty (not even the Executable name is present)");
                    }
                    NoParameters = true;
                    return false;
                }

                if (commandLine.IndexOf(switchStartChar + "?", StringComparison.Ordinal) > 0 ||
                    commandLine.ToLower().IndexOf(switchStartChar + "help", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    NeedToShowHelp = true;
                    return false;
                }

                if (paramList.Length == 1)
                {
                    if (DebugMode)
                    {
                        Console.WriteLine("No arguments were provided");
                    }

                    NoParameters = true;
                    return false;
                }

                // Parse the command line
                // Note that paramList[0] is the path to the Executable for the calling program

                for (var paramIndex = 1; paramIndex < paramList.Length; paramIndex++)
                {
                    if (paramList[paramIndex].Length == 0)
                    {
                        continue;
                    }

                    var paramName = paramList[paramIndex].TrimStart(' ');
                    var paramValue = string.Empty;

                    bool isSwitchParam;
                    if (paramName.StartsWith(switchStartChar.ToString()))
                    {
                        isSwitchParam = true;
                    }
                    else if (paramName.StartsWith(ALTERNATE_SWITCH_CHAR.ToString()) || paramName.StartsWith(DEFAULT_SWITCH_CHAR.ToString()))
                    {
                        isSwitchParam = true;
                    }
                    else
                    {
                        // Parameter doesn't start with switchStartChar or / or -
                        isSwitchParam = false;
                    }

                    if (isSwitchParam)
                    {
                        // Look for switchParameterChar in paramList[paramIndex]
                        var charIndex = paramList[paramIndex].IndexOf(switchParameterChar);

                        if (charIndex >= 0)
                        {
                            // Parameter is of the form /I:MyParam or /I:"My Parameter" or -I:"My Parameter" or /MyParam:Setting
                            paramValue = paramName.Substring(charIndex + 1).Trim();

                            // Remove any starting and ending quotation marks
                            paramValue = paramValue.Trim('"');

                            paramName = paramName.Substring(0, charIndex);
                        }
                        else
                        {
                            // Parameter is of the form /S or -S
                        }

                        // Remove the switch character from paramName
                        paramName = paramName.Substring(1).Trim();

                        if (DebugMode)
                        {
                            Console.WriteLine("SwitchParam: " + paramName + "=" + paramValue);
                        }

                        // Note: This will add paramName if it doesn't exist (which is normally the case)
                        mSwitches[paramName] = paramValue;
                    }
                    else
                    {
                        // Non-switch parameter since switchParameterChar was not found and does not start with switchStartChar

                        // Remove any starting and ending quotation marks
                        paramName = paramName.Trim('"');

                        if (DebugMode)
                        {
                            Console.WriteLine("NonSwitchParam " + mNonSwitchParameters.Count + ": " + paramName);
                        }

                        mNonSwitchParameters.Add(paramName);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in ParseCommandLine", ex);
            }

            if (DebugMode)
            {
                Console.WriteLine();
                Console.WriteLine("Switch Count = " + mSwitches.Count);
                Console.WriteLine("NonSwitch Count = " + mNonSwitchParameters.Count);
                Console.WriteLine();
            }

            return mSwitches.Count + mNonSwitchParameters.Count > 0;
        }

        /// <summary>
        /// Pause the program for the specified number of milliseconds, displaying a period at a set interval while paused
        /// </summary>
        /// <param name="millisecondsToPause">Milliseconds to pause; default 5 seconds</param>
        /// <param name="millisecondsBetweenDots">Seconds between each period; default 1 second</param>
        [Obsolete("Use ConsoleMsgUtils.PauseAtConsole(...) instead.", false)]
        public static void PauseAtConsole(int millisecondsToPause = 5000, int millisecondsBetweenDots = 1000)
        {
            ConsoleMsgUtils.PauseAtConsole(millisecondsToPause, millisecondsBetweenDots);
        }

        /// <summary>
        /// Returns the value of the non-switch parameter at the given index
        /// </summary>
        /// <param name="parameterIndex">Parameter index</param>
        /// <returns>The value of the parameter at the given index; empty string if no value or invalid index</returns>
        public string RetrieveNonSwitchParameter(int parameterIndex)
        {
            var paramValue = string.Empty;

            if (parameterIndex < mNonSwitchParameters.Count)
            {
                paramValue = mNonSwitchParameters[parameterIndex];
            }

            if (string.IsNullOrEmpty(paramValue))
            {
                return string.Empty;
            }

            return paramValue;
        }

        /// <summary>
        /// Returns the parameter at the given index
        /// </summary>
        /// <param name="parameterIndex">Parameter index</param>
        /// <param name="paramName">Parameter name (output)</param>
        /// <param name="paramValue">Value associated with the parameter; empty string if no value (output)</param>
        /// <returns>True if a parameterIndex is valid; false if >= mSwitches.Count</returns>
        public bool RetrieveParameter(int parameterIndex, out string paramName, out string paramValue)
        {
            try
            {
                paramName = string.Empty;
                paramValue = string.Empty;

                if (parameterIndex < mSwitches.Count)
                {
                    using (var iEnum = mSwitches.GetEnumerator())
                    {
                        for (var switchIndex = 0; iEnum.MoveNext(); switchIndex++)
                        {
                            if (switchIndex != parameterIndex)
                                continue;

                            paramName = iEnum.Current.Key;
                            paramValue = iEnum.Current.Value;
                            return true;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in RetrieveParameter", ex);
            }

            return false;
        }

        /// <summary>
        /// Look for parameter on the command line and returns its value in paramValue
        /// </summary>
        /// <param name="paramName">Parameter name</param>
        /// <param name="paramValue">Value associated with the parameter; empty string if no value (output)</param>
        /// <returns>True if present, otherwise false</returns>
        public bool RetrieveValueForParameter(string paramName, out string paramValue)
        {
            return RetrieveValueForParameter(paramName, out paramValue, false);
        }

        /// <summary>
        /// Look for parameter on the command line and returns its value in paramValue
        /// </summary>
        /// <param name="paramName">Parameter name</param>
        /// <param name="paramValue">Value associated with the parameter; empty string if no value (output)</param>
        /// <param name="caseSensitive">True to perform case-sensitive matching of the parameter name</param>
        /// <returns>True if present, otherwise false</returns>
        public bool RetrieveValueForParameter(string paramName, out string paramValue, bool caseSensitive)
        {
            try
            {
                paramValue = string.Empty;

                if (caseSensitive)
                {
                    if (mSwitches.ContainsKey(paramName))
                    {
                        paramValue = Convert.ToString(mSwitches[paramName]);
                        return true;
                    }

                    return false;
                }

                var result = (from item in mSwitches
                              where string.Equals(item.Key, paramName, StringComparison.OrdinalIgnoreCase)
                              select item).ToList();

                if (result.Count > 0)
                {
                    paramValue = result.First().Value;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception("Error in RetrieveValueForParameter", ex);
            }
        }

        private string[] SplitCommandLineParams(string commandLine)
        {
            var paramList = new List<string>();

            var indexStart = 0;
            var indexEnd = 0;

            try
            {
                if (!string.IsNullOrEmpty(commandLine))
                {
                    // Make sure the command line doesn't have any carriage return or linefeed characters
                    commandLine = commandLine.Replace("\r", " ");
                    commandLine = commandLine.Replace("\n", " ");

                    var insideDoubleQuotes = false;

                    while (indexStart < commandLine.Length)
                    {
                        // Step through the characters to find the next space
                        // However, if we find a double quote, stop checking for spaces

                        if (commandLine[indexEnd] == '"')
                        {
                            insideDoubleQuotes = !insideDoubleQuotes;
                        }

                        if (!insideDoubleQuotes || indexEnd == commandLine.Length - 1)
                        {
                            if (commandLine[indexEnd] == ' ' || indexEnd == commandLine.Length - 1)
                            {
                                // Found the end of a parameter
                                var paramName = commandLine.Substring(indexStart, indexEnd - indexStart + 1).TrimEnd(' ');

                                if (paramName.StartsWith('"'.ToString()))
                                {
                                    paramName = paramName.Substring(1);
                                }

                                if (paramName.EndsWith('"'.ToString()))
                                {
                                    paramName = paramName.Substring(0, paramName.Length - 1);
                                }

                                if (!string.IsNullOrEmpty(paramName))
                                {
                                    if (DebugMode)
                                    {
                                        Console.WriteLine("Param " + paramList.Count + ": " + paramName);
                                    }
                                    paramList.Add(paramName);
                                }

                                indexStart = indexEnd + 1;
                            }
                        }

                        indexEnd++;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in SplitCommandLineParams", ex);
            }

            return paramList.ToArray();
        }
    }
}
