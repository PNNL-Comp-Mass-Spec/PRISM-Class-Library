using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace PRISM
{

    /// <summary>
    /// Tools to execute a stored procedure
    /// </summary>
    public class clsExecuteDatabaseSP : clsEventNotifier
    {

        #region "Constants"

        public const int RET_VAL_OK = 0;

        /// <summary>
        /// Typically caused by timeout expired
        /// </summary>
        public const int RET_VAL_EXCESSIVE_RETRIES = -5;

        /// <summary>
        /// Typically caused by transaction (Process ID 143) was deadlocked on lock resources with another process and has been chosen as the deadlock victim
        /// </summary>
        public const int RET_VAL_DEADLOCK = -4;

        public const int DEFAULT_SP_RETRY_COUNT = 3;

        public const int DEFAULT_SP_RETRY_DELAY_SEC = 20;

        public const int DEFAULT_SP_TIMEOUT_SEC = 30;

        #endregion

        #region "Module variables"

        private string m_ConnStr;

        private int mTimeoutSeconds = DEFAULT_SP_TIMEOUT_SEC;

        #endregion

        #region "Properties"
        public string DBconnectionString
        {
            get { return m_ConnStr; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new Exception("Connection string cannot be empty");
                }
                m_ConnStr = value;
            }
        }

        public bool DebugMessagesEnabled { get; set; }

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

        #region "Methods"


        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        public clsExecuteDatabaseSP(string connectionString)
        {
            m_ConnStr = connectionString;

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        public clsExecuteDatabaseSP(string connectionString, int timoutSeconds)
        {
            m_ConnStr = connectionString;
            mTimeoutSeconds = timoutSeconds;

        }

        /// <summary>
        /// Event handler for InfoMessage event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <remarks>Errors and warnings from SQL Server are caught here</remarks>
        private void OnInfoMessage(object sender, SqlInfoMessageEventArgs args)
        {

            foreach (SqlError err in args.Errors)
            {
                var s = "Message: " + err.Message +
                        ", Source: " + err.Source +
                        ", Class: " + err.Class +
                        ", State: " + err.State +
                        ", Number: " + err.Number +
                        ", LineNumber: " + err.LineNumber +
                        ", Procedure:" + err.Procedure +
                        ", Server: " + err.Server;

                OnErrorEvent(s);
            }

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
        public int ExecuteSP(
            SqlCommand spCmd,
            out List<List<string>> lstResults,
            short retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = 5)
        {
            // If this value is in error msg, then exception occurred before resultCode was set
            var resultCode = -9999;

            string errorMessage;
            var dtStartTime = DateTime.UtcNow;

            lstResults = new List<List<string>>();

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            var blnDeadlockOccurred = false;

            // Multiple retry loop for handling SP execution failures
            while (retryCount > 0)
            {

                var success = false;

                try
                {
                    using (var dbConnection = new SqlConnection(m_ConnStr))
                    {
                        dbConnection.InfoMessage += OnInfoMessage;

                        spCmd.Connection = dbConnection;
                        spCmd.CommandTimeout = TimeoutSeconds;

                        dbConnection.Open();

                        var reader = spCmd.ExecuteReader();

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

                        if (spCmd.Parameters.Contains("@Return"))
                            resultCode = Convert.ToInt32(spCmd.Parameters["@Return"].Value);

                    }
                    success = true;
                }
                catch (Exception ex)
                {
                    retryCount -= 1;
                    errorMessage = "Exception filling data adapter for " + spCmd.CommandText + ": " + ex.Message;
                    errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                    errorMessage += "; " + Utilities.GetExceptionStackTrace(ex);

                    OnErrorEvent(errorMessage);
                    Console.WriteLine(errorMessage);

                    if (ex.Message.StartsWith("Could not find stored procedure " + spCmd.CommandText))
                    {
                        retryCount = 0;
                    }
                    else if (ex.Message.Contains("was deadlocked"))
                    {
                        blnDeadlockOccurred = true;
                    }
                }
                finally
                {
                    if (DebugMessagesEnabled)
                    {
                        var debugMessage = "SP execution time: " + DateTime.UtcNow.Subtract(dtStartTime).TotalSeconds.ToString("##0.000") +
                                           " seconds for SP " + spCmd.CommandText;

                        OnDebugEvent(debugMessage);
                    }
                }

                if (success)
                    break;

                if (retryCount > 0)
                {
                    clsProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);
                }
            }

            if (retryCount < 1)
            {
                // Too many retries, log and return error
                errorMessage = "Excessive retries";
                if (blnDeadlockOccurred)
                {
                    errorMessage += " (including deadlock)";
                }
                errorMessage += " executing SP " + spCmd.CommandText;

                OnErrorEvent(errorMessage);

                if (blnDeadlockOccurred)
                {
                    return RET_VAL_DEADLOCK;
                }

                return RET_VAL_EXCESSIVE_RETRIES;
            }

            return resultCode;

        }




        /// <summary>
        /// Method for executing a db stored procedure, assuming no data table is returned; will retry the call to the procedure up to DEFAULT_SP_RETRY_COUNT=3 times
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        public int ExecuteSP(SqlCommand spCmd)
        {

            return ExecuteSP(spCmd, DEFAULT_SP_RETRY_COUNT);

        }

        /// <summary>
        /// Method for executing a db stored procedure, assuming no data table is returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        public int ExecuteSP(SqlCommand spCmd, int maxRetryCount)
        {

            return ExecuteSP(spCmd, maxRetryCount, DEFAULT_SP_RETRY_DELAY_SEC);

        }

        /// <summary>
        /// Method for executing a db stored procedure, assuming no data table is returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks></remarks>
        public int ExecuteSP(SqlCommand spCmd, int maxRetryCount, int retryDelaySeconds)
        {

            string errorMessage;
            return ExecuteSP(spCmd, maxRetryCount, out errorMessage, retryDelaySeconds);

        }

        /// <summary>
        /// Method for executing a db stored procedure when a data table is not returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="errorMessage">Error message (output)</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks>No logging is performed by this procedure</remarks>
        public int ExecuteSP(SqlCommand spCmd, int maxRetryCount, out string errorMessage)
        {
            return ExecuteSP(spCmd, maxRetryCount, out errorMessage, DEFAULT_SP_RETRY_DELAY_SEC);
        }

        /// <summary>
        /// Method for executing a db stored procedure when a data table is not returned
        /// </summary>
        /// <param name="spCmd">SQL command object containing stored procedure params</param>
        /// <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="errorMessage">Error message (output)</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        /// <remarks>No logging is performed by this procedure</remarks>
        public int ExecuteSP(SqlCommand spCmd, int maxRetryCount, out string errorMessage, int retryDelaySeconds)
        {

            // If this value is in error msg, then exception occurred before resultCode was set
            var resultCode = -9999;

            var dtStartTime = DateTime.UtcNow;
            var retryCount = maxRetryCount;
            var blnDeadlockOccurred = false;

            errorMessage = string.Empty;

            if (retryCount < 1)
            {
                retryCount = 1;
            }

            if (retryDelaySeconds < 1)
            {
                retryDelaySeconds = 1;
            }

            // Multiple retry loop for handling SP execution failures
            while (retryCount > 0)
            {
                blnDeadlockOccurred = false;
                try
                {
                    using (var Cn = new SqlConnection(m_ConnStr))
                    {

                        Cn.Open();

                        spCmd.Connection = Cn;
                        spCmd.CommandTimeout = TimeoutSeconds;

                        dtStartTime = DateTime.UtcNow;
                        spCmd.ExecuteNonQuery();

                        resultCode = Convert.ToInt32(spCmd.Parameters["@Return"].Value);

                    }

                    errorMessage = string.Empty;

                    break;
                }
                catch (Exception ex)
                {
                    retryCount -= 1;
                    errorMessage = "Exception calling stored procedure " + spCmd.CommandText + ": " + ex.Message;
                    errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                    errorMessage += "; " + Utilities.GetExceptionStackTrace(ex);

                    OnErrorEvent(errorMessage);

                    if (ex.Message.StartsWith("Could not find stored procedure " + spCmd.CommandText))
                    {
                        break;
                    }
                    else if (ex.Message.Contains("was deadlocked"))
                    {
                        blnDeadlockOccurred = true;
                    }
                }
                finally
                {
                    if (DebugMessagesEnabled)
                    {
                        var debugMessage = "SP execution time: " + DateTime.UtcNow.Subtract(dtStartTime).TotalSeconds.ToString("##0.000") + " seconds for SP " + spCmd.CommandText;
                        OnDebugEvent(debugMessage);
                    }
                }

                if (retryCount > 0)
                {
                    clsProgRunner.SleepMilliseconds(retryDelaySeconds * 1000);
                }
            }

            if (retryCount < 1)
            {
                // Too many retries, log and return error
                errorMessage = "Excessive retries";
                if (blnDeadlockOccurred)
                {
                    errorMessage += " (including deadlock)";
                }
                errorMessage += " executing SP " + spCmd.CommandText;

                OnErrorEvent(errorMessage);

                if (blnDeadlockOccurred)
                {
                    return RET_VAL_DEADLOCK;
                }

                return RET_VAL_EXCESSIVE_RETRIES;
            }

            return resultCode;

        }


        #endregion
    }

}
