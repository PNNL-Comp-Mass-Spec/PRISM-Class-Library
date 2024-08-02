using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using PRISM.Logging;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Tools to retrieve data from a database
    /// </summary>
    public interface IDBTools : IEventNotifier
    {
        // Ignore Spelling: Postgres, Utils, varchar

        /// <summary>
        /// Database connection string
        /// </summary>
        string ConnectStr { get; set; }

        /// <summary>
        /// Database server type
        /// </summary>
        DbServerTypes DbServerType { get; }

        /// <summary>
        /// Set to True to raise debug events
        /// </summary>
        bool DebugMessagesEnabled { get; set; }

        /// <summary>
        /// Timeout length, in seconds, when waiting for a query or stored procedure to finish executing
        /// </summary>
        int TimeoutSeconds { get; set; }

        /// <summary>
        /// The name of the server to which the connection string connects
        /// </summary>
        string ServerName { get; }

        /// <summary>
        /// The name of the database to which the connection string connects
        /// </summary>
        string DatabaseName { get; }

        /// <summary>
        /// For SQL queries against PostgreSQL databases, when this is true,
        /// if any of the column names after the SELECT keyword has capital letters,
        /// the column names in the result table (or columnName list) will be auto-capitalized
        /// </summary>
        /// <remarks>
        /// <para>Defaults to true</para>
        /// <para>Only matches column names that have letters, numbers, and underscores</para>
        /// <para>Ignores column names that are quoted with double quotes</para>
        /// <para>If a column name occurs more than once, it will not be included in the dictionary</para>
        /// </remarks>
        public bool CapitalizeColumnNamesInResults { get; set; }

        /// <summary>
        /// Test connecting to the database
        /// </summary>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the connection</param>
        /// <returns>True if success, false if unable to connect</returns>
        bool TestDatabaseConnection(int retryCount = 3, int retryDelaySeconds = 5);

        /// <summary>
        /// Test connecting to the database
        /// </summary>
        /// <param name="serverVersion">Version string returned by the server connection</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the connection</param>
        /// <returns>True if success, false if unable to connect</returns>
        bool TestDatabaseConnection(out string serverVersion, int retryCount = 3, int retryDelaySeconds = 5);

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
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryScalar(
            string sqlQuery,
            out object queryResult,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod"
        );

        /// <summary>
        /// Run a query against a SQL database, return the scalar result
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="queryResult">Result (single value) returned by the query</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryScalar(
            DbCommand cmd,
            out object queryResult,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Run a query against a SQL database, return the results as a list of strings
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="results">Results (list of, list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryResults(
            string sqlQuery,
            out List<List<string>> results,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryResultsDataTable(
            string sqlQuery,
            out DataTable queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query</param>
        /// <param name="timeoutSeconds">Number of seconds to set as the command timeout; if &lt;=0, <see cref="TimeoutSeconds"/> is used</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryResultsDataSet(
            string sqlQuery,
            out DataSet queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Run a query against a SQL database, return the results as a list of strings
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="results">Results (list of, list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryResults(
            DbCommand cmd,
            out List<List<string>> results,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Run a query against a SQL database, return the results as a list of strings
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="results">Results (list of, list of strings)</param>
        /// <param name="columnNames">Column names (as returned by the database)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryResults(
            DbCommand cmd,
            out List<List<string>> results,
            out List<string> columnNames,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataTable object
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="queryResults">Results (list of, list of strings)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryResultsDataTable(
            DbCommand cmd,
            out DataTable queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Run a query against a SQL database, return the results as a DataSet object
        /// </summary>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// By default, retries the query up to 3 times
        /// </remarks>
        /// <param name="cmd">Query or procedure to run</param>
        /// <param name="queryResults">Results (as a DataSet)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the query or procedure</param>
        /// <param name="callingFunction">Name of the calling method (for logging purposes)</param>
        /// <returns>True if success, false if an error</returns>
        bool GetQueryResultsDataSet(
            DbCommand cmd,
            out DataSet queryResults,
            int retryCount = 3,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        IEnumerable<T> GetQueryResultsEnumerable<T>(
            string sqlQuery,
            Func<DbDataReader, T> rowObjectCreator,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            int timeoutSeconds = -1,
            [CallerMemberName] string callingFunction = "UnknownMethod");

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
        IEnumerable<T> GetQueryResultsEnumerable<T>(
            DbCommand cmd,
            Func<DbDataReader, T> rowObjectCreator,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5,
            [CallerMemberName] string callingFunction = "UnknownMethod");

        /// <summary>
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="results">If SP successful, contains Results (list of, list of strings)</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 for no limit</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        int ExecuteSPData(
            DbCommand spCmd,
            out List<List<string>> results,
            int retryCount = 3,
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
        int ExecuteSPDataTable(
            DbCommand spCmd,
            out DataTable results,
            int retryCount = 3,
            int retryDelaySeconds = 5);

        /// <summary>
        /// Method for executing a db stored procedure if a data table is to be returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="results">If SP successful, contains results as a DataSet</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        int ExecuteSPDataSet(
            DbCommand spCmd,
            out DataSet results,
            int retryCount = 3,
            int retryDelaySeconds = 5);

        /// <summary>
        /// Method for executing a db stored procedure, assuming no data table is returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        int ExecuteSP(
            DbCommand spCmd,
            int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT,
            int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC);

        /// <summary>
        /// Method for executing a db stored procedure when a data table is not returned
        /// </summary>
        /// <remarks>No logging is performed by this procedure</remarks>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="errorMessage">Error message (output)</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        int ExecuteSP(
            DbCommand spCmd,
            out string errorMessage,
            int maxRetryCount = DbUtilsConstants.DEFAULT_SP_RETRY_COUNT,
            int retryDelaySeconds = DbUtilsConstants.DEFAULT_SP_RETRY_DELAY_SEC);

        /// <summary>
        /// Creates a DbCommand for the database type
        /// </summary>
        /// <param name="cmdText">SQL Query or stored procedure name</param>
        /// <param name="cmdType">Command type</param>
        DbCommand CreateCommand(string cmdText, CommandType cmdType = CommandType.Text);

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
        DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            ParameterDirection direction = ParameterDirection.Input);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <remarks>
        /// If dbType is Text or VarChar, sets the parameter's value to string.Empty
        /// For Postgres, if dbType is VarChar and size is 0, initializes the parameter as text and sets the value to string.Empty
        /// </remarks>
        /// <param name="command">Database command</param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            int size,
            ParameterDirection direction = ParameterDirection.Input);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <remarks>
        /// If dbType is Text or VarChar, sets the parameter's value to string.Empty
        /// For Postgres, if dbType is VarChar and size is 0, initializes the parameter as text and sets the value to string.Empty
        /// </remarks>
        /// <param name="command">Database command</param>
        /// <param name="name">Parameter name</param>
        /// <param name="dataTypeName">Database data type name</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        DbParameter AddParameter(
            DbCommand command,
            string name,
            string dataTypeName,
            int size,
            ParameterDirection direction = ParameterDirection.Input);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type
        /// </summary>
        /// <param name="command">Database command</param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="value">Parameter value</param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        DbParameter AddParameter(
            DbCommand command,
            string name,
            SqlType dbType,
            int size,
            object value,
            ParameterDirection direction = ParameterDirection.Input);

        /// <summary>
        /// Adds a parameter to the DbCommand, appropriate for the database type. If supported by the database, this version can avoid boxing of primitives
        /// </summary>
        /// <param name="command">Database command</param>
        /// <param name="name">Parameter name</param>
        /// <param name="dbType">Database data type</param>
        /// <param name="size">Size (typically for varchar, but sometimes for date and time)</param>
        /// <param name="value">Parameter value</param>
        /// <param name="direction">Parameter direction</param>
        /// <returns>The newly added parameter</returns>
        DbParameter AddTypedParameter<T>(
            DbCommand command,
            string name,
            SqlType dbType,
            int size = 0,
            // ReSharper disable once RedundantTypeSpecificationInDefaultExpression
            T value = default(T),
            ParameterDirection direction = ParameterDirection.Input);
    }
}
