namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Constants for use by classes implementing or using <see cref="IDBTools"/>
    /// </summary>
    public class DbUtilsConstants
    {
        /// <summary>
        /// Return value indicating everything is ok
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
    }
}
