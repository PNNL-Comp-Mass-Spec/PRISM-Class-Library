using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Basic class for keeping parameters flags and properties for command line arguments tied together,
    /// supporting properties of primitive types (and arrays of primitive types).
    ///
    /// Supports parameter flags similar to /d -dd --dir, with case sensitivity when needed,
    /// with the separator between parameter flag and parameter as ' ', ':', or '=',
    /// and also supports using a parameter flag as a switch (if the associated property is a bool).
    ///
    /// If an argument is supplied multiple times, it only keeps the last one supplied.
    /// If the property is an array, multiple values are provided using '-paramName value -paramName value ...' or similar.
    /// Includes support for showing help with no args supplied, or with argument names of "?" and "help" (can be overridden).
    /// </summary>
    /// <remarks>
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
    ///
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
    ///
    ///     if (!parseResults.Success)
    ///     {
    ///       return -1;
    ///     }
    ///
    ///     if (!options.ValidateArgs(out var errorMessage))
    ///     {
    ///         parser.PrintHelp();
    ///         Console.WriteLine();
    ///         ConsoleMsgUtils.ShowWarning("Validation error:");
    ///         ConsoleMsgUtils.ShowWarning(errorMessage);
    ///         return -1;
    ///     }
    ///
    ///     options.OutputSetOptions();
    ///
    /// An example class suitable for use when instantiating the CommandLineParser is GenericParserOptions in this project
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class CommandLineParser<T> where T : class, new()
    {
        // ReSharper disable StaticMemberInGenericType
        private static readonly char[] mDefaultParamChars = { '-', '/' };
        private static readonly char[] mDefaultSeparatorChars = { ' ', ':', '=' };
        private static readonly string[] mDefaultHelpArgs = { "?", "help" };

        /// <summary>
        /// Results from the parsing
        /// </summary>
        public class ParserResults
        {
            #region Properties

            /// <summary>
            /// Parsing status - false if parsing failed
            /// </summary>
            public bool Success { get; private set; }

            /// <summary>
            /// Errors that occurred during parsing
            /// </summary>
            public IReadOnlyList<string> ParseErrors => mParseErrors;

            /// <summary>
            /// Target object, populated with the parsed arguments when the parsing completes
            /// </summary>
            public T ParsedResults { get; }

            #endregion

            /// <summary>
            /// Modifiable list of parsing errors
            /// </summary>
            private readonly List<string> mParseErrors = new List<string>();

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="parsed"></param>
            public ParserResults(T parsed)
            {
                Success = true;
                ParsedResults = parsed;
            }

            /// <summary>
            /// Set the parsing status to failed
            /// </summary>
            internal void Failed()
            {
                Success = false;
            }

            /// <summary>
            /// Add a Parsing error to the parsing error list.
            /// </summary>
            /// <param name="error"></param>
            internal void AddParseError(string error)
            {
                mParseErrors.Add(error);
            }

            /// <summary>
            /// Add a Parsing error to the parsing error list.
            /// </summary>
            /// <param name="format"></param>
            /// <param name="args"></param>
            [StringFormatMethod("format")]
            internal void AddParseError(string format, params object[] args)
            {
                AddParseError(string.Format(format, args));
            }

            /// <summary>
            /// Print the parsing errors to console
            /// </summary>
            public void OutputErrors()
            {
                foreach (var error in ParseErrors)
                {
                    Console.WriteLine(error);
                }
            }
        }

        private char[] paramChars = mDefaultParamChars;
        private char[] separatorChars = mDefaultSeparatorChars;
        private Dictionary<string, ArgInfo> validArguments;
        private Dictionary<PropertyInfo, OptionAttribute> propertiesAndAttributes;

        #region Properties

        /// <summary>
        /// Developer contact info
        /// </summary>
        /// <remarks>If defined, shown at the end of PrintHelp</remarks>
        public string ContactInfo { get; set; }

        /// <summary>
        /// Entry assembly name
        /// </summary>
        public string EntryAssemblyName { get; }

        /// <summary>
        /// Executable version info
        /// </summary>
        public string ExeVersionInfo { get; }

        /// <summary>
        /// Get or set the characters allowed at the beginning of an argument specifier
        /// </summary>
        public IEnumerable<char> ParamFlagCharacters
        {
            get => paramChars;
            set
            {
                var distinct = value.Distinct().ToArray();
                if (distinct.Length > 0)
                {
                    paramChars = distinct;
                }
            }
        }

        /// <summary>
        /// Get or set the characters allowed as separators between an argument specifier and argument value
        /// </summary>
        // ReSharper disable once UnusedMember.Global
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

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="entryAsmName">Name of the executing assembly</param>
        /// <param name="versionInfo">Executable version info</param>
        public CommandLineParser(string entryAsmName = "", string versionInfo = "")
        {
            EntryAssemblyName = entryAsmName ?? string.Empty;
            ExeVersionInfo = versionInfo ?? string.Empty;

            Results = new ParserResults(new T());
            propertiesAndAttributes = null;
            validArguments = null;

            ProgramInfo = string.Empty;
            ContactInfo = string.Empty;
            UsageExamples = new List<string>();
        }

        /// <summary>
        /// Parse the arguments into <paramref name="options"/>, returning a bool. Entry assembly name is retrieved via reflection.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="options"></param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns>True on success, false if argument parse failed</returns>
        // ReSharper disable once UnusedMember.Global
        public static bool ParseArgs(string[] args, T options, string versionInfo = "")
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
            return ParseArgs(args, options, entryAssemblyName, versionInfo);
        }

        /// <summary>
        /// Parse the arguments into <paramref name="options"/>, returning a bool.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="options"></param>
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
        /// Parse the arguments, returning the parsing results in <see cref="ParserResults"/>. Entry assembly name is retrieved via reflection.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns>Parser results</returns>
        // ReSharper disable once UnusedMember.Global
        public static ParserResults ParseArgs(string[] args, string versionInfo)
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
            return ParseArgs(args, entryAssemblyName, versionInfo);
        }

        /// <summary>
        /// Parse the arguments, returning the parsing results in <see cref="ParserResults"/>.
        /// </summary>
        /// <param name="args"></param>
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
        /// <param name="args"></param>
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
                    PrintHelp();
                }
                else if (outputErrors)
                {
                    // Show errors
                    Results.OutputErrors();
                }
                Results.Failed();
                return Results;
            }

            try
            {
                // Parse the arguments into a dictionary
                var preprocessed = ArgsPreprocess(args);
                if (preprocessed == null)
                {
                    // Preprocessing failed, tell the user why
                    if (onErrorOutputHelp)
                    {
                        PrintHelp();
                    }
                    else if (outputErrors)
                    {
                        Results.OutputErrors();
                    }
                    Results.Failed();
                    return Results;
                }

                var props = GetPropertiesAttributes();
                var validArgs = GetValidArgs();

                // Show the help if a default help argument is provided, but only if the templated class does not define the arg provided
                foreach (var helpArg in mDefaultHelpArgs)
                {
                    // Make sure the help arg is not defined in the template class
                    if (preprocessed.ContainsKey(helpArg) && validArgs.ContainsKey(helpArg.ToLower()) && validArgs[helpArg.ToLower()].IsBuiltInArg)
                    {
                        PrintHelp();
                        Results.Failed();
                        return Results;
                    }
                }

                foreach (var prop in props)
                {
                    var specified = false;
                    var keyGiven = string.Empty;
                    List<string> value = null;

                    // Find any arguments that match this property
                    foreach (var key in prop.Value.ParamKeys)
                    {
                        if (preprocessed.ContainsKey(key))
                        {
                            specified = true;
                            keyGiven = key;
                            if (value == null)
                            {
                                value = preprocessed[key];
                            }
                            else
                            {
                                // Add in other values provided by argument keys that belong to this property
                                value.AddRange(preprocessed[key]);
                            }
                        }
                    }

                    var positionalArgName = GetPositionalArgName(prop.Value.ArgPosition);
                    if (prop.Value.ArgPosition > 0 && preprocessed.ContainsKey(positionalArgName))
                    {
                        specified = true;
                        keyGiven = "PositionalArgument" + prop.Value.ArgPosition;
                        if (value == null)
                        {
                            value = preprocessed[positionalArgName];
                        }
                        else
                        {
                            value.AddRange(preprocessed[positionalArgName]);
                        }
                    }

                    if (prop.Value.Required && (!specified || value == null || value.Count == 0))
                    {
                        Results.AddParseError(@"Error: Required argument missing: {0}{1}", paramChars[0], prop.Value.ParamKeys[0]);
                        Results.Failed();
                    }

                    if (!specified)
                    {
                        continue;
                    }

                    // switch handling - no value specified
                    if (prop.Key.PropertyType == typeof(bool) && (value == null || value.Count == 0 || string.IsNullOrWhiteSpace(value.Last())))
                    {
                        prop.Key.SetValue(Results.ParsedResults, true);
                        continue;
                    }

                    // ArgExistsProperty handling
                    if (prop.Value.ArgExistsPropertyInfo != null)
                    {
                        prop.Value.ArgExistsPropertyInfo.SetValue(Results.ParsedResults, true);

                        // if no value provided, then don't set it
                        if (value == null || value.Count == 0 || value.All(string.IsNullOrWhiteSpace))
                        {
                            continue;
                        }
                    }

                    object lastVal = value.Last();
                    try
                    {
                        // parse/cast the value to the appropriate type, checking the min and max limits, and set the value using reflection
                        object castValue;
                        if (prop.Key.PropertyType.IsArray)
                        {
                            var castValues = Array.CreateInstance(prop.Key.PropertyType.GetElementType(), value.Count);
                            var i = 0;
                            foreach (var val in value)
                            {
                                lastVal = val;
                                var castVal = ParseValueToType(prop.Key.PropertyType.GetElementType(), prop.Value, keyGiven, val);
                                castValues.SetValue(castVal, i++);
                            }
                            castValue = castValues;
                        }
                        else
                        {
                            castValue = ParseValueToType(prop.Key.PropertyType, prop.Value, keyGiven, value.Last());
                        }
                        prop.Key.SetValue(Results.ParsedResults, castValue);
                    }
                    catch (InvalidCastException)
                    {
                        Results.AddParseError(
                            @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""",
                            keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed();
                    }
                    catch (FormatException)
                    {
                        Results.AddParseError(
                            @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""",
                            keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed();
                    }
                    catch (ArgumentException)
                    {
                        Results.AddParseError(
                            @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""",
                            keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed();
                    }
                    catch (OverflowException)
                    {
                        Results.AddParseError(
                            @"Error: argument {0}, cannot cast ""{1}"" to type ""{2}"" (out of range)",
                            keyGiven, lastVal, prop.Key.PropertyType.Name);
                        Results.Failed();
                    }
                }
            }
            catch (Exception)
            {
                Results.Failed();
            }

            if (!Results.Success)
            {
                if (onErrorOutputHelp)
                {
                    PrintHelp();
                }
                else if (outputErrors)
                {
                    Results.OutputErrors();
                }
            }

            return Results;
        }

        /// <summary>
        /// Parses a value to the specified type, checking min and max limits
        /// </summary>
        /// <param name="propertyType"></param>
        /// <param name="parseData"></param>
        /// <param name="argKey"></param>
        /// <param name="valueToParse"></param>
        /// <returns>Converted value</returns>
        private object ParseValueToType(Type propertyType, OptionAttribute parseData, string argKey, string valueToParse)
        {
            object castValue = null;
            if (!string.Equals("null", valueToParse, StringComparison.OrdinalIgnoreCase))
            {
                castValue = ConvertToType(valueToParse, propertyType);
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
                            Results.AddParseError(@"Error: argument {0}, value of {1} is less than minimum of {2}", argKey, castValue, castMin);
                            Results.Failed();
                        }
                    }
                    else
                    {
                        Results.AddParseError(
                            @"Error: argument {0}, unable to check value of {1} against minimum of ""{2}"": cannot cast/compare minimum to type ""{3}""",
                            argKey, castValue, parseData.Min, propertyType.Name);
                        Results.Failed();
                    }
                }
                if (parseData.Max != null)
                {
                    // HACK: prevent allowed conversions from say, double to int; change the value to a string because Convert.ChangeType cannot do 2-step conversions (like string->double->int)
                    if (ConvertToType(parseData.Max.ToString(), propertyType) is IComparable castMax)
                    {
                        if (castMax.CompareTo(castValue) < 0)
                        {
                            Results.AddParseError(
                                @"Error: argument {0}, value of {1} is greater than maximum of {2}",
                                argKey, castValue, castMax);
                            Results.Failed();
                        }
                    }
                    else
                    {
                        Results.AddParseError(
                            @"Error: argument {0}, unable to check value of {1} against maximum of ""{2}"": cannot cast/compare maximum to type ""{3}""",
                            argKey, castValue, parseData.Max, propertyType.Name);
                        Results.Failed();
                    }
                }
            }
            catch (InvalidCastException)
            {
                Results.AddParseError(@"Error: argument {0}, cannot cast min or max to type ""{1}""", argKey, propertyType.Name);
                Results.Failed();
            }
            catch (FormatException)
            {
                Results.AddParseError(@"Error: argument {0}, cannot cast min or max to type ""{1}""", argKey, propertyType.Name);
                Results.Failed();
            }
            catch (ArgumentException)
            {
                Results.AddParseError(@"Error: argument {0}, cannot cast min or max to type ""{1}""", argKey, propertyType.Name);
                Results.Failed();
            }
            catch (OverflowException)
            {
                Results.AddParseError(@"Error: argument {0}, cannot cast min or max to type ""{1}"" (out of range)", argKey, propertyType.Name);
                Results.Failed();
            }
            return castValue;
        }

        /// <summary>
        /// Parse most objects normally, but parse enums using Enum.Parse
        /// </summary>
        /// <param name="valueToConvert"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        private object ConvertToType(object valueToConvert, Type targetType)
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

            // Support using '0', '1', 'y', 'yes', 'n', 'no' with booleans
            if (targetType == typeof(bool))
            {
                var valueLCase = valueToConvert.ToString().ToLowerInvariant();
                if (int.TryParse(valueLCase, out var boolResult))
                {
                    return boolResult != 0;
                }
                if (valueLCase.Equals("n") || valueLCase.Equals("no"))
                {
                    return false;
                }
                if (valueLCase.Equals("y") || valueLCase.Equals("yes"))
                {
                    return true;
                }
            }

            return Convert.ChangeType(valueToConvert, targetType);
        }

        /// <summary>
        /// Parse the arguments to a dictionary
        /// </summary>
        /// <param name="args"></param>
        /// <returns>Dictionary where keys are argument names and values are the setting for the argument</returns>
        private Dictionary<string, List<string>> ArgsPreprocess(IReadOnlyList<string> args)
        {
            var validArgs = GetValidArgs();
            if (validArgs == null)
            {
                return null;
            }

            var processed = new Dictionary<string, List<string>>();
            var positionArgumentNumber = 0;

            for (var i = 0; i < args.Count; i++)
            {
                if (!paramChars.Contains(args[i][0]))
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
                    processed[argName].Add(args[i]);

                    continue;
                }

                var key = args[i].TrimStart(paramChars);
                var value = string.Empty;
                var nextArgIsNumber = false;
                if (paramChars.Contains('-') && i + 1 < args.Count && args[i + 1].StartsWith("-"))
                {
                    // Try converting to most forgiving number format
                    nextArgIsNumber = double.TryParse(args[i + 1], out _);

                    // Check if the program supports a numeric argument (but we only need to remove a '-', because a '/' won't parse as a double)
                    if (nextArgIsNumber && validArgs.ContainsKey(args[i + 1].TrimStart('-')))
                    {
                        nextArgIsNumber = false;
                    }
                }

                var containedSeparator = false;
                // Only split off the first separator, since others may be part of drive specifiers
                var separatorIndex = key.IndexOfAny(separatorChars);
                if (separatorIndex > -1)
                {
                    containedSeparator = true;
                    value = key.Substring(separatorIndex + 1);
                    key = key.Substring(0, separatorIndex);
                }

                if (!containedSeparator && i + 1 < args.Count && (nextArgIsNumber || !paramChars.Contains(args[i + 1][0])))
                {
                    value = args[i + 1];
                    i++;
                }

                // Key normalization - usually allow case-insensitivity
                var ciKey = key.ToLower();
                if (validArgs.ContainsKey(ciKey))
                {
                    var argInfo = validArgs[ciKey];
                    // if argument is case-sensitive, make sure it matches an argument
                    if (argInfo.CaseSensitive && !argInfo.AllArgNormalCase.Contains(key))
                    {
                        Results.AddParseError(string.Format("Error: Arg " + key + "does not match valid argument"));
                        return null;
                    }

                    if (!argInfo.CaseSensitive)
                    {
                        key = argInfo.ArgNormalCase;
                    }
                }

                // The last duplicate option gets priority
                if (!processed.ContainsKey(key))
                {
                    processed.Add(key, new List<string>());
                }
                processed[key].Add(value);
            }

            return processed;
        }

        /// <summary>
        /// Generate the special argument name used to track positional arguments
        /// </summary>
        /// <param name="argPosition"></param>
        /// <returns>Argument name</returns>
        private string GetPositionalArgName(int argPosition)
        {
            return "##" + argPosition + "##";
        }

        /// <summary>
        /// Display the help contents, using the information supplied by the Option attributes and the default constructor for the templated type
        /// </summary>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        // ReSharper disable once UnusedMember.Global
        public static void ShowHelp(string entryAssemblyName = "", string versionInfo = "")
        {
            var parser = new CommandLineParser<T>(entryAssemblyName, versionInfo);
            parser.PrintHelp();
        }

        /// <summary>
        /// Display the help contents, using the information supplied by the Option attributes and the default constructor for the templated type
        /// </summary>
        public void PrintHelp()
        {
            const int paramKeysWidth = 18;
            const int helpTextWidth = 56;
            var contents = CreateHelpContents();

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
                Console.WriteLine(@"{0} {1}", EntryAssemblyName, ExeVersionInfo);
            }
            if (!string.IsNullOrWhiteSpace(EntryAssemblyName))
            {
                Console.WriteLine();
                Console.WriteLine(@"Usage: {0}", EntryAssemblyName + ".exe");
            }
            else
            {
                Console.WriteLine(@"Usage:");
            }

            var outputFormatString = "  {0,-" + paramKeysWidth + "}  {1}";

            // Output the help contents, creating columns with wrapping before outputting
            foreach (var option in contents)
            {
                var overflow = new List<Tuple<string, string>>();

                // Wrap the argument names
                var keyOverflow = WrapParagraphAsList(option.Key, paramKeysWidth);

                // Wrap the argument help text
                var textOverflow = WrapParagraphAsList(option.Value, helpTextWidth);

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
        /// <returns></returns>
        private Dictionary<string, string> CreateHelpContents()
        {
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
                if (validArgs.ContainsKey(helpArg.ToLower()))
                {
                    if (validArgs[helpArg.ToLower()].IsBuiltInArg)
                    {
                        if (!string.IsNullOrWhiteSpace(helpArgString))
                        {
                            helpArgString += ", ";
                        }
                        helpArgString += paramChars[0] + helpArg;
                    }
                }
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
                var defaultValue = "null";
                if (defaultValueObj != null)
                {
                    defaultValue = defaultValueObj.ToString();
                }
                if (string.IsNullOrWhiteSpace(defaultValue))
                {
                    defaultValue = $"\"{defaultValue}\"";
                }

                // Create the list of parameter keys
                var keys = string.Empty;
                foreach (var key in prop.Value.ParamKeys)
                {
                    if (!string.IsNullOrWhiteSpace(keys))
                    {
                        keys += ", ";
                    }

                    if (duplicateKeyCheck.ContainsKey(key))
                    {
                        // Critical error - make sure the user focuses on the error, because it's a big one, and must be fixed by the developer.
                        contents.Clear();
                        return contents;
                    }
                    duplicateKeyCheck.Add(key, prop.Key);

                    keys += paramChars[0] + key;
                }

                if (prop.Value.ArgPosition > 0)
                {
                    if (!string.IsNullOrWhiteSpace(keys))
                    {
                        keys += ", ";
                    }
                    var key = $"arg#{prop.Value.ArgPosition}";

                    if (duplicateKeyCheck.ContainsKey(key))
                    {
                        // Critical error - make sure the user focuses on the error, because it's a big one, and must be fixed by the developer.
                        contents.Clear();
                        return contents;
                    }
                    duplicateKeyCheck.Add(key, prop.Key);

                    keys += key;
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
                    var enumValues = Enum.GetValues(prop.Key.PropertyType);
                    foreach (var val in enumValues)
                    {
                        var enumVal = (Enum)Convert.ChangeType(val, prop.Key.PropertyType);
                        var valName = enumVal.ToString();
                        var valValue = Convert.ChangeType(enumVal, Enum.GetUnderlyingType(prop.Key.PropertyType));
                        var desc = enumVal.GetDescriptionAttribute(prop.Key.PropertyType);
                        helpText += $"\n  {valValue} or '{valName}'";
                        if (desc != null && !desc.IsDefaultAttribute() && !string.IsNullOrWhiteSpace(desc.Description))
                        {
                            helpText += $": {desc.Description}";
                        }
                    }
                }

                if (contents.ContainsKey(keys))
                {
                    // Critical error - make sure the user focuses on the error, because it's a big one, and must be fixed by the developer.
                    contents.Clear();
                    return contents;
                }
                contents.Add(keys, helpText);
            }

            if (props.Values.Any(x => x.ArgPosition > 0))
            {
                contents.Add("NOTE:", "arg#1, arg#2, etc. refer to positional arguments, used like \"AppName.exe [arg#1] [arg#2] [other args]\".");
            }

            return contents;
        }

        /// <summary>
        /// Wraps the words in textToWrap to the set width (where possible)
        /// </summary>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph</returns>
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        public static string WrapParagraph(string textToWrap, int wrapWidth = 80)
        {
            return ConsoleMsgUtils.WrapParagraph(textToWrap, wrapWidth);
        }

        /// <summary>
        /// Wraps the words in textToWrap to the set width (where possible)
        /// </summary>
        /// <param name="textToWrap">Text to wrap</param>
        /// <param name="wrapWidth">Max length per line</param>
        /// <returns>Wrapped paragraph as a list of strings</returns>
        /// <remarks>Use the 'alert' character ('\a') to create a non-breaking space</remarks>
        public static List<string> WrapParagraphAsList(string textToWrap, int wrapWidth)
        {
            return ConsoleMsgUtils.WrapParagraphAsList(textToWrap, wrapWidth);
        }

        /// <summary>
        /// Get the arguments that are valid for the class, dealing with argument name collision and invalid characters as needed
        /// </summary>
        /// <returns>Dictionary where key is argument name, and value is Argument Info</returns>
        /// <remarks>Position arguments are tracked via special flags: ##1##, ##2##, etc.</remarks>
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
                        // ArgExistsProperty is set to a empty or whitespace value
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

                foreach (var key in prop.Value.ParamKeys)
                {
                    var lower = key.ToLower();
                    ArgInfo info;

                    // Check for name collision on the same spelling
                    if (!validArgs.ContainsKey(lower))
                    {
                        info = new ArgInfo();
                        validArgs.Add(lower, info);
                        info.ArgNormalCase = key;
                    }
                    else
                    {
                        info = validArgs[lower];
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

                    foreach (var invalidChar in separatorChars)
                    {
                        if (key.Contains(invalidChar.ToString()))
                        {
                            // ERROR: Parameter separator character in parameter!
                            Results.AddParseError(
                                @"Error: bad character in argument key ""{0}"" in {1}; key contains invalid char '{2}'",
                                key, typeof(T).Name, invalidChar);
                            return null;
                        }
                    }

                    info.AllArgNormalCase.Add(key);
                }

                var argPosition = prop.Value.ArgPosition;
                if (argPosition > 0)
                {
                    var info = new ArgInfo();
                    var argName = GetPositionalArgName(argPosition);

                    if (validArgs.ContainsKey(argName))
                    {
                        // ERROR: Duplicate position specified
                        Results.AddParseError(
                            @"Error: Multiple properties in class {0} specify ArgPosition {1}; conflict involves ""{2}""",
                            typeof(T).Name, argPosition, prop.Key.Name);
                        return null;
                    }

                    validArgs.Add(argName, info);
                    info.ArgNormalCase = argName;
                    info.AllArgNormalCase.Add(argName);
                }
            }

            foreach (var helpArg in mDefaultHelpArgs)
            {
                if (!validArgs.ContainsKey(helpArg.ToLower()))
                {
                    var info = new ArgInfo
                    {
                        ArgNormalCase = helpArg,
                        CanBeSwitch = true,
                        IsBuiltInArg = true,
                    };
                    info.AllArgNormalCase.Add(helpArg);

                    validArgs.Add(helpArg, info);
                }
            }

            validArguments = validArgs;
            return validArgs;
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
            /// If the name is case sensitive
            /// </summary>
            public bool CaseSensitive { get; set; }

            /// <summary>
            /// If one of the arguments with this spelling is a bool
            /// </summary>
            public bool CanBeSwitch { get; set; }

            /// <summary>
            /// If the argument key is internally defined
            /// </summary>
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

            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                // Check for the attribute
                if (!Attribute.IsDefined(property, typeof(OptionAttribute)))
                {
                    continue;
                }

                var attrib = property.GetCustomAttributes(typeof(OptionAttribute), true);
                var attribList = attrib.ToArray();
                if (attribList.Length == 0)
                {
                    continue;
                }

                var optionData = (OptionAttribute)attribList[0];

                // ignore any duplicates (shouldn't occur anyway)
                props.Add(property, optionData);

                if (!string.IsNullOrWhiteSpace(optionData.ArgExistsProperty))
                {
                    var match = properties.FirstOrDefault(x => x.Name.Equals(optionData.ArgExistsProperty));
                    if (match != null && match.PropertyType == typeof(bool))
                    {
                        optionData.ArgExistsPropertyInfo = match;
                    }
                }
            }

            propertiesAndAttributes = props;
            return props;
        }
    }

    /// <summary>
    /// Attribute class to flag properties that are command line arguments
    /// </summary>
    // ReSharper disable RedundantAttributeUsageProperty
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class OptionAttribute : Attribute
    {
        /// <summary>
        /// Text displayed on the help screen
        /// </summary>
        public string HelpText { get; set; }

        /// <summary>
        /// True if the argument is required
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Valid command line argument name (or names)
        /// </summary>
        public string[] ParamKeys { get; }

        /// <summary>
        /// Maps a given unnamed argument to this parameter
        /// Defaults to 0 meaning not mapped to any unnamed arguments
        /// </summary>
        /// <remarks>
        /// For example, in MyUtility.exe InputFilePath.txt OutputFilePath.txt
        /// InputFilePath.txt is at position 1 and
        /// OutputFilePath.txt is at position 2
        /// </remarks>
        public int ArgPosition { get; set; }

        /// <summary>
        /// If the help screen should show the default value for an argument (value pulled from the default constructor); Defaults to true.
        /// </summary>
        public bool HelpShowsDefault { get; set; }

        /// <summary>
        /// Format string for adding default values to end of help text. If this is not set, and <see cref="HelpShowsDefault"/>
        /// is true, the default will be displayed as " (Default: [value][, min (if set)][, max (if set)])".
        /// Use "{0}" for default value, "{1}" for min, and "{2}" for max. Use '\a' for a non-breaking space.
        /// </summary>
        public string DefaultValueFormatString { get; set; }

        /// <summary>
        /// Minimum value, for a numeric argument
        /// </summary>
        public object Min { get; set; }

        /// <summary>
        /// Maximum value, for a numeric argument
        /// </summary>
        public object Max { get; set; }

        /// <summary>
        /// If the property is an enum, enum values are listed by default. Set this to 'true' to not list the enum values.
        /// </summary>
        public bool DoNotListEnumValues { get; set; }

        /// <summary>
        /// Set to 'true' to hide the argument from the help out
        /// </summary>
        /// <remarks>This is useful for supporting obsolete arguments</remarks>
        public bool Hidden { get; set; }

        /// <summary>
        /// If the argument is specified, the given boolean property will be set to "true"
        /// </summary>
        public string ArgExistsProperty { get; set; }

        /// <summary>
        /// If <see cref="ArgExistsProperty"/> is specified, and refers to a valid boolean property, this will be set to that property.
        /// </summary>
        internal PropertyInfo ArgExistsPropertyInfo { get; set; }

        /// <summary>
        /// Constructor supporting any number of param keys.
        /// </summary>
        /// <param name="paramKeys">Must supply at least one key for the argument, and it must be distinct within the class</param>
        /// <remarks>Not CLS compliant</remarks>
        public OptionAttribute(params string[] paramKeys)
        {
            // Check for null and remove blank entries
            ParamKeys = paramKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? throw new ArgumentNullException(nameof(paramKeys), "Argument cannot be null");

            if (ParamKeys.Length == 0)
                throw new ArgumentException("At least one argument name must be provided", nameof(paramKeys));

            if (string.IsNullOrWhiteSpace(ParamKeys[0]))
                throw new ArgumentException("Argument name cannot be whitespace", nameof(paramKeys));

            ArgPosition = 0;
            Max = null;
            Min = null;
            HelpShowsDefault = true;
            ArgExistsProperty = null;
            ArgExistsPropertyInfo = null;
        }

        /// <summary>
        /// Constructor, taking a single paramKey or a multiple param keys separated by a '|'
        /// </summary>
        /// <param name="paramKey">Must supply at least one key for the argument, and it must be distinct within the class; multiple keys can be specified, separated by a '|'</param>
        /// <remarks>CLS compliant</remarks>
        public OptionAttribute(string paramKey) : this(paramKey?.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
        }

        /// <summary>
        /// Constructor supporting up to 4 param keys
        /// </summary>
        /// <param name="paramKey1"></param>
        /// <param name="paramKey2"></param>
        /// <param name="paramKey3"></param>
        /// <param name="paramKey4"></param>
        /// <remarks>CLS compliant</remarks>
        public OptionAttribute(string paramKey1, string paramKey2, string paramKey3 = "", string paramKey4 = "") : this(new[] { paramKey1, paramKey2, paramKey3, paramKey4 })
        {
        }

        /// <summary>
        /// ToString overload (show the first supported argument name)
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ParamKeys[0];
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
