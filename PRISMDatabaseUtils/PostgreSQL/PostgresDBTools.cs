using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using PRISM;

namespace PRISMDatabaseUtils.PostgreSQL
{
    /// <summary>
    /// Tools to retrieve data from a database or call procedures
    /// </summary>
    internal class PostgresDBTools : DBToolsBase, IDBTools
    {
        // Ignore Spelling: backend, msg, Npgsql, PostgreSQL, sqlCmd, tmp, varchar

        #region "Member Variables"

        private string mConnStr;

        /// <summary>
        /// Timeout length, in seconds, when waiting for a query or procedure to finish running
        /// </summary>
        private int mTimeoutSeconds;

        #endregion

        #region "Properties"

        /// <summary>
        /// Database connection string
        /// </summary>
        public string ConnectStr
        {
            get => mConnStr;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new Exception("Connection string cannot be empty");
                }
                mConnStr = value;
                ParseConnectionString(value);
            }
        }

        /// <summary>
        /// Database server type
        /// </summary>
        public DbServerTypes DbServerType => DbServerTypes.PostgreSQL;

        /// <summary>
        /// Set to True to raise debug events
        /// </summary>
        public bool DebugMessagesEnabled { get; set; }

        /// <summary>
        /// Timeout length, in seconds, when waiting for a query to finish executing
        /// </summary>
        public int TimeoutSeconds
        {
            get => mTimeoutSeconds;
            set
            {
                if (value <= 0)
                    value = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC;

                if (value < 10)
                    value = 10;

                mTimeoutSeconds = value;
            }
        }

        /// <summary>
        /// The name of the server to which the connection string connects
        /// </summary>
        public string ServerName { get; private set; }

        /// <summary>
        /// The name of the database to which the connection string connects
        /// </summary>
        public string DatabaseName { get; private set; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="timeoutSeconds">Query timeout, in seconds</param>
        /// <param name="debugMode">When true, show queries and procedure calls using OnDebugEvent</param>
        public PostgresDBTools(
            string connectionString,
            int timeoutSeconds = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC,
            bool debugMode = false)
        {
            ConnectStr = connectionString;
            mTimeoutSeconds = timeoutSeconds;
            DebugMessagesEnabled = debugMode;
        }

        private void ParseConnectionString(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            ServerName = builder.Host ?? string.Empty;
            DatabaseName = builder.Database ?? string.Empty;
        }

        /// <summary>
        /// Get a mapping from column name to column index, based on column order
        /// </summary>
        /// <remarks>Use in conjunction with GetColumnValue, e.g. GetColumnValue(resultRow, columnMap, "ID")</remarks>
        /// <param name="columns"></param>
        /// <returns>Mapping from column name to column index</returns>
        // ReSharper disable once UnusedMember.Global
        public Dictionary<string, int> GetColumnMapping(IReadOnlyList<string> columns)
        {
            var columnMap = new Dictionary<string, int>();

            for (var i = 0; i < columns.Count; i++)
            {
                columnMap.Add(columns[i], i);
            }

            return columnMap;
        }

        /// <summary>
        /// Event handler for Notice event
        /// </summary>
        /// <remarks>Errors and warnings from PostgresSQL are reported here</remarks>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnNotice(object sender, NpgsqlNoticeEventArgs args)
        {
            var msg = new StringBuilder();
            var notice = args.Notice;

            if (notice.Routine != null &&
                notice.InvariantSeverity.Equals("NOTICE") &&
                notice.Routine.Equals("DropErrorMsgNonExistent", StringComparison.OrdinalIgnoreCase))
            {
                // Example message: "table \"tmp_mgr_params\" does not exist, skipping"
                // This is an informational message that can be ignored
                return;
            }

            msg.Append("Message: " + notice.MessageText);
            msg.Append(", Source: " + notice.Where);
            msg.Append(", Class: " + notice.Severity);
            msg.Append(", State: " + notice.SqlState);
            msg.Append(", LineNumber: " + notice.Line);
            msg.Append(", Procedure:" + notice.Routine);
            msg.Append(", Server: " + notice.File);

            if (notice.InvariantSeverity.Equals("NOTICE"))
            {
                OnDebugEvent(msg.ToString());
                return;
            }

            if (notice.InvariantSeverity.Equals("INFO"))
            {
                OnStatusEvent(msg.ToString());
                return;
            }

            if (notice.InvariantSeverity.Equals("WARNING"))
            {
                OnWarningEvent(msg.ToString());
                return;
            }

            OnErrorEvent(msg.ToString());
        }

        /// <summary>
        /// Run a query against a SQL database, return the scalar result
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResult">Result (single value) returned by the query</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryScalar(
            string sqlQuery,
            out object queryResult,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            if (timeoutSeconds <= 0)
            {
                timeoutSeconds = TimeoutSeconds;
            }

            var cmd = new NpgsqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryScalar(cmd, out queryResult, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the scalar result
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query to run</param>
        /// <param name="queryResult">Result (single value) returned by the query</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryScalar(
            DbCommand cmd,
            out object queryResult,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            if (string.IsNullOrWhiteSpace(callingFunction))
            {
                callingFunction = "UnknownCaller";
            }

            if (cmd is not NpgsqlCommand sqlCmd)
            {
                if (cmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(cmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {cmd.GetType().FullName}.", nameof(cmd));
            }

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            // Make sure we dispose of the command object; however, it must be done outside of the while loop (since we use the same command for retries)
            // Could use clones for each try, but that would cause problems with "Output" parameters
            using (sqlCmd)
            {
                while (true)
                {
                    try
                    {
                        using var dbConnection = new NpgsqlConnection(ConnectStr);

                        dbConnection.Notice += OnNotice;
                        dbConnection.Open();

                        if (DebugMessagesEnabled)
                        {
                            OnDebugEvent("GetQueryScalar: " + sqlCmd.CommandText);
                        }

                        // PostgresSQL requires a transaction when calling procedures that return result sets.
                        // Without the transaction, the cursor is closed before we can read any data.
                        using var transaction = dbConnection.BeginTransaction();

                        sqlCmd.Connection = dbConnection;

                        queryResult = sqlCmd.ExecuteScalar();

                        transaction.Commit();

                        return true;
                    }
                    catch (Exception ex)
                    {
                        retryCount--;

                        if (string.IsNullOrWhiteSpace(callingFunction))
                        {
                            callingFunction = "Unknown";
                        }

                        var errorMessage = string.Format(
                            "Exception querying database (called from {0}): {1}; " +
                            "ConnectionString: {2}, RetryCount = {3}, Query {4}",
                            callingFunction, ex.Message, ConnectStr, retryCount, sqlCmd);

                        OnErrorEvent(errorMessage);

                        if (IsFatalException(ex))
                        {
                            // No point in retrying the query; it will fail again
                            queryResult = null;
                            return false;
                        }

                        if (retryCount <= 0)
                            break;

                        // Delay for 5 seconds before trying again
                        ProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);
                    }
                }
            }

            queryResult = null;
            return false;
        }

        /// <summary>
        /// Run a query against a SQL Server database, return the results as a list of strings
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="results">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryResults(
            string sqlQuery,
            out List<List<string>> results,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            if (timeoutSeconds <= 0)
            {
                timeoutSeconds = TimeoutSeconds;
            }

            var cmd = new NpgsqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryResults(cmd, out results, retryCount, maxRowsToReturn, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL Server database, return the results as a list of strings
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query to run</param>
        /// <param name="results">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryResults(
            DbCommand cmd,
            out List<List<string>> results,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            // Declare a local variable to append the results to
            // This is required because we cannot use an out parameter in a lambda expression (the Action => block below)
            var dbResults = new List<List<string>>();
            results = dbResults;

            var readMethod = new Action<NpgsqlCommand>(x =>
            {
                using var reader = x.ExecuteReader();

                while (reader.Read())
                {
                    var currentRow = new List<string>();

                    for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                    {
                        var value = reader.GetValue(columnIndex);

                        if (DBNull.Value.Equals(value))
                        {
                            currentRow.Add(string.Empty);
                        }
                        else
                        {
                            currentRow.Add(value.ToString());
                        }
                    }

                    dbResults.Add(currentRow);

                    if (maxRowsToReturn > 0 && dbResults.Count >= maxRowsToReturn)
                    {
                        break;
                    }
                }
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataTable object
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResults">Results (as a DataTable)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryResultsDataTable(
            string sqlQuery,
            out DataTable queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            if (timeoutSeconds <= 0)
            {
                timeoutSeconds = TimeoutSeconds;
            }

            var cmd = new NpgsqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryResultsDataTable(cmd, out queryResults, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataTable object
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query to run</param>
        /// <param name="queryResults">Results (as a DataTable)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryResultsDataTable(
            DbCommand cmd,
            out DataTable queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            var results = new DataTable();
            queryResults = results;
            var readMethod = new Action<NpgsqlCommand>(x =>
            {
                using var da = new NpgsqlDataAdapter(x);
                da.Fill(results);
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataSet object
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResults">Results (as a DataSet)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryResultsDataSet(
            string sqlQuery,
            out DataSet queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            if (timeoutSeconds <= 0)
            {
                timeoutSeconds = TimeoutSeconds;
            }

            var cmd = new NpgsqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryResultsDataSet(cmd, out queryResults, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataSet object
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query to run</param>
        /// <param name="queryResults">Results (as a DataSet)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryResultsDataSet(
            DbCommand cmd,
            out DataSet queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            var results = new DataSet();
            queryResults = results;
            var readMethod = new Action<NpgsqlCommand>(x =>
            {
                using var da = new NpgsqlDataAdapter(x);
                da.Fill(results);
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results via <paramref name="readMethod"/>
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query to run</param>
        /// <param name="readMethod">Method to read and return data from the command; command will be ready to run, executing and processing of returned data is left to the this Action</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        private bool GetQueryResults(
            DbCommand cmd,
            Action<NpgsqlCommand> readMethod,
            int retryCount,
            int retryDelaySeconds,
            string callingFunction)
        {
            if (string.IsNullOrWhiteSpace(callingFunction))
            {
                callingFunction = "UnknownCaller";
            }

            if (cmd is not NpgsqlCommand sqlCmd)
            {
                if (cmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(cmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {cmd.GetType().FullName}.", nameof(cmd));
            }

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            // Make sure we dispose of the command object; however, it must be done outside of the while loop (since we use the same command for retries)
            // Could use clones for each try, but that would cause problems with "Output" parameters
            using (sqlCmd)
            {
                while (true)
                {
                    try
                    {
                        using var dbConnection = new NpgsqlConnection(ConnectStr);

                        dbConnection.Notice += OnNotice;
                        dbConnection.Open();

                        if (DebugMessagesEnabled)
                        {
                            OnDebugEvent("GetQueryResults: " + cmd.CommandText);
                        }

                        // PostgresSQL requires a transaction when calling procedures that return result sets.
                        // Without the transaction, the cursor is closed before we can read any data.
                        using var transaction = dbConnection.BeginTransaction();

                        sqlCmd.Connection = dbConnection;

                        readMethod(sqlCmd);

                        transaction.Commit();

                        return true;
                    }
                    catch (Exception ex)
                    {
                        retryCount--;

                        if (string.IsNullOrWhiteSpace(callingFunction))
                        {
                            callingFunction = "Unknown";
                        }

                        var errorMessage = string.Format(
                            "Exception querying database (called from {0}): {1}; " +
                            "ConnectionString: {2}, RetryCount = {3}, Query {4}",
                            callingFunction, ex.Message, ConnectStr, retryCount, sqlCmd);

                        OnErrorEvent(errorMessage);

                        if (IsFatalException(ex))
                        {
                            // No point in retrying the query; it will fail again
                            return false;
                        }

                        if (retryCount <= 0)
                            break;

                        // Delay for 5 seconds before trying again
                        ProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Convert a "stored procedure" command to work properly with Npgsql
        /// Npgsql treats <see cref="CommandType.StoredProcedure"/> as a function, calling it with "SELECT * FROM CommandText()")
        /// We instead want to handle "stored procedure" command as CALL procedure_name()
        /// </summary>
        private static void ConvertStoredProcedureCommand(NpgsqlCommand sqlCmd)
        {
            if (sqlCmd.CommandType != CommandType.StoredProcedure)
            {
                return;
            }

            // By default, Npgsql commands of type CommandType.StoredProcedure are treated as functions,
            // and thus when querying the database, Npgsql sends a command of the form
            // SELECT * FROM my_function_name(param1, param2, param3)

            // PostgreSQL 11 introduced procedures, which are executed via CALL
            // Auto-update sqlCmd to be CommandType.Text with SQL in the form
            // CALL my_procedure_name(param1 => @param1, param2 => @param2, param3 => @param3)
            //
            // When the command is run, Npgsql will replace @param1, @param2, etc. with the values for each parameter

            sqlCmd.CommandType = CommandType.Text;
            var procedureName = sqlCmd.CommandText;
            var procArgs = string.Join(", ", sqlCmd.Parameters.Select(x => $"{x.ParameterName} => @{x.ParameterName}"));
            sqlCmd.CommandText = $"CALL {procedureName}({procArgs})";
        }

        /// <summary>
        /// Method for calling a procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing procedure params</param>
        /// <param name="readMethod">Method to read and return data from the command; command will be ready to run, executing and processing of returned data is left to the this Action</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        private int ExecuteSPData(
            DbCommand spCmd,
            Action<NpgsqlCommand> readMethod,
            int retryCount,
            int retryDelaySeconds)
        {
            if (spCmd is not NpgsqlCommand sqlCmd)
            {
                if (spCmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(spCmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {spCmd.GetType().FullName}.", nameof(spCmd));
            }

            UpdateSqlServerParameterNames(sqlCmd);

            ConvertStoredProcedureCommand(sqlCmd);

            // If this value is in error msg, exception occurred before resultCode was set
            var resultCode = -9999;

            var startTime = DateTime.UtcNow;

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            var deadlockOccurred = false;

            // Make sure we dispose of the command object; however, it must be done outside of the while loop (since we use the same command for retries)
            // Could use clones for each try, but that would cause problems with "Output" parameters
            using (sqlCmd)
            {
                // Multiple retry loop for handling SP execution failures
                string errorMessage;
                while (retryCount > 0)
                {
                    var success = false;
                    try
                    {
                        using (var dbConnection = new NpgsqlConnection(mConnStr))
                        {
                            dbConnection.Notice += OnNotice;
                            dbConnection.Open();

                            if (DebugMessagesEnabled)
                            {
                                OnDebugEvent("ExecuteSPData: " + sqlCmd.CommandText);
                            }

                            // PostgresSQL requires a transaction when calling procedures that return result sets.
                            // Without the transaction, the cursor is closed before we can read any data.
                            using var transaction = dbConnection.BeginTransaction();

                            sqlCmd.Connection = dbConnection;

                            // Multiple cursors not supported
                            var cursorName = string.Empty;
                            using (var reader = sqlCmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    // Really only expecting a single row; extract all ref cursors
                                    foreach (var column in reader.GetColumnSchema().Where(x => x.NpgsqlDbType == NpgsqlDbType.Refcursor))
                                    {
                                        var name = reader[column.ColumnName].CastDBVal<string>();
                                        if (string.IsNullOrWhiteSpace(cursorName))
                                        {
                                            cursorName = name;
                                        }
                                        else
                                        {
                                            OnWarningEvent($"Reading of multiple RefCursors not supported; ignoring RefCursor {name}.");
                                            // Log error: Multiple cursors not supported
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(cursorName))
                            {
                                // We got a cursor; read it and populate the output object
                                using var cmd = new NpgsqlCommand($"FETCH ALL FROM {cursorName}", dbConnection);

                                readMethod(cmd);
                            }

                            resultCode = GetReturnCode(sqlCmd.Parameters);

                            transaction.Commit();
                        }

                        success = true;
                    }
                    catch (Exception ex)
                    {
                        retryCount--;
                        errorMessage = "Exception filling data adapter for " + sqlCmd.CommandText + ": " + ex.Message;
                        errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                        errorMessage += "; " + StackTraceFormatter.GetExceptionStackTrace(ex);

                        OnErrorEvent(errorMessage);

                        if (IsFatalException(ex))
                        {
                            retryCount = 0;
                        }
                        else if (ex.Message.Contains("was deadlocked"))
                        {
                            deadlockOccurred = true;
                        }
                    }
                    finally
                    {
                        if (DebugMessagesEnabled)
                        {
                            var debugMessage = string.Format(
                                "SP execution time: {0:##0.000} seconds for SP {1}",
                                DateTime.UtcNow.Subtract(startTime).TotalSeconds,
                                spCmd.CommandText);

                            OnDebugEvent(debugMessage);
                        }
                    }

                    if (success)
                        break;

                    if (retryCount > 0)
                    {
                        ProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);
                    }
                }

                if (retryCount < 1)
                {
                    // Too many retries, log and return error
                    errorMessage = "Excessive retries";
                    if (deadlockOccurred)
                    {
                        errorMessage += " (including deadlock)";
                    }
                    errorMessage += " executing SP " + sqlCmd.CommandText;

                    OnErrorEvent(errorMessage);

                    if (deadlockOccurred)
                    {
                        return DbUtilsConstants.RET_VAL_DEADLOCK;
                    }

                    return DbUtilsConstants.RET_VAL_EXCESSIVE_RETRIES;
                }
            }

            return resultCode;
        }

        /// <summary>
        /// Method for calling a procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing procedure params</param>
        /// <param name="results">If SP successful, contains Results (list of list of strings)</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the procedure</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 for no limit</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        public int ExecuteSPData(
            DbCommand spCmd,
            out List<List<string>> results,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5)
        {
            // Declare a local variable to append the results to
            // This is required because we cannot use an out parameter in a lambda expression (the Action => block below)
            var dbResults = new List<List<string>>();
            results = dbResults;

            var readMethod = new Action<NpgsqlCommand>(x =>
            {
                using var reader = x.ExecuteReader();

                while (reader.Read())
                {
                    var currentRow = new List<string>();

                    for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                    {
                        var value = reader.GetValue(columnIndex);

                        if (DBNull.Value.Equals(value))
                        {
                            currentRow.Add(string.Empty);
                        }
                        else
                        {
                            currentRow.Add(value.ToString());
                        }
                    }

                    dbResults.Add(currentRow);

                    if (maxRowsToReturn > 0 && dbResults.Count >= maxRowsToReturn)
                    {
                        break;
                    }
                }
            });

            return ExecuteSPData(spCmd, readMethod, retryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for calling a procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing procedure params</param>
        /// <param name="results">If SP successful, contains results as a DataTable</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        public int ExecuteSPDataTable(
            DbCommand spCmd,
            out DataTable results,
            int retryCount = 3,
            int retryDelaySeconds = 5)
        {
            var queryResults = new DataTable();
            results = queryResults;
            var readMethod = new Action<NpgsqlCommand>(x =>
            {
                using var da = new NpgsqlDataAdapter(x);
                da.Fill(queryResults);
            });

            return ExecuteSPData(spCmd, readMethod, retryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for calling a procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing procedure params</param>
        /// <param name="results">If SP successful, contains results as a DataSet</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        public int ExecuteSPDataSet(
            DbCommand spCmd,
            out DataSet results,
            int retryCount = 3,
            int retryDelaySeconds = 5)
        {
            var queryResults = new DataSet();
            results = queryResults;
            var readMethod = new Action<NpgsqlCommand>(x =>
            {
                using var da = new NpgsqlDataAdapter(x);
                da.Fill(queryResults);
            });

            return ExecuteSPData(spCmd, readMethod, retryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for calling a procedure, assuming no data table is returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        public int ExecuteSP(DbCommand spCmd, int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT, int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC)
        {
            return ExecuteSP(spCmd, out _, maxRetryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for calling a procedure when a data table is not returned
        /// </summary>
        /// <remarks>No logging is performed by this procedure</remarks>
        /// <param name="spCmd">SQL command object containing procedure params</param>
        /// <param name="errorMessage">Error message (output)</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        public int ExecuteSP(DbCommand spCmd, out string errorMessage, int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT, int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC)
        {
            if (spCmd is not NpgsqlCommand sqlCmd)
            {
                if (spCmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(spCmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {spCmd.GetType().FullName}.", nameof(spCmd));
            }

            UpdateSqlServerParameterNames(sqlCmd);

            UpdateSqlServerParameterValues(sqlCmd);

            ConvertStoredProcedureCommand(sqlCmd);

            // If this value is in error msg, exception occurred before resultCode was set
            var resultCode = -9999;

            var startTime = DateTime.UtcNow;
            var retryCount = maxRetryCount;
            var deadlockOccurred = false;

            errorMessage = string.Empty;

            if (retryCount < 1)
            {
                retryCount = 1;
            }

            if (retryDelaySeconds < 1)
            {
                retryDelaySeconds = 1;
            }

            // Make sure we dispose of the command object; however, it must be done outside of the while loop (since we use the same command for retries)
            // Could use clones for each try, but that would cause problems with "Output" parameters
            using (sqlCmd)
            {
                // Multiple retry loop for handling SP execution failures
                while (retryCount > 0)
                {
                    deadlockOccurred = false;
                    try
                    {
                        using (var dbConnection = new NpgsqlConnection(mConnStr))
                        {
                            dbConnection.Notice += OnNotice;
                            dbConnection.Open();

                            sqlCmd.Connection = dbConnection;

                            if (DebugMessagesEnabled)
                            {
                                OnDebugEvent("ExecuteSP: " + spCmd.CommandText);
                            }

                            startTime = DateTime.UtcNow;
                            sqlCmd.ExecuteNonQuery();

                            resultCode = GetReturnCode(sqlCmd.Parameters);
                        }

                        errorMessage = string.Empty;
                        break;
                    }
                    catch (Exception ex)
                    {
                        retryCount--;
                        errorMessage = "Exception calling procedure " + sqlCmd.CommandText + ": " + ex.Message;
                        errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                        errorMessage += "; " + StackTraceFormatter.GetExceptionStackTrace(ex);

                        OnErrorEvent(errorMessage);

                        if (IsFatalException(ex))
                        {
                            break;
                        }
                        else if (ex.Message.Contains("was deadlocked"))
                        {
                            deadlockOccurred = true;
                        }
                    }
                    finally
                    {
                        if (DebugMessagesEnabled)
                        {
                            var debugMessage = string.Format(
                                "SP execution time: {0:##0.000} seconds for SP {1}",
                                DateTime.UtcNow.Subtract(startTime).TotalSeconds,
                                spCmd.CommandText);

                            OnDebugEvent(debugMessage);
                        }
                    }

                    if (retryCount > 0)
                    {
                        ProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);
                    }
                }

                if (retryCount < 1)
                {
                    // Too many retries, log and return error
                    errorMessage = "Excessive retries";
                    if (deadlockOccurred)
                    {
                        errorMessage += " (including deadlock)";
                    }
                    errorMessage += " executing SP " + sqlCmd.CommandText;

                    OnErrorEvent(errorMessage);

                    if (deadlockOccurred)
                    {
                        return DbUtilsConstants.RET_VAL_DEADLOCK;
                    }

                    return DbUtilsConstants.RET_VAL_EXCESSIVE_RETRIES;
                }
            }

            return resultCode;
        }

        /// <inheritdoc />
        public DbCommand CreateCommand(string cmdText, CommandType cmdType = CommandType.Text)
        {
            return new NpgsqlCommand(cmdText) { CommandType = cmdType, CommandTimeout = TimeoutSeconds };
        }

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <remarks>
        /// If dbType is Text or VarChar, sets the parameter's value to string.Empty
        /// </remarks>
        /// <param name="command"></param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        public override DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            ParameterDirection direction = ParameterDirection.Input)
        {
            return AddParameter(command, name, dbType, 0, direction);
        }

        /// <inheritdoc />
        public DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            int size,
            ParameterDirection direction = ParameterDirection.Input)
        {
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (dbType)
            {
                case SqlType.Text:
                case SqlType.VarChar when size == 0:
                    return AddParameter(command, name, SqlType.Text, 0, string.Empty, direction);

                case SqlType.VarChar:
                    return AddParameter(command, name, dbType, size, string.Empty, direction);

                default:
                    return AddParameter(command, name, dbType, size, null, direction);
            }
        }

        /// <inheritdoc />
        public DbParameter AddParameter(
            DbCommand command,
            string name,
            string dataTypeName,
            int size,
            ParameterDirection direction = ParameterDirection.Input)
        {
            var parameter = AddParameterByDataTypeName(command, name, dataTypeName, size, direction);
            return parameter;
        }

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <param name="command"></param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="value"></param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        public override DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            int size,
            object value,
            ParameterDirection direction = ParameterDirection.Input)
        {
            if (command is not NpgsqlCommand npgCmd)
            {
                throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {command.GetType().FullName}.", nameof(command));
            }

            // Optional: force the parameter name to be lowercase
            // See https://stackoverflow.com/a/45080006/1179467

            // string updatedName;
            // if (name.StartsWith("\""))
            //     // Surrounded by double quotes; leave as-is
            //     updatedName = name;
            // else
            //     updatedName = name.ToLower();

            var param = new NpgsqlParameter(name, ConvertSqlType(dbType), size)
            {
                Direction = direction,
                Value = value,
            };

            if (dbType == SqlType.Decimal)
                SetDefaultPrecision(param);

            npgCmd.Parameters.Add(param);

            return param;
        }

        /// <inheritdoc />
        public DbParameter AddTypedParameter<T>(
            DbCommand command,
            string name,
            SqlType dbType,
            int size = 0,
            T value = default,
            ParameterDirection direction = ParameterDirection.Input)
        {
            if (command is not NpgsqlCommand npgCmd)
            {
                throw new ArgumentException($"This method requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {command.GetType().FullName}.", nameof(command));
            }

            var param = new NpgsqlParameter<T>(name, value)
            {
                NpgsqlDbType = ConvertSqlType(dbType),
                Size = size,
                Direction = direction,
                Value = value,
            };

            if (dbType == SqlType.Decimal)
                SetDefaultPrecision(param);

            npgCmd.Parameters.Add(param);

            return param;
        }

        /// <summary>
        /// Convert from enum SqlType to NpgsqlDbTypes.NpgsqlDbType
        /// </summary>
        /// <param name="sqlType"></param>
        private static NpgsqlDbType ConvertSqlType(SqlType sqlType)
        {
            return sqlType switch
            {
                SqlType.Bit => NpgsqlDbType.Bit,
                SqlType.Boolean => NpgsqlDbType.Boolean,
                SqlType.TinyInt or SqlType.SmallInt => NpgsqlDbType.Smallint,
                SqlType.Int => NpgsqlDbType.Integer,
                SqlType.BigInt => NpgsqlDbType.Bigint,
                SqlType.Real => NpgsqlDbType.Real,
                SqlType.Float => NpgsqlDbType.Double,
                SqlType.Decimal => NpgsqlDbType.Numeric,        // Includes Numeric
                SqlType.Money => NpgsqlDbType.Money,
                SqlType.Char => NpgsqlDbType.Char,
                SqlType.VarChar => NpgsqlDbType.Varchar,
                SqlType.Text => NpgsqlDbType.Text,
                SqlType.Citext => NpgsqlDbType.Citext,
                SqlType.Name => NpgsqlDbType.Name,
                SqlType.Date => NpgsqlDbType.Date,
                SqlType.Time => NpgsqlDbType.Time,
                SqlType.Timestamp => NpgsqlDbType.Timestamp,    // Includes DateTime;
                SqlType.TimestampTz => NpgsqlDbType.TimestampTz,
                SqlType.UUID => NpgsqlDbType.Uuid,
                SqlType.XML => NpgsqlDbType.Xml,
                SqlType.Interval => NpgsqlDbType.Interval,
                SqlType.JSON => NpgsqlDbType.Json,
                _ => throw new ArgumentOutOfRangeException(nameof(sqlType), sqlType, $"Conversion for SqlType {sqlType} is not defined"),
            };
        }

        /// <summary>
        /// Examine the exception message to determine whether it is safe to rerun a failed query or failed procedure execution
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>True if the same error will happen again, so a retry is pointless</returns>
        protected static bool IsFatalException(Exception ex)
        {
            return
                ex.Message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("open data reader exists for this command", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("permission denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ex.Message.IndexOf("No password has been provided but the backend requires one", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Look for parameter names that start with @
        /// Auto-change the @ to _
        /// </summary>
        /// <param name="spCmd"></param>
        private static void UpdateSqlServerParameterNames(NpgsqlCommand spCmd)
        {
            foreach (NpgsqlParameter parameter in spCmd.Parameters)
            {
                if (parameter.ParameterName.Equals("@Return", StringComparison.OrdinalIgnoreCase) &&
                    parameter.Direction == ParameterDirection.ReturnValue)
                {
                    // Auto-change @Return parameters of type ReturnValue to _returnCode of type text
                    parameter.ParameterName = "_returnCode";
                    parameter.DbType = DbType.String;
                    parameter.Direction = ParameterDirection.InputOutput;
                    parameter.Value = string.Empty;
                    continue;
                }

                if (parameter.ParameterName.Length > 1 && parameter.ParameterName.StartsWith("@"))
                {
                    parameter.ParameterName = "_" + parameter.ParameterName.Substring(1);
                }
            }
        }

        /// <summary>
        /// Assure that parameter values are integers, not enums
        /// </summary>
        /// <param name="spCmd"></param>
        private static void UpdateSqlServerParameterValues(NpgsqlCommand spCmd)
        {
            // When sending an enum to a PostgreSQL function or procedure, if you don't cast to an integer, you get this error:
            // Can't write CLR type Namespace.ClassName+EnumName with handler type Int32Handler
            // The following checks for this
            foreach (NpgsqlParameter parameter in spCmd.Parameters)
            {
                if (parameter.Value != null && parameter.Value.GetType().IsEnum)
                {
                    parameter.Value = (int)parameter.Value;
                }
            }
        }
    }
}
