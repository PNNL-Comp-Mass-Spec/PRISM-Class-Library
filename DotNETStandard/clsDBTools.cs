using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace PRISM
{

    /// <summary>
    /// Tools to manipulates the database.
    /// </summary>
    public class clsDBTools : clsEventNotifier
    {

        #region "Constants"

        public const int DEFAULT_SP_TIMEOUT_SEC = 30;

        #endregion

        #region "Member Variables"

        // DB access
        private string m_ConnStr;

        private int mTimeoutSeconds;

        #endregion

        #region "Properties"

        public int TimeoutSeconds
        {
            get { return mTimeoutSeconds; }
            set
            {
                if (value == 0)
                    value = DEFAULT_SP_TIMEOUT_SEC;
                if (value < 10)
                    value = 10;

                mTimeoutSeconds = value;
            }
        }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public clsDBTools(string connectionString)
        {
            m_ConnStr = connectionString;
        }

        /// <summary>
        /// The property sets and gets a connection string.
        /// </summary>
        public string ConnectStr
        {
            get { return m_ConnStr; }
            set { m_ConnStr = value; }
        }

        /// <summary>
        /// The subroutine is an event handler for InfoMessage event.
        /// </summary>
        /// <remarks>
        /// The errors and warnings sent from the SQL server are caught here
        /// </remarks>
        private void OnInfoMessage(object sender, SqlInfoMessageEventArgs args)
        {
            foreach (SqlError err in args.Errors)
            {
                var s = "";
                s += "Message: " + err.Message;
                s += ", Source: " + err.Source;
                s += ", Class: " + err.Class;
                s += ", State: " + err.State;
                s += ", Number: " + err.Number;
                s += ", LineNumber: " + err.LineNumber;
                s += ", Procedure:" + err.Procedure;
                s += ", Server: " + err.Server;
                OnErrorEvent(s);
            }
        }

        /// <summary>
        /// Run a query against a SQL Server database, return the results as a list of strings
        /// </summary>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="lstResults">Results (list of list of strings)</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
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
            string callingFunction,
            short retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5)
        {

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            lstResults = new List<List<string>>();

            while (retryCount > 0)
            {
                try
                {
                    using (var dbConnection = new SqlConnection(m_ConnStr))
                    {
                        dbConnection.InfoMessage += OnInfoMessage;

                        using (var cmd = new SqlCommand(sqlQuery, dbConnection))
                        {

                            cmd.CommandTimeout = TimeoutSeconds;

                            dbConnection.Open();

                            var reader = cmd.ExecuteReader();

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

                                lstResults.Add(lstCurrentRow);

                                if (maxRowsToReturn > 0 && lstResults.Count >= maxRowsToReturn)
                                {
                                    break;
                                }
                            }

                        }
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
                    var errorMessage = string.Format("Exception querying database (called from {0}): {1}; " + "ConnectionString: {2}, RetryCount = {3}, Query {4}", callingFunction, ex.Message, m_ConnStr, retryCount, sqlQuery);

                    OnErrorEvent(errorMessage);

                    // Delay for 5 seconds before trying again
                    clsProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);

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
    }
}
