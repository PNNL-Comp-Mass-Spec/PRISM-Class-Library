﻿using System;
using System.Security.Principal;

namespace PRISM.Logging
{
    /// <summary>
    /// Logs messages to a database by calling a stored procedure
    /// </summary>
    public abstract class DatabaseLogger : BaseLogger
    {
        /// <summary>
        /// Interval, in milliseconds, between flushing log messages to the database
        /// </summary>
        protected const int LOG_INTERVAL_MILLISECONDS = 1000;

        /// <summary>
        /// Database timeout length, in seconds
        /// </summary>
        protected const int TIMEOUT_SECONDS = 15;

        /// <summary>
        /// Messages will be sent to the database if they are this value or lower
        /// </summary>
        private LogLevels mLogThresholdLevel;

        /// <summary>
        /// Database connection string
        /// </summary>
        public static string ConnectionString { get; protected set; }

        /// <summary>
        /// When true, also send any messages to the file logger
        /// </summary>
        public bool EchoMessagesToFileLogger { get; set; } = true;

        /// <summary>
        /// True if the connection string and stored procedure name are defined
        /// </summary>
        public static bool HasConnectionInfo => !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(LoggingProcedure.ProcedureName);

        /// <summary>
        /// When true, log type will be changed from all caps to InitialCaps (e.g. INFO to Info)
        /// </summary>
        public static bool InitialCapsLogTypes { get; set; } = true;

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.DEBUG or higher)
        /// </summary>
        public bool IsDebugEnabled { get; private set; }

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.ERROR or higher)
        /// </summary>
        public bool IsErrorEnabled { get; private set; }

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.FATAL or higher)
        /// </summary>
        public bool IsFatalEnabled { get; private set; }

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.INFO or higher)
        /// </summary>
        public bool IsInfoEnabled { get; private set; }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// True if info level logging is enabled (LogLevel is LogLevels.WARN or higher)
        /// </summary>
        public bool IsWarnEnabled { get; private set; }

        /// <summary>
        /// Get or set the current log threshold level
        /// </summary>
        /// <remarks>
        /// If the LogLevel is DEBUG, all messages are logged
        /// If the LogLevel is INFO, all messages except DEBUG messages are logged
        /// If the LogLevel is ERROR, only FATAL and ERROR messages are logged
        /// </remarks>
        public LogLevels LogLevel
        {
            get => mLogThresholdLevel;
            set => SetLogLevel(value);
        }

        /// <summary>
        /// Information for the procedure used to store log messages in the database
        /// </summary>
        public static LogProcedureInfo LoggingProcedure { get; } = new();

        /// <summary>
        /// The module name identifies the logging process
        /// </summary>
        public static string MachineName => System.Net.Dns.GetHostName();

        /// <summary>
        /// The username running this program
        /// </summary>
        public static string UserName
        {
            get
            {
#if !NET462
            // System.Runtime.InteropServices.RuntimeInformation is not available with .NET 4.6.2
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return string.Empty;
            }
#endif
                return WindowsIdentity.GetCurrent().Name;
            }
        }

        /// <summary>
        /// Update the database connection info
        /// </summary>
        /// <remarks>Will append today's date to the base name</remarks>
        /// <param name="moduleName">Program name to be sent to the PostedBy field when contacting the database</param>
        /// <param name="connectionString">Database connection string</param>
        public abstract void ChangeConnectionInfo(
            string moduleName,
            string connectionString);

        /// <summary>
        /// Update the database connection info
        /// </summary>
        /// <remarks>Will append today's date to the base name</remarks>
        /// <param name="moduleName">Program name to be sent to the PostedBy field when contacting the database</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedure">Stored procedure to call</param>
        /// <param name="logTypeParamName">LogType parameter name</param>
        /// <param name="messageParamName">Message parameter name</param>
        /// <param name="postedByParamName">Log source parameter name</param>
        /// <param name="logTypeParamSize">LogType parameter size</param>
        /// <param name="messageParamSize">Message parameter size</param>
        /// <param name="postedByParamSize">Log source parameter size</param>
        public abstract void ChangeConnectionInfo(
            string moduleName,
            string connectionString,
            string storedProcedure,
            string logTypeParamName,
            string messageParamName,
            string postedByParamName,
            int logTypeParamSize = 128,
            int messageParamSize = 4096,
            int postedByParamSize = 128);

        /// <summary>
        /// Immediately write out any queued messages (using the current thread)
        /// </summary>
        /// <remarks>
        /// <para>
        /// There is no need to call this method if you create an instance of this class
        /// </para>
        /// <para>
        /// On the other hand, if you only call static methods in this class, call this method
        /// before ending the program to assure that all messages have been logged
        /// </para>
        /// </remarks>
        public abstract void FlushPendingMessages();

        /// <summary>
        /// Construct the string MachineName:UserName
        /// </summary>
        protected static string GetDefaultModuleName()
        {
            return MachineName + ":" + UserName;
        }

        /// <summary>
        /// Convert log level to a string, optionally changing from all caps to initial caps
        /// </summary>
        /// <param name="logLevel">Log level</param>
        protected static string LogLevelToString(LogLevels logLevel)
        {
            var logLevelText = logLevel.ToString();

            if (!InitialCapsLogTypes)
                return logLevelText;

            return logLevelText.Substring(0, 1).ToUpper() + logLevelText.Substring(1).ToLower();
        }

        /// <summary>
        /// Disable database logging
        /// </summary>
        public abstract void RemoveConnectionInfo();

        /// <summary>
        /// Update the log threshold level
        /// </summary>
        /// <param name="logLevel">Log threshold level</param>
        private void SetLogLevel(LogLevels logLevel)
        {
            mLogThresholdLevel = logLevel;
            IsDebugEnabled = mLogThresholdLevel >= LogLevels.DEBUG;
            IsErrorEnabled = mLogThresholdLevel >= LogLevels.ERROR;
            IsFatalEnabled = mLogThresholdLevel >= LogLevels.FATAL;
            IsInfoEnabled = mLogThresholdLevel >= LogLevels.INFO;
            IsWarnEnabled = mLogThresholdLevel >= LogLevels.WARN;
        }

        /// <summary>
        /// Log a debug message
        /// (provided the log threshold is LogLevels.DEBUG; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Debug(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.DEBUG, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.DEBUG, message, ex);
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Log an error message
        /// (provided the log threshold is LogLevels.ERROR or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Error(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.ERROR, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.ERROR, message, ex);
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Log a fatal error message
        /// (provided the log threshold is LogLevels.FATAL or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Fatal(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.FATAL, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.FATAL, message, ex);
        }

        /// <summary>
        /// Log an informational message
        /// (provided the log threshold is LogLevels.INFO or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Info(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.INFO, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.INFO, message, ex);
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Log a warning message
        /// (provided the log threshold is LogLevels.WARN or higher; see this.LogLevel)
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception; can be null</param>
        public override void Warn(string message, Exception ex = null)
        {
            if (!AllowLog(LogLevels.WARN, mLogThresholdLevel))
                return;

            WriteLog(LogLevels.WARN, message, ex);
        }

        /// <summary>
        /// Log a message (regardless of the log threshold level)
        /// </summary>
        /// <param name="logLevel">Log level</param>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception</param>
        public void WriteLog(LogLevels logLevel, string message, Exception ex = null)
        {
            var logMessage = new LogMessage(logLevel, message, ex);
            WriteLog(logMessage);
        }

        /// <summary>
        /// Log a message (regardless of the log threshold level)
        /// </summary>
        /// <param name="logMessage">Message</param>
        public abstract void WriteLog(LogMessage logMessage);
    }
}
