﻿using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using PRISM.Logging;

namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Tools to retrieve data from a database
    /// </summary>
    public interface IDBTools : IEventNotifier
    {
        /// <summary>
        /// Database connection string.
        /// </summary>
        string ConnectStr { get; set; }

        /// <summary>
        /// Set to True to raise debug events
        /// </summary>
        bool DebugMessagesEnabled { get; set; }

        /// <summary>
        /// Timeout length, in seconds, when waiting for a query or stored procedure to finish executing
        /// </summary>
        int TimeoutSeconds { get; set; }

        /// <summary>
        /// The name of the server to which the connection string connects.
        /// </summary>
        string ServerName { get; }

        /// <summary>
        /// The name of the database to which the connection string connects.
        /// </summary>
        string DatabaseName { get; }

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
        bool GetQueryScalar(
            string sqlQuery,
            out object queryResult,
            short retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod"
        );

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
        bool GetQueryScalar(
            DbCommand cmd,
            out object queryResult,
            short retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Run a query against a SQL database, return the results as a list of strings
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
        bool GetQueryResults(
            string sqlQuery,
            out List<List<string>> lstResults,
            short retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        bool GetQueryResultsDataTable(
            string sqlQuery,
            out DataTable queryResults,
            short retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        bool GetQueryResultsDataSet(
            string sqlQuery,
            out DataSet queryResults,
            short retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Run a query against a SQL database, return the results as a list of strings
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
        bool GetQueryResults(
            DbCommand cmd,
            out List<List<string>> lstResults,
            short retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        bool GetQueryResultsDataTable(
            DbCommand cmd,
            out DataTable queryResults,
            short retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        bool GetQueryResultsDataSet(
            DbCommand cmd,
            out DataSet queryResults,
            short retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        int ExecuteSPData(
            DbCommand spCmd,
            out List<List<string>> lstResults,
            short retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5);

        /// <summary>
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="results">If SP successful, contains results as a DataTable</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        int ExecuteSPDataTable(
            DbCommand spCmd,
            out DataTable results,
            short retryCount = 3,
            int retryDelaySeconds = 5);

        /// <summary>
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="results">If SP successful, contains results as a DataSet</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        int ExecuteSPDataSet(
            DbCommand spCmd,
            out DataSet results,
            short retryCount = 3,
            int retryDelaySeconds = 5);

        /// <summary>
        /// Method for executing a db stored procedure, assuming no data table is returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        int ExecuteSP(DbCommand spCmd, int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT, int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC);

        /// <summary>
        /// Method for executing a db stored procedure when a data table is not returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="errorMessage">Error message (output)</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks>No logging is performed by this procedure</remarks>
        int ExecuteSP(DbCommand spCmd, out string errorMessage, int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT, int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC);

        /// <summary>
        /// Creates a DbCommand for the database type
        /// </summary>
        /// <param name="cmdText"></param>
        /// <param name="cmdType"></param>
        /// <returns></returns>
        DbCommand CreateCommand(string cmdText, CommandType cmdType = CommandType.Text);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <param name="command"></param>
        /// <param name="name"></param>
        /// <param name="dbType"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        DbParameter AddParameter(DbCommand command, string name, SqlType dbType, int size = 0, object value = null, ParameterDirection direction = ParameterDirection.Input);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type. If supported by the database, this version can avoid boxing of primitives.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="name"></param>
        /// <param name="dbType"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        DbParameter AddTypedParameter<T>(DbCommand command, string name, SqlType dbType, int size = 0, T value = default(T), ParameterDirection direction = ParameterDirection.Input);
    }
}