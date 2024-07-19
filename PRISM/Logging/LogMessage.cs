using System;

namespace PRISM.Logging
{
    /// <summary>
    /// Class for tracking individual messages to log
    /// </summary>
    public class LogMessage
    {
        // Ignore Spelling: dd, MM, yyyy, HH:mm:ss, hh:mm:ss tt

        /// <summary>
        /// Default timestamp format mode
        /// </summary>
        public const TimestampFormatMode DEFAULT_TIMESTAMP_FORMAT = TimestampFormatMode.YearMonthDay24hr;

        /// <summary>
        /// Month/day/year Time (24 hour clock)
        /// </summary>
        public const string DATE_TIME_FORMAT_MONTH_DAY_YEAR_24H = "MM/dd/yyyy HH:mm:ss";

        /// <summary>
        /// Month/day/year Time am/pm
        /// </summary>
        public const string DATE_TIME_FORMAT_MONTH_DAY_YEAR_12H = "MM/dd/yyyy hh:mm:ss tt";

        /// <summary>
        /// Year-month-day Time (24 hour clock)
        /// </summary>
        public const string DATE_TIME_FORMAT_YEAR_MONTH_DAY_24H = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Year-month-day Time am/pm
        /// </summary>
        public const string DATE_TIME_FORMAT_YEAR_MONTH_DAY_12H = "yyyy-MM-dd hh:mm:ss tt";

        /// <summary>
        /// Timestamp format mode
        /// </summary>
        public enum TimestampFormatMode
        {
            /// <summary>
            /// Month/day/year Time (24 hour clock)
            /// </summary>
            MonthDayYear24hr = 0,

            /// <summary>
            /// Month/day/year Time am/pm
            /// </summary>
            MonthDayYear12hr = 1,

            /// <summary>
            /// Year-month-day Time (24 hour clock)
            /// </summary>
            YearMonthDay24hr = 2,

            /// <summary>
            /// Year-month-day Time am/pm
            /// </summary>
            YearMonthDay12hr = 3
        }

        /// <summary>
        /// Log level (aka log message type)
        /// </summary>
        public BaseLogger.LogLevels LogLevel { get; }

        /// <summary>
        /// Log message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Exception associated with the message (may be null)
        /// </summary>
        public Exception MessageException { get; }

        /// <summary>
        /// Message date (UTC-based time)
        /// </summary>
        public DateTime MessageDateUTC { get; }

        /// <summary>
        /// Message date (Local time)
        /// </summary>
        public DateTime MessageDateLocal => MessageDateUTC.ToLocalTime();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logLevel">Log level</param>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception</param>
        public LogMessage(BaseLogger.LogLevels logLevel, string message, Exception ex = null)
        {
            LogLevel = logLevel;
            Message = message;
            MessageException = ex;
            MessageDateUTC = DateTime.UtcNow;
        }

        /// <summary>
        /// Get the log message, formatted in the form Date, Message, LogType
        /// </summary>
        /// <param name="timestampFormat">Timestamp format mode</param>
        /// <returns>Formatted message (does not include anything regarding MessageException)</returns>
        public string GetFormattedMessage(TimestampFormatMode timestampFormat)
        {
            return GetFormattedMessage(true, timestampFormat);
        }

        /// <summary>
        /// Get the log message, formatted as Date, Message, LogType
        /// </summary>
        /// <param name="useLocalTime">When true, use the local time, otherwise use UTC time</param>
        /// <param name="timestampFormat">Timestamp format mode</param>
        /// <returns>Formatted message (does not include anything regarding MessageException)</returns>
        public string GetFormattedMessage(
            bool useLocalTime = true,
            TimestampFormatMode timestampFormat = DEFAULT_TIMESTAMP_FORMAT)
        {
            var formatString = GetTimestampFormatString(timestampFormat);

            string timeStamp;

            if (useLocalTime)
                timeStamp = MessageDateLocal.ToString(formatString);
            else
                timeStamp = MessageDateUTC.ToString(formatString);

            return string.Format("{0}, {1}, {2}", timeStamp, Message, LogLevel.ToString());
        }

        private string GetTimestampFormatString(TimestampFormatMode timestampFormat)
        {
            return timestampFormat switch
            {
                TimestampFormatMode.MonthDayYear24hr => DATE_TIME_FORMAT_MONTH_DAY_YEAR_24H,
                TimestampFormatMode.MonthDayYear12hr => DATE_TIME_FORMAT_MONTH_DAY_YEAR_12H,
                TimestampFormatMode.YearMonthDay24hr => DATE_TIME_FORMAT_YEAR_MONTH_DAY_24H,
                TimestampFormatMode.YearMonthDay12hr => DATE_TIME_FORMAT_YEAR_MONTH_DAY_12H,
                _ => GetTimestampFormatString(DEFAULT_TIMESTAMP_FORMAT),
            };
        }

        /// <summary>
        /// The log message and log type, separated by a comma
        /// </summary>
        public override string ToString()
        {
            return Message + ", " + LogLevel;
        }
    }
}
