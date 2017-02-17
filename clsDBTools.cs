using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace PRISM
{

    /// <summary>
    /// Tools to manipulates the database.
    /// </summary>
    public class clsDBTools
    {

        #region "Member Variables"

        // DB access
        private string m_connection_str;

        private SqlConnection m_DBCn;
        public event ErrorEventEventHandler ErrorEvent;
        public delegate void ErrorEventEventHandler(string errorMessage);

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public clsDBTools(string connectionString)
        {
            m_connection_str = connectionString;
        }

        /// <summary>
        /// The property sets and gets a connection string.
        /// </summary>
        public string ConnectStr
        {
            get { return m_connection_str; }
            set { m_connection_str = value; }
        }

        /// <summary>
        /// The function opens a database connection.
        /// </summary>
        /// <return>True if the connection was successfully opened</return>
        /// <remarks>Retries the connection up to 3 times</remarks>
        private bool OpenConnection()
        {
            var retryCount = 3;
            var sleepTimeMsec = 300;
            while (retryCount > 0)
            {
                try
                {
                    m_DBCn = new SqlConnection(m_connection_str);
                    m_DBCn.InfoMessage += OnInfoMessage;
                    m_DBCn.Open();
                    retryCount = 0;
                    return true;
                }
                catch (SqlException e)
                {
                    retryCount -= 1;
                    m_DBCn.Close();
                    OnError("Connection problem", e);
                    Thread.Sleep(sleepTimeMsec);
                    sleepTimeMsec *= 2;
                }
            }

            OnError("Unable to open connection after multiple tries");
            return false;
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
                OnError(s);
            }
        }

        /// <summary>
        /// The function gets a disconnected dataset as specified by the SQL statement.
        /// </summary>
        /// <param name="SQL">A SQL string.</param>
        /// <param name="DS">A dataset.</param>
        /// <param name="rowCount">A row counter.</param>
        /// <return>Returns a disconnected dataset as specified by the SQL statement.</return>
        public bool GetDiscDataSet(string SQL, ref DataSet DS, ref int rowCount)
        {

            // Verify database connection is open
            if (!OpenConnection())
                return false;

            try
            {
                // Get the dataset
                var adapter = new SqlDataAdapter(SQL, m_DBCn);
                DS = new DataSet();
                rowCount = adapter.Fill(DS);
                return true;
            }
            catch (Exception ex)
            {
                // If error happened, log it
                OnError("Error reading database", ex);
                return false;
            }
            finally
            {
                // Be sure connection is closed
                m_DBCn.Close();
            }

        }

        /// <summary>
        /// Run a query against a SQL Server database, return the results as a list of strings
        /// </summary>
        /// <param name="sqlQuery">Query to run</param>
        /// <param name="lstResults">Results (list of list of strings)</param>
        /// <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <param name="timeoutSeconds">Query timeout (in seconds); minimum is 5 seconds; suggested value is 30 seconds</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Uses the connection string passed to the constructor of this class
        /// Null values are converted to empty strings
        /// Numbers are converted to their string equivalent
        /// By default, retries the query up to 3 times
        /// </remarks>
        public bool GetQueryResults(string sqlQuery, out List<List<string>> lstResults, string callingFunction, short retryCount = 3, int timeoutSeconds = 30, int maxRowsToReturn = 0)
        {

            if (retryCount < 1)
                retryCount = 1;
            if (timeoutSeconds < 5)
                timeoutSeconds = 5;

            lstResults = new List<List<string>>();

            while (retryCount > 0)
            {
                try
                {
                    using (var dbConnection = new SqlConnection(m_connection_str))
                    {
                        using (var cmd = new SqlCommand(sqlQuery, dbConnection))
                        {

                            cmd.CommandTimeout = timeoutSeconds;

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
                    var errorMessage = string.Format("Exception querying database (called from {0}): {1}; " + "ConnectionString: {2}, RetryCount = {3}, Query {4}", callingFunction, ex.Message, m_connection_str, retryCount, sqlQuery);

                    OnError(errorMessage);

                    // Delay for 5 seconds before trying again
                    Thread.Sleep(5000);
                    
                }
            }

            return false;

        }

        private void OnError(string errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                ErrorEvent?.Invoke(errorMessage);
            }
        }

        private void OnError(string errorMessage, Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                ErrorEvent?.Invoke(errorMessage + ": " + ex.Message);
            }
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
