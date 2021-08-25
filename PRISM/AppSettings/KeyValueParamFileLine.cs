using System.Collections.Generic;

// ReSharper disable UnusedMember.Global

namespace PRISM.AppSettings
{
    /// <summary>
    /// Line of data from a Key=Value parameter file, stored in<see cref="Text"/>
    /// </summary>
    /// <remarks>
    /// The class optionally also tracks the parameter name and value, using <see cref="ParamName"/> and <see cref="ParamValue"/>
    /// </remarks>
    public class KeyValueParamFileLine
    {
        /// <summary>
        /// Line number in the parameter file
        /// </summary>
        public int LineNumber { get; }

        /// <summary>
        /// Text of the line from the parameter file (including the comment, if any)
        /// </summary>
        public string Text { get; protected set; }

        /// <summary>
        /// Parameter name if this line contains a parameter, otherwise an empty string
        /// </summary>
        public string ParamName { get; private set; }

        /// <summary>
        /// Parameter value if this line contains a parameter, otherwise an empty string
        /// </summary>
        public string ParamValue { get; private set; }

        /// <summary>
        /// Comment text; may be an empty string
        /// </summary>
        /// <remarks>If a comment is defined, this includes the leading # comment character</remarks>
        public string Comment { get; private set; }

        /// <summary>
        /// True if ParamName has text, otherwise false
        /// </summary>
        /// <remarks>If ParamName has text but ParamValue is empty, this still returns true</remarks>
        public bool HasParameter => !string.IsNullOrWhiteSpace(ParamName);

        /// <summary>
        /// Constructor that takes a line number and line text
        /// </summary>
        /// <param name="lineNumber"></param>
        /// <param name="lineText"></param>
        /// <param name="parseKeyValuePair">When true, parse lineText to determine the key name and value</param>
        public KeyValueParamFileLine(int lineNumber, string lineText, bool parseKeyValuePair = false)
        {
            LineNumber = lineNumber;
            Text = lineText;

            if (!parseKeyValuePair)
            {
                ParamName = string.Empty;
                ParamValue = string.Empty;

                return;
            }

            var parsedSetting = KeyValueParamFileReader.GetKeyValueSetting(lineText, out var comment);

            ParamName = parsedSetting.Key;
            ParamValue = parsedSetting.Value;
            StoreComment(comment);
        }

        /// <summary>
        /// Constructor that just takes an instance of this class
        /// </summary>
        /// <param name="paramFileLine"></param>
        public KeyValueParamFileLine(KeyValueParamFileLine paramFileLine) : this(paramFileLine.LineNumber, paramFileLine.Text)
        {
            ParamName = paramFileLine.ParamName;
            ParamValue = paramFileLine.ParamValue;
            StoreComment(paramFileLine.Comment);
        }

        private void StoreComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                Comment = string.Empty;
                return;
            }

            var trimmedComment = comment.Trim();

            if (trimmedComment.StartsWith("#"))
            {
                Comment = trimmedComment;
                return;
            }

            Comment = string.Format("# {0}", trimmedComment);
        }

        /// <summary>
        /// Associate a parameter with this data line
        /// </summary>
        /// <param name="paramName">Parameter name</param>
        /// <param name="paramValue">Parameter value</param>
        /// <param name="comment">Optional comment</param>
        /// <param name="updateTextProperty">When true, update <see cref="Text"/></param>
        public void StoreParameter(string paramName, string paramValue, string comment = "", bool updateTextProperty = false)
        {
            ParamName = paramName;
            ParamValue = paramValue;
            StoreComment(comment);

            if (updateTextProperty)
                UpdateTextUsingStoredData();
        }

        /// <summary>
        /// Associate a parameter with this data line
        /// </summary>
        /// <param name="paramInfo">Parameter</param>
        /// <param name="comment">Optional comment</param>
        /// <param name="updateTextProperty">When true, update <see cref="Text"/></param>
        public void StoreParameter(KeyValuePair<string, string> paramInfo, string comment = "", bool updateTextProperty = false)
        {
            ParamName = paramInfo.Key;
            ParamValue = paramInfo.Value;
            StoreComment(comment);

            if (updateTextProperty)
                UpdateTextUsingStoredData();
        }

        /// <summary>
        /// Update property <see cref="Text"/> using <see cref="ParamName"/>, <see cref="ParamValue"/>, and <see cref="Comment"/>
        /// </summary>
        private void UpdateTextUsingStoredData()
        {
            Text = string.Format("{0}={1}{2}",
                ParamName,
                ParamValue,
                string.IsNullOrWhiteSpace(Comment) ?
                    string.Empty :
                    string.Format("    {0}", Comment));
        }

        /// <summary>
        /// Update the value for this parameter
        /// </summary>
        /// <param name="value"></param>
        /// <param name="updateTextProperty">When true, update <see cref="Text"/></param>
        protected void UpdateValue(string value, bool updateTextProperty = false)
        {
            ParamValue = value ?? string.Empty;

            if (updateTextProperty)
                UpdateTextUsingStoredData();
        }

        /// <summary>
        /// Return the text of the line from the parameter file (including the comment, if any)
        /// </summary>
        public override string ToString()
        {
            return Text;
        }
    }
}
