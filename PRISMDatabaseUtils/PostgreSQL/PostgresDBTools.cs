using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
        // Ignore Spelling: backend, enums, msg, Npgsql, Postgre, Postgres, PostgreSQL, refcursor, SQL, sqlCmd, tmp, username, Utils, varchar

        private string mConnStr;

        private static readonly Regex mProcedureAccessDeniedMatcher = new("User.+cannot use procedure", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Timeout length, in seconds, when waiting for a query or procedure to finish running
        /// </summary>
        private int mTimeoutSeconds;

        /// <summary>
        /// When true, for SQL queries, if any of the column names after the SELECT keyword has capital letters,
        /// the column names in the result table (or columnName list) will be auto-capitalized
        /// </summary>
        /// <remarks>
        /// <para>Defaults to true</para>
        /// <para>Only matches column names that have letters, numbers, and underscores</para>
        /// <para>Ignores column names that are quoted with double quotes</para>
        /// <para>If a column name occurs more than once, it will not be included in the dictionary</para>
        /// </remarks>
        public bool CapitalizeColumnNamesInResults { get; set; } = true;

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
        /// <param name="columns">List of column names</param>
        /// <returns>Mapping from column name to column index</returns>
        // ReSharper disable once UnusedMember.Global
        public static Dictionary<string, int> GetColumnMapping(IReadOnlyList<string> columns)
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
        /// <param name="sender">Sender</param>
        /// <param name="args">Arguments</param>
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
        /// Test connecting to the database
        /// </summary>
        /// <param name="serverVersion">Version string returned by the server connection</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the connection</param>
        /// <returns>True if success, false if unable to connect</returns>
        public override bool TestDatabaseConnection(out string serverVersion, int retryCount = 3, int retryDelaySeconds = 5)
        {
            serverVersion = string.Empty;

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            while (true)
            {
                try
                {
                    using var dbConnection = new NpgsqlConnection(ConnectStr);

                    dbConnection.Notice += OnNotice;
                    dbConnection.Open();

                    serverVersion = dbConnection.ServerVersion;
                    return true;
                }
                catch (Exception ex)
                {
                    retryCount--;

                    var errorMessage = string.Format(
                        "Exception testing connection to database: {0}; " +
                        "ConnectionString: {1}, RetryCount = {2}",
                        ex.Message, MaskConnectionStringPassword(ConnectStr), retryCount);

                    OnErrorEvent(errorMessage);

                    if (IsFatalException(ex, out _))
                    {
                        // No point in retrying the query; it will fail again
                        return false;
                    }

                    if (retryCount <= 0)
                        break;

                    // Delay for 5 seconds before trying again
                    AppUtils.SleepMilliseconds(retryDelaySeconds * 1000);
                }
            }

            return false;
        }

        /// <summary>
        /// Run a query against a PostgreSQL database, return the scalar result
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResult">Result (single value) returned by the query</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
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
        /// Run a query against a PostgreSQL database, return the scalar result
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="queryResult">Result (single value) returned by the query</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
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
                    throw new ArgumentException($"Method GetQueryScalar requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(cmd));
                }

                throw new ArgumentException($"Method GetQueryScalar requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {cmd.GetType().FullName}.", nameof(cmd));
            }

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            // ReSharper disable once GrammarMistakeInComment

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

                        var fatalException = IsFatalException(ex, out var permissionDenied);

                        // Exception querying database
                        // or
                        // Permission denied querying database

                        var errorMessage = string.Format(
                            "{0} querying database (called from {1}): {2}; ConnectionString: {3}, RetryCount = {4}, Query {5}",
                            permissionDenied ? "Permission denied" : "Exception",
                            callingFunction, ex.Message, MaskConnectionStringPassword(ConnectStr), retryCount, sqlCmd);

                        OnErrorEvent(errorMessage);

                        if (fatalException)
                        {
                            // No point in retrying the query; it will fail again
                            queryResult = null;
                            return false;
                        }

                        if (retryCount <= 0)
                            break;

                        // Delay for 5 seconds before trying again
                        AppUtils.SleepMilliseconds(retryDelaySeconds * 1000);
                    }
                }
            }

            queryResult = null;
            return false;
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Run a query against a SQL Server database, return the results as a list of strings (does not include column names)
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>Null values are converted to empty strings</para>
        /// <para>Numbers are converted to their string equivalent</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="results">Results (list of, list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
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

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Run a query against a PostgreSQL database, return the results as a list of strings (does not include column names)
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>Null values are converted to empty strings</para>
        /// <para>Numbers are converted to their string equivalent</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="results">Results (list of, list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
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
            return GetQueryResults(cmd, out results, out _, retryCount, maxRowsToReturn, retryDelaySeconds, callingFunction);
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Run a query against a SQL Server database, return the results as a list of strings
        /// Also returns the column names in a separate list
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>Null values are converted to empty strings</para>
        /// <para>Numbers are converted to their string equivalent</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// <para>
        /// If the query has specific column names (instead of just *),
        /// and if any of those names have capital letters (and no spaces),
        /// the names will be capitalized by this method (where possible)
        /// </para>
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="results">Results (list of, list of strings)</param>
        /// <param name="columnNames">Column names (as returned by the database)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        public bool GetQueryResults(
            DbCommand cmd,
            out List<List<string>> results,
            out List<string> columnNames,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            // Declare local variables to append the query results and column names to
            // This is required because we cannot use an out parameter in a lambda expression (the Action => block below)
            var dbResults = new List<List<string>>();
            results = dbResults;

            var dbColumns = new List<string>();
            columnNames = dbColumns;

            var readMethod = new Action<NpgsqlCommand>(x =>
            {
                using var reader = x.ExecuteReader();

                while (reader.Read())
                {
                    var currentRow = new List<string>();

                    if (dbColumns.Count == 0)
                    {
                        // Store the column names
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            dbColumns.Add(reader.GetName(i));
                        }
                    }

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

                CapitalizeColumnNames(cmd, dbColumns);
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        // ReSharper disable once UnusedMember.Global

        /// <summary>
        /// Run a query against a PostgreSQL database, return the results as a DataTable object
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResults">Results (as a DataTable)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
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
        /// Run a query against a PostgreSQL database, return the results as a DataTable object
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="queryResults">Results (as a DataTable)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
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

                // Capitalize column names in the data table
                CapitalizeColumnNames(cmd, results);
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a PostgreSQL database, return the results as a DataSet object
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResults">Results (as a DataSet)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
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
        /// Run a query against a PostgreSQL database, return the results as a DataSet object
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="queryResults">Results (as a DataSet)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
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

                if (results.Tables.Count == 0)
                    return;

                // Capitalize column names in the first data table in the query results
                CapitalizeColumnNames(cmd, results.Tables[0]);
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a PostgreSQL database, return the results via <paramref name="readMethod"/>
        /// </summary>
        /// <remarks>
        /// <para>Uses the connection string passed to the constructor of this class</para>
        /// <para>By default, retries the query up to 3 times</para>
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="readMethod">Method to read and return data from the command; command will be ready to run, executing and processing of returned data is left to this Action</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
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
                    throw new ArgumentException($"Method GetQueryResults requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(cmd));
                }

                throw new ArgumentException($"Method GetQueryResults requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {cmd.GetType().FullName}.", nameof(cmd));
            }

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            // ReSharper disable once GrammarMistakeInComment

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

                        var fatalException = IsFatalException(ex, out var permissionDenied);

                        // Exception querying database
                        // or
                        // Permission denied querying database

                        var errorMessage = string.Format(
                            "{0} querying database (called from {1}): {2}; ConnectionString: {3}, RetryCount = {4}, Query {5}",
                            permissionDenied ? "Permission denied" : "Exception",
                            callingFunction, ex.Message, MaskConnectionStringPassword(ConnectStr), retryCount, sqlCmd);

                        OnErrorEvent(errorMessage);

                        if (fatalException)
                        {
                            // No point in retrying the query; it will fail again
                            return false;
                        }

                        if (retryCount <= 0)
                            break;

                        // Delay for 5 seconds before trying again
                        AppUtils.SleepMilliseconds(retryDelaySeconds * 1000);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as an IEnumerable of objects
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the connection (but not the query) up to 3 times
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="rowObjectCreator">method to create an object from a row in a <see cref="DbDataReader"/></param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>Data; empty if no data or error</returns>
        public IEnumerable<T> GetQueryResultsEnumerable<T>(
            string sqlQuery,
            Func<DbDataReader, T> rowObjectCreator,
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
            return GetQueryResultsEnumerable(cmd, rowObjectCreator, retryCount, maxRowsToReturn, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as an IEnumerable of objects
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the connection (but not the query) up to 3 times
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="rowObjectCreator">method to create an object from a row in a <see cref="DbDataReader"/></param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>Data; empty if no data or error</returns>
        public IEnumerable<T> GetQueryResultsEnumerable<T>(
            DbCommand cmd,
            Func<DbDataReader, T> rowObjectCreator,
            int retryCount = 3,
            int maxRowsToReturn = 0,
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
                    throw new ArgumentException($"Method GetQueryResultsEnumerable requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(cmd));
                }

                throw new ArgumentException($"Method GetQueryResultsEnumerable requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {cmd.GetType().FullName}.", nameof(cmd));
            }

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            // ReSharper disable once GrammarMistakeInComment

            // Make sure we dispose of the command object; however, it must be done outside of the while loop (since we use the same command for retries)
            // Could use clones for each try, but that would cause problems with "Output" parameters
            using (sqlCmd)
            {
                while (true)
                {
                    using var dbConnection = new NpgsqlConnection(ConnectStr);

                    dbConnection.Notice += OnNotice;

                    NpgsqlDataReader reader;

                    try
                    {
                        // Open connection
                        dbConnection.Open();

                        if (DebugMessagesEnabled)
                        {
                            OnDebugEvent("GetQueryResults: " + cmd.CommandText);
                        }

                        sqlCmd.Connection = dbConnection;

                        // initialize the reader
                        reader = sqlCmd.ExecuteReader();
                    }
                    catch (Exception ex)
                    {
                        retryCount--;

                        if (string.IsNullOrWhiteSpace(callingFunction))
                        {
                            callingFunction = "Unknown";
                        }

                        var fatalException = IsFatalException(ex, out var permissionDenied);

                        // Exception querying database
                        // or
                        // Permission denied querying database

                        var errorMessage = string.Format(
                            "{0} querying database (called from {1}): {2}; ConnectionString: {3}, RetryCount = {4}, Query {5}",
                            permissionDenied ? "Permission denied" : "Exception",
                            callingFunction, ex.Message, MaskConnectionStringPassword(ConnectStr), retryCount, sqlCmd);

                        OnErrorEvent(errorMessage);

                        if (fatalException)
                        {
                            // No point in retrying the query; it will fail again
                            yield break;
                        }

                        if (retryCount <= 0)
                            break;

                        // Delay for 5 seconds before trying again
                        AppUtils.SleepMilliseconds(retryDelaySeconds * 1000);
                        continue;
                    }

                    var rowCount = 0;

                    if (maxRowsToReturn == 0)
                    {
                        maxRowsToReturn = int.MaxValue;
                    }

                    // ReSharper disable GrammarMistakeInComment

                    // Cannot use 'yield return' inside of a try-catch block
                    // Instead, initialize the reader there to limit potential exceptions outside of try-catch

                    // ReSharper restore GrammarMistakeInComment

                    using (reader)
                    {
                        while (reader.Read() && rowCount < maxRowsToReturn)
                        {
                            yield return rowObjectCreator(reader);
                            rowCount++;
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Convert a "stored procedure" command to work properly with Npgsql
        /// Npgsql 6.0 and earlier treated <see cref="CommandType.StoredProcedure"/> as a function, calling it with "SELECT * FROM CommandText()")
        /// We instead want to handle "stored procedure" command as CALL procedure_name()
        /// </summary>
        /// <remarks>
        /// Npgsql 7.0 changed the behavior of <see cref="CommandType.StoredProcedure"/> to use "CALL", but this method is still valid
        /// </remarks>
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
        /// <param name="readMethod">Method to read and return data from the command; command will be ready to run, executing and processing of returned data is left to this Action</param>
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
                    throw new ArgumentException($"Method ExecuteSPData requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(spCmd));
                }

                throw new ArgumentException($"Method ExecuteSPData requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {spCmd.GetType().FullName}.", nameof(spCmd));
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

            // ReSharper disable once GrammarMistakeInComment

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

                            // Look for any refcursor output parameters
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
                                            OnWarningEvent($"Reading of multiple RefCursors is not supported; ignoring RefCursor {name}.");
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(cursorName))
                            {
                                // Cursor found; read it and populate the output object
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

                        var fatalException = IsFatalException(ex, out var permissionDenied);

                        // Exception filling data adapter
                        // or
                        // Permission denied filling data adapter

                        errorMessage = string.Format(
                            "{0} filling data adapter: {1}; resultCode = {2}; SQL: {3}; Retry count = {4}; {5}",
                            permissionDenied ? "Permission denied" : "Exception",
                            ex.Message, resultCode, sqlCmd.CommandText, retryCount,
                            StackTraceFormatter.GetExceptionStackTrace(ex));

                        OnErrorEvent(errorMessage);

                        if (fatalException)
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
                        AppUtils.SleepMilliseconds(retryDelaySeconds * 1000);
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

        // ReSharper disable once GrammarMistakeInComment
        // ReSharper disable once UnusedMember.Global

        /// <summary>
        /// Method for calling a procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing procedure params</param>
        /// <param name="results">If SP successful, contains Results (list of, list of strings)</param>
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
        public int ExecuteSP(
            DbCommand spCmd,
            int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT,
            int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC)
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
        public int ExecuteSP(
            DbCommand spCmd,
            out string errorMessage,
            int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT,
            int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC)
        {
            if (spCmd is not NpgsqlCommand sqlCmd)
            {
                if (spCmd == null)
                {
                    throw new ArgumentException($"Method ExecuteSP requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of 'null'.", nameof(spCmd));
                }

                throw new ArgumentException($"Method ExecuteSP requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {spCmd.GetType().FullName}.", nameof(spCmd));
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

            // ReSharper disable once GrammarMistakeInComment

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

                        var fatalException = IsFatalException(ex, out var permissionDenied);

                        var procedureName = GetProcedureNameFromCommand(sqlCmd.CommandText);

                        // Exception calling procedure
                        // or
                        // Permission denied calling procedure

                        errorMessage = string.Format(
                            "{0} calling procedure {1}: {2}; resultCode = {3}; SQL: {4}; Retry count = {5}; {6}",
                            permissionDenied ? "Permission denied" : "Exception",
                            procedureName, ex.Message, resultCode, sqlCmd.CommandText, retryCount,
                            StackTraceFormatter.GetExceptionStackTrace(ex));

                        OnErrorEvent(errorMessage);

                        if (fatalException)
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
                        AppUtils.SleepMilliseconds(retryDelaySeconds * 1000);
                    }
                }

                if (retryCount < 1)
                {
                    // Too many retries, log and return error

                    var procedureName = GetProcedureNameFromCommand(sqlCmd.CommandText);

                    // Excessive retries calling procedure
                    // or
                    // Excessive retries (including deadlock) calling procedure

                    var logMessage = string.Format(
                        "Excessive retries{0} calling procedure {1}; {2}",
                        deadlockOccurred ? " (including deadlock) " : string.Empty,
                        procedureName, sqlCmd.CommandText);

                    OnErrorEvent(logMessage);

                    if (string.IsNullOrWhiteSpace(errorMessage))
                    {
                        errorMessage = logMessage;
                    }

                    if (deadlockOccurred)
                    {
                        return DbUtilsConstants.RET_VAL_DEADLOCK;
                    }

                    return DbUtilsConstants.RET_VAL_EXCESSIVE_RETRIES;
                }
            }

            return resultCode;
        }

        private static string GetProcedureNameFromCommand(string sqlCmd)
        {
            var charIndex = sqlCmd.IndexOf('(');

            if (charIndex <= 0)
            {
                return sqlCmd;
            }

            var startIndex = sqlCmd.StartsWith("CALL ", StringComparison.Ordinal) ? 5 : 0;

            return sqlCmd.Substring(startIndex, charIndex - startIndex);
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
        /// <param name="command">Database command</param>
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
            return AddParameterByDataTypeName(command, name, dataTypeName, size, direction);
        }

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <param name="command">Database command</param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="value">Value</param>
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
                throw new ArgumentException($"Method AddParameter requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {command.GetType().FullName}.", nameof(command));
            }

            // Optional: force the parameter name to be lowercase
            // See https://stackoverflow.com/a/45080006/1179467

            // ReSharper disable once GrammarMistakeInComment

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

        // ReSharper disable once UnusedMember.Global

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
                throw new ArgumentException($"Method AddTypedParameter requires a parameter of type {typeof(NpgsqlCommand).FullName}, but got an argument of type {command.GetType().FullName}.", nameof(command));
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
        /// Parse the query text to look for the list of columns after the SELECT keyword
        /// If any of the columns has capital letters, update the column name in the data table to match the capitalized column names
        /// </summary>
        /// <param name="cmd">DbCommand</param>
        /// <param name="dataTable">Datatable</param>
        private void CapitalizeColumnNames(IDisposable cmd, DataTable dataTable)
        {
            var columnNames = new List<string>();

            for (var i = 0; i < dataTable.Columns.Count; i++)
            {
                columnNames.Add(dataTable.Columns[i].ColumnName);
            }

            CapitalizeColumnNames(cmd, columnNames);

            for (var i = 0; i < dataTable.Columns.Count; i++)
            {
                if (!dataTable.Columns[i].ColumnName.Equals(columnNames[i]))
                {
                    dataTable.Columns[i].ColumnName = columnNames[i];
                }
            }
        }

        /// <summary>
        /// Parse the query text to look for the list of columns after the SELECT keyword
        /// If any of the columns has capital letters, update the column name in the data table to match the capitalized column names
        /// </summary>
        /// <param name="cmd">DbCommand</param>
        /// <param name="columnNames">List of column names</param>
        private void CapitalizeColumnNames(IDisposable cmd, IList<string> columnNames)
        {
            if (!CapitalizeColumnNamesInResults)
                return;

            if (cmd is not NpgsqlCommand sqlCmd)
                return;

            // ReSharper disable once MergeIntoPattern
            if (sqlCmd.CommandType != CommandType.Text)
                return;

            var columnNameMap = GetColumnCapitalizationMap(sqlCmd.CommandText, out var columnCountWithCapitalLetters);

            if (columnCountWithCapitalLetters == 0)
                return;

            for (var i = 0; i < columnNames.Count; i++)
            {
                if (columnNameMap.TryGetValue(columnNames[i], out var capitalizedName) &&
                    !columnNames[i].Equals(capitalizedName))
                {
                    columnNames[i] = capitalizedName;
                }
            }
        }

        /// <summary>
        /// Convert from enum SqlType to NpgsqlDbTypes.NpgsqlDbType
        /// </summary>
        /// <param name="sqlType">SQL type enum</param>
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
        /// Looks for column names in the first SELECT clause in the SQL
        /// Populates a dictionary that maps case-insensitive column names to capitalized names, ignoring column names that are quoted with double quotes
        /// </summary>
        /// <remarks>
        /// <para>If a column name occurs more than once, it will not be included in the dictionary</para>
        /// <para>Only matches column names that have letters, numbers, and underscores</para>
        /// </remarks>
        /// <param name="sqlQuery">SQL query</param>
        /// <param name="columnCountWithCapitalLetters">Output: number of columns with at least one capital letter (ignoring quoted column names)</param>
        /// <returns>Dictionary where both keys and values are the column names, but the dictionary uses case-insensitive string lookups</returns>
        private static Dictionary<string, string> GetColumnCapitalizationMap(string sqlQuery, out int columnCountWithCapitalLetters)
        {
            var columnNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Keys in this dictionary are column names, values are the number of times each column was found in the SELECT list in the SQL query
            var columnCountByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            columnCountWithCapitalLetters = 0;

            var quotedNameMatcher = new Regex(@"""[^""]+""", RegexOptions.Compiled);
            var columnNameMatcher = new Regex("(?<ColumnName>[a-z0-9_]+)[\t ]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // Replace any quoted names with "X"
            sqlQuery = quotedNameMatcher.Replace(sqlQuery, "\"X\"");

            // Replace \r and \n with spaces
            sqlQuery = sqlQuery.Replace('\r', ' ').Replace('\n', ' ');

            // Extract the text between the first SELECT ... FROM
            var selectMatcher = new Regex(@"\bselect\b", RegexOptions.IgnoreCase);
            var fromMatcher = new Regex(@"\bfrom\b", RegexOptions.IgnoreCase);

            var selectMatch = selectMatcher.Match(sqlQuery);
            var fromMatch = fromMatcher.Match(sqlQuery);

            if (!selectMatch.Success || !fromMatch.Success)
            {
                return columnNameMap;
            }

            var startIndex = selectMatch.Index + selectMatch.Length;

            var columnList = sqlQuery.Substring(startIndex, fromMatch.Index - startIndex);

            // Split on commas
            foreach (var column in columnList.Split(','))
            {
                var columnMatch = columnNameMatcher.Match(column);

                if (!columnMatch.Success)
                    continue;

                var columnName = columnMatch.Groups[0].Value.Trim();

                if (columnCountByName.TryGetValue(columnName, out var existingCount))
                {
                    // Duplicate column
                    columnCountByName[columnName] = existingCount + 1;
                    continue;
                }

                columnNameMap.Add(columnName, columnName);
                columnCountByName.Add(columnName, 1);

                if (!columnName.ToLower().Equals(columnName))
                {
                    columnCountWithCapitalLetters++;
                }
            }

            // Populate a new dictionary, using only those columns that appeared once in the select list
            var filteredColumnNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in columnCountByName)
            {
                if (item.Value > 1)
                    continue;

                var columnName = item.Key;
                var capitalizedName = columnNameMap[columnName];

                filteredColumnNameMap.Add(columnName, capitalizedName);
            }

            return filteredColumnNameMap;
        }

        /// <summary>
        /// Examine the exception message to determine whether it is safe to rerun a failed query or failed procedure execution
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <param name="permissionDenied">Output: true if the error message contains "permission denied" or "User username cannot use procedure"</param>
        /// <returns>True if the same error will happen again, so a retry is pointless</returns>
        protected static bool IsFatalException(Exception ex, out bool permissionDenied)
        {
            if (ex.Message.IndexOf("permission denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                mProcedureAccessDeniedMatcher.IsMatch(ex.Message))
            {
                permissionDenied = true;
                return true;
            }

            permissionDenied = false;

            return ex.Message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ex.Message.IndexOf("No such host is known", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ex.Message.IndexOf("open data reader exists for this command", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ex.Message.IndexOf("LDAP authentication failed for user", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ex.Message.IndexOf("No password has been provided but the backend requires one", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ex.Message.IndexOf("password authentication failed for user", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Look for parameter names that start with @
        /// Auto-change the at sign to an underscore: @ to _
        /// </summary>
        /// <remarks>Parameters named "@Return" with a ReturnValue parameter direction are auto-renamed to "_returnCode" and updated to have an InputOutput parameter direction</remarks>
        /// <param name="spCmd">NpgSQL command</param>
        private static void UpdateSqlServerParameterNames(NpgsqlCommand spCmd)
        {
            foreach (var parameter in spCmd.Parameters.Cast<NpgsqlParameter>())
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
        /// <param name="spCmd">NpgSQL command</param>
        private static void UpdateSqlServerParameterValues(NpgsqlCommand spCmd)
        {
            // When sending an enum to a PostgreSQL function or procedure, if you don't cast to an integer, you get this error:
            // Can't write CLR type Namespace.ClassName+EnumName with handler type Int32Handler
            // The following checks for this
            foreach (var parameter in spCmd.Parameters.Cast<NpgsqlParameter>())
            {
                if (parameter.Value != null && parameter.Value.GetType().IsEnum)
                {
                    parameter.Value = (int)parameter.Value;
                }
            }
        }
    }
}
