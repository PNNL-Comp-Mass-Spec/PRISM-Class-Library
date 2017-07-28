using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PRISM
{
    /// <summary>
    /// Basic class for keeping parameters flags and properties for command line arguments tied together.
    /// Only supports properties of primitive types (and arrays of primitive types)
    /// Supports parameter flags similar to /d -dd --dir, with case sensitivity when needed,
    /// with the separator between parameter flag and parameter as ' ' or ':',
    /// and also supports using a parameter flag as a switch (if the associated property is a bool).
    /// If an argument is supplied multiple times, it only keeps the last one supplied
    /// If the property is an array, multiple values are provided using '-paramName value -paramName value ...' or similar
    /// Includes support for showing help with no args supplied, or with argument names of "?" and "help" (can be overridden)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CommandLineParser<T> where T : class, new()
    {
        private static readonly char[] defaultParamChars = new char[] { '-', '/' };
        private static readonly char[] defaultSeparatorChars = new char[] { ' ', ':', '=' };
        private static readonly string[] defaultHelpArgs = new string[] {"?", "help"};

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
            public List<string> ParseErrors { get; }

            /// <summary>
            /// Target object, populated with the parsed arguments when the parsing completes
            /// </summary>
            public T ParsedResults { get; }

            #endregion

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="parsed"></param>
            public ParserResults(T parsed)
            {
                Success = true;
                ParseErrors = new List<string>();
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

        private char[] paramChars = defaultParamChars;
        private char[] separatorChars = defaultSeparatorChars;
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
            EntryAssemblyName = entryAsmName;
            ExeVersionInfo = versionInfo;

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
        /// <returns>false if argument parse failed</returns>
        public static bool ParseArgs(string[] args, T options, string versionInfo = "")
        {
            var entryAssemblyName = Assembly.GetEntryAssembly().GetName().Name;
            return ParseArgs(args, options, entryAssemblyName, versionInfo);
        }

        /// <summary>
        /// Parse the arguments into <paramref name="options"/>, returning a bool.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="options"></param>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns>false if argument parse failed</returns>
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
        /// <returns></returns>
        public static ParserResults ParseArgs(string[] args, string versionInfo)
        {
            var entryAssemblyName = Assembly.GetEntryAssembly().GetName().Name;
            return ParseArgs(args, entryAssemblyName, versionInfo);
        }

        /// <summary>
        /// Parse the arguments, returning the parsing results in <see cref="ParserResults"/>.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns></returns>
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
        /// <returns></returns>
        public ParserResults ParseArgs(string[] args, bool onErrorOutputHelp = true, bool outputErrors = true)
        {
            if (args.Length == 0)
            {
                // Automatically output help when no arguments are supplied
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
                foreach (var helpArg in defaultHelpArgs)
                {
                    if (preprocessed.ContainsKey(helpArg) && validArgs.ContainsKey(helpArg.ToLower()))
                    {
                        // Make sure the help arg is not defined in the template class
                        if (validArgs[helpArg.ToLower()].IsBuiltInArg)
                        {
                            PrintHelp();
                            Results.Failed();
                            return Results;
                        }
                    }
                }

                foreach (var prop in props)
                {
                    var specified = false;
                    var keyGiven = "";
                    List<string> value = null;
                    // Find any arguments that match this property
                    foreach (var key in prop.Value.ParamKeys)
                    {
                        if (preprocessed.ContainsKey(key))
                        {
                            specified = true;
                            keyGiven = key;
                            value = preprocessed[key];
                        }
                    }

                    var positionalArgName = GetPositionalArgName(prop.Value.ArgPosition);
                    if (prop.Value.ArgPosition > 0 && preprocessed.ContainsKey(positionalArgName))
                    {
                        specified = true;
                        keyGiven = "PositionalArgument" + prop.Value.ArgPosition;
                        value = preprocessed[positionalArgName];
                    }

                    if (prop.Value.Required && (!specified || value == null || value.Count == 0))
                    {
                        Results.ParseErrors.Add(string.Format(@"Error: Required argument missing: {0}{1}", paramChars[0], prop.Value.ParamKeys[0]));
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

                    try
                    {
                        // parse/cast the value to the appropriate type, checking the min and max limits, and set the value using reflection
                        object castValue = null;
                        if (prop.Key.PropertyType.IsArray)
                        {
                            var castVals = Array.CreateInstance(prop.Key.PropertyType.GetElementType(), value.Count);
                            var i = 0;
                            foreach (var val in value)
                            {
                                var castVal = ParseValueToType(prop.Key.PropertyType.GetElementType(), prop.Value, keyGiven, val);
                                castVals.SetValue(castVal, i++);
                            }
                            castValue = castVals;
                        }
                        else
                        {
                            castValue = ParseValueToType(prop.Key.PropertyType, prop.Value, keyGiven, value.Last());
                        }
                        prop.Key.SetValue(Results.ParsedResults, castValue);
                    }
                    catch (InvalidCastException)
                    {
                        Results.ParseErrors.Add(string.Format(@"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""", keyGiven, value, prop.Key.PropertyType.Name));
                        Results.Failed();
                    }
                    catch (FormatException)
                    {
                        Results.ParseErrors.Add(string.Format(@"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""", keyGiven, value, prop.Key.PropertyType.Name));
                        Results.Failed();
                    }
                    catch (OverflowException)
                    {
                        Results.ParseErrors.Add(string.Format(@"Error: argument {0}, cannot cast ""{1}"" to type ""{2}"" (out of range)", keyGiven, value, prop.Key.PropertyType.Name));
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
        /// <returns></returns>
        private object ParseValueToType(Type propertyType, OptionAttribute parseData, string argKey, string valueToParse)
        {
            object castValue = null;
            if (!"null".Equals(valueToParse, StringComparison.OrdinalIgnoreCase))
            {
                castValue = Convert.ChangeType(valueToParse, propertyType);
            }
            try
            {
                // Test the min/max, if supplied to the options attribute
                if (parseData.Min != null)
                {
                    // HACK: prevent allowed conversions from say, double to int; change the value to a string because Convert.ChangeType cannot do 2-step conversions (like string->double->int)
                    if (Convert.ChangeType(parseData.Min.ToString(), propertyType) is IComparable castMin)
                    {
                        if (castMin.CompareTo(castValue) > 0)
                        {
                            Results.ParseErrors.Add(string.Format(@"Error: argument {0}, value of {1} is less than minimum of {2}", argKey, castValue, castMin));
                            Results.Failed();
                        }
                    }
                    else
                    {
                        Results.ParseErrors.Add(string.Format(@"Error: argument {0}, unable to check value of {1} against minimum of ""{2}"": cannot cast/compare minimum to type ""{3}""", argKey, castValue, parseData.Min, propertyType.Name));
                        Results.Failed();
                    }
                }
                if (parseData.Max != null)
                {
                    // HACK: prevent allowed conversions from say, double to int; change the value to a string because Convert.ChangeType cannot do 2-step conversions (like string->double->int)
                    if (Convert.ChangeType(parseData.Max.ToString(), propertyType) is IComparable castMax)
                    {
                        if (castMax.CompareTo(castValue) < 0)
                        {
                            Results.ParseErrors.Add(string.Format(@"Error: argument {0}, value of {1} is greater than maximum of {2}", argKey, castValue, castMax));
                            Results.Failed();
                        }
                    }
                    else
                    {
                        Results.ParseErrors.Add(string.Format(@"Error: argument {0}, unable to check value of {1} against maximum of ""{2}"": cannot cast/compare maximum to type ""{3}""", argKey, castValue, parseData.Max, propertyType.Name));
                        Results.Failed();
                    }
                }
            }
            catch (InvalidCastException)
            {
                Results.ParseErrors.Add(string.Format(@"Error: argument {0}, cannot cast min or max to type ""{1}""", argKey, propertyType.Name));
                Results.Failed();
            }
            catch (FormatException)
            {
                Results.ParseErrors.Add(string.Format(@"Error: argument {0}, cannot cast min or max to type ""{1}""", argKey, propertyType.Name));
                Results.Failed();
            }
            catch (OverflowException)
            {
                Results.ParseErrors.Add(string.Format(@"Error: argument {0}, cannot cast min or max to type ""{1}"" (out of range)", argKey, propertyType.Name));
                Results.Failed();
            }
            return castValue;
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

                    if (!validArgs.TryGetValue(argName, out var argInfo))
                        continue;

                    if (!processed.ContainsKey(argName))
                    {
                        processed.Add(argName, new List<string>());
                    }
                    processed[argName].Add(args[i]);

                    continue;
                }

                var key = args[i].TrimStart(paramChars);
                var value = "";
                var nextArgIsNumber = false;
                if (paramChars.Contains('-') && i + 1 < args.Count && args[i + 1].StartsWith("-"))
                {
                    double x;
                    // Try converting to most forgiving number format
                    nextArgIsNumber = double.TryParse(args[i + 1], out x);

                    // Check if the program supports a numeric argument (but we only need to remove a '-', because a '/' won't parse as a double)
                    if (nextArgIsNumber && validArgs.ContainsKey(args[i + 1].TrimStart('-')))
                    {
                        nextArgIsNumber = false;
                    }
                }

                var containedSeparator = false;
                foreach (var separatorChar in separatorChars)
                {
                    var separator = separatorChar.ToString();
                    if (key.Contains(separator))
                    {
                        containedSeparator = true;
                        // Only split off the first separator, since others may be part of drive specifiers
                        var pos = key.IndexOf(separatorChar);
                        value = key.Substring(pos + 1);
                        key = key.Substring(0, pos);
                    }
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
                        Results.ParseErrors.Add(string.Format("Error: Arg " + key + "does not match valid argument"));
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
        /// <returns></returns>
        private string GetPositionalArgName(int argPosition)
        {
            return "##" + argPosition + "##";
        }

        /// <summary>
        /// Display the help contents, using the information supplied by the Option attributes and the default constructor for the templated type
        /// </summary>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
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
            const int paramKeysWidth = 22;
            const int helpTextWidth = 52;
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
                Console.WriteLine(@"Usage: {0}", EntryAssemblyName + ".exe");
            }
            else
            {
                Console.WriteLine(@"Usage:");
            }

            var outputFormatString = "  {0,-" + paramKeysWidth + "}    {1}";

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
                    var key = "";
                    var text = "";
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
        /// Create the help text and argument name list for each argument
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> CreateHelpContents()
        {
            var contents = new Dictionary<string, string>();

            var optionsForDefaults = new T();

            var props = GetPropertiesAttributes();
            var validArgs = GetValidArgs();
            var helpArgString = "";
            // Add the default help string
            foreach (var helpArg in defaultHelpArgs)
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
                var defaultValueObj = prop.Key.GetValue(optionsForDefaults);
                var defaultValue = "null";
                if (defaultValueObj != null)
                {
                    defaultValue = defaultValueObj.ToString();
                }

                var keys = "";
                foreach (var key in prop.Value.ParamKeys)
                {
                    if (!string.IsNullOrWhiteSpace(keys))
                    {
                        keys += ", ";
                    }

                    keys += paramChars[0] + key;
                }

                var helpText = "";
                if (prop.Value.Required)
                {
                    helpText += "Required. ";
                }

                helpText += prop.Value.HelpText;

                if (prop.Value.HelpShowsDefault)
                {
                    helpText += string.Format(" (Default: {0})", defaultValue);
                }

                if (contents.ContainsKey(keys))
                {
                    // Critical error - make sure the user focuses on the error, because it's a big one, and must be fixed by the developer.
                    contents.Clear();
                    return contents;
                }
                contents.Add(keys, helpText);
            }

            return contents;
        }

        private string WrapParagraph(string textToWrap, int wrapWidth = 80)
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

        private List<string> WrapParagraphAsList(string textToWrap, int wrapWidth)
        {
            var split = textToWrap.Split(' ');
            var wrappedText = new List<string>();

            var line = string.Empty;

            foreach (var key in split)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    if (key.Length + line.Length > wrapWidth)
                    {
                        wrappedText.Add(line);
                        line = "";
                    }
                    else
                    {
                        line += " ";
                    }
                }
                line += key;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                wrappedText.Add(line);
            }

            return wrappedText;
        }

        /// <summary>
        /// Get the arguments that are valid for the class, dealing with argument name collision and invalid characters as needed
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, ArgInfo> GetValidArgs()
        {
            if (this.validArguments != null)
            {
                return this.validArguments;
            }
            var validArgs = new Dictionary<string, ArgInfo>();
            var props = GetPropertiesAttributes();

            foreach (var prop in props)
            {
                var canBeSwitch = prop.Key.PropertyType == typeof(bool);
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
                        Results.ParseErrors.Add(string.Format(
                            @"Error: Duplicate option keys specified in class {0}; key is ""{1}""", typeof(T).Name,
                            key));
                        return null;
                    }

                    foreach (var invalidChar in paramChars)
                    {
                        if (key.StartsWith(invalidChar.ToString()))
                        {
                            // ERROR: Parameter marker character at start of parameter!
                            Results.ParseErrors.Add(string.Format(@"Error: bad character in argument key ""{0}"" in {1}; key cannot start with char '{2}'", key, typeof(T).Name, invalidChar));
                            return null;
                        }
                    }
                    foreach (var invalidChar in separatorChars)
                    {
                        if (key.Contains(invalidChar.ToString()))
                        {
                            // ERROR: Parameter separator character in parameter!
                            Results.ParseErrors.Add(string.Format(@"Error: bad character in argument key ""{0}"" in {1}; key contains invalid char '{2}'", key, typeof(T).Name, invalidChar));
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
                        Results.ParseErrors.Add(string.Format(
                            @"Error: Multiple properties in class {0} specify ArgPosition {1}; conflict involves ""{2}""",
                            typeof(T).Name, argPosition, prop.Key.Name));
                        return null;
                    }

                    validArgs.Add(argName, info);
                    info.ArgNormalCase = argName;
                    info.AllArgNormalCase.Add(argName);
                }
            }

            foreach (var helpArg in defaultHelpArgs)
            {
                if (!validArgs.ContainsKey(helpArg.ToLower()))
                {
                    var info = new ArgInfo()
                    {
                        ArgNormalCase = helpArg,
                        CanBeSwitch = true,
                        IsBuiltInArg = true,
                    };
                    info.AllArgNormalCase.Add(helpArg);

                    validArgs.Add(helpArg, info);
                }
            }

            this.validArguments = validArgs;
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
            public List<string> AllArgNormalCase { get; private set; }

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
                ArgNormalCase = "";
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

#if !(NETSTANDARD1_x || NETSTANDARD2_0)
            var properties = typeof(T).GetProperties();
#else
            var properties = typeof(T).GetTypeInfo().GetProperties();
#endif

            foreach (var property in properties)
            {
#if !(NETSTANDARD1_x || NETSTANDARD2_0)
                // Check for the attribute
                if (!Attribute.IsDefined(property, typeof(OptionAttribute)))
                {
                    continue;
                }
#endif

                var attrib = property.GetCustomAttributes(typeof(OptionAttribute), true);
                if (attrib == null)
                {
                    continue;
                }
                var attribList = attrib.ToArray();
                if (attribList.Length == 0)
                {
                    continue;
                }

                // ignore any duplicates (shouldn't occur anyway)
                props.Add(property, attribList[0] as OptionAttribute);
            }

            propertiesAndAttributes = props;
            return props;
        }
    }

    /// <summary>
    /// Attribute class to flag properties that are command line arguments
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    [CLSCompliant(false)]
    public class OptionAttribute : Attribute
    {
        /// <summary>
        /// Text displayed on the help screen
        /// </summary>
        public string HelpText { get; set; }

        /// <summary>
        /// If the argument is required
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Strings that mark a command line argument (when parsing the command line)
        /// </summary>
        public string[] ParamKeys { get; private set; }

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
        /// If the help screen should show the default value for an argument (value pulled from the default constructor)
        /// </summary>
        public bool HelpShowsDefault { get; set; }

        /// <summary>
        /// Minimum value, for a numeric argument
        /// </summary>
        public object Min { get; set; }

        /// <summary>
        /// Maximum value, for a numeric argument
        /// </summary>
        public object Max { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="paramKeys">Must supply at least one key for the argument, and it must be distinct within the class</param>
        public OptionAttribute(params string[] paramKeys)
        {
            this.ParamKeys = paramKeys;
            this.Max = null;
            this.Min = null;
        }

        /// <summary>
        /// ToString overload (for debugging ease)
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ParamKeys[0];
        }
    }
}
