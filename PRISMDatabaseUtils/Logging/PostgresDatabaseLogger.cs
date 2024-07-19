using System;
using System.Collections.Concurrent;
using System.Threading;
using PRISM;
using PRISM.Logging;

namespace PRISMDatabaseUtils.Logging;

/// <summary>
/// Logs messages to a PostgreSQL database by calling a procedure
/// Connect using PostgresDBTools (which uses Npgsql)
/// </summary>
public sealed class PostgresDatabaseLogger : DatabaseLogger
{
    private const string DEFAULT_PROCEDURE_NAME = "post_log_entry";

    private const string DEFAULT_PG_PARAM_NAME_LOG_TYPE = "_type";

    private const string DEFAULT_PG_PARAM_NAME_MESSAGE = "_message";

    private const string DEFAULT_PG_PARAM_NAME_POSTED_BY = "_postedBy";

    // Ignore Spelling: Npgsql

    private static readonly ConcurrentQueue<LogMessage> mMessageQueue = new();

    private static readonly object mMessageQueueLock = new();

    // ReSharper disable once UnusedMember.Local
    private static readonly Timer mQueueLogger = new(LogMessagesCallback, null, 500, LOG_INTERVAL_MILLISECONDS);

    /// <summary>
    /// Tracks the number of successive dequeue failures
    /// </summary>
    private static int mFailedDequeueEvents;

    /// <summary>
    /// Module name
    /// </summary>
    private static string mModuleName;

