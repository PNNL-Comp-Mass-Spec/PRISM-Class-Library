using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using PRISM;

// ReSharper disable UnusedMember.Global

namespace PRISMDatabaseUtils.MSSQLServer
{
    /// <summary>
    /// Tools to retrieve data from a database
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal class SQLServerDBTools : DBToolsBase, IDBTools
    {
        #region "Member Variables"

        private string mConnStr;

        /// <summary>
        /// Timeout length, in seconds, when waiting for a query to finish running
        /// </summary>
        private int mTimeoutSeconds;

        #endregion

        #region "Properties"

        /// <summary>
        /// Database connection string.
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
        public DbServerTypes DbServerType => DbServerTypes.MSSQLServer;

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
                if (value == 0)
                    value = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC;

                if (value < 10)
                    value = 10;

                mTimeoutSeconds = value;
            }
        }

        /// <summary>
        /// The name of the server to which the connection string connects.
        /// </summary>
        public string ServerName { get; private set; }

        /// <summary>
        /// The name of the database to which the connection string connects.
        /// </summary>
        public string DatabaseName { get; private set; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="timeoutSeconds"></param>
        public SQLServerDBTools(string connectionString, int timeoutSeconds = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC)
        {
            ConnectStr = connectionString;
            mTimeoutSeconds = timeoutSeconds;
        }

        private void ParseConnectionString(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            ServerName = builder.DataSource ?? string.Empty;
            DatabaseName = builder.InitialCatalog ?? string.Empty;
        }

        /// <summary>
        /// This method is an event handler for InfoMessage event.
        /// </summary>
        /// <remarks>
        /// The errors and warnings sent from the SQL server are caught here
        /// </remarks>
        private void OnInfoMessage(object sender, SqlInfoMessageEventArgs args)
        {
            var errMsg = new StringBuilder();
            foreach (SqlError err in args.Errors)
            {
                errMsg.Clear();
                errMsg.Append("Message: " + err.Message);
                errMsg.Append(", Source: " + err.Source);
                errMsg.Append(", Class: " + err.Class);
                errMsg.Append(", State: " + err.State);
                errMsg.Append(", Number: " + err.Number);
                errMsg.Append(", LineNumber: " + err.LineNumber);
                errMsg.Append(", Procedure:" + err.Procedure);
                errMsg.Append(", Server: " + err.Server);

                OnErrorEvent(errMsg.ToString());
            }
        }

        /// <summary>
        /// The function gets a disconnected DataSet as specified by the SQL statement.
        /// </summary>
        /// <param name="sqlQuery">A SQL string.</param>
        /// <param name="DS">A DataSet.</param>
        /// <param name="rowCount">A row counter.</param>
        /// <return>Returns a disconnected DataSet as specified by the SQL statement.</return>
        [Obsolete("Use GetQueryResults since support for DataSet objects is unavailable in .NET Standard 1.0; Use GetQueryResultsDataset.", true)]
        public bool GetDiscDataSet(string sqlQuery, ref DataSet DS, ref int rowCount)
        {
            var retryCount = 3;
            var retryDelaySeconds = 5;

            while (retryCount > 0)
            {
                try
                {
                    using (var dbConnection = new SqlConnection(ConnectStr))
                    {
                        dbConnection.InfoMessage += OnInfoMessage;

                        // Get the DataSet
                        var adapter = new SqlDataAdapter(sqlQuery, dbConnection);
                        DS = new DataSet();
                        rowCount = adapter.Fill(DS);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    retryCount -= 1;
                    var errorMessage =
                        string.Format("Exception querying database ({0}; " + "ConnectionString: {1}, RetryCount = {2}, Query {3}",
                                      ex.Message, ConnectStr, retryCount, sqlQuery);

                    OnErrorEvent(errorMessage);

                    // Delay for 5 seconds before trying again
                    ProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);
                }
            }

            return false;
        }

        /// <summary>
        /// Run a query against a SQL database, return the scalar result
        /// </summary>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResult">Result (single value) returned by the query</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
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

            var cmd = new SqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryScalar(cmd, out queryResult, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the scalar result
        /// </summary>
        /// <param name="cmd">Query to run</param>
        /// <param name="queryResult">Result (single value) returned by the query</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
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

            if (!(cmd is SqlCommand sqlCmd))
            {
                if (cmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of 'null'.", nameof(cmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of type {cmd.GetType().FullName}.", nameof(cmd));
            }

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            // Make sure we dispose of the command object; however, it must be done outside of the while loop (since we use the same command for retries)
            // Could use clones for each try, but that would cause problems with "Output" parameters
            using (sqlCmd)
            {
                while (retryCount > 0)
                {
                    try
                    {
                        using (var dbConnection = new SqlConnection(ConnectStr))
                        {
                            dbConnection.InfoMessage += OnInfoMessage;

                            sqlCmd.Connection = dbConnection;

                            dbConnection.Open();

                            queryResult = sqlCmd.ExecuteScalar();
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        retryCount -= 1;
                        if (string.IsNullOrWhiteSpace(callingFunction))
                        {
                            callingFunction = "Unknown";
                        }
                        var errorMessage = string.Format("Exception querying database (called from {0}): {1}; " + "ConnectionString: {2}, RetryCount = {3}, Query {4}", callingFunction, ex.Message, ConnectStr, retryCount, sqlCmd);

                        OnErrorEvent(errorMessage);

                        if (ex.Message.IndexOf("Login failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ex.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ex.Message.IndexOf("Invalid column name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ex.Message.IndexOf("permission was denied", StringComparison.OrdinalIgnoreCase) >= 0)
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
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="lstResults">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        public bool GetQueryResults(
            string sqlQuery,
            out List<List<string>> lstResults,
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

            var cmd = new SqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryResults(cmd, out lstResults, retryCount, maxRowsToReturn, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL Server database, return the results as a list of strings
        /// </summary>
        /// <param name="cmd">Query to run</param>
        /// <param name="lstResults">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        public bool GetQueryResults(
            DbCommand cmd,
            out List<List<string>> lstResults,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            var results = new List<List<string>>();
            lstResults = results;
            var readMethod = new Action<SqlCommand>(x =>
            {
                using (var reader = x.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        return;
                    }

                    while (reader.Read())
                    {
                        var lstCurrentRow = new List<string>();

                        for (var columnIndex = 0; columnIndex <= reader.FieldCount - 1; columnIndex++)
                        {
                            var value = reader.GetValue(columnIndex);

                            if (DBNull.Value.Equals(value))
                            {
                                lstCurrentRow.Add(string.Empty);
                            }
                            else
                            {
                                lstCurrentRow.Add(value.ToString());
                            }
                        }

                        results.Add(lstCurrentRow);

                        if (maxRowsToReturn > 0 && results.Count >= maxRowsToReturn)
                        {
                            break;
                        }
                    }
                }
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataTable object
        /// </summary>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResults">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
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

            var cmd = new SqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryResultsDataTable(cmd, out queryResults, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataTable object
        /// </summary>
        /// <param name="cmd">Query to run</param>
        /// <param name="queryResults">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        public bool GetQueryResultsDataTable(
            DbCommand cmd,
            out DataTable queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            var results = new DataTable();
            queryResults = results;
            var readMethod = new Action<SqlCommand>(x =>
            {
                using (var da = new SqlDataAdapter(x))
                {
                    da.Fill(results);
                }
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataSet object
        /// </summary>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="queryResults">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
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

            var cmd = new SqlCommand(sqlQuery) { CommandType = CommandType.Text, CommandTimeout = timeoutSeconds };
            return GetQueryResultsDataSet(cmd, out queryResults, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataSet object
        /// </summary>
        /// <param name="cmd">Query to run</param>
        /// <param name="queryResults">Results (list of list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        public bool GetQueryResultsDataSet(
            DbCommand cmd,
            out DataSet queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            var results = new DataSet();
            queryResults = results;
            var readMethod = new Action<SqlCommand>(x =>
            {
                using (var da = new SqlDataAdapter(x))
                {
                    da.Fill(results);
                }
            });

            return GetQueryResults(cmd, readMethod, retryCount, retryDelaySeconds, callingFunction);
        }

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataSet object
        /// </summary>
        /// <param name="cmd">Query to run</param>
        /// <param name="readMethod">method to read and return data from the command; command will be ready to run, executing and processing of returned data is left to the this Action.</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        private bool GetQueryResults(
            DbCommand cmd,
            Action<SqlCommand> readMethod,
            int retryCount,
            int retryDelaySeconds,
            string callingFunction)
        {
            if (string.IsNullOrWhiteSpace(callingFunction))
            {
                callingFunction = "UnknownCaller";
            }

            if (!(cmd is SqlCommand sqlCmd))
            {
                if (cmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of 'null'.", nameof(cmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of type {cmd.GetType().FullName}.", nameof(cmd));
            }

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            // Make sure we dispose of the command object; however, it must be done outside of the while loop (since we use the same command for retries)
            // Could use clones for each try, but that would cause problems with "Output" parameters
            using (sqlCmd)
            {
                while (retryCount > 0)
                {
                    try
                    {
                        using (var dbConnection = new SqlConnection(ConnectStr))
                        {
                            dbConnection.InfoMessage += OnInfoMessage;

                            sqlCmd.Connection = dbConnection;

                            dbConnection.Open();

                            readMethod(sqlCmd);
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        retryCount -= 1;
                        if (string.IsNullOrWhiteSpace(callingFunction))
                        {
                            callingFunction = "Unknown";
                        }
                        var errorMessage = string.Format("Exception querying database (called from {0}): {1}; " + "ConnectionString: {2}, RetryCount = {3}, Query {4}", callingFunction, ex.Message, ConnectStr, retryCount, sqlCmd);

                        OnErrorEvent(errorMessage);

                        if (ex.Message.IndexOf("Login failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ex.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ex.Message.IndexOf("Invalid column name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ex.Message.IndexOf("permission was denied", StringComparison.OrdinalIgnoreCase) >= 0)
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
        /// The function updates a database table as specified in the SQL statement.
        /// </summary>
        /// <param name="SQL">A SQL string.</param>
        /// <param name="affectedRows">Affected Rows to be updated.</param>
        /// <return>Returns Boolean showing if the database was updated.</return>
        [Obsolete("Functionality of this method has been disabled for safety; an exception will be raised if it is called")]
        public bool UpdateDatabase(string SQL, out int affectedRows)
        {
            affectedRows = 0;

            throw new Exception("This method is obsolete (because it blindly executes the SQL); do not use");

            /*
                // Updates a database table as specified in the SQL statement

                affectedRows = 0;

                // Verify database connection is open
                if (!OpenConnection())
                    return false;

                try
                {
                    var cmd = new SqlCommand(SQL, m_DBCn);
                    affectedRows = cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex)
                {
                    // If error happened, log it
                    OnError("Error updating database", ex);
                    return false;
                }
                finally
                {
                    m_DBCn.Close();
                }
              */
        }

        #region "Methods"

        /// <summary>
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="readMethod">method to read and return data from the command; command will be ready to run, executing and processing of returned data is left to the this Action.</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        private int ExecuteSPData(
            DbCommand spCmd,
            Action<SqlCommand> readMethod,
            int retryCount = 3,
            int retryDelaySeconds = 5)
        {
            if (!(spCmd is SqlCommand sqlCmd))
            {
                if (spCmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of 'null'.", nameof(spCmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of type {spCmd.GetType().FullName}.", nameof(spCmd));
            }

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
                        using (var dbConnection = new SqlConnection(mConnStr))
                        {
                            dbConnection.InfoMessage += OnInfoMessage;

                            sqlCmd.Connection = dbConnection;

                            dbConnection.Open();

                            readMethod(sqlCmd);

                            if (sqlCmd.Parameters.Contains("@Return"))
                            {
                                resultCode = Convert.ToInt32(sqlCmd.Parameters["@Return"].Value);
                            }
                            else
                            {
                                //OnDebugEvent(string.Format(
                                //                 "Cannot read the return code for stored procedure {0} " +
                                //                 "since cpCmd does not contain a parameter named @Return",
                                //                 sqlCmd.CommandText));
                                resultCode = 0;
                            }
                        }

                        success = true;
                    }
                    catch (Exception ex)
                    {
                        retryCount -= 1;
                        errorMessage = "Exception filling data adapter for " + sqlCmd.CommandText + ": " + ex.Message;
                        errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                        errorMessage += "; " + StackTraceFormatter.GetExceptionStackTrace(ex);

                        OnErrorEvent(errorMessage);
                        Console.WriteLine(errorMessage);

                        if (ex.Message.StartsWith("Could not find stored procedure " + sqlCmd.CommandText))
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
                            var debugMessage = "SP execution time: " + DateTime.UtcNow.Subtract(startTime).TotalSeconds.ToString("##0.000") +
                                               " seconds for SP " + sqlCmd.CommandText;

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
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="lstResults">If SP successful, contains Results (list of list of strings)</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 for no limit</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        public int ExecuteSPData(
            DbCommand spCmd,
            out List<List<string>> lstResults,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5)
        {
            var results = new List<List<string>>();
            lstResults = results;
            var readMethod = new Action<SqlCommand>(x =>
            {
                using (var reader = spCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var lstCurrentRow = new List<string>();

                        for (var columnIndex = 0; columnIndex <= reader.FieldCount - 1; columnIndex++)
                        {
                            var value = reader.GetValue(columnIndex);

                            if (DBNull.Value.Equals(value))
                            {
                                lstCurrentRow.Add(string.Empty);
                            }
                            else
                            {
                                lstCurrentRow.Add(value.ToString());
                            }
                        }

                        results.Add(lstCurrentRow);

                        if (maxRowsToReturn > 0 && results.Count >= maxRowsToReturn)
                        {
                            break;
                        }
                    }
                }
            });

            return ExecuteSPData(spCmd, readMethod, retryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="results">If SP successful, contains results as a DataTable</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        public int ExecuteSPDataTable(
            DbCommand spCmd,
            out DataTable results,
            int retryCount = 3,
            int retryDelaySeconds = 5)
        {
            var queryResults = new DataTable();
            results = queryResults;
            var readMethod = new Action<SqlCommand>(x =>
            {
                using (var da = new SqlDataAdapter(x))
                {
                    da.Fill(queryResults);
                }
            });

            return ExecuteSPData(spCmd, readMethod, retryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="results">If SP successful, contains results as a DataSet</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        public int ExecuteSPDataSet(
            DbCommand spCmd,
            out DataSet results,
            int retryCount = 3,
            int retryDelaySeconds = 5)
        {
            var queryResults = new DataSet();
            results = queryResults;
            var readMethod = new Action<SqlCommand>(x =>
            {
                using (var da = new SqlDataAdapter(x))
                {
                    da.Fill(queryResults);
                }
            });

            return ExecuteSPData(spCmd, readMethod, retryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for executing a db stored procedure, assuming no data table is returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        public int ExecuteSP(DbCommand spCmd, int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT, int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC)
        {
            return ExecuteSP(spCmd, out _, maxRetryCount, retryDelaySeconds);
        }

        /// <summary>
        /// Method for executing a db stored procedure when a data table is not returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="errorMessage">Error message (output)</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks>No logging is performed by this procedure</remarks>
        public int ExecuteSP(DbCommand spCmd, out string errorMessage, int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT, int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC)
        {
            if (!(spCmd is SqlCommand sqlCmd))
            {
                if (spCmd == null)
                {
                    throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of 'null'.", nameof(spCmd));
                }

                throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of type {spCmd.GetType().FullName}.", nameof(spCmd));
            }

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
                        using (var dbConnection = new SqlConnection(mConnStr))
                        {
                            dbConnection.Open();

                            sqlCmd.Connection = dbConnection;

                            startTime = DateTime.UtcNow;
                            sqlCmd.ExecuteNonQuery();

                            if (sqlCmd.Parameters.Contains("@Return"))
                            {
                                resultCode = Convert.ToInt32(sqlCmd.Parameters["@Return"].Value);
                            }
                            else
                            {
                                //OnDebugEvent(string.Format(
                                //                 "Cannot read the return code for stored procedure {0} " +
                                //                 "since spCmd does not contain a parameter named @Return",
                                //                 sqlCmd.CommandText));
                                resultCode = 0;
                            }
                        }

                        errorMessage = string.Empty;
                        break;
                    }
                    catch (Exception ex)
                    {
                        retryCount -= 1;
                        errorMessage = "Exception calling stored procedure " + sqlCmd.CommandText + ": " + ex.Message;
                        errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                        errorMessage += "; " + StackTraceFormatter.GetExceptionStackTrace(ex);

                        OnErrorEvent(errorMessage);

                        if (ex.Message.StartsWith("Could not find stored procedure " + sqlCmd.CommandText))
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
                            var debugMessage = "SP execution time: " + DateTime.UtcNow.Subtract(startTime).TotalSeconds.ToString("##0.000") + " seconds for SP " + sqlCmd.CommandText;
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

        #endregion

        #region IExecuteSP implementation

        /// <inheritdoc />
        public DbCommand CreateCommand(string cmdText, CommandType cmdType = CommandType.Text)
        {
            return new SqlCommand(cmdText) { CommandType = cmdType, CommandTimeout = TimeoutSeconds };
        }

        /// <inheritdoc />
        public DbParameter AddParameter(DbCommand command, string name, SqlType dbType, int size = 0, object value = null,
            ParameterDirection direction = ParameterDirection.Input)
        {
            if (!(command is SqlCommand sqlCmd))
            {
                throw new ArgumentException($"This method requires a parameter of type {typeof(SqlCommand).FullName}, but got an argument of type {command.GetType().FullName}.", nameof(command));
            }

            var param = new SqlParameter(name, ConvertSqlType(dbType), size)
            {
                Direction = direction,
                Value = value,
            };

            sqlCmd.Parameters.Add(param);

            return param;
        }

        /// <inheritdoc />
        public DbParameter AddTypedParameter<T>(DbCommand command, string name, SqlType dbType, int size = 0, T value = default(T),
            ParameterDirection direction = ParameterDirection.Input)
        {
            return AddParameter(command, name, dbType, size, value, direction);
        }

        private SqlDbType ConvertSqlType(SqlType sqlType)
        {
            switch (sqlType)
            {
                case SqlType.Int: return SqlDbType.Int;
                case SqlType.BigInt: return SqlDbType.BigInt;
                case SqlType.Real: return SqlDbType.Real;
                case SqlType.Float: return SqlDbType.Float;
                case SqlType.TinyInt: return SqlDbType.TinyInt;
                case SqlType.SmallInt: return SqlDbType.SmallInt;
                case SqlType.Char: return SqlDbType.Char;
                case SqlType.VarChar: return SqlDbType.VarChar;
                case SqlType.Text: return SqlDbType.Text;
                case SqlType.Date: return SqlDbType.Date;
                case SqlType.DateTime: return SqlDbType.DateTime;
                case SqlType.Xml: return SqlDbType.Xml;
                default: throw new NotSupportedException($"Conversion for type {sqlType} not known");
            }
        }

        #endregion
    }
}
