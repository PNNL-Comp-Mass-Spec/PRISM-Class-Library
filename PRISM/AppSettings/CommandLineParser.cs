﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

// ReSharper disable UnusedMember.Global

// ReSharper disable once CheckNamespace
namespace PRISM
{
    // Ignore Spelling: arg, args, asm, bool, Conf, dd, dir, foreach, nameof, Preprocess, templated, typeof

    /// <summary>
    /// <para>
    /// Class for keeping parameters flags and properties for command line arguments tied together,
    /// supporting properties of primitive types (and arrays of primitive types)
    /// </para>
    /// <para>
    /// Supports parameter flags similar to /d -dd --dir, with case sensitivity when needed,
    /// with the separator between parameter flag and parameter as ' ', ':', or '=',
    /// and also supports using a parameter flag as a switch (if the associated property is a bool)
    /// </para>
    /// <para>
    /// If an argument is supplied multiple times, it only keeps the last one supplied.
    /// If the property is an array, multiple values are provided using '-paramName value -paramName value ...' or similar.
    /// Includes support for showing help with no args supplied, or with argument names of "?" and "help" (can be overridden).
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Either call static method ParseArgs like this:
    ///   static int Main(string[] args) {
    ///     var options = new ProgramOptions();
    ///     var asmName = typeof(Program).GetTypeInfo().Assembly.GetName();
    ///     var version = ProgramOptions.GetAppVersion();
    ///     if (!CommandLineParser&lt;ProgramOptions&gt;.ParseArgs(args, options, asmName.Name, version))
    ///     {
    ///         return -1;
    ///     }
    ///     if (!options.ValidateArgs(out var errorMessage))
    ///     {
    ///         ConsoleMsgUtils.ShowWarning("Validation error:");
    ///         ConsoleMsgUtils.ShowWarning(errorMessage);
    ///         return -1;
    ///     }
    ///   }
    /// </para>
    /// <para>
    /// Or instantiate this class, which allows for suppressing the auto-display of the syntax if an argument error is encountered
    ///   static int Main(string[] args) {
    ///     var asmName = typeof(Program).GetTypeInfo().Assembly.GetName();
    ///     var exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
    ///     var version = ProgramOptions.GetAppVersion();
    ///     var parser = new CommandLineParser&lt;ProgramOptions&gt;(asmName.Name, version)
    ///     {
    ///         ProgramInfo = "This program ...",
    ///         ContactInfo = "Program written by ...",
    ///         UsageExamples = {
    ///               exeName + @" C:\WorkDir InputFile.txt /Preview",
    ///               exeName + @" C:\WorkDir InputFile.txt",
    ///               exeName + @" C:\WorkDir InputFile.txt /Debug",
    ///         }
    ///     };
    ///     var parseResults = parser.ParseArgs(args);
    ///     var options = parseResults.ParsedResults;
    ///   }
    /// </para>
    /// <para>
    ///     if (!parseResults.Success)
    ///     {
    ///       return -1;
    ///     }
    /// </para>
    /// <para>
    ///     if (!options.ValidateArgs(out var errorMessage))
    ///     {
    ///         parser.PrintHelp();
    ///         Console.WriteLine();
    ///         ConsoleMsgUtils.ShowWarning("Validation error:");
    ///         ConsoleMsgUtils.ShowWarning(errorMessage);
    ///         return -1;
    ///     }
    /// </para>
    /// <para>    options.OutputSetOptions();</para>
    /// <para>An example class suitable for use when instantiating the CommandLineParser is GenericParserOptions in this project</para>
    /// </remarks>
    /// <typeparam name="T">Type</typeparam>
    public class CommandLineParser<T> where T : class, new()
    {
        // ReSharper disable StaticMemberInGenericType
        private static readonly char[] mDefaultParamChars = { '-', '/' };
        private static readonly char[] mDefaultSeparatorChars = { ' ', ':', '=' };
        private static readonly string[] mDefaultHelpArgs = { "?", "help" };
        private static readonly string[] mDefaultParamFileArgs = { "ParamFile" };
        private static readonly string[] mDefaultCreateExampleParamFileArgs = { "CreateParamFile" };

        /// <summary>
        /// Tracks parameter parsing errors
        /// </summary>
        public readonly struct ParseErrorInfo
        {
            /// <summary>
            /// Set to true if a required parameter is missing
            /// </summary>
            public readonly bool IsMissingRequiredParameter;

            /// <summary>
            /// Error message
            /// </summary>
            public readonly string Message;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="message">Message</param>
            /// <param name="isMissingRequiredParameter">True if this is a required, but missing parameter</param>
            public ParseErrorInfo(string message, bool isMissingRequiredParameter = false)
            {
                Message = message;
                IsMissingRequiredParameter = isMissingRequiredParameter;
            }

            /// <summary>
            /// ToString overload (show the error message)
            /// </summary>
            public override string ToString()
            {
                return Message;
            }
        }

        /// <summary>
        /// Results from the parsing
        /// </summary>
        public class ParserResults
        {
            /// <summary>
            /// Parsing status - false if parsing failed
            /// </summary>
            public bool Success { get; private set; }

            /// <summary>
            /// Errors that occurred during parsing
            /// </summary>
            public IReadOnlyList<ParseErrorInfo> ParseErrors => mParseErrors;

            /// <summary>
            /// The path to the parameter file (if one was defined)
            /// </summary>
            /// <remarks>This is the parameter file name or path defined by the user; it is not necessarily a full path</remarks>
            public string ParamFilePath { get; internal set; }

            /// <summary>
            /// Target object, populated with the parsed arguments when the parsing completes
            /// </summary>
            public T ParsedResults { get; }

            /// <summary>
            /// Modifiable list of parsing errors
            /// </summary>
            private readonly List<ParseErrorInfo> mParseErrors = new();

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="parsed">Parsed results</param>
            public ParserResults(T parsed)
            {
                Success = true;
                ParsedResults = parsed;
                ParamFilePath = string.Empty;
            }

            /// <summary>
            /// Set the parsing status to failed
            /// </summary>
            /// <remarks>This is also called when -CreateParamFile is provided or if not arguments were specified</remarks>
            internal void Failed(string failureReason, bool showWarning = true)
            {
                if (showWarning)
                    ConsoleMsgUtils.ShowWarning("Argument parsing failed: " + failureReason);

                Success = false;
            }

            /// <summary>
            /// Add a Parsing error to the parsing error list
            /// </summary>
            /// <remarks>This is also called when -CreateParamFile is provided</remarks>
            /// <param name="message">Error message</param>
            /// <param name="isMissingRequiredParameter">True if this is a missing, required parameter</param>
            internal void AddParseError(string message, bool isMissingRequiredParameter = false)
            {
                var errorInfo = new ParseErrorInfo(message, isMissingRequiredParameter);
                mParseErrors.Add(errorInfo);
            }

            /// <summary>
            /// Add a Parsing error to the parsing error list
            /// </summary>
            /// <param name="format">Format string for string.Format()</param>
            /// <param name="args">Value to substitute in the format string</param>
            /// <returns>Error message</returns>
            [StringFormatMethod("format")]
            internal string AddParseError(string format, params object[] args)
            {
                var message = string.Format(format, args);
                AddParseError(message);
                return message;
            }

            /// <summary>
            /// Print the parsing errors to console
            /// </summary>
            /// <param name="skipMissingParamErrors">When true, do not show errors regarding missing required parameters</param>
            public void OutputErrors(bool skipMissingParamErrors = false)
            {
                foreach (var error in ParseErrors)
                {
                    if (error.IsMissingRequiredParameter && skipMissingParamErrors)
                        continue;

                    ConsoleMsgUtils.ShowWarning(error.Message);
                }
            }
        }

        private const int DEFAULT_PARAM_KEYS_FIELD_WIDTH = 18;
        private const int DEFAULT_PARAM_DESCRIPTION_FIELD_WIDTH = 56;

        private const string NULL_VALUE_FLAG = "null";

        private char[] paramChars = mDefaultParamChars;
        private char[] separatorChars = mDefaultSeparatorChars;
        private Dictionary<string, ArgInfo> validArguments;
        private Dictionary<PropertyInfo, OptionAttribute> propertiesAndAttributes;
        private readonly List<string> paramFileArgs = new(mDefaultParamFileArgs);

        /// <summary>
        /// Developer contact info
        /// </summary>
        /// <remarks>If defined, shown at the end of PrintHelp</remarks>
        public string ContactInfo { get; set; }

        /// <summary>
        /// This is set to true if one of the arguments is -CreateParamFile
        /// and thus an example parameter file was created or shown at the console
        /// </summary>
        public bool CreateParamFileProvided { get; private set; }

        /// <summary>
        /// Entry assembly name
        /// </summary>
        public string EntryAssemblyName { get; }

        /// <summary>
        /// Executable version info
        /// </summary>
        public string ExeVersionInfo { get; }

        /// <summary>
        /// When true, if a parameter has multiple key names, when showing the syntax using PrintHelp,
        /// hide any whose length is greater than ParamKeysFieldWidth - 2
        /// </summary>
        public bool HideLongParamKeyNamesAtConsole { get; set; }

        /// <summary>
        /// Field width for the left column (key names)
        /// </summary>
        /// <remarks>Minimum allowed value: 10</remarks>
        public int ParamKeysFieldWidth { get; set; }

        /// <summary>
        /// Field width for the right column (parameter descriptions)
        /// </summary>
        /// <remarks>Minimum allowed value: 20</remarks>
        public int ParamDescriptionFieldWidth { get; set; }

        /// <summary>
        /// Full path to the parameter file, if a parameter file was defined
        /// </summary>
        /// <remarks>
        /// <para>
        /// This path will be updated after calling ParseArgs
        /// </para>
        /// <para>
        /// A parameter file is defined, by default, using /ParamFile:ParameterFilePath.conf or -ParamFile ParameterFilePath.conf
        /// </para>
        /// <para>
        /// Additional argument names for specifying the parameter file can be defined
        /// by calling AddParamFileKey after instantiating the CommandLineParser class
        /// </para>
        /// </remarks>
        public string ParameterFilePath { get; private set; }