    /// <summary>
    /// Program name to pass to the PostedBy field when contacting the database
    /// </summary>
    /// <remarks>Will be auto-defined in LogQueuedMessages if blank</remarks>
    public static string ModuleName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(mModuleName))
            {
                mModuleName = GetDefaultModuleName();
            }
            return mModuleName;
        }
        set => mModuleName = value;
    }

    /// <summary>
    /// Constructor when the connection info is unknown
    /// </summary>
    /// <remarks>
    /// No database logging will occur until ChangeConnectionInfo is called (to define the connection string and procedure)
    /// </remarks>
    /// <param name="logLevel">Log threshold level</param>
    public PostgresDatabaseLogger(LogLevels logLevel = LogLevels.INFO) : this(string.Empty, string.Empty, logLevel)
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="moduleName">
    /// Program name to pass to the postedByParamName field when contacting the database
    /// (will be auto-defined later if blank)
    /// </param>
    /// <param name="connectionString">PostgreSQL style connection string</param>
    /// <param name="logLevel">Log threshold level</param>
    /// <param name="procedureName">Procedure used to store the log message</param>
    /// <param name="logTypeParamName">LogType parameter name (string representation of logLevel)</param>
    /// <param name="messageParamName">Message parameter name</param>
    /// <param name="postedByParamName">Log source parameter name</param>
    /// <param name="logTypeParamSize">Ignored since the procedure parameters are declared as text</param>
    /// <param name="messageParamSize">Ignored since the procedure parameters are declared as text</param>
    /// <param name="postedByParamSize">Ignored since the procedure parameters are declared as text</param>
    public PostgresDatabaseLogger(
        string moduleName,
        string connectionString,
        LogLevels logLevel = LogLevels.INFO,
        string procedureName = DEFAULT_PROCEDURE_NAME,
        string logTypeParamName = DEFAULT_PG_PARAM_NAME_LOG_TYPE,
        string messageParamName = DEFAULT_PG_PARAM_NAME_MESSAGE,
        string postedByParamName = DEFAULT_PG_PARAM_NAME_POSTED_BY,
        int logTypeParamSize = 128,
        int messageParamSize = 4096,
        int postedByParamSize = 128)
    {
        ChangeConnectionInfo(
            moduleName, connectionString, procedureName,
            logTypeParamName, messageParamName, postedByParamName,
            logTypeParamSize, messageParamSize, postedByParamSize);

        LogLevel = logLevel;
    }

    /// <summary>
    /// Update the database connection info
    /// </summary>
    /// <param name="moduleName">
    /// Program name to pass to the postedByParamName field when contacting the database
    /// (will be auto-defined later if blank)
    /// </param>
    /// <param name="connectionString">PostgreSQL style connection string</param>
    public override void ChangeConnectionInfo(
        string moduleName,
        string connectionString)
    {
        ChangeConnectionInfo(
            moduleName, connectionString,
            DEFAULT_PROCEDURE_NAME,
            DEFAULT_PG_PARAM_NAME_LOG_TYPE,
            DEFAULT_PG_PARAM_NAME_MESSAGE,
            DEFAULT_PG_PARAM_NAME_POSTED_BY);
    }

    /// <summary>
    /// Update the database connection info
    /// </summary>
    /// <param name="moduleName">
    /// Program name to pass to the postedByParamName field when contacting the database
    /// (will be auto-defined later if blank)
    /// </param>
    /// <param name="connectionString">PostgreSQL style connection string</param>
    /// <param name="procedureName">Procedure used to store the log message</param>
    /// <param name="logTypeParamName">LogType parameter name</param>
    /// <param name="messageParamName">Message parameter name</param>
    /// <param name="postedByParamName">Log source parameter name</param>
    /// <param name="logTypeParamSize">Ignored since the procedure parameters are declared as text</param>
    /// <param name="messageParamSize">Ignored since the procedure parameters are declared as text</param>
    /// <param name="postedByParamSize">Ignored since the procedure parameters are declared as text</param>
    public override void ChangeConnectionInfo(
        string moduleName,
        string connectionString,
        string procedureName,
        string logTypeParamName,
        string messageParamName,
        string postedByParamName,
        int logTypeParamSize = 128,
        int messageParamSize = 4096,
        int postedByParamSize = 128)
    {
        ModuleName = moduleName;

        ConnectionString = connectionString;

        LoggingProcedure.UpdateProcedureInfo(
            procedureName,
            logTypeParamName, messageParamName, postedByParamName,
            logTypeParamSize, messageParamSize, postedByParamSize);
    }

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
    public override void FlushPendingMessages()
    {
        // Maximum time, in seconds, to continue to call StartLogQueuedMessages while the message queue is not empty
        const int MAX_TIME_SECONDS = 5;

        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow.Subtract(startTime).TotalSeconds < MAX_TIME_SECONDS)
        {
            StartLogQueuedMessages();

            if (mMessageQueue.IsEmpty)
                break;

            AppUtils.SleepMilliseconds(10);
        }
    }

    /// <summary>
    /// Callback invoked by the mQueueLogger timer
    /// </summary>
    /// <param name="state">State</param>
    private static void LogMessagesCallback(object state)
    {
        ShowTrace("PostgresDatabaseLogger.mQueueLogger callback raised");
        StartLogQueuedMessages();
    }

    private static void LogQueuedMessages()
    {
        try
        {
            if (mMessageQueue.IsEmpty || !HasConnectionInfo)
                return;

            ShowTrace(string.Format("PostgresDatabaseLogger connecting to {0}", ConnectionString));
            var messagesWritten = 0;

            var dbTools = DbToolsFactory.GetDBTools(ConnectionString, debugMode: TraceMode);

            // var query = "call post_log_entry(_postedBy => @_postedBy, _type => @_type, _message => @_message)";

            var query = string.Format(
                "call {0}({1} => @{1}, {2} => @{2}, {3} => @{3})",
                LoggingProcedure.ProcedureName,
                LoggingProcedure.LogSourceParamName,
                LoggingProcedure.LogTypeParamName,
                LoggingProcedure.MessageParamName
                );

            while (!mMessageQueue.IsEmpty)
            {
                if (!mMessageQueue.TryDequeue(out var logMessage))
                {
                    mFailedDequeueEvents++;
                    LogDequeueError(mFailedDequeueEvents, mMessageQueue.Count);
                    return;
                }

                mFailedDequeueEvents = 0;

                if (logMessage.LogLevel is LogLevels.ERROR or LogLevels.FATAL)
                {
                    MostRecentErrorMessage = logMessage.Message;
                }

                // With SQL server, we can repeatedly call ExecuteNonQuery on a command
                // For example, see the while loop in method LogQueuedMessages in SQLServerDatabaseLogger

                // Due to the logic used by ExecuteSP in PostgresDBTools, if you call .ExecuteSP a second time,
                // an exception will be raised, with message "An open data reader exists for this command"

                // Thus, create a new instance of DbCommand for each call to the logging procedure

                var spCmd = dbTools.CreateCommand(query);

                dbTools.AddParameter(spCmd, LoggingProcedure.LogTypeParamName, SqlType.Text).Value = LogLevelToString(logMessage.LogLevel);
                dbTools.AddParameter(spCmd, LoggingProcedure.MessageParamName, SqlType.Text).Value = logMessage.Message;
                dbTools.AddParameter(spCmd, LoggingProcedure.LogSourceParamName, SqlType.Text).Value = ModuleName;

                var returnCode = dbTools.ExecuteSP(spCmd, 2);

                if (returnCode != 0)
                {
                    // The error should have already been reported by a call to OnErrorEvent
                    continue;
                }

                messagesWritten++;
            }

            ShowTrace(string.Format("PostgresDatabaseLogger connection closed; wrote {0} messages", messagesWritten));
        }
        catch (Exception ex)
        {
            ConsoleMsgUtils.ShowErrorCustom(
                "Error writing queued log messages to the database using PostgreSQL: " + ex.Message,
                ex, false, false);
        }
    }

    /// <summary>
    /// Disable database logging
    /// </summary>
    public override void RemoveConnectionInfo()
    {
        ChangeConnectionInfo(ModuleName, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// Check for queued messages
    /// If found, try to log them, wrapping then attempt with Monitor.TryEnter and Monitor.Exit
    /// </summary>
    private static void StartLogQueuedMessages()
    {
        if (mMessageQueue.IsEmpty)
            return;

        lock (mMessageQueueLock)
        {
            LogQueuedMessages();
        }
    }

    /// <summary>
    /// Log a message (regardless of this.LogLevel)
    /// </summary>
    /// <param name="logMessage">Log message</param>
    public override void WriteLog(LogMessage logMessage)
    {
        mMessageQueue.Enqueue(logMessage);

        if (EchoMessagesToFileLogger)
            FileLogger.WriteLog(logMessage);
    }

    /// <summary>
    /// Class is disposing; write out any queued messages
    /// </summary>
    ~PostgresDatabaseLogger()
    {
        ShowTrace("Disposing PostgresDatabaseLogger");
        StartLogQueuedMessages();
    }
}
