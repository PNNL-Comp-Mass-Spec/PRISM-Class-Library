namespace PRISMDatabaseUtils
{
    // Ignore Spelling: cmd, arg, sql, Npgsql, PostgreSQL

    /// <summary>
    /// Commonly used SQL database types; converted by implementations of IExecuteSP to the database-specific types
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enum is used instead of System.Data.DbType to allow for use of commonly-used database types with understandable behavior, instead of just Int32/String/etc.
    /// This also allows us to differentiate between Time, Date, and DateTime.
    /// </para>
    /// <para>
    /// If you need to access a data type not listed here, use this design pattern:
    /// </para>
    /// <para>
    ///   var newParam = dbTools.AddParameter(cmd, argName, SqlType.Real);
    ///   if (newParam is NpgsqlParameter sqlParam)
    ///   {
    ///       sqlParam.NpgsqlDbType = NpgsqlDbType.Range;
    ///   }
    /// </para>
    /// </remarks>
    public enum SqlType
    {
        /// <summary>
        /// In SQL Server, values in a bit column can be 0, 1, or Null
        /// In PostgreSQL, values in a bit column hold a string of 1's and 0's
        /// </summary>
        Bit,

        /// <summary>
        /// 1-byte state of true or false
        /// Specific to PostgreSQL
        /// </summary>
        Boolean,

        /// <summary>
        /// 1-byte integer
        /// </summary>
        TinyInt,

        /// <summary>
        /// 2-byte integer
        /// </summary>
        SmallInt,

        /// <summary>
        /// 4-byte integer
        /// </summary>
        Int,

        /// <summary>
        /// 4-byte integer
        /// </summary>
        Integer = Int,

        /// <summary>
        /// 8-byte integer
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
        /// 8-byte floating point value
        /// The name Double is specific to PostgreSQL
        /// </summary>
        Double = Float,

        /// <summary>
        /// Floating point number with a specific precision and scale
        /// </summary>
        Decimal,

        /// <summary>
        /// Floating point number with a specific precision and scale
        /// Synonymous with Decimal
        /// </summary>
        Numeric = Decimal,

        /// <summary>
        /// 8-byte floating point value
        /// </summary>
        Money,

        /// <summary>
        /// Fixed-length string (with size)
        /// </summary>
        Char,

        /// <summary>
        /// Variable-length string, with optional size limit
        /// </summary>
        VarChar,

        /// <summary>
        /// Variable-length string
        /// </summary>
        Text,

        /// <summary>
        /// Case-insensitive text
        /// Specific to PostgreSQL
        /// </summary>
        Citext,

        /// <summary>
        /// Object name
        /// Specific to PostgreSQL
        /// </summary>
        Name,

        /// <summary>
        /// Date (no time of day)
        /// 4-bytes
        /// </summary>
        Date,

        /// <summary>
        /// Time of day (no date)
        /// 8 bytes
        /// </summary>
        Time,

        /// <summary>
        /// Date and Time
        /// </summary>
        DateTime,

        /// <summary>
        /// Date and time
        /// Specific to PostgreSQL
        /// </summary>
        Timestamp = DateTime,

        /// <summary>
        /// Date and Time, with timezone
        /// Corresponds to DateTimeOffset in SQL Server
        /// </summary>
        TimestampTz,

        /// <summary>
        /// Time interval
        /// Specific to PostgreSQL
        /// </summary>
        Interval,

        /// <summary>
        /// Universally Unique Identifier (aka GUID)
        /// 16 bytes
        /// </summary>
        UUID,

        /// <summary>
        /// Used to store XML (Extensible Markup Language) data
        /// </summary>
        XML,

        /// <summary>
        /// Used to store JSON (JavaScript Object Notation) data
        /// Specific to PostgreSQL
        /// </summary>
        JSON
    }
}