        /// <summary>
        /// If true, disables the support for the parameter file creation/parsing
        /// </summary>
        public bool DisableParameterFileSupport { get; set; }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// The characters allowed at the beginning of an argument specifier. Defaults to '-' and '/', and other characters are not supported.
        /// The point of this property is to change a program to only use '-' (or '--') or only use '/' (or '//') as the argument specifier prefix
        /// </summary>
        public IEnumerable<char> ParamFlagCharacters
        {
            get => paramChars;
            set
            {
                var newValue = value.ToArray();
                var distinct = newValue.Distinct().Where(x => mDefaultParamChars.Contains(x)).ToArray();

                if (distinct.Length == 0 && newValue.Length > 0)
                {
                    var badChars = newValue.Distinct().Where(x => !mDefaultParamChars.Contains(x)).ToArray();
                    throw new ArgumentException($"{nameof(ParamFlagCharacters)} only supports the following characters: '{string.Join("', '", mDefaultParamChars)}'; " +
                                                $"Character(s) '{string.Join("', '", badChars)}' are not supported.");
                }

                if (distinct.Length > 0)
                {
                    paramChars = distinct;
                }
            }
        }

        /// <summary>
        /// Get or set the characters allowed as separators between an argument specifier and argument value
        /// </summary>
        public IEnumerable<char> ParamSeparatorCharacters
        {
            get => separatorChars;
            set
            {
                var distinct = value.Distinct().ToArray();

                if (distinct.Length > 0)
                {
                    separatorChars = distinct;
                }
            }
        }

        /// <summary>
        /// Description of the program's purpose / usage
        /// </summary>
        /// <remarks>If defined, shown at the start of PrintHelp (though after any error messages)</remarks>
        public string ProgramInfo { get; set; }

        /// <summary>
        /// Parsing results. Contains success value, target object, and error list
        /// </summary>
        public ParserResults Results { get; private set; }

