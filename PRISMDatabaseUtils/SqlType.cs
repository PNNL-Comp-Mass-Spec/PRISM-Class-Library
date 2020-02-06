namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Commonly used SQL database types; converted by implementations of IExecuteSP to the database-specific types
    /// </summary>
    /// <remarks>This is used instead of System.Data.DbType to allow the commonly-used names with understandable behavior, instead of just Int32/String/etc., and things like 'Time' that maps to 'DateTime' and no option for only a 'Date'</remarks>
    public enum SqlType
    {
        /// <summary>
        /// 4-byte int
        /// </summary>
        Int,
        /// <summary>
        /// 8-byte int
        /// </summary>
        BigInt,
        /// <summary>
        /// 4-byte floating point value
        /// </summary>
        Real,
        /// <summary>
        /// 8-byte floating point value
        /// </summary>
        Float,
        /// <summary>
        /// 1-byte int
        /// </summary>
        TinyInt,
        /// <summary>
        /// 2-byte int
        /// </summary>
        SmallInt,
        /// <summary>
        /// fixed-length string (with size)
        /// </summary>
        Char,
        /// <summary>
        /// variable-length string, with optional size limit
        /// </summary>
        VarChar,
        /// <summary>
        /// variable-length string
        /// </summary>
        Text,
        /// <summary>
        /// Date
        /// </summary>
        Date,
        /// <summary>
        /// Date and Time
        /// </summary>
        DateTime,
        /// <summary>
        /// 4-byte int
        /// </summary>
        Xml,

    }
}
