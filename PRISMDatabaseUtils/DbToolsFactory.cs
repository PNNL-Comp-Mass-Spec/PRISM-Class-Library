using System;
using PRISMDatabaseUtils.PostgresSQL;

namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Factory class for creating Database interaction objects
    /// </summary>
    public static class DbToolsFactory
    {
        /// <summary>
        /// Enum of supported database server systems
        /// </summary>
        public enum DbServerTypes
        {
            /// <summary>
            /// Undefined server type
            /// </summary>
            Undefined,

            /// <summary>
            /// Microsoft SQL Server
            /// </summary>
            MSSQLServer,

            /// <summary>
            /// Postgres SQL
            /// </summary>
            PostgresSQL,
        }

        /// <summary>
        /// Checks elements in the connection string to determine which database engine it refers to.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static DbServerTypes GetServerTypeFromConnectionString(string connectionString)
        {
            if (connStr.IndexOf("Data Source", StringComparison.OrdinalIgnoreCase) > -1 || connStr.IndexOf("Integrated Security", StringComparison.OrdinalIgnoreCase) > -1 || connStr.IndexOf("Initial Catalog", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return DbServerTypes.MSSQLServer;
            }

            if (connStr.IndexOf("host", StringComparison.OrdinalIgnoreCase) > -1 || connStr.IndexOf("hostaddr", StringComparison.OrdinalIgnoreCase) > -1 || connStr.IndexOf("dbname", StringComparison.OrdinalIgnoreCase) > -1)
            {
                return DbServerTypes.PostgresSQL;
            }

            return DbServerTypes.MSSQLServer;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="timeoutSeconds"></param>
        /// <returns></returns>
        public static IDBTools GetDBTools(string connectionString, int timeoutSeconds = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC)
        {
            switch (GetServerTypeFromConnectionString(connStr))
            {
                case DbServerTypes.PostgresSQL:
                    return new PostgresDBTools(connStr, timeoutSeconds);
                case DbServerTypes.MSSQLServer:
                default:
                    return new SQLServerDBTools(connStr, timeoutSeconds);
            }
        }

        /// <summary>
        /// Get the preferred SQL type for a .NET type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static SqlType GetPreferredSqlTypeForType(Type type)
        {
            if (type == typeof(int))
                return SqlType.Int;
            if (type == typeof(long))
                return SqlType.BigInt;
            if (type == typeof(short))
                return SqlType.SmallInt;
            if (type == typeof(float))
                return SqlType.Real;
            if (type == typeof(double))
                return SqlType.Float;
            if (type == typeof(DateTime))
                return SqlType.DateTime;
            if (type == typeof(string))
                return SqlType.VarChar;

            return SqlType.VarChar;
        }
    }
}