        /// <summary>
        /// Usage examples to display to the user at the end of the help text
        /// </summary>
        public List<string> UsageExamples { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="entryAsmName">Name of the executing assembly</param>
        /// <param name="versionInfo">Executable version info</param>
        public CommandLineParser(string entryAsmName = "", string versionInfo = "") : this(new T(), entryAsmName, versionInfo)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Existing parsed options</param>
        /// <param name="entryAsmName">Name of the executing assembly</param>
        /// <param name="versionInfo">Executable version info</param>
        public CommandLineParser(T options, string entryAsmName = "", string versionInfo = "")
        {
            EntryAssemblyName = entryAsmName ?? string.Empty;
            ExeVersionInfo = versionInfo ?? string.Empty;
            DisableParameterFileSupport = false;
            ParameterFilePath = string.Empty;

            Results = new ParserResults(options);
            propertiesAndAttributes = null;
            validArguments = null;

            HideLongParamKeyNamesAtConsole = true;
            ParamKeysFieldWidth = DEFAULT_PARAM_KEYS_FIELD_WIDTH;
            ParamDescriptionFieldWidth = DEFAULT_PARAM_DESCRIPTION_FIELD_WIDTH;

            ProgramInfo = string.Empty;
            ContactInfo = string.Empty;
            UsageExamples = new List<string>();
        }

        /// <summary>
        /// Internal wrapper - returns an empty list if param file support is disabled
        /// </summary>
        private IReadOnlyList<string> AllowedParamFileArgs => DisableParameterFileSupport ? [] : paramFileArgs;

        /// <summary>
        /// Internal wrapper - returns an empty list if param file support is disabled
        /// </summary>
        private IReadOnlyList<string> AllowedCreateParamFileArgs => DisableParameterFileSupport ? [] : mDefaultCreateExampleParamFileArgs;

        /// <summary>
        /// Add additional param keys that can be used to specify a parameter file argument, for example "Conf"
        /// </summary>
        /// <remarks>The default argument name for parameter files is /ParamFile or -ParamFile</remarks>
        /// <param name="paramKey">Parameter key</param>
        public void AddParamFileKey(string paramKey)
        {
            if (string.IsNullOrWhiteSpace(paramKey))
            {
                return;
            }

            foreach (var existing in paramFileArgs)
            {
                if (existing.Equals(paramKey, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            paramFileArgs.Add(paramKey);
        }

        /// <summary>
        /// Writes the values in <see cref="Results"/>.ParsedResults as a parameter file
        /// </summary>
        /// <param name="paramFilePath">Path for the parameter file</param>
        /// <returns>True if the parameter file was created</returns>
        public bool CreateParamFile(string paramFilePath)
        {
            if (string.IsNullOrWhiteSpace(paramFilePath))
            {
                return false;
            }

            return WriteParamFile(paramFilePath);
        }

        /// <summary>
        /// Parse the arguments into <paramref name="options"/>, returning a bool. Entry assembly name is retrieved via reflection
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <param name="options">Options</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns>True on success, false if argument parse failed</returns>
        public static bool ParseArgs(string[] args, T options, string versionInfo = "")
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
            return ParseArgs(args, options, entryAssemblyName, versionInfo);
        }

        /// <summary>
        /// Parse the arguments into <paramref name="options"/>, returning a bool
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <param name="options">Options</param>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns>True on success, false if argument parse failed</returns>
        public static bool ParseArgs(string[] args, T options, string entryAssemblyName, string versionInfo)
        {
            var parser = new CommandLineParser<T>(entryAssemblyName, versionInfo)
            {
                Results = new ParserResults(options)
            };

            return parser.ParseArgs(args).Success;
        }

        /// <summary>
        /// Parse the arguments, returning the parsing results in <see cref="ParserResults"/>. Entry assembly name is retrieved via reflection
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns>Parser results</returns>
        public static ParserResults ParseArgs(string[] args, string versionInfo)
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
            return ParseArgs(args, entryAssemblyName, versionInfo);
        }

        /// <summary>
        /// Parse the arguments, returning the parsing results in <see cref="ParserResults"/>
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns>Parser results</returns>
        public static ParserResults ParseArgs(string[] args, string entryAssemblyName, string versionInfo)
        {
            var parser = new CommandLineParser<T>(entryAssemblyName, versionInfo);
            return parser.ParseArgs(args);
        }

        /// <summary>
        /// Parse the arguments, returning the parsing results
        /// </summary>
        /// <remarks>
        /// The command line arguments in the args array can be provided in various forms, including:
        /// -i InputFile.txt
        /// -i:InputFile.txt
        /// -i=InputFile.txt
        /// /d
        /// -d
        /// --dir
        /// </remarks>
        /// <param name="args">Command line arguments</param>
        /// <param name="onErrorOutputHelp">When an error occurs, display the error and output the help</param>
        /// <param name="outputErrors">When an error occurs, output the error</param>
        /// <returns>Parser results</returns>
        public ParserResults ParseArgs(string[] args, bool onErrorOutputHelp = true, bool outputErrors = true)
        {
            if (args.Length == 0)
            {
                if (onErrorOutputHelp)
                {
                    // Automatically output help when no arguments are supplied
                    PrintHelp(ParamKeysFieldWidth, ParamDescriptionFieldWidth);
                }
                else if (outputErrors)
                {
                    // Show errors
                    Results.OutputErrors();
                }
                Results.Failed("Empty args array", false);
                return Results;
            }

            var createExampleParamFile = false;
            var exampleParamFilePath = string.Empty;

            try
            {
                // Parse the arguments into a dictionary
                var preprocessed = ArgsPreprocess(args, false);

                if (preprocessed == null)
                {
                    // Preprocessing failed, tell the user why
                    if (onErrorOutputHelp)
                    {
                        PrintHelp(ParamKeysFieldWidth, ParamDescriptionFieldWidth);
                    }
                    else if (outputErrors)
                    {
                        Results.OutputErrors();
                    }
                    Results.Failed("preprocessed is null");
                    return Results;
                }

                var props = GetPropertiesAttributes();
                var validArgs = GetValidArgs();

                // Show the help if a default help argument is provided, but only if the templated class does not define the argument provided
                foreach (var helpArg in mDefaultHelpArgs)
                {
                    // Make sure the help argument is not defined in the template class
                    if (preprocessed.ContainsKey(helpArg) && validArgs.ContainsKey(helpArg.ToLower()) && validArgs[helpArg.ToLower()].IsBuiltInArg)
                    {
                        PrintHelp(ParamKeysFieldWidth, ParamDescriptionFieldWidth);
                        Results.Failed("Help argument found: " + helpArg, false);
                        return Results;
                    }
                }

                // Look for any unknown arguments
                if (HasUnknownArguments(validArgs, preprocessed))
                {
                    if (outputErrors)
                    {
                        Results.OutputErrors();
                    }
                    Results.Failed("Unknown arguments found");
                    return Results;
                }

                var paramFileLoaded = false;
                DirectoryInfo paramFileDirectory = null;
                var filePreprocessedArgs = new Dictionary<string, List<string>>();

                // Check for a parameter file, and load any arguments from it
                // Do not automatically merge with the command-line arguments
                foreach (var paramFileArg in AllowedParamFileArgs)
                {
                    // Make sure the param file arg is not defined in the template class
                    if (!preprocessed.ContainsKey(paramFileArg) ||
                        !validArgs.ContainsKey(paramFileArg.ToLower()) ||
                        !validArgs[paramFileArg.ToLower()].IsBuiltInArg)
                    {
                        continue;
                    }

                    if (paramFileLoaded)
                    {
                        // Only permit one param file; I don't want to get into merging results from multiple param files in a predictable fashion
                        Results.AddParseError("Error: Only one parameter file argument allowed: {0}{1}", paramChars[0], paramFileArg);

                        if (outputErrors)
                        {
                            Results.OutputErrors();
                        }
                        Results.Failed("Multiple param file arguments");
                        return Results;
                    }

                    if (!ReadParamFile(preprocessed[paramFileArg].LastOrDefault(), out var paramFileLines, out paramFileDirectory))
                    {
                        if (outputErrors)
                        {
                            Results.OutputErrors();
                        }
                        Results.Failed("Param file read error");
                        return Results;
                    }

                    paramFileLoaded = true;

                    // Call ArgsPreprocess on the options loaded from the parameter file
                    var filePreprocessed = ArgsPreprocess(paramFileLines, true);

                    var emptyArrayArgs = new List<string>();

                    // Check and possibly error on duplicate parameters in the parameter file
                    foreach (var prop in props)
                    {
                        // TODO: Also need to check these against "ValidArgs", for param name case sensitivity
                        // find all matching arguments, including synonyms
                        var entries = filePreprocessed.Where(x => prop.Value.ParamKeys.Any(k => k.Equals(x.Key))).ToList();

                        // if multiple values and not an array, report an error
                        if (!prop.Key.PropertyType.IsArray && entries.Sum(x => x.Value.Count) > 1)
                        {
                            // Report error

                            var paramNameOrList = entries.Count > 1
                                ? string.Join("','", entries.Select(x => x.Key))
                                : entries[0].Key;

                            Results.AddParseError("Error: Duplicated parameter '{1}' in parameter file '{0}'", preprocessed[paramFileArg].LastOrDefault(), paramNameOrList);
                            Results.Failed("Duplicate parameter in param file: " + paramNameOrList);
                        }
                        else if (prop.Key.PropertyType.IsArray && entries.TrueForAll(x => x.Value.Count == 0 || x.Value.TrueForAll(y => string.IsNullOrWhiteSpace(y))))
                        {
                            emptyArrayArgs.AddRange(entries.Select(x => x.Key));
                        }
                    }

                    foreach (var arg in emptyArrayArgs)
                    {
                        filePreprocessed.Remove(arg);
                    }

                    if (!Results.Success)
                    {
                        if (outputErrors)
                        {
                            Results.OutputErrors();
                        }
                        return Results;
                    }

                    // TODO: This is code that could be used to merge multiple param files together

                    //// Add original results of ArgsPreprocess to the new preprocessed arguments
                    //foreach (var cmdArg in filePreprocessedArgs)
                    //{
                    //    if (filePreprocessed.TryGetValue(cmdArg.Key, out var paramValues))
                    //    {
                    //        // Always append the argument values to the end, so that command-line arguments will overwrite param file arguments
                    //        // The one exception is for array parameters, where the command-line arguments will add to the param file arguments
                    //        paramValues.AddRange(cmdArg.Value);
                    //    }
                    //    else
                    //    {
                    //        filePreprocessed.Add(cmdArg.Key, cmdArg.Value);
                    //    }
                    //}

                    // Use the merged preprocessed arguments
                    filePreprocessedArgs = filePreprocessed;
                }

                // Determine if we need to write an example parameter file
                foreach (var createExampleParamFileArg in AllowedCreateParamFileArgs)
                {
                    // Make sure the help arg is not defined in the template class
                    if (preprocessed.ContainsKey(createExampleParamFileArg) && validArgs.ContainsKey(createExampleParamFileArg.ToLower()) && validArgs[createExampleParamFileArg.ToLower()].IsBuiltInArg)
                    {
                        createExampleParamFile = true;
                        exampleParamFilePath = preprocessed[createExampleParamFileArg].LastOrDefault();
                    }
                }

                // Iterate over the list of properties with "OptionAttribute".
                // NOTE: This automatically silently ignores any unknown arguments from the command line or parameter file.
                foreach (var prop in props)
                {
                    var specified = false;
                    var keyGiven = string.Empty;
                    var value = new List<string>();

                    // Load the param file arguments first - for non-array arguments, later values in the list override earlier values
                    // Find any param file arguments that match this property
                    foreach (var key in prop.Value.ParamKeys)
                    {
                        if (!filePreprocessedArgs.ContainsKey(key))
                        {
                            continue;
                        }

                        keyGiven = key;

                        var fileArgs = filePreprocessedArgs[key];

                        if (fileArgs.Count == 0 || fileArgs.All(string.IsNullOrWhiteSpace))
                        {
                            // We require parameter values in a parameter file to consider an argument valid
                            // If no value was provided in the file, just act as if the parameter it wasn't specified
                            // This also avoids overwriting default parameter values with type defaults
                            continue;
                        }

                        specified = true;

                        // Add in other values provided by argument keys that belong to this property
                        AppendArgumentValues(value, fileArgs.Where(x => !string.IsNullOrWhiteSpace(x)));
                    }

                    // Find any arguments that match this property
                    foreach (var key in prop.Value.ParamKeys)
                    {
                        var isSwitch = prop.Key.PropertyType == typeof(bool);

                        if (!preprocessed.ContainsKey(key))
                        {
                            continue;
                        }

                        keyGiven = key;
                        specified = true;

                        if (isSwitch && preprocessed[key].Count == 0)
                        {
                            // Switch arguments from the command line: force value override by removing any value(s) read from the param file(s).
                            // This fixes an issue with switch arguments not reading properly when a value is provided in the param file, unless a value is provided on the command line.
                            value.Clear();
                        }

                        // Add in other values provided by argument keys that belong to this property
                        AppendArgumentValues(value, preprocessed[key]);
                    }

                    var positionalArgName = GetPositionalArgName(prop.Value.ArgPosition);

                    if (prop.Value.ArgPosition > 0 && preprocessed.TryGetValue(positionalArgName, out var argValue))
                    {
                        keyGiven = "PositionalArgument" + prop.Value.ArgPosition;
                        specified = true;

                        AppendArgumentValues(value, argValue);
                    }

                    var currentValue = prop.Key.GetValue(Results.ParsedResults);

                    bool currentValueIsDefault;

                    if (prop.Key.PropertyType == typeof(string))
                    {
                        currentValueIsDefault = string.IsNullOrEmpty((string)currentValue);
                    }
                    else
                    {
                        var defaultValue = GetDefaultValue(prop.Key.PropertyType);

                        if (defaultValue == null)
                            currentValueIsDefault = currentValue != null;
                        else
                            currentValueIsDefault = currentValue?.Equals(defaultValue) != false;
                    }

                    if (prop.Value.Required && currentValueIsDefault && (!specified || value.Count == 0))
                    {
                        var message = string.Format("Error: Required argument missing: {0}{1}", paramChars[0], prop.Value.ParamKeys[0]);
                        Results.AddParseError(message, true);
                        Results.Failed("Missing required argument", !createExampleParamFile);
                    }

                    if (!specified)
                    {
                        continue;
                    }

                    // Switch handling - no value specified
                    if (prop.Key.PropertyType == typeof(bool) && (value.Count == 0 || string.IsNullOrWhiteSpace(value.Last())))
                    {
                        prop.Key.SetValue(Results.ParsedResults, true);
                        continue;
                    }

                    // ArgExistsProperty handling
                    if (prop.Value.ArgExistsPropertyInfo != null)
                    {
                        prop.Value.ArgExistsPropertyInfo.SetValue(Results.ParsedResults, true);

                        // if no value provided, then don't set it
                        if (value.Count == 0 || value.All(string.IsNullOrWhiteSpace))
                        {
                            continue;
                        }
                    }

                    if (value.Count == 0)
                    {
                        // The value was likely an empty string, which AppendArgumentValues ignores
                        value.Add(string.Empty);
                    }

                    var lastVal = value.Last();
                    try
                    {
                        // Parse/cast the value to the appropriate type, checking the min and max limits, and set the value using reflection
                        // Also, use "RemoveMatchingQuotes" to remove one pair of matching quotes, if they are provided
                        object castValue;

                        if (prop.Key.PropertyType.IsArray)
                        {
                            var castValues = Array.CreateInstance(prop.Key.PropertyType.GetElementType(), value.Count);
                            var i = 0;

                            foreach (var val in value.Select(RemoveMatchingQuotes))
                            {
                                lastVal = val;
                                var castVal = ParseValueToType(prop.Key.PropertyType.GetElementType(), prop.Value, keyGiven, val);
                                castValues.SetValue(castVal, i++);
                            }
                            castValue = castValues;
                        }
                        else
                        {
                            castValue = ParseValueToType(prop.Key.PropertyType, prop.Value, keyGiven, RemoveMatchingQuotes(value.Last()));
                        }
                        prop.Key.SetValue(Results.ParsedResults, castValue);
                    }
                    catch (InvalidCastException)
                    {
                        const string castError = @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""";
                        var failureMessage = Results.AddParseError(castError, keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed(failureMessage);
                    }
                    catch (FormatException)
                    {
                        const string castError = @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}"" using the given formatter";
                        var failureMessage = Results.AddParseError(castError, keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed(failureMessage);
                    }
                    catch (ArgumentException)
                    {
                        const string castError = @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}"" (ArgumentException)";
                        var failureMessage= Results.AddParseError(castError, keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed(failureMessage);
                    }
                    catch (OverflowException)
                    {
                        const string castError = @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}"" (out of range)";
                        var failureMessage= Results.AddParseError(castError, keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed(failureMessage);
                    }

                    if (!Results.Success || !prop.Value.IsInputFilePath)
                    {
                        continue;
                    }

                    // Assure that the path is not surrounded by single quotes or by double quotes
                    VerifyPathNotQuoted(prop);

                    if (paramFileLoaded)
                    {
                        // The current property specifies a file path
                        // Auto-update it if the file is not located in the current working directory
                        // Only do this if this path was read from a parameter file (and if it's not rooted)
                        VerifyFileOrDirectoryPath(prop, paramFileDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in CommandLineParser.ParseArgs", ex);
                Console.WriteLine();
                Console.WriteLine("Command line arguments:");

                foreach (var arg in args)
                {
                    Console.WriteLine(arg);
                }

                Results.Failed("Error in CommandLineParser.ParseArgs");
            }

            if (createExampleParamFile)
            {
                if (WriteParamFile(exampleParamFilePath) && !string.IsNullOrWhiteSpace(exampleParamFilePath))
                {
                    Results.AddParseError(@"Created example parameter file at ""{0}""", exampleParamFilePath);
                }

                CreateParamFileProvided = true;

                Results.AddParseError("-CreateParamFile provided; exiting program.");

                if (outputErrors)
                {
                    Results.OutputErrors(true);
                }

                Results.Failed("-CreateParamFile provided; exiting program", false);
                return Results;
            }

            if (Results.Success)
            {
                return Results;
            }

            if (onErrorOutputHelp)
            {
                PrintHelp(ParamKeysFieldWidth, ParamDescriptionFieldWidth);
            }
            else if (outputErrors)
            {
                Results.OutputErrors();
            }

            return Results;
        }

        /// <summary>
        /// Parse arguments only from a parameter file (not from the command line); does not output errors
        /// </summary>
        /// <remarks>
        /// The initial goal of this method is supporting the idea of a "processing method" save/load, for use in GUI programs
        /// </remarks>
        /// <param name="paramFilePath">Path (relative or absolute) to a parameter file</param>
        /// <returns>Parser results</returns>
        public ParserResults ParseParamFile(string paramFilePath)
        {
            var args = new[] { "-ParamFile", paramFilePath };
            return ParseArgs(args, false, false);
        }

        /// <summary>
        /// Reads a parameter file
        /// </summary>
        /// <param name="paramFilePath">Parameter file path</param>
        /// <param name="paramFileLines">Output: List of parameters read from the parameter file; each line will start with a dash</param>
        /// <param name="paramFileDirectory">Output: parameter file directory</param>
        /// <returns>True if success, false if an error</returns>
        private bool ReadParamFile(string paramFilePath, out List<string> paramFileLines, out DirectoryInfo paramFileDirectory)
        {
            var validParameterFilePath = false;

            paramFileLines = new List<string>();
            paramFileDirectory = null;

            try
            {
                if (string.IsNullOrWhiteSpace(paramFilePath))
                {
                    Results.AddParseError("Error: empty parameter file path; likely a programming bug");
                    return false;
                }

                Results.ParamFilePath = paramFilePath;

                var paramFile = new FileInfo(paramFilePath);

                ParameterFilePath = paramFile.FullName;
                validParameterFilePath = true;

                if (!paramFile.Exists)
                {
                    Results.AddParseError("Error: Specified parameter file was not found: " + paramFilePath);
                    Results.AddParseError("  ... Full path: " + paramFile.FullName);

                    return false;
                }

                paramFileDirectory = paramFile.Directory;

                // Read parameter file into List
                var lines = ReadParamFile(paramFile);

                paramFileLines.AddRange(lines);

                return true;
            }
            catch (Exception ex)
            {
                if (!validParameterFilePath)
                {
                    Results.AddParseError("Error: Invalid parameter file path: " + paramFilePath);
                    Results.AddParseError("Exception: " + ex.Message);
                }
                else
                {
                    Results.AddParseError("Error: Exception while reading the parameter file: " + ex.Message);
                }
                return false;
            }
        }

        /// <summary>
        /// Reads a parameter file
        /// </summary>
        /// <param name="paramFile">Parameter file</param>
        /// <returns>
        /// List of parameters read from the parameter file
        /// Each line will start with a dash
        /// </returns>
        private IEnumerable<string> ReadParamFile(FileSystemInfo paramFile)
        {
            var lines = new List<string>();

            if (!paramFile.Exists)
            {
                return lines;
            }

            try
            {
                var commentChars = new[] { '#', ';' };

                using var reader = new StreamReader(new FileStream(paramFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var trimmedLine = line.Trim();

                    if (trimmedLine.IndexOfAny(commentChars) == 0)
                    {
                        // Comment line; skip it
                        continue;
                    }

                    // Add '-' at the beginning of the list so that the arguments are properly recognized
                    if (trimmedLine.StartsWith("-"))
                    {
                        lines.Add(trimmedLine);
                    }
                    else
                    {
                        lines.Add("-" + trimmedLine);
                    }
                }
            }
            catch (Exception e)
            {
                var failureMessage = Results.AddParseError(@"Error reading parameter file ""{0}"": {1}", paramFile.FullName, e);
                Results.Failed("Error in ReadParamFile: " + failureMessage);
            }

            return lines;
        }

        /// <summary>
        /// Writes the values in <see cref="Results"/>.ParsedResults as a parameter file,
        /// or show at the console if paramFilePath is an empty string
        /// </summary>
        /// <param name="paramFilePath">Parameter file path</param>
        /// <returns>True if successful, false if an error</returns>
        private bool WriteParamFile(string paramFilePath)
        {
            var isFile = !string.IsNullOrWhiteSpace(paramFilePath);

            try
            {
                var lines = GetParamFileContents();

                if (isFile)
                {
                    var paramFile = new FileInfo(paramFilePath);
                    File.WriteAllLines(paramFile.FullName, lines);
                    return true;
                }

                Console.WriteLine();
                Console.WriteLine("##### Example parameter file contents: #####");
                Console.WriteLine();

                foreach (var line in lines)
                {
                    Console.WriteLine(line);
                }

                Console.WriteLine();
                Console.WriteLine("##### End Example parameter file contents: #####");
                Console.WriteLine();

                return true;
            }
            catch (Exception e)
            {
                var target = isFile ? $"file \"{paramFilePath}\"" : "console";
                ConsoleMsgUtils.ShowError(e, "Error writing parameters to {0}!", target);
                return false;
            }
        }

        private IEnumerable<string> GetParamFileContents()
        {
            var lines = new List<string>();
            var commentsProcessed = 0;
            var secondaryArguments = 0;

            var props = GetPropertiesAttributes();

            foreach (var prop in props.OrderByDescending(x => x.Value.Required))
            {
                if (prop.Value.Hidden)
                {
                    continue;
                }

                // Construct the comment description, e.g.
                // # Required: The name of the input file
                // or
                // # When true, log messages to a file

                // Parameter comments after the first comment will be preceded by a newline
                // (\n on Linux and \r\n on Windows)

                var paramComment = string.Format(
                    "{0}# {1}{2}",
                    commentsProcessed == 0 ? string.Empty : Environment.NewLine,
                    prop.Value.Required ? "Required: " : string.Empty,
                    prop.Value.HelpText?.Replace("\r\n", "\n").Replace("\n", Environment.NewLine + "# ") ?? "");

                if (paramComment.Trim().Length > 1)
                {
                    lines.Add(paramComment);
                }

                string prefix;

                if (prop.Value.SecondaryArg)
                {
                    // Comment out secondary arguments
                    prefix = "# ";
                    secondaryArguments++;
                }
                else
                {
                    prefix = string.Empty;
                }

                var key = prop.Value.ParamFileOutputParamName;

                if (prop.Key.PropertyType.IsArray)
                {
                    var obj = prop.Key.GetValue(Results.ParsedResults);

                    if (obj == null)
                    {
                        lines.Add(string.Format("{0}{1}={2}", prefix, key, string.Empty));
                    }
                    else
                    {
                        foreach (var value in (Array)obj)
                        {
                            var writeValue = value;

                            if (value is string str && string.IsNullOrWhiteSpace(str))
                            {
                                // wrap value in quotes to preserve the value on read
                                writeValue = $"\"{value}\"";
                            }

                            lines.Add(string.Format("{0}{1}={2}", prefix, key, writeValue));
                        }
                    }
                }
                else
                {
                    var value = prop.Key.GetValue(Results.ParsedResults);

                    if (value is string str && string.IsNullOrWhiteSpace(str))
                    {
                        // wrap value in quotes to preserve the value on read
                        value = $"\"{value}\"";
                    }
                    else if (value?.ToString() == "\0")
                    {
                        value = string.Empty;
                    }

                    lines.Add(string.Format("{0}{1}={2}", prefix, key, value));
                }

                commentsProcessed++;
            }

            if (secondaryArguments > 0)
            {
                lines.Add(string.Format(
                    "{0}{1}{0}{2}", Environment.NewLine,
                    "# Secondary arguments are shown above with their default value, but commented out using #",
                    "# Enable and customize them by removing # from the start of the Key=Value line"));
            }
            return lines;
        }

        /// <summary>
        /// Parses a value to the specified type, checking min and max limits
        /// </summary>
        /// <param name="propertyType">Property type</param>
        /// <param name="parseData">Data to parse</param>
        /// <param name="argKey">Argument key</param>
        /// <param name="valueToParse">Value to parse</param>
        /// <returns>Converted value</returns>
        private object ParseValueToType(Type propertyType, OptionAttribute parseData, string argKey, string valueToParse)
        {
            object castValue = null;

            if (!string.Equals(NULL_VALUE_FLAG, valueToParse, StringComparison.OrdinalIgnoreCase))
            {
                if (propertyType.IsValueType && string.IsNullOrEmpty(valueToParse))
                {
                    // Don't crash with empty strings
                    castValue = GetDefaultValue(propertyType);
                }
                else
                {
                    castValue = ConvertToType(valueToParse, propertyType);
                }
            }

            try
            {
                // Test the min/max, if supplied to the options attribute
                if (parseData.Min != null)
                {
                    // HACK: prevent allowed conversions from say, double to int; change the value to a string because Convert.ChangeType cannot do 2-step conversions (like string->double->int)
                    if (ConvertToType(parseData.Min.ToString(), propertyType) is IComparable castMin)
                    {
                        if (castMin.CompareTo(castValue) > 0)
                        {
                            const string parseError = "Error: argument {0}, value of {1} is less than minimum of {2}";
                            var failureMessage = Results.AddParseError(parseError, argKey, castValue, castMin);
                            Results.Failed(failureMessage);
                        }
                    }
                    else
                    {
                        const string parseError = @"Error: argument {0}, unable to check value of {1} against minimum of ""{2}"": cannot cast/compare minimum to type ""{3}""";
                        var failureMessage = Results.AddParseError(parseError, argKey, castValue, parseData.Min, propertyType.Name);
                        Results.Failed(failureMessage);
                    }
                }

                if (parseData.Max != null)
                {
                    // HACK: prevent allowed conversions from say, double to int; change the value to a string because Convert.ChangeType cannot do 2-step conversions (like string->double->int)
                    if (ConvertToType(parseData.Max.ToString(), propertyType) is IComparable castMax)
                    {
                        if (castMax.CompareTo(castValue) < 0)
                        {
                            const string parseError = "Error: argument {0}, value of {1} is greater than maximum of {2}";
                            var failureMessage = Results.AddParseError(parseError, argKey, castValue, castMax);
                            Results.Failed(failureMessage);
                        }
                    }
                    else
                    {
                        const string parseError = @"Error: argument {0}, unable to check value of {1} against maximum of ""{2}"": cannot cast/compare maximum to type ""{3}""";
                        Results.AddParseError(parseError, argKey, castValue, parseData.Max, propertyType.Name);
                        Results.Failed(parseError);
                    }
                }
            }
            catch (InvalidCastException)
            {
                const string castError = @"Error: argument {0}, cannot cast min or max to type ""{1}""";
                Results.AddParseError(castError, argKey, propertyType.Name);
                Results.Failed(castError);
            }
            catch (FormatException)
            {
                const string castError = @"Error: argument {0}, cannot cast min or max to type ""{1}"" using the given formatter";
                Results.AddParseError(castError, argKey, propertyType.Name);
                Results.Failed(castError);
            }
            catch (ArgumentException)
            {
                const string castError = @"Error: argument {0}, cannot cast min or max to type ""{1}"" (ArgumentException)";
                Results.AddParseError(castError, argKey, propertyType.Name);
                Results.Failed(castError);
            }
            catch (OverflowException)
            {
                const string castError = @"Error: argument {0}, cannot cast min or max to type ""{1}"" (out of range)";
                Results.AddParseError(castError, argKey, propertyType.Name);
                Results.Failed(castError);
            }
            return castValue;
        }

        /// <summary>
        /// Parse most objects normally, but parse enums using Enum.Parse
        /// </summary>
        /// <param name="valueToConvert">Value to convert</param>
        /// <param name="targetType">Target type</param>
        private static object ConvertToType(object valueToConvert, Type targetType)
        {
            // Properly parse enums
            if (targetType.IsEnum)
            {
                var result = Enum.Parse(targetType, valueToConvert.ToString(), true);

                if (!Enum.IsDefined(targetType, result) && targetType.CustomAttributes.All(x => x.AttributeType != typeof(FlagsAttribute)))
                {
                    throw new ArgumentException("Cast attempted to undefined enum!", nameof(valueToConvert));
                }
                return result;
            }

            if (targetType != typeof(bool))
            {
                return Convert.ChangeType(valueToConvert, targetType);
            }

            // Support using '0', '1', 'y', 'yes', 'n', 'no' with booleans
            var valueLCase = valueToConvert.ToString().ToLowerInvariant();

            if (int.TryParse(valueLCase, out var boolResult))
            {
                return boolResult != 0;
            }

            return valueLCase switch
            {
                "n" => false,
                "no" => false,
                "y" => true,
                "yes" => true,
                _ => Convert.ChangeType(valueToConvert, targetType)
            };
        }

        private static void AppendArgumentValue(ICollection<string> existingArgumentValues, string newArgumentValue)
        {
            AppendArgumentValues(existingArgumentValues, new List<string> { newArgumentValue });
        }

        private static void AppendArgumentValues(ICollection<string> existingArgumentValues, IEnumerable<string> newArgumentValues)
        {
            // Append new argument values, trimming trailing \r or \n characters

            // This can happen while debugging with Visual Studio if the user pastes a list of arguments
            // into the Command Line Arguments text box, and the pasted text contains a carriage return

            foreach (var value in newArgumentValues)
            {
                var trimmedValue = value.Trim('\r', '\n');

                if (string.IsNullOrWhiteSpace(trimmedValue))
                    continue;

                existingArgumentValues.Add(trimmedValue);
            }
        }

        /// <summary>
        /// Parse the arguments to a dictionary
        /// </summary>
        /// <remarks>
        /// Arguments loaded from a parameter file will each start with a dash, followed by the argument name, then an equals sign, then the value
        /// </remarks>
        /// <param name="args">List of arguments</param>
        /// <param name="parsingParamFileArgs">Set this to true if the arguments were loaded from a parameter file</param>
        /// <returns>
        /// Dictionary where keys are argument names and values are the setting for the argument
        /// Values are a list in case the parameter is specified more than once
        /// </returns>
        private Dictionary<string, List<string>> ArgsPreprocess(IReadOnlyList<string> args, bool parsingParamFileArgs)
        {
            var validArgs = GetValidArgs();

            if (validArgs == null)
            {
                return null;
            }

            // In this dictionary, keys are argument names and values are the settings for the argument
            var processed = new Dictionary<string, List<string>>();
            var positionArgumentNumber = 0;

            var whitespaceCharsToTrim = new[] { '\t', '\r', '\n'};

            for (var i = 0; i < args.Count; i++)
            {
                // Assure that the argument does not end with a tab, carriage return, or newline
                // This can happen while debugging with Visual Studio if the user pastes a list of arguments
                // into the Command Line Arguments text box, and the pasted text contains a carriage return
                var trimmedArg = args[i].TrimEnd(whitespaceCharsToTrim);

                if (string.IsNullOrWhiteSpace(trimmedArg))
                {
                    // The argument was likely "\r\n" and thus trimmedArg is an empty string
                    continue;
                }

                var argIsLinuxRootedPath = paramChars.Contains('/') && trimmedArg.StartsWith("/") &&
                                           (trimmedArg.TrimStart('/').Count(x => x == '/') >= 1 ||
                                            ((File.Exists(trimmedArg) || Directory.Exists(trimmedArg)) &&
                                             !validArgs.ContainsKey(trimmedArg.TrimStart('/'))));

                if (!parsingParamFileArgs && (argIsLinuxRootedPath || !paramChars.Contains(trimmedArg[0])))
                {
                    // Positional argument
                    positionArgumentNumber++;
                    var argName = GetPositionalArgName(positionArgumentNumber);

                    if (!validArgs.TryGetValue(argName, out _))
                        continue;

                    if (!processed.ContainsKey(argName))
                    {
                        processed.Add(argName, new List<string>());
                    }

                    AppendArgumentValue(processed[argName], trimmedArg);
                    continue;
                }

                var key = trimmedArg.TrimStart(paramChars);
                var value = string.Empty;

                var containedSeparator = false;

                // Only split off the first separator, since others may be part of drive specifiers
                var separatorIndex = key.IndexOfAny(separatorChars);

                if (separatorIndex > -1)
                {
                    containedSeparator = true;
                    value = key.Substring(separatorIndex + 1);
                    key = key.Substring(0, separatorIndex);
                }

                if (!containedSeparator && !parsingParamFileArgs && i + 1 < args.Count)
                {
                    var nextArg = args[i + 1];
                    var nextArgIsNumber = false;
                    var nextArgIsLinuxRootedPath = false;

                    if (paramChars.Contains('-') && nextArg.StartsWith("-"))
                    {
                        // Try converting to most forgiving number format
                        nextArgIsNumber = double.TryParse(nextArg, out _);

                        // Check if the program supports a numeric argument (but we only need to remove a '-', because a '/' won't parse as a double)
                        if (nextArgIsNumber && validArgs.ContainsKey(nextArg.TrimStart('-')))
                        {
                            nextArgIsNumber = false;
                        }
                    }

                    if (paramChars.Contains('/') && nextArg.StartsWith("/"))
                    {
                        // ReSharper disable once GrammarMistakeInComment

                        // Don't try to treat a Linux rooted path (starts with '/') as an argument
                        if (nextArg.TrimStart('/').Count(x => x == '/') >= 1)
                        {
                            // Path has at least one '/' that is not at the start of the string
                            nextArgIsLinuxRootedPath = true;
                        }
                        else if ((File.Exists(nextArg) || Directory.Exists(nextArg)) && !validArgs.ContainsKey(nextArg.TrimStart('/')))
                        {
                            // Path exists on current system, and is not an argument name
                            // TODO: Could possibly change this check to not include the 'exists' check, and just check if it's not a valid argument, but needs more testing for error conditions first.
                            nextArgIsLinuxRootedPath = true;
                        }
                    }

                    if (nextArgIsNumber || nextArgIsLinuxRootedPath || !paramChars.Contains(args[i + 1][0]))
                    {
                        value = args[i + 1];
                        i++;
                    }
                }

                // Key normalization - usually allow case-insensitivity
                var ciKey = key.ToLower();

                if (validArgs.TryGetValue(ciKey, out var argInfo))
                {
                    // if argument is case-sensitive, make sure it matches an argument
                    if (argInfo.CaseSensitive && !argInfo.AllArgNormalCase.Contains(key))
                    {
                        // Return an error if there is a case-sensitive argument, and we only matched to it when comparing case-insensitive
                        Results.AddParseError("Error: Arg {0} does not match valid argument", key);
                        return null;
                    }

                    if (!argInfo.CaseSensitive)
                    {
                        key = argInfo.ArgNormalCase;
                    }
                }

                // ReSharper disable once GrammarMistakeInComment

                // Keep track of each of the values defined for an argument (if listed multiple times on the command line or in a parameter file)
                // If the argument's property is an array, all of the arguments are kept
                // If the argument's property is not an array, the last duplicate option gets priority (see "var lastVal = value.Last()" in ParseArgs)

                if (!processed.ContainsKey(key))
                {
                    processed.Add(key, new List<string>());
                }

                AppendArgumentValue(processed[key], value);
            }

            return processed;
        }

        private static object GetDefaultValue(Type t)
        {
            if (t.IsValueType)
            {
                return Activator.CreateInstance(t);
            }

            return null;
        }

        /// <summary>
        /// Generate the special argument name used to track positional arguments
        /// </summary>
        /// <param name="argPosition">Argument position</param>
        /// <returns>Argument name</returns>
        private static string GetPositionalArgName(int argPosition)
        {
            return "##" + argPosition + "##";
        }

        /// <summary>
        /// Look for any unrecognized command line arguments
        /// </summary>
        /// <param name="validArgs">Dictionary of valid arguments read from the template class; includes mDefaultHelpArgs, mDefaultParamFileArgs, and mDefaultCreateExampleParamFileArgs</param>
        /// <param name="suppliedArgs">Dictionary of user-supplied arguments; keys are argument names and values are the argument value (or values)</param>
        private bool HasUnknownArguments(Dictionary<string, ArgInfo> validArgs, Dictionary<string, List<string>> suppliedArgs)
        {
            var knownArgNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var knownCaseSensitiveArgNames = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var validArg in validArgs)
            {
                if (validArg.Value.CaseSensitive)
                {
                    foreach (var item in validArg.Value.AllArgNormalCase)
                    {
                        knownCaseSensitiveArgNames.Add(item);
                    }
                }
                else
                {
                    foreach (var item in validArg.Value.AllArgNormalCase)
                    {
                        knownArgNames.Add(item);
                    }
                }
            }

            var invalidArguments = new List<string>();

            foreach (var userArg in suppliedArgs)
            {
                if (knownArgNames.Contains(userArg.Key) || knownCaseSensitiveArgNames.Contains(userArg.Key))
                    continue;

                invalidArguments.Add(userArg.Key);
                Results.AddParseError("Error: Unrecognized argument name: {0}", userArg.Key);
            }

            return invalidArguments.Count != 0;
        }

        /// <summary>
        /// Display the help contents, using the information supplied by the Option attributes and the default constructor for the templated class
        /// </summary>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <param name="paramKeysWidth">Field width for the left column (key names); minimum 10</param>
        /// <param name="helpDescriptionWidth">Field width for the right column (parameter descriptions); minimum 20</param>
        public static void ShowHelp(string entryAssemblyName = "", string versionInfo = "", int paramKeysWidth = 18, int helpDescriptionWidth = 56)
        {
            var parser = new CommandLineParser<T>(entryAssemblyName, versionInfo);
            parser.PrintHelp(paramKeysWidth, helpDescriptionWidth);
        }

        /// <summary>
        /// Display the help contents, using the information supplied by the Option attributes and the default constructor for the templated class
        /// </summary>
        /// <param name="paramKeysWidth">Field width for the left column (key names); minimum 10</param>
        /// <param name="helpDescriptionWidth">Field width for the right column (parameter descriptions); minimum 20</param>
        public void PrintHelp(int paramKeysWidth = 18, int helpDescriptionWidth = 56)
        {
            if (paramKeysWidth <= 0 && helpDescriptionWidth <= 0)
            {
                paramKeysWidth = DEFAULT_PARAM_KEYS_FIELD_WIDTH;
                helpDescriptionWidth = DEFAULT_PARAM_DESCRIPTION_FIELD_WIDTH;
            }

            if (paramKeysWidth < 10)
                paramKeysWidth = 10;

            if (helpDescriptionWidth < 20)
                helpDescriptionWidth = 20;

            var contents = CreateHelpContents(paramKeysWidth);

            // Output any errors that occurring while creating the help content
            Results.OutputErrors();

            if (!string.IsNullOrWhiteSpace(ProgramInfo))
            {
                Console.WriteLine();
                Console.WriteLine(WrapParagraph(ProgramInfo));
            }

            Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(ExeVersionInfo))
            {
                Console.WriteLine("{0} {1}", EntryAssemblyName, ExeVersionInfo);
            }
            if (!string.IsNullOrWhiteSpace(EntryAssemblyName))
            {
                Console.WriteLine();
                Console.WriteLine("Usage: {0}", EntryAssemblyName + ".exe");
            }
            else
            {
                Console.WriteLine("Usage:");
            }

            var outputFormatString = "  {0,-" + (paramKeysWidth + 1) + "}  {1}";

            // Output the help contents, creating columns with wrapping before outputting
            foreach (var option in contents)
            {
                var overflow = new List<Tuple<string, string>>();

                // Wrap the argument names
                var keyOverflow = WrapParagraphAsList(option.Key, paramKeysWidth);

                // Wrap the argument help text
                var textOverflow = WrapParagraphAsList(option.Value, helpDescriptionWidth);

                // Join the wrapped argument names and help text
                for (var i = 0; i < keyOverflow.Count || i < textOverflow.Count; i++)
                {
                    var key = string.Empty;
                    var text = string.Empty;

                    if (i < keyOverflow.Count)
                    {
                        key = keyOverflow[i];
                    }

                    if (i < textOverflow.Count)
                    {
                        if (textOverflow[i].StartsWith(" (Default:"))
                            text = textOverflow[i].Trim();
                        else
                            text = textOverflow[i];
                    }
                    overflow.Add(new Tuple<string, string>(key, text));
                }

                // Output the argument data with proper spacing
                Console.WriteLine();

                foreach (var line in overflow)
                {
                    Console.WriteLine(outputFormatString, line.Item1, line.Item2);
                }
            }

            Console.WriteLine();

            if (UsageExamples.Count > 0)
            {
                Console.WriteLine("Examples:");
                Console.WriteLine();

                foreach (var example in UsageExamples)
                {
                    Console.WriteLine(example);
                    Console.WriteLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(ContactInfo))
            {
                Console.WriteLine();
                Console.WriteLine(WrapParagraph(ContactInfo));
            }
        }

        /// <summary>
        /// Update the HelpText for a property at runtime
        /// </summary>
        /// <param name="propertyName">Property to update (case-sensitive)</param>
        /// <param name="helpText">New help text</param>
        public void UpdatePropertyHelpText(string propertyName, string helpText)
        {
            foreach (var property in GetPropertiesAttributes())
            {
                if (!string.Equals(property.Key.Name, propertyName))
                    continue;

                property.Value.HelpText = helpText;
            }
        }

        /// <summary>
        /// Change the HelpText for a property at runtime, searching for textToFind and replacing with replacementText
        /// </summary>
        /// <param name="propertyName">Property to update (case-sensitive)</param>
        /// <param name="textToFind">Text to find</param>
        /// <param name="replacementText">Text to use for a replacement</param>
        public void UpdatePropertyHelpText(string propertyName, string textToFind, string replacementText)
        {
            foreach (var property in GetPropertiesAttributes())
            {
                if (!string.Equals(property.Key.Name, propertyName))
                    continue;

                property.Value.HelpText = property.Value.HelpText.Replace(textToFind, replacementText);
            }
        }

        /// <summary>
        /// Create the help text and argument name list for each argument
        /// </summary>
        /// <param name="paramKeysWidth">
        /// Field width for the left column (key names)
        /// If HideLongParamKeyNamesAtConsole is true, key names longer than this width - 2 will be hidden
        /// </param>
        private Dictionary<string, string> CreateHelpContents(int paramKeysWidth)
        {
            // This dictionary tracks the key name(s) and help text for each parameter
            // Keys are the key name or list of key names (e.g. -InputFile, -InputFilePath, -i, -input, arg#1)
            // Values are the help text
            var contents = new Dictionary<string, string>();

            var duplicateKeyCheck = new Dictionary<string, PropertyInfo>();

            var optionsForDefaults = new T();

            var props = GetPropertiesAttributes();
            var validArgs = GetValidArgs();

            if (validArgs == null)
            {
                contents.Add("ERROR!!!", "Cannot determine arguments. Fix errors in code!");
                return contents;
            }

            var helpArgString = string.Empty;

            // Add the default help string
            foreach (var helpArg in mDefaultHelpArgs)
            {
                if (!validArgs.ContainsKey(helpArg.ToLower()) || !validArgs[helpArg.ToLower()].IsBuiltInArg)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(helpArgString))
                {
                    helpArgString += ", ";
                }

                helpArgString += paramChars[0] + helpArg;
            }

            if (!string.IsNullOrWhiteSpace(helpArgString))
            {
                contents.Add(helpArgString, "Show this help screen");
            }

            foreach (var prop in props)
            {
                if (prop.Value.Hidden)
                {
                    continue;
                }

                // Get the default value to display
                var defaultValueObj = prop.Key.GetValue(optionsForDefaults);
                var defaultValue = NULL_VALUE_FLAG;

                if (defaultValueObj != null)
                {
                    defaultValue = defaultValueObj.ToString();
                }

                if (string.IsNullOrWhiteSpace(defaultValue))
                {
                    defaultValue = $"\"{defaultValue}\"";
                }

                // Look for long key names that will be hidden
                var keyNamesToHide = new SortedSet<string>();

                if (HideLongParamKeyNamesAtConsole)
                {
                    GetLongKeyNamesToHide(paramKeysWidth, prop, keyNamesToHide);
                }

                // Create the list of parameter keys
                var keys = new List<string>();

                foreach (var key in prop.Value.ParamKeys)
                {
                    if (duplicateKeyCheck.ContainsKey(key))
                    {
                        // Critical error - make sure the user focuses on the error, because it's a big one, and must be fixed by the developer
                        contents.Clear();

                        ConsoleMsgUtils.ShowWarning(
                            "Critical error in CommandLineParser.CreateHelpContents: duplicateKeyCheck.ContainsKey(key): " + key);
                        return contents;
                    }

                    duplicateKeyCheck.Add(key, prop.Key);

                    if (keyNamesToHide.Contains(key))
                    {
                        // Do not show this parameter key name since it is long
                        continue;
                    }

                    keys.Add(paramChars[0] + key);
                }

                if (prop.Value.ArgPosition > 0)
                {
                    var key = $"arg#{prop.Value.ArgPosition}";

                    if (duplicateKeyCheck.ContainsKey(key))
                    {
                        // Critical error - make sure the user focuses on the error, because it's a big one, and must be fixed by the developer
                        contents.Clear();

                        ConsoleMsgUtils.ShowWarning(
                            "Critical error in CommandLineParser.CreateHelpContents: duplicateKeyCheck.ContainsKey(key): " + key);
                        return contents;
                    }
                    duplicateKeyCheck.Add(key, prop.Key);

                    keys.Add(key);
                }

                // Create the help text
                var helpText = string.Empty;

                if (prop.Value.Required)
                {
                    helpText += "Required. ";
                }

                helpText += prop.Value.HelpText;

                if (prop.Value.HelpShowsDefault || !string.IsNullOrWhiteSpace(prop.Value.DefaultValueFormatString))
                {
                    if (!string.IsNullOrWhiteSpace(prop.Value.DefaultValueFormatString))
                    {
                        // Enforce a whitespace character between the help text and the default value
                        if (!char.IsWhiteSpace(prop.Value.DefaultValueFormatString[0]))
                        {
                            helpText += " ";
                        }
                        helpText += string.Format(prop.Value.DefaultValueFormatString, defaultValue, prop.Value.Min, prop.Value.Max);
                    }
                    else
                    {
                        if (prop.Key.PropertyType.IsEnum)
                        {
                            defaultValue += $"\a(or\a{Convert.ChangeType(defaultValueObj, Enum.GetUnderlyingType(prop.Key.PropertyType))})";
                        }

                        helpText += $" (Default:\a{defaultValue}";

                        if (prop.Value.Min != null)
                        {
                            helpText += $", Min:\a{prop.Value.Min}";
                        }

                        if (prop.Value.Max != null)
                        {
                            helpText += $", Max:\a{prop.Value.Max}";
                        }
                        helpText += ")";
                    }
                }

                // For enums, list the possible values
                if (prop.Key.PropertyType.IsEnum && !prop.Value.DoNotListEnumValues)
                {
                    helpText += "\nPossible values are: ";

                    if (prop.Key.PropertyType.CustomAttributes.Any(x => x.AttributeType == typeof(FlagsAttribute)))
                    {
                        helpText += "(Bit flags)";
                    }

                    // List the valid enum values
                    foreach (var val in Enum.GetValues(prop.Key.PropertyType))
                    {
                        var enumVal = (Enum)Convert.ChangeType(val, prop.Key.PropertyType);
                        var valName = enumVal.ToString();
                        var valValue = Convert.ChangeType(enumVal, Enum.GetUnderlyingType(prop.Key.PropertyType));
                        var desc = enumVal.GetDescriptionAttribute(prop.Key.PropertyType);
                        helpText += $"\n  {valValue} or '{valName}'";

                        if (desc?.IsDefaultAttribute() == false && !string.IsNullOrWhiteSpace(desc.Description))
                        {
                            helpText += $": {desc.Description}";
                        }
                    }
                }

                var delimitedKeyNames = string.Join(", ", keys);

                if (contents.ContainsKey(delimitedKeyNames))
                {
                    // Critical error - make sure the user focuses on the error, because it's a big one, and must be fixed by the developer
                    contents.Clear();

                    ConsoleMsgUtils.ShowWarning(
                        "Critical error in CommandLineParser.CreateHelpContents: contents.ContainsKey(delimitedKeyNames): " + delimitedKeyNames);
                    return contents;
                }
                contents.Add(delimitedKeyNames, helpText);
            }

            var paramFileArgString = string.Empty;

            // Add the default param file string
            foreach (var paramFileArg in AllowedParamFileArgs)
            {
                if (!validArgs.ContainsKey(paramFileArg.ToLower()) || !validArgs[paramFileArg.ToLower()].IsBuiltInArg)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(paramFileArgString))
                {
                    paramFileArgString += ", ";
                }

                paramFileArgString += paramChars[0] + paramFileArg;
            }

            if (!string.IsNullOrWhiteSpace(paramFileArgString))
            {
                contents.Add(paramFileArgString, "Path to a file containing program parameters. Additional arguments on the command line can " +
                                                 "supplement or override the arguments in the param file. " +
                                                 "Lines starting with '#' or ';' will be treated as comments; blank lines are ignored. " +
                                                 "Lines that start with text that does not match a parameter will also be ignored.");
            }

            var createParamFileArgString = string.Empty;

            // Add the default param file string
            foreach (var createParamFileArg in AllowedCreateParamFileArgs)
            {
                if (!validArgs.ContainsKey(createParamFileArg.ToLower()) || !validArgs[createParamFileArg.ToLower()].IsBuiltInArg)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(createParamFileArgString))
                {
                    createParamFileArgString += ", ";
                }

                createParamFileArgString += paramChars[0] + createParamFileArg;
            }

