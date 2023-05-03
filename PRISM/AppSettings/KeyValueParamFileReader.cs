using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// ReSharper disable UnusedMember.Global

namespace PRISM.AppSettings
{
    /// <summary>
    /// Class for reading parameter files with Key=Value settings (as used by MS-GF+, MSPathFinder, and TopPIC)
    /// </summary>
    public class KeyValueParamFileReader : EventNotifier
    {
        // Ignore Spelling: arg

        /// <summary>
        /// Most recent error message
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Parameter file path
        /// </summary>
        public string ParamFileName { get; }

        /// <summary>
        /// Parameter file path (as passed to the constructor)
        /// </summary>
        public string ParamFilePath { get; }

        /// <summary>
        /// This will be set to true if the parameter file was not found
        /// </summary>
        public bool ParamFileNotFound { get; private set; }

        /// <summary>
        /// Tool name (for inclusion in error messages)
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// Constructor that takes tool name and parameter file path
        /// </summary>
        /// <remarks>
        /// paramFilePath can be blank if you plan to call ParseKeyValueParameterList
        /// </remarks>
        /// <param name="toolName">Tool name (for logging)</param>
        /// <param name="paramFilePath">Parameter file path</param>
        public KeyValueParamFileReader(string toolName, string paramFilePath)
        {
            ErrorMessage = string.Empty;
            ToolName = toolName ?? "Undefined tool";

            if (string.IsNullOrWhiteSpace(paramFilePath))
            {
                ParamFileName = string.Empty;
                ParamFilePath = string.Empty;
                return;
            }

            ParamFileName = Path.GetFileName(paramFilePath);
            ParamFilePath = paramFilePath;
        }

        /// <summary>
        /// Constructor that takes a working directory path and parameter file name
        /// </summary>
        /// <remarks>
        /// Parameter file name and working directory path will be validated in ParseKeyValueParameterFile
        /// workDirPath and paramFileName can be blank if you plan to call ParseKeyValueParameterList
        /// </remarks>
        /// <param name="toolName">Tool name (for logging)</param>
        /// <param name="workDirPath">Directory with the parameter file</param>
        /// <param name="paramFileName">Parameter file name</param>
        public KeyValueParamFileReader(string toolName, string workDirPath, string paramFileName)
        {
            ErrorMessage = string.Empty;
            ToolName = toolName ?? "Undefined tool";
            ParamFileName = paramFileName;
            ParamFilePath = Path.Combine(workDirPath ?? string.Empty, paramFileName ?? string.Empty);
        }

        /// <summary>
        /// Convert the parameter info into a command line
        /// </summary>
        /// <remarks>Returns an empty string if multiple parameters resolve to the same argument name</remarks>
        /// <param name="paramFileEntries">Parameter names and values read from tool's parameter file</param>
        /// <param name="paramToArgMapping">Dictionary mapping parameter names to argument names</param>
        /// <param name="paramNamesToSkip">Parameter names in paramFileEntries to skip</param>
        /// <param name="argumentPrefix">Argument prefix; typically -- or -</param>
        /// <returns>String with command line arguments</returns>
        public string ConvertParamsToArgs(
            List<KeyValuePair<string, string>> paramFileEntries,
            Dictionary<string, string> paramToArgMapping,
            SortedSet<string> paramNamesToSkip,
            string argumentPrefix)
        {
            var cmdLineArguments = new StringBuilder(500);

            try
            {
                // Keep track of the arguments already appended (use case-sensitive matching)
                var argumentsAppended = new SortedSet<string>(StringComparer.Ordinal);

                foreach (var kvSetting in paramFileEntries)
                {
                    if (paramNamesToSkip?.Contains(kvSetting.Key) == true)
                        continue;

                    // Check whether kvSetting.key is one of the standard keys defined in paramToArgMapping
                    if (paramToArgMapping.TryGetValue(kvSetting.Key, out var argumentName))
                    {
                        if (argumentsAppended.Contains(argumentName))
                        {
                            var errMsg = string.Format(
                                "Duplicate argument {0} specified for parameter {1} in the {2} parameter file",
                                argumentName, kvSetting.Key, ToolName);

                            LogError(errMsg);
                            return string.Empty;
                        }

                        argumentsAppended.Add(argumentName);

                        cmdLineArguments.Append(" " + argumentPrefix + argumentName + " " + kvSetting.Value);
                    }
                    else
                    {
                        OnWarningEvent("Ignoring unknown setting {0} from parameter file {1}", kvSetting.Key, Path.GetFileName(ParamFilePath));
                    }
                }
            }
            catch (Exception ex)
            {
                var errMsg = string.Format(
                    "Exception converting parameters loaded from the {0} parameter file into command line arguments",
                    ToolName);

                LogError(errMsg, ex);
                return string.Empty;
            }

            return cmdLineArguments.ToString();
        }

