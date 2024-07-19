using System;
using System.Linq;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace PRISM
{
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
        /// If the help screen should show the default value for an argument (value pulled from the default constructor)
        /// </summary>
        /// <remarks>Defaults to true</remarks>
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
        /// If the property is an enum, the enum values are listed by default
        /// </summary>
        /// <remarks>Set this to 'true' to not list the enum values</remarks>
        public bool DoNotListEnumValues { get; set; }

        /// <summary>
        /// Set to 'true' to hide the argument from the help out
        /// </summary>
        /// <remarks>This is useful for supporting obsolete arguments</remarks>
        public bool Hidden { get; set; }

        /// <summary>
        /// Set to 'true' for properties that specify an input file path (or input directory path)
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the path stored in this parameter is surrounded by double quotes or by single quotes, those quotes will be auto-removed
        /// </para>
        /// <para>
        /// Furthermore, if a parameter file is specified (using -ParamFile:Options.conf), the command line parser
        /// will process IsInputFilePath properties to look for files (or directories) in the working directory
        /// </para>
        /// <para>
        /// If the file (or directory) is not found, the parser will also look for the file (or directory) in the directory with the parameter file,
        /// and if the item is found there, the path stored in the property will be updated
        /// </para>
        /// </remarks>
        public bool IsInputFilePath { get; set; }

        /// <summary>
        /// If the argument is specified, the given boolean property will be set to "true"
        /// </summary>
        public string ArgExistsProperty { get; set; }

        /// <summary>
        /// If <see cref="ArgExistsProperty"/> is specified, and refers to a valid boolean property, this will be set to that property
        /// </summary>
        internal PropertyInfo ArgExistsPropertyInfo { get; set; }

        /// <summary>
        /// The preferred output parameter name/key when writing a parameter file
        /// </summary>
        internal string ParamFileOutputParamName { get; }

        /// <summary>
        /// When true, this is a secondary (not primary) argument and will be commented out in example parameter files created by CreateParamFile
        /// </summary>
        public bool SecondaryArg { get; set; }

        /// <summary>
        /// Constructor supporting any number of param keys
        /// </summary>
        /// <remarks>Not CLS compliant</remarks>
        /// <param name="paramKeys">Must supply at least one key for the argument, and it must be distinct within the class. Parameter file output will use either the first param key prefixed by '+', or the first param key if there are no matches; the prefix '+' is otherwise ignored.</param>
        public OptionAttribute(params string[] paramKeys)
        {
            // Check for null and remove blank entries; also remove leading/trailing spaces, and leading '+'
            ParamKeys = paramKeys?.Select(x => x.TrimStart(' ', '+').Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? throw new ArgumentNullException(nameof(paramKeys), "Argument cannot be null");

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
            IsInputFilePath = false;

            ParamFileOutputParamName = ParamKeys[0];

            if (paramKeys.Any(x => x.Trim().StartsWith("+")))
            {
                foreach (var key in paramKeys)
                {
                    if (string.IsNullOrWhiteSpace(key) || !key.Contains("+"))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(key.TrimStart(' ', '+').Trim()))
                    {
                        continue;
                    }

                    var trimmed = key.Trim();
                    if (trimmed.StartsWith("+"))
                    {
                        ParamFileOutputParamName = trimmed.TrimStart(' ', '+');

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Constructor, taking a single paramKey or a multiple param keys separated by a '|'
        /// </summary>
        /// <remarks>CLS compliant</remarks>
        /// <param name="paramKey">Must supply at least one key for the argument, and it must be distinct within the class; multiple keys can be specified, separated by a '|'</param>
        public OptionAttribute(string paramKey) : this(paramKey?.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
        }

        /// <summary>
        /// Constructor supporting up to 4 param keys
        /// </summary>
        /// <remarks>CLS compliant</remarks>
        /// <param name="paramKey1">Parameter key 1</param>
        /// <param name="paramKey2">Parameter key 2</param>
        /// <param name="paramKey3">Parameter key 3</param>
        /// <param name="paramKey4">Parameter key 4</param>
        public OptionAttribute(string paramKey1, string paramKey2, string paramKey3 = "", string paramKey4 = "") : this(new[] { paramKey1, paramKey2, paramKey3, paramKey4 })
        {
        }

        /// <summary>
        /// ToString overload (show the first supported argument name)
        /// </summary>
        public override string ToString()
        {
            return ParamKeys[0];
        }
    }
}
