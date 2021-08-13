using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Tools to execute a stored procedure
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [Obsolete("Use PRISMDatabaseUtils.MSSQLServer.SQLServerDBTools instead", true)]
    public class ExecuteDatabaseSP : EventNotifier
    {
        //Ignore Spelling: spCmd, Namespace

        #region "Constants"

        /// <summary>
        /// Return value indicating everything is OK
        /// </summary>
        public const int RET_VAL_OK = 0;

        /// <summary>
        /// Typically caused by timeout expired
        /// </summary>
        public const int RET_VAL_EXCESSIVE_RETRIES = -5;

        /// <summary>
        /// Typically caused by transaction (Process ID 143) was deadlocked on lock resources with another process and has been chosen as the deadlock victim
        /// </summary>
        public const int RET_VAL_DEADLOCK = -4;

        /// <summary>
        /// Default number of times to retry calling the stored procedure
        /// </summary>
        public const int DEFAULT_SP_RETRY_COUNT = 3;

        /// <summary>
        /// Default delay, in seconds, when retrying a stored procedure call
        /// </summary>
        public const int DEFAULT_SP_RETRY_DELAY_SEC = 20;

        /// <summary>
        /// Default timeout length, in seconds, when waiting for a stored procedure to finish executing
        /// </summary>
        public const int DEFAULT_SP_TIMEOUT_SEC = 30;

        #endregion

        #region "Module variables"

        private string mConnectionString;

        /// <summary>
        /// Timeout length, in seconds, when waiting for a stored procedure to finish executing
        /// </summary>
        private int mTimeoutSeconds = DEFAULT_SP_TIMEOUT_SEC;

        #endregion

        #region "Properties"

        /// <summary>
        /// Database connection string
        /// </summary>
        // ReSharper disable once IdentifierTypo
        public string DBconnectionString
        {
            get => mConnectionString;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new Exception("Connection string cannot be empty");
                }
                mConnectionString = value;
            }
        }

        /// <summary>
        /// Set to True to raise debug events
        /// </summary>
        public bool DebugMessagesEnabled { get; set; }

        /// <summary>
        /// Timeout length, in seconds, when waiting for a stored procedure to finish executing
        /// </summary>
        public int TimeoutSeconds
        {
            get => mTimeoutSeconds;
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
        public ExecuteDatabaseSP(string connectionString)
        {
            mConnectionString = connectionString;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ExecuteDatabaseSP(string connectionString, int timeoutSeconds)
        {
            mConnectionString = connectionString;
            mTimeoutSeconds = timeoutSeconds;
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
        /// <param name="results">If SP successful, contains Results (list of list of strings)</param>
        /// <param name="retryCount">Maximum number of times to attempt to call the stored procedure</param>
        /// <param name="maxRowsToReturn">Maximum rows to return; 0 for no limit</param>
        /// <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        /// <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        public int ExecuteSP(
            SqlCommand spCmd,
            out List<List<string>> results,
            int retryCount = 3,
            int maxRowsToReturn = 0,
            int retryDelaySeconds = DEFAULT_SP_RETRY_COUNT)
        {
            // If this value is in error msg, exception occurred before resultCode was set
            var resultCode = -9999;

            string errorMessage;
            var startTime = DateTime.UtcNow;

            results = new List<List<string>>();

            if (retryCount < 1)
                retryCount = 1;

            if (retryDelaySeconds < 1)
                retryDelaySeconds = 1;

            var deadlockOccurred = false;

            // Multiple retry loop for handling SP execution failures
            while (retryCount > 0)
            {
                var success = false;

                try
                {
                    using (var dbConnection = new SqlConnection(mConnectionString))
                    {
                        dbConnection.InfoMessage += OnInfoMessage;

                        spCmd.Connection = dbConnection;
                        spCmd.CommandTimeout = TimeoutSeconds;

                        dbConnection.Open();

                        var reader = spCmd.ExecuteReader();

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

                            results.Add(currentRow);

                            if (maxRowsToReturn > 0 && results.Count >= maxRowsToReturn)
                            {
                                break;
                            }
                        }

                        if (spCmd.Parameters.Contains("@Return"))
                        {
                            resultCode = Convert.ToInt32(spCmd.Parameters["@Return"].Value);
                        }
                        else
                        {
                            OnDebugEvent(string.Format(
                                             "Cannot read the return code for stored procedure {0} " +
                                             "since spCmd does not contain a parameter named @Return",
                                             spCmd.CommandText));
                            resultCode = 0;
                        }
                    }
                    success = true;
                }
                catch (Exception ex)
                {
                    retryCount--;
                    errorMessage = "Exception filling data adapter for " + spCmd.CommandText + ": " + ex.Message;
                    errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                    errorMessage += "; " + StackTraceFormatter.GetExceptionStackTrace(ex);

                    OnErrorEvent(errorMessage);
                    Console.WriteLine(errorMessage);

                    if (ex.Message.StartsWith("Could not find stored procedure " + spCmd.CommandText))
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
                                           " seconds for SP " + spCmd.CommandText;

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
                errorMessage += " executing SP " + spCmd.CommandText;

                OnErrorEvent(errorMessage);

                if (deadlockOccurred)
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
        public int ExecuteSP(SqlCommand spCmd, int maxRetryCount, int retryDelaySeconds)
        {
            return ExecuteSP(spCmd, maxRetryCount, out _, retryDelaySeconds);
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

            // Multiple retry loop for handling SP execution failures
            while (retryCount > 0)
            {
                deadlockOccurred = false;
                try
                {
                    using (var dbConnection = new SqlConnection(mConnectionString))
                    {
                        dbConnection.Open();

                        spCmd.Connection = dbConnection;
                        spCmd.CommandTimeout = TimeoutSeconds;

                        startTime = DateTime.UtcNow;
                        spCmd.ExecuteNonQuery();

                        if (spCmd.Parameters.Contains("@Return"))
                        {
                            resultCode = Convert.ToInt32(spCmd.Parameters["@Return"].Value);
                        }
                        else
                        {
                            OnDebugEvent(string.Format(
                                             "Cannot read the return code for stored procedure {0} " +
                                             "since spCmd does not contain a parameter named @Return",
                                             spCmd.CommandText));
                            resultCode = 0;
                        }
                    }

                    errorMessage = string.Empty;

                    break;
                }
                catch (Exception ex)
                {
                    retryCount--;
                    errorMessage = "Exception calling stored procedure " + spCmd.CommandText + ": " + ex.Message;
                    errorMessage += "; resultCode = " + resultCode + "; Retry count = " + retryCount;
                    errorMessage += "; " + StackTraceFormatter.GetExceptionStackTrace(ex);

                    OnErrorEvent(errorMessage);

                    if (ex.Message.StartsWith("Could not find stored procedure " + spCmd.CommandText))
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
                        var debugMessage = "SP execution time: " + DateTime.UtcNow.Subtract(startTime).TotalSeconds.ToString("##0.000") + " seconds for SP " + spCmd.CommandText;
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
                errorMessage += " executing SP " + spCmd.CommandText;

                OnErrorEvent(errorMessage);

                if (deadlockOccurred)
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