            if (!string.IsNullOrWhiteSpace(createParamFileArgString))
            {
                contents.Add(createParamFileArgString, "Create an example parameter file. Can supply a path; if path is not supplied, " +
                                                       "the example parameter file content will output to the console.");
            }

            if (props.Values.Any(x => x.ArgPosition > 0))
            {
                contents.Add("NOTE:", "arg#1, arg#2, etc. refer to positional arguments, used like \"AppName.exe [arg#1] [arg#2] [other args]\".");
            }

            return contents;
        }

        private static void GetLongKeyNamesToHide(int paramKeysWidth, KeyValuePair<PropertyInfo, OptionAttribute> prop, ISet<string> keyNamesToHide)
        {
            var shortestLongName = string.Empty;

            foreach (var key in prop.Value.ParamKeys)
            {
                if (key.Length <= paramKeysWidth - 2)
                    continue;

                // Do not show this parameter key name since it is long
                keyNamesToHide.Add(key);

                if (shortestLongName.Length == 0)
                {
                    shortestLongName = key;
                }
                else if (key.Length < shortestLongName.Length)
                {
                    shortestLongName = key;
                }
            }

            if (keyNamesToHide.Count > 0 && keyNamesToHide.Count == prop.Value.ParamKeys.Length)
            {
                // All key names are long; show the shortest one
                keyNamesToHide.Remove(shortestLongName);
            }
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Wraps the words in textToWrap to the set width (where possible)
        /// </summary>
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph</returns>
        public static string WrapParagraph(string textToWrap, int wrapWidth = 80)
        {
            return ConsoleMsgUtils.WrapParagraph(textToWrap, wrapWidth);
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Wraps the words in textToWrap to the set width (where possible)
        /// </summary>
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph as a list of strings</returns>
        public static List<string> WrapParagraphAsList(string textToWrap, int wrapWidth)
        {
            return ConsoleMsgUtils.WrapParagraphAsList(textToWrap, wrapWidth);
        }

        /// <summary>
        /// Get the arguments that are valid for the class, dealing with argument name collision and invalid characters as needed
        /// </summary>
        /// <remarks>Position arguments are tracked via special flags: ##1##, ##2##, etc.</remarks>
        /// <returns>Dictionary where key is argument name, and value is Argument Info</returns>
        private Dictionary<string, ArgInfo> GetValidArgs()
        {
            if (validArguments != null)
            {
                return validArguments;
            }

            var validArgs = new Dictionary<string, ArgInfo>();
            var props = GetPropertiesAttributes();

            if (props == null)
            {
                return null;
            }

            foreach (var prop in props)
            {
                var canBeSwitch = prop.Key.PropertyType == typeof(bool);

                if (prop.Value.ArgExistsProperty != null && prop.Value.ArgExistsPropertyInfo == null)
                {
                    if (string.IsNullOrWhiteSpace(prop.Value.ArgExistsProperty))
                    {
                        // ArgExistsProperty is set to an empty or whitespace value
                        Results.AddParseError(
                            @"Error: {0} must be either null, or a boolean property name (use nameof()); class {1}, property {2}, current value is ""{3}""",
                            nameof(prop.Value.ArgExistsProperty), typeof(T).Name, prop.Key.Name, prop.Value.ArgExistsProperty);
                    }
                    else
                    {
                        // ArgExistsProperty is set to a non-existent or non-boolean property name
                        Results.AddParseError(
                            @"Error: {0} does not exist or is not a boolean property name; class {1}, property {2}, current value is ""{3}""",
                            nameof(prop.Value.ArgExistsProperty), typeof(T).Name, prop.Key.Name, prop.Value.ArgExistsProperty);
                    }
                    return null;
                }

                if (prop.Value.IsInputFilePath && prop.Key.PropertyType != typeof(string))
                {
                    Results.AddParseError(
                        @"Error: Property ""{0}"" has attribute IsInputFilePath=true; the property must be of type String, but is of type {1}",
                        nameof(prop.Key.Name), prop.Key.PropertyType.Name);
                    return null;
                }

                foreach (var key in prop.Value.ParamKeys)
                {
                    var lower = key.ToLower();
                    ArgInfo info;

                    // Check for name collision on the same spelling
                    if (!validArgs.TryGetValue(lower, out var arg))
                    {
                        info = new ArgInfo();
                        validArgs.Add(lower, info);
                        info.ArgNormalCase = key;
                    }
                    else
                    {
                        info = arg;
                        info.CaseSensitive = true;
                    }

                    info.CanBeSwitch |= canBeSwitch;

                    if (info.AllArgNormalCase.Contains(key))
                    {
                        // ERROR: Duplicate arguments!
                        Results.AddParseError(
                            @"Error: Duplicate option keys specified in class {0}; key is ""{1}""", typeof(T).Name, key);
                        return null;
                    }

                    foreach (var invalidChar in paramChars)
                    {
                        if (key.StartsWith(invalidChar.ToString()))
                        {
                            // ERROR: Parameter marker character at start of parameter!
                            Results.AddParseError(
                                @"Error: bad character in argument key ""{0}"" in {1}; key cannot start with char '{2}'",
                                key, typeof(T).Name, invalidChar);
                            return null;
                        }
                    }

                    if (key.Contains("/"))
                    {
                        // ERROR: Linux path separator character in parameter! (we allow '-', but '/' is not allowed because of potential issues)
                        Results.AddParseError(
                            @"Error: bad character in argument key ""{0}"" in {1}; key contains invalid char '{2}'",
                            key, typeof(T).Name, "/");
                        return null;
                    }

                    foreach (var invalidChar in separatorChars)
                    {
                        if (!key.Contains(invalidChar.ToString()))
                        {
                            continue;
                        }

                        // ERROR: Parameter separator character in parameter!
                        Results.AddParseError(
                            @"Error: bad character in argument key ""{0}"" in {1}; key contains invalid char '{2}'",
                            key, typeof(T).Name, invalidChar);
                        return null;
                    }

                    info.AllArgNormalCase.Add(key);
                }

                var argPosition = prop.Value.ArgPosition;

                if (argPosition <= 0)
                {
                    continue;
                }

                var positionalArgInfo = new ArgInfo();
                var argName = GetPositionalArgName(argPosition);

                if (validArgs.ContainsKey(argName))
                {
                    // ERROR: Duplicate position specified
                    Results.AddParseError(
                        @"Error: Multiple properties in class {0} specify ArgPosition {1}; conflict involves ""{2}""",
                        typeof(T).Name, argPosition, prop.Key.Name);

                    return null;
                }

                validArgs.Add(argName, positionalArgInfo);
                positionalArgInfo.ArgNormalCase = argName;
                positionalArgInfo.AllArgNormalCase.Add(argName);
            }

            foreach (var helpArg in mDefaultHelpArgs)
            {
                if (validArgs.ContainsKey(helpArg.ToLower()))
                {
                    continue;
                }

                var info = new ArgInfo
                {
                    ArgNormalCase = helpArg,
                    CanBeSwitch = true,
                    IsBuiltInArg = true,
                };

                info.AllArgNormalCase.Add(helpArg);

                validArgs.Add(helpArg, info);
            }

            foreach (var paramFileArg in AllowedParamFileArgs)
            {
                if (validArgs.ContainsKey(paramFileArg.ToLower()))
                {
                    continue;
                }

                var info = new ArgInfo
                {
                    ArgNormalCase = paramFileArg,
                    CanBeSwitch = false,
                    IsBuiltInArg = true,
                };

                info.AllArgNormalCase.Add(paramFileArg);

                validArgs.Add(paramFileArg.ToLower(), info);
            }

            foreach (var createParamArg in AllowedCreateParamFileArgs)
            {
                if (validArgs.ContainsKey(createParamArg.ToLower()))
                {
                    continue;
                }

                var info = new ArgInfo
                {
                    ArgNormalCase = createParamArg,
                    CanBeSwitch = true,
                    IsBuiltInArg = true,
                };

                info.AllArgNormalCase.Add(createParamArg);

                validArgs.Add(createParamArg.ToLower(), info);
            }

            validArguments = validArgs;
            return validArgs;
        }

        /// <summary>
        /// Look for the file (or directory) specified by the given property (whose type is string)
        /// If the item does not exist in the working directory, but does exist in paramFileDirectory, auto-update the path
        /// </summary>
        /// <param name="prop">Property</param>
        /// <param name="paramFileDirectory">Parameter file directory</param>
        private void VerifyFileOrDirectoryPath(KeyValuePair<PropertyInfo, OptionAttribute> prop, FileSystemInfo paramFileDirectory)
        {
            try
            {
                var fileOrDirectoryPath = (string)prop.Key.GetValue(Results.ParsedResults);

                // Replace wildcard characters with underscores
                var cleanFileOrDirectoryPath = PathUtils.GetCleanPath(fileOrDirectoryPath);

                if (Path.IsPathRooted(cleanFileOrDirectoryPath) || paramFileDirectory == null)
                    return;

                var fileToFind = new FileInfo(cleanFileOrDirectoryPath);

                if (fileToFind.Exists)
                    return;

                var directoryToFind = new DirectoryInfo(cleanFileOrDirectoryPath);

                if (directoryToFind.Exists)
                    return;

                var alternatePath = Path.Combine(paramFileDirectory.FullName, fileToFind.Name);
                var alternateInputFile = new FileInfo(alternatePath);

                if (alternateInputFile.Exists)
                {
                    prop.Key.SetValue(Results.ParsedResults, alternateInputFile.FullName);
                    return;
                }

                var alternateInputDirectory = new DirectoryInfo(alternatePath);

                if (alternateInputDirectory.Exists)
                {
                    prop.Key.SetValue(Results.ParsedResults, alternateInputDirectory.FullName);
                }
            }
            catch
            {
                // Silently ignore errors here
            }
        }

        /// <summary>
        /// Assure that the path is not surrounded by single quotes or by double quotes
        /// </summary>
        /// <param name="prop">Property</param>
        private void VerifyPathNotQuoted(KeyValuePair<PropertyInfo, OptionAttribute> prop)
        {
            try
            {
                var fileOrDirectoryPath = (string)prop.Key.GetValue(Results.ParsedResults);

                if (fileOrDirectoryPath.StartsWith("\"") && fileOrDirectoryPath.EndsWith("\""))
                {
                    // The path is surrounded by double quotes; remove them
                    prop.Key.SetValue(Results.ParsedResults, fileOrDirectoryPath.Trim('"'));
                    return;
                }

                if (fileOrDirectoryPath.StartsWith("'") && fileOrDirectoryPath.EndsWith("'"))
                {
                    // The path is surrounded by single quotes; remove them
                    prop.Key.SetValue(Results.ParsedResults, fileOrDirectoryPath.Trim('\''));
                }
            }
            catch
            {
                // Silently ignore errors here
            }
        }

        /// <summary>
        /// Assure that the path is not surrounded by single quotes or by double quotes
        /// </summary>
        /// <param name="value">Value</param>
        private static string RemoveMatchingQuotes(string value)
        {
            if (value == null || value.Length < 2)
            {
                // don't strip a single quote; it may have already been unquoted.
                return value;
            }

            try
            {
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    // The value is surrounded by double quotes; remove them
                    return value.Trim('"');
                }

                if (value.StartsWith("'") && value.EndsWith("'"))
                {
                    // The value is surrounded by single quotes; remove them
                    return value.Trim('\'');
                }
            }
            catch
            {
                // Silently ignore errors here
            }

            return value;
        }

        /// <summary>
        /// Data about a single spelling of an argument name (name and NAME will be in the same instance)
        /// </summary>
        private class ArgInfo
        {
            /// <summary>
            /// The first listed argument name
            /// </summary>
            public string ArgNormalCase { get; set; }

            /// <summary>
            /// All arguments with the same name that differ only in capitalization
            /// </summary>
            public List<string> AllArgNormalCase { get; }

            /// <summary>
            /// If the name is case-sensitive
            /// </summary>
            public bool CaseSensitive { get; set; }

            /// <summary>
            /// If one of the arguments with this spelling is a bool
            /// </summary>
            public bool CanBeSwitch { get; set; }

            /// <summary>
            /// If the argument key is internally defined
            /// </summary>
            /// <remarks>Examples include: ?, help, ParamFile, and CreateParamFile</remarks>
            public bool IsBuiltInArg { get; set; }

            /// <summary>
            /// Constructor
            /// </summary>
            public ArgInfo()
            {
                ArgNormalCase = string.Empty;
                AllArgNormalCase = new List<string>(2);
                CaseSensitive = false;
                CanBeSwitch = false;
                IsBuiltInArg = false;
            }
        }

        /// <summary>
        /// Parse the properties of the templated class
        /// </summary>
        /// <returns>PropertyInfo and the OptionAttribute instance for each property that has the attribute</returns>
        private Dictionary<PropertyInfo, OptionAttribute> GetPropertiesAttributes()
        {
            if (propertiesAndAttributes != null)
            {
                return propertiesAndAttributes;
            }

            var props = new Dictionary<PropertyInfo, OptionAttribute>();

            var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

            foreach (var property in properties)
            {
                // Check for and retrieve the attribute
                if (!GetOptionAttributes(property, out var attributes))
                {
                    continue;
                }

                var optionData = attributes[0];

                // Ignore any duplicates (shouldn't occur anyway)
                props.Add(property, optionData);

                if (string.IsNullOrWhiteSpace(optionData.ArgExistsProperty))
                    continue;

                var match = Array.Find(properties, x => x.Name.Equals(optionData.ArgExistsProperty));

                if (match != null && match.PropertyType == typeof(bool))
                {
                    optionData.ArgExistsPropertyInfo = match;
                }
            }

            propertiesAndAttributes = props;
            return props;
        }

        private bool GetOptionAttributes(PropertyInfo property, out List<OptionAttribute> attributes, bool firstMatchOnly = true)
        {
            attributes = new List<OptionAttribute>();
            if (Attribute.IsDefined(property, typeof(OptionAttribute), true))
            {
                // Note: as noted in the below link, MemberInfo.GetCustomAttributes() inheritance boolean is ignored for properties and events
                // so 'property.GetCustomAttributes(typeof(OptionAttribute), true)', while convenient, will not find attributes only defined in the base class
                // Attribute.IsDefined does find the attributes in the base class, and
                // Attribute.GetCustomAttributes(property, typeof(OptionAttribute), true) will include attributes only defined in the base class
                // https://learn.microsoft.com/en-us/dotnet/api/system.reflection.memberinfo.getcustomattributes?view=netframework-4.7.2#system-reflection-memberinfo-getcustomattributes(system-type-system-boolean)

                attributes.AddRange(Attribute.GetCustomAttributes(property, typeof(OptionAttribute), true).Cast<OptionAttribute>());

                if (firstMatchOnly)
                {
                    return true;
                }
            }

            var interfaces = typeof(T).GetInterfaces();
            foreach (var i in interfaces)
            {
                var prop = i.GetProperty(property.Name, property.PropertyType);
                if (prop == null)
                {
                    continue;
                }

                if (Attribute.IsDefined(prop, typeof(OptionAttribute), true))
                {
                    attributes.AddRange(prop.GetCustomAttributes(typeof(OptionAttribute), true).Cast<OptionAttribute>());

                    if (firstMatchOnly)
                    {
                        return true;
                    }
                }
            }

            return attributes.Count > 0;
        }
    }

    /// <summary>
    /// Some extension methods for working with enums
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Get the string from the DescriptionAttribute of an enum value
        /// </summary>
        /// <remarks>From https://stackoverflow.com/questions/1799370/getting-attributes-of-enums-value
        /// </remarks>
        public static DescriptionAttribute GetDescriptionAttribute(this Enum enumValue, Type enumType)
        {
            return enumType.GetMember(enumValue.ToString()).First().GetCustomAttribute<DescriptionAttribute>();
        }
    }
}
