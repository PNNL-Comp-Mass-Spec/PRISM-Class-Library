using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CommandLineParser<T> where T : class, new()
    {
        private static readonly char[] defaultParamChars = new char[] { '-', '/' };
        private static readonly char[] defaultSeparatorChars = new char[] { ' ', ':', '=' };

        public class ParserResults
        {
            public bool Success { get; private set; }
            public List<string> ParseErrors { get; private set; }
            public T ParsedResults { get; private set; }

            public ParserResults(T parsed)
            {
                Success = true;
                ParseErrors = new List<string>();
                ParsedResults = parsed;
            }

            internal void Failed()
            {
                Success = false;
            }

            public void OutputErrors()
            {
                foreach (var error in ParseErrors)
                {
                    Console.WriteLine(error);
                }
            }
        }

        private readonly string entryAssemblyName;
        private readonly string versionInfo;
        private char[] paramChars = defaultParamChars;
        private char[] separatorChars = defaultSeparatorChars;

        public ParserResults Results { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="entryAsmName">Name of the executing assembly</param>
        /// <param name="versionInfo"></param>
        public CommandLineParser(string entryAsmName = "", string versionInfo = "")
        {
            this.entryAssemblyName = entryAsmName;
            this.versionInfo = versionInfo;
            Results = new ParserResults(new T());
        }

        public IEnumerable<char> ParamFlagCharacters
        {
            get { return paramChars; }
            set { paramChars = value.Distinct().ToArray(); }
        }

        public IEnumerable<char> ParamSeparatorCharacters
        {
            get { return separatorChars; }
            set { separatorChars = value.Distinct().ToArray(); }
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
            return ParseArgs(args, entryAssemblyName, versionInfo).Success;
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
            var parser = new CommandLineParser<T>(entryAssemblyName, versionInfo);
            parser.Results = new ParserResults(options);
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
        /// <param name="onErrorOutputHelp"></param>
        /// <returns></returns>
        public ParserResults ParseArgs(string[] args, bool onErrorOutputHelp = true)
        {
            if (args.Length == 0)
            {
                if (onErrorOutputHelp)
                {
                    PrintHelp();
                }
                Results.Failed();
                return Results;
            }
            try
            {
                var preprocessed = ArgsPreprocess(args);
                if (preprocessed == null)
                {
                    if (onErrorOutputHelp)
                    {
                        PrintHelp();
                    }
                    Results.Failed();
                    return Results;
                }
                var props = GetPropertiesAttributes();

                foreach (var prop in props)
                {
                    var specified = false;
                    var keyGiven = "";
                    List<string> value = null;
                    foreach (var key in prop.Value.ParamKeys)
                    {
                        if (preprocessed.ContainsKey(key))
                        {
                            specified = true;
                            keyGiven = key;
                            value = preprocessed[key];
                        }
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

            if (!Results.Success && onErrorOutputHelp)
            {
                PrintHelp();
            }

            return Results;
        }

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

        private Dictionary<string, List<string>> ArgsPreprocess(string[] args)
        {
            var validArgs = GetValidArgs();
            if (validArgs == null)
            {
                return null;
            }

            var processed = new Dictionary<string, List<string>>();

            for (var i = 0; i < args.Length; i++)
            {
                if (paramChars.Contains(args[i][0]))
                {
                    var key = args[i].TrimStart(paramChars);
                    var value = "";
                    var nextArgIsNumber = false;
                    if (paramChars.Contains('-') && i + 1 < args.Length && args[i + 1].StartsWith("-"))
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
                    if (!containedSeparator && i + 1 < args.Length && (nextArgIsNumber || !paramChars.Contains(args[i + 1][0])))
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
                        else if (!argInfo.CaseSensitive)
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
            }

            return processed;
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

            Console.WriteLine();
            if (!string.IsNullOrWhiteSpace(versionInfo))
            {
                Console.WriteLine(@"{0} {1}", entryAssemblyName, versionInfo);
            }
            if (!string.IsNullOrWhiteSpace(entryAssemblyName))
            {
                Console.WriteLine(@"Usage: {0}", entryAssemblyName + ".exe");
            }
            else
            {
                Console.WriteLine(@"Usage:");
            }

            var outputFormatString = "  {0,-" + paramKeysWidth + "}    {1}";

            foreach (var option in contents)
            {
                var overflow = new List<Tuple<string, string>>();
                var keyOverflow = new List<string>();
                var textOverflow = new List<string>();

                if (option.Key.Length <= paramKeysWidth)
                {
                    keyOverflow.Add(option.Key);
                }
                else
                {
                    var split = option.Key.Split(' ');
                    var line = "";
                    foreach (var key in split)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            if (key.Length + line.Length > paramKeysWidth)
                            {
                                keyOverflow.Add(line);
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
                        keyOverflow.Add(line);
                    }
                }

                if (option.Value.Length <= helpTextWidth)
                {
                    textOverflow.Add(option.Value);
                }
                else
                {
                    var split = option.Value.Split(' ');
                    var line = "";
                    foreach (var word in split)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            if (word.Length + line.Length > helpTextWidth)
                            {
                                textOverflow.Add(line);
                                line = "";
                            }
                            else
                            {
                                line += " ";
                            }
                        }
                        line += word;
                    }
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        textOverflow.Add(line);
                    }
                }

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

                Console.WriteLine();
                foreach (var line in overflow)
                {
                    Console.WriteLine(outputFormatString, line.Item1, line.Item2);
                }
            }

            Console.WriteLine();
        }

        private Dictionary<string, string> CreateHelpContents()
        {
            var contents = new Dictionary<string, string>();

            var optionsForDefaults = new T();

            var props = GetPropertiesAttributes();

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

                    keys += "-" + key;
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

        /// <summary>
        /// Get the valid args
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, ArgInfo> GetValidArgs()
        {
            var validArgs = new Dictionary<string, ArgInfo>();
            var argCollisions = new Dictionary<string, bool>();
            var props = GetPropertiesAttributes();

            foreach (var prop in props)
            {
                foreach (var key in prop.Value.ParamKeys)
                {
                    var lower = key.ToLower();
                    if (!argCollisions.ContainsKey(lower))
                    {
                        argCollisions.Add(lower, false);
                    }
                    else
                    {
                        argCollisions[lower] = true;
                    }
                }
            }

            foreach (var prop in props)
            {
                var canBeSwitch = prop.Key.PropertyType == typeof(bool);
                foreach (var key in prop.Value.ParamKeys)
                {
                    var lower = key.ToLower();
                    ArgInfo info = null;
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
                        Results.ParseErrors.Add(string.Format(@"Error: Duplicate option keys specified in class {0}; key is ""{1}""", typeof(T).Name, key));
                        return null;
                    }
                    foreach (var invalidChar in paramChars)
                    {
                        if (key.StartsWith(invalidChar.ToString()))
                        {
                            // ERROR: Parameter marker character in parameter!
                            Results.ParseErrors.Add(string.Format(@"Error: bad character in argument key ""{0}"" in {1}; key cannot start with char '{2}'", key, typeof(T).Name, invalidChar));
                            return null;
                        }
                    }
                    foreach (var invalidChar in separatorChars)
                    {
                        if (key.Contains(invalidChar.ToString()))
                        {
                            // ERROR: Parameter marker character in parameter!
                            Results.ParseErrors.Add(string.Format(@"Error: bad character in argument key ""{0}"" in {1}; key contains invalid char '{2}'", key, typeof(T).Name, invalidChar));
                            return null;
                        }
                    }

                    info.AllArgNormalCase.Add(key);
                }
            }

            return validArgs;
        }

        private class ArgInfo
        {
            public string ArgNormalCase { get; set; }
            public List<string> AllArgNormalCase { get; private set; }
            public bool CaseSensitive { get; set; }
            public bool CanBeSwitch { get; set; }

            public ArgInfo()
            {
                ArgNormalCase = "";
                AllArgNormalCase = new List<string>(2);
                CaseSensitive = false;
                CanBeSwitch = false;
            }
        }

        private Dictionary<PropertyInfo, OptionAttribute> GetPropertiesAttributes()
        {
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

        public override string ToString()
        {
            return ParamKeys[0];
        }
    }
}