        private static IEnumerable<KeyValuePair<string, string>> GetKeyValueParameters(IEnumerable<KeyValueParamFileLine> paramFileLines)
        {
            var paramFileEntries = from paramFileLine in paramFileLines
                                   where !string.IsNullOrWhiteSpace(paramFileLine.ParamName)
                                   select new KeyValuePair<string, string>(paramFileLine.ParamName, paramFileLine.ParamValue);
            return paramFileEntries;
        }

        /// <summary>
        /// Parse settingText to extract the key name and value (separated by an equals sign)
        /// </summary>
        /// <remarks>
        /// If the line starts with # it is treated as a comment line and an empty key/value pair will be returned
        /// If the line contains a # sign in the middle, the comment text is stored in output argument comment
        /// </remarks>
        /// <param name="settingText"></param>
        /// <param name="removeComment">
        /// When true, if the value of the setting has a # delimited comment, remove it
        /// When false, the value of the setting will include the comment
        /// (default false)
        /// </param>
        /// <returns>Key/Value pair</returns>
        public static KeyValuePair<string, string> GetKeyValueSetting(string settingText, bool removeComment = false)
        {
            return GetKeyValueSetting(settingText, out _, removeComment);
        }

        /// <summary>
        /// Parse settingText to extract the key name and value (separated by an equals sign)
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the line starts with #, it is treated as a comment line and an empty key/value pair will be returned
        /// </para>
        /// <para>
        /// If the line contains a # sign in the middle, the comment text will be removed from the value if removeComment is true
        /// </para>
        /// <para>
        /// In either case, output argument comment will include the comment
        /// </para>
        /// </remarks>
        /// <param name="settingText">Text to parse</param>
        /// <param name="comment">Output: Comment text, if any (including the # sign)</param>
        /// <param name="removeComment">
        /// When true, if the value of the setting has a # delimited comment, remove it
        /// When false, the value of the setting will include the comment
        /// (default true, since this method has output argument comment)
        /// </param>
        /// <returns>Key/Value pair</returns>
        public static KeyValuePair<string, string> GetKeyValueSetting(string settingText, out string comment, bool removeComment = true)
        {
            comment = string.Empty;

            var emptyKvPair = new KeyValuePair<string, string>(string.Empty, string.Empty);

            if (string.IsNullOrWhiteSpace(settingText))
                return emptyKvPair;

            settingText = settingText.Trim();

            if (settingText.StartsWith("#") || !settingText.Contains('='))
                return emptyKvPair;

            var charIndex = settingText.IndexOf("=", StringComparison.Ordinal);

            if (charIndex <= 0)
                return emptyKvPair;

            var key = settingText.Substring(0, charIndex).Trim();

            var value = charIndex < settingText.Length - 1
                ? settingText.Substring(charIndex + 1).Trim()
                : string.Empty;

            var commentIndex = value.IndexOf('#');

            if (commentIndex >= 0)
            {
                comment = value.Substring(commentIndex).Trim();
            }

            if (commentIndex < 0 || !removeComment)
                return new KeyValuePair<string, string>(key, value);

            var valueClean = commentIndex < settingText.Length - 1
                ? value.Substring(0, commentIndex).Trim()
                : string.Empty;

            return new KeyValuePair<string, string>(key, valueClean);
        }

