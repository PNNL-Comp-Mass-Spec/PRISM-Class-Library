using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PRISM
{
    /// <summary>
    /// Basic class for keeping parameters flags and properties for command line arguments tied together.
    /// Supports parameter flags similar to /d -dd --dir, with case sensitivity when needed,
    /// with the separator between parameter flag and parameter as ' ' or ':',
    /// and also supports using a parameter flag as a switch (if the associated property is a bool).
    /// Doesn't currently support supplying multiple values for an argument (it only keeps the last one supplied)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CommandLineParser<T> where T : class, new()
    {
        private static char[] paramChars = new char[] { '-', '/' };
        private static char[] separatorChars = new char[] { ' ', ':', '=' };

        private static Dictionary<string, string> ArgsPreprocess(string[] args)
        {
            var validArgs = GetValidArgs();
            if (validArgs == null)
            {
                return null;
            }

            var processed = new Dictionary<string, string>();

            for (var i = 0; i < args.Length; i++)
            {
                if (paramChars.Contains(args[i][0]))
                {
                    var key = args[i].TrimStart(paramChars);
                    var value = "";
                    var nextArgIsNumber = false;
                    if (i + 1 < args.Length && args[i + 1].StartsWith("-"))
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
                    if (!containedSeparator && i + 1 < args.Length && (nextArgIsNumber || !args[i + 1].StartsWith("-")))
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
                            Console.WriteLine("Error: Arg " + key + "does not match valid argument");
                            return null;
                        }
                        else if (!argInfo.CaseSensitive)
                        {
                            key = argInfo.ArgNormalCase;
                        }
                    }

                    // The last duplicate option gets priority
                    processed[key] = value;
                }
            }

            return processed;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="args"></param>
        /// <param name="options"></param>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        /// <returns></returns>
        public static bool ParseArgs(string[] args, T options, string entryAssemblyName = "", string versionInfo = "")
        {
            var success = true;
            if (args.Length == 0)
            {
                ShowHelp(entryAssemblyName, versionInfo);
                return false;
            }
            try
            {
                var preprocessed = ArgsPreprocess(args);
                if (preprocessed == null)
                {
                    ShowHelp(entryAssemblyName, versionInfo);
                    return false;
                }
                var props = GetPropertiesAttributes();

                foreach (var prop in props)
                {
                    var specified = false;
                    var keyGiven = "";
                    var value = "";
                    foreach (var key in prop.Value.ParamKeys)
                    {
                        if (preprocessed.ContainsKey(key))
                        {
                            specified = true;
                            keyGiven = key;
                            value = preprocessed[key];
                        }
                    }

                    if (prop.Value.Required && (!specified || string.IsNullOrWhiteSpace(value)))
                    {
                        Console.WriteLine(@"Error: Required argument missing: {0}{1}", paramChars[0], prop.Value.ParamKeys[0]);
                        success = false;
                    }

                    if (!specified)
                    {
                        continue;
                    }

                    // switch handling - no value specified
                    if (prop.Key.PropertyType == typeof(bool) && string.IsNullOrWhiteSpace(value))
                    {
                        prop.Key.SetValue(options, true);
                        continue;
                    }

                    try
                    {
                        object castValue = null;
                        if (!"null".Equals(value, StringComparison.OrdinalIgnoreCase))
                        {
                            castValue = Convert.ChangeType(value, prop.Key.PropertyType);
                        }
                        try
                        {
                            // Test the min/max, if supplied to the options attribute
                            if (prop.Value.Min != null)
                            {
                                // HACK: prevent allowed conversions from say, double to int; change the value to a string because Convert.ChangeType cannot do 2-step conversions (like string->double->int)
                                if (Convert.ChangeType(prop.Value.Min.ToString(), prop.Key.PropertyType) is IComparable castMin)
                                {
                                    if (castMin.CompareTo(castValue) > 0)
                                    {
                                        Console.WriteLine(@"Error: argument {0}, value of {1} is less than minimum of {2}", keyGiven, castValue, castMin);
                                        success = false;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine(@"Error: argument {0}, unable to check value of {1} against minimum of ""{2}"": cannot cast/compare minimum to type ""{3}""", keyGiven, castValue, prop.Value.Min, prop.Key.PropertyType.Name);
                                    success = false;
                                }
                            }
                            if (prop.Value.Max != null)
                            {
                                // HACK: prevent allowed conversions from say, double to int; change the value to a string because Convert.ChangeType cannot do 2-step conversions (like string->double->int)
                                if (Convert.ChangeType(prop.Value.Max.ToString(), prop.Key.PropertyType) is IComparable castMax)
                                {
                                    if (castMax.CompareTo(castValue) < 0)
                                    {
                                        Console.WriteLine(@"Error: argument {0}, value of {1} is greater than maximum of {2}", keyGiven, castValue, castMax);
                                        success = false;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine(@"Error: argument {0}, unable to check value of {1} against maximum of ""{2}"": cannot cast/compare maximum to type ""{3}""", keyGiven, castValue, prop.Value.Max, prop.Key.PropertyType.Name);
                                    success = false;
                                }
                            }
                        }
                        catch (InvalidCastException)
                        {
                            Console.WriteLine(@"Error: argument {0}, cannot cast min or max to type ""{1}""", keyGiven, prop.Key.PropertyType.Name);
                            success = false;
                        }
                        catch (FormatException)
                        {
                            Console.WriteLine(@"Error: argument {0}, cannot cast min or max to type ""{1}""", keyGiven, prop.Key.PropertyType.Name);
                            success = false;
                        }
                        catch (OverflowException)
                        {
                            Console.WriteLine(@"Error: argument {0}, cannot cast min or max to type ""{1}"" (out of range)", keyGiven, prop.Key.PropertyType.Name);
                            success = false;
                        }
                        prop.Key.SetValue(options, castValue);
                    }
                    catch (InvalidCastException)
                    {
                        Console.WriteLine(@"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""", keyGiven, value, prop.Key.PropertyType.Name);
                        success = false;
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine(@"Error: argument {0}, cannot cast ""{1}"" to type ""{2}""", keyGiven, value, prop.Key.PropertyType.Name);
                        success = false;
                    }
                    catch (OverflowException)
                    {
                        Console.WriteLine(@"Error: argument {0}, cannot cast ""{1}"" to type ""{2}"" (out of range)", keyGiven, value, prop.Key.PropertyType.Name);
                        success = false;
                    }
                }
            }
            catch (Exception)
            {
                success = false;
            }

            if (!success)
            {
                ShowHelp(entryAssemblyName, versionInfo);
            }

            return success;
        }

        /// <summary>
        /// Display the help contents, using the information supplied by the Option attributes and the default constructor for the templated type
        /// </summary>
        /// <param name="entryAssemblyName">Name of the executable</param>
        /// <param name="versionInfo">Executable version info</param>
        public static void ShowHelp(string entryAssemblyName = "", string versionInfo = "")
        {
            const int paramKeysWidth = 22;
            const int helpTextWidth = 52;
            var contents = CreateHelpContents();

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

        private static Dictionary<string, string> CreateHelpContents()
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
        private static Dictionary<string, ArgInfo> GetValidArgs()
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
                        Console.WriteLine(@"Error: Duplicate option keys specified in class {0}; key is ""{1}""", typeof(T).Name, key);
                        return null;
                    }
                    foreach (var invalidChar in paramChars)
                    {
                        if (key.StartsWith(invalidChar.ToString()))
                        {
                            // ERROR: Parameter marker character in parameter!
                            Console.WriteLine(@"Error: bad character in argument key ""{0}"" in {1}; key cannot start with char '{2}'", key, typeof(T).Name, invalidChar);
                            return null;
                        }
                    }
                    foreach (var invalidChar in separatorChars)
                    {
                        if (key.Contains(invalidChar.ToString()))
                        {
                            // ERROR: Parameter marker character in parameter!
                            Console.WriteLine(@"Error: bad character in argument key ""{0}"" in {1}; key contains invalid char '{2}'", key, typeof(T).Name, invalidChar);
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

        private static Dictionary<PropertyInfo, OptionAttribute> GetPropertiesAttributes()
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
    }
}