        /// <summary>
        /// Get the value associated with the given parameter
        /// </summary>
        /// <param name="paramFileEntries"></param>
        /// <param name="paramName"></param>
        /// <param name="valueIfMissing"></param>
        public static string GetParameterValue(List<KeyValuePair<string, string>> paramFileEntries, string paramName, string valueIfMissing = "")
        {
            foreach (var paramEntry in paramFileEntries.Where(paramEntry => paramEntry.Key.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
            {
                return paramEntry.Value ?? string.Empty;
            }

            return valueIfMissing;
        }

        /// <summary>
        /// Get the boolean value associated with the given parameter
        /// </summary>
        /// <remarks>Will return valueIfMissing if the parameter is missing or does not contain "true" or "false"</remarks>
        /// <param name="paramFileEntries"></param>
        /// <param name="paramName"></param>
        /// <param name="valueIfMissing"></param>
        public static bool GetParameterValue(List<KeyValuePair<string, string>> paramFileEntries, string paramName, bool valueIfMissing)
        {
            foreach (var paramEntry in paramFileEntries.Where(paramEntry => paramEntry.Key.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
            {
                if (bool.TryParse(paramEntry.Value, out var value))
                    return value;

                ConsoleMsgUtils.ShowWarning("Value for parameter {0} is not a boolean: {1}", paramName, paramEntry.Value ?? string.Empty);
                return valueIfMissing;
            }

            return valueIfMissing;
        }

        /// <summary>
        /// Get the integer value associated with the given parameter
        /// </summary>
        /// <remarks>Will return valueIfMissing if the parameter is missing or does not contain an integer</remarks>
        /// <param name="paramFileEntries"></param>
        /// <param name="paramName"></param>
        /// <param name="valueIfMissing"></param>
        public static int GetParameterValue(List<KeyValuePair<string, string>> paramFileEntries, string paramName, int valueIfMissing)
        {
            foreach (var paramEntry in paramFileEntries.Where(paramEntry => paramEntry.Key.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
            {
                if (int.TryParse(paramEntry.Value, out var value))
                    return value;

                ConsoleMsgUtils.ShowWarning("Value for parameter {0} is not an integer: {1}", paramName, paramEntry.Value ?? string.Empty);
                return valueIfMissing;
            }

            return valueIfMissing;
        }
        private void LogError(string errorMessage)
        {
            ErrorMessage = errorMessage;
            OnErrorEvent(errorMessage);
        }

        private void LogError(string errorMessage, Exception ex)
        {
            ErrorMessage = errorMessage;
            OnErrorEvent(errorMessage, ex);
        }

        /// <summary>
        /// Returns true if paramFileEntries contains parameter paramName and the parameter's value is True or a positive integer
        /// </summary>
        /// <param name="paramFileEntries"></param>
        /// <param name="paramName"></param>
        /// <param name="caseSensitiveParamName">When true, require a case-sensitive match to the parameter names in paramFileEntries</param>
        public bool ParamIsEnabled(List<KeyValuePair<string, string>> paramFileEntries, string paramName, bool caseSensitiveParamName = false)
        {
            var stringComp = caseSensitiveParamName ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            foreach (var kvSetting in paramFileEntries)
            {
                if (!string.Equals(kvSetting.Key, paramName, stringComp))
                    continue;

                if (bool.TryParse(kvSetting.Value, out var boolValue))
                {
                    return boolValue;
                }

                if (int.TryParse(kvSetting.Value, out var parsedValue) && parsedValue > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Read a parameter file with Key=Value settings (as used by MS-GF+, MSPathFinder, and TopPIC)
        /// </summary>
        /// <param name="paramFileEntries">Output: List of setting names and values read from the parameter file</param>
        /// <param name="removeComments">When true, remove # delimited comments from setting values</param>
        /// <returns>True if success, false if an error</returns>
        public bool ParseKeyValueParameterFile(out List<KeyValuePair<string, string>> paramFileEntries, bool removeComments = false)
        {
            var success = ParseKeyValueParameterFileWork(out var paramFileLines, removeComments);

            if (!success)
            {
                paramFileEntries = new List<KeyValuePair<string, string>>();
                return false;
            }

            paramFileEntries = GetKeyValueParameters(paramFileLines).ToList();

            if (paramFileEntries.Count > 0)
                return true;

            LogError(string.Format("{0} parameter file has no valid Key=Value settings", ToolName));
            return false;
        }

        /// <summary>
        /// Read a parameter file with Key=Value settings (as used by MS-GF+, MSPathFinder, and TopPIC)
        /// </summary>
        /// <param name="paramFileLines">Output: contents of the parameter file, including parsed keys and values</param>
        /// <param name="removeComments">When true, remove # delimited comments from setting values</param>
        /// <returns>True if success, false if an error</returns>
        public bool ParseKeyValueParameterFileGetAllLines(out List<KeyValueParamFileLine> paramFileLines, bool removeComments = false)
        {
            var success = ParseKeyValueParameterFileWork(out paramFileLines, removeComments);

            if (GetKeyValueParameters(paramFileLines).Any())
                return success;

            LogError(string.Format("{0} parameter file has no valid Key=Value settings", ToolName));
            return false;
        }

        /// <summary>
        /// Read a parameter file with Key=Value settings (as used by MS-GF+, MSPathFinder, and TopPIC)
        /// </summary>
        /// <param name="paramFileLines">Output: contents of the parameter file (each item in the list has the line number and the full data line)</param>
        /// <param name="removeComments">When true, remove # delimited comments from setting values</param>
        /// <returns>True if success, false if an error</returns>
        private bool ParseKeyValueParameterFileWork(out List<KeyValueParamFileLine> paramFileLines, bool removeComments = false)
        {
            paramFileLines = new List<KeyValueParamFileLine>();
            ParamFileNotFound = false;

            if (string.IsNullOrWhiteSpace(ParamFileName))
            {
                ParamFileNotFound = true;
                LogError(ToolName + " parameter file not defined when instantiating the KeyValueParamFileReader class");
                return false;
            }

            if (!File.Exists(ParamFilePath))
            {
                ParamFileNotFound = true;
                LogError(ToolName + " parameter file not found: " + ParamFilePath);
                return false;
            }

            try
            {
                using var paramFileReader = new StreamReader(new FileStream(ParamFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                var lineNumber = 0;

                while (!paramFileReader.EndOfStream)
                {
                    var dataLine = paramFileReader.ReadLine();
                    lineNumber++;

                    var paramFileLine = new KeyValueParamFileLine(lineNumber, dataLine);

                    var kvSetting = GetKeyValueSetting(dataLine, out var comment, removeComments);

                    if (!string.IsNullOrWhiteSpace(kvSetting.Key))
                    {
                        paramFileLine.StoreParameter(kvSetting, comment);
                    }

                    paramFileLines.Add(paramFileLine);
                }

                return true;
            }
            catch (Exception ex)
            {
                var errMsg = string.Format(
                    "Exception reading {0} parameter file {1} in ParseKeyValueParameterFileWork",
                    ToolName, Path.GetFileName(ParamFilePath));

                LogError(errMsg, ex);
                return false;
            }
        }

        /// <summary>
        /// Parse Key=Value settings defined in a list of strings
        /// </summary>
        /// <param name="parameterList">List of Key=Value settings to parse</param>
        /// <param name="paramFileEntries">Output: List of setting names and values read from the parameter file</param>
        /// <param name="removeComments">When true, remove # delimited comments from setting values</param>
        /// <returns>True if success, false if an error</returns>
        public bool ParseKeyValueParameterList(List<string> parameterList, out List<KeyValuePair<string, string>> paramFileEntries, bool removeComments = false)
        {
            var paramFileLines = new List<KeyValueParamFileLine>();
            ParamFileNotFound = false;
            bool success;

            try
            {
                var lineNumber = 0;

                foreach (var item in parameterList)
                {
                    lineNumber++;
                    ParseParameterEntry(paramFileLines, lineNumber, item, removeComments);
                }

                success = true;
            }
            catch (Exception ex)
            {
                LogError("Exception parsing list of parameters passed to ParseKeyValueParameterList", ex);
                success = false;
            }

            if (!success)
            {
                paramFileEntries = new List<KeyValuePair<string, string>>();
                return false;
            }

            paramFileEntries = GetKeyValueParameters(paramFileLines).ToList();

            if (paramFileEntries.Count > 0)
                return true;

            LogError("No valid Key=Value settings were found in parameterList passed to ParseKeyValueParameterList");
            return false;
        }

        private static void ParseParameterEntry(ICollection<KeyValueParamFileLine> paramFileLines, int lineNumber, string item, bool removeComments)
        {
            var paramFileLine = new KeyValueParamFileLine(lineNumber, item);

            var kvSetting = GetKeyValueSetting(item, out var comment, removeComments);

            if (!string.IsNullOrWhiteSpace(kvSetting.Key))
            {
                paramFileLine.StoreParameter(kvSetting, comment);
            }

            paramFileLines.Add(paramFileLine);
        }
    }
}
