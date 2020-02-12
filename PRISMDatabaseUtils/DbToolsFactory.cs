using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Npgsql;
using PRISMDatabaseUtils.MSSQLServer;
using PRISMDatabaseUtils.PostgresSQL;

namespace PRISMDatabaseUtils
{
    /// <summary>
    /// Factory class for creating Database interaction objects
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
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
        /// Set to true once mConnectionStringKeywordMap has been initialized
        /// </summary>
        private static bool mConnectionStringKeywordMapInitialized;

        private static readonly Regex mDbServerTypeMatcher = new
            Regex(@"DbServerType\s*=(?<ServerType>[a-z]+)\s*;?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Map between Regex matchers and the server type for each Regex
        /// </summary>
        private static readonly List<KeyValuePair<Regex, DbServerTypes>> mConnectionStringKeywordMap = new List<KeyValuePair<Regex, DbServerTypes>>();

        /// <summary>
        /// Checks elements in the connection string to determine which database engine it refers to.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static DbServerTypes GetServerTypeFromConnectionString(string connectionString)
        {

            // Example SQL Server connection string:
            // "Data Source=MyServer;Initial Catalog=MyDatabase;integrated security=SSPI"

            // Example PostgreSQL connection string:
            // "Host=MyServer;Username=MyUser;Password=pass;Database=MyDatabase"

            if (!mConnectionStringKeywordMapInitialized)
            {
                InitializeConnectionStringKeywordMap();
            }

            foreach (var keywordInfo in mConnectionStringKeywordMap)
            {
                var match = keywordInfo.Key.Match(connectionString);

                if (!match.Success)
                {
                    continue;
                }

                if (keywordInfo.Value != DbServerTypes.Undefined)
                    return keywordInfo.Value;

                // Successful match to DbServerType=...

                // Assume DbServerType=SqlServer
                // Assume DbServerType=MSSqlServer
                // or     DbServerType=Postgres
                // or     DbServerType=PostgreSQL

                var serverType = match.Groups["ServerType"].Value;

                if (serverType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
                    serverType.Equals("MSSqlServer", StringComparison.OrdinalIgnoreCase))
                {
                    return DbServerTypes.MSSQLServer;
                }

                if (serverType.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                    serverType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    return DbServerTypes.PostgresSQL;
                }

                throw new Exception(string.Format("Invalid value for {0}; use SqlServer, MSSqlServer, Postgres, or PostgreSQL", match.Value));

            }

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                if (!string.IsNullOrWhiteSpace(builder.DataSource))
                {
                    return DbServerTypes.MSSQLServer;
                }
            }
            catch
            {
                // Ignore errors here
            }

            try
            {
                var builder = new NpgsqlConnectionStringBuilder();
                if (!string.IsNullOrWhiteSpace(builder.Host))
                {
                    return DbServerTypes.PostgresSQL;
                }
            }
            catch
            {
                // Ignore errors here
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
            var serverType = GetServerTypeFromConnectionString(connectionString);

            // Remove "DbServerType=...;" if present
            var standardConnectionString = mDbServerTypeMatcher.Replace(connectionString, string.Empty);

            switch (serverType)
            {
                case DbServerTypes.PostgresSQL:
                    return new PostgresDBTools(standardConnectionString, timeoutSeconds);

                default:
                    // Includes DbServerTypes.MSSQLServer
                    return new SQLServerDBTools(standardConnectionString, timeoutSeconds);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="serverType"></param>
        /// <param name="connectionString"></param>
        /// <param name="timeoutSeconds"></param>
        /// <returns></returns>
        public static IDBTools GetDBTools(DbServerTypes serverType, string connectionString, int timeoutSeconds = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC)
        {
            switch (serverType)
            {
                case DbServerTypes.Undefined:
                    return GetDBTools(connectionString, timeoutSeconds);

                case DbServerTypes.PostgresSQL:
                    return new PostgresDBTools(connectionString, timeoutSeconds);

                default:
                    // Includes DbServerTypes.MSSQLServer
                    return new SQLServerDBTools(connectionString, timeoutSeconds);
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

        private static void InitializeConnectionStringKeywordMap()
        {
            mConnectionStringKeywordMap.Clear();

            // This is a special case that will be resolved by GetServerTypeFromConnectionString
            // DbServerType is a non-standard connection string keyword, which allows the user to explicitly specify the database server type
            mConnectionStringKeywordMap.Add(new KeyValuePair<Regex, DbServerTypes>(mDbServerTypeMatcher, DbServerTypes.Undefined));

            InitializeKeywordInfo(@"Data Source\s*=", DbServerTypes.MSSQLServer);
            InitializeKeywordInfo(@"Initial Catalog\s*=", DbServerTypes.MSSQLServer);
            InitializeKeywordInfo(@"host\s*=", DbServerTypes.PostgresSQL);
            InitializeKeywordInfo(@"db\s*=", DbServerTypes.PostgresSQL);

            mConnectionStringKeywordMapInitialized = true;
        }

        private static void InitializeKeywordInfo(string matchSpec, DbServerTypes serverType)
        {
            var keywordMatcher = new Regex(matchSpec, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var keywordInfo = new KeyValuePair<Regex, DbServerTypes>(keywordMatcher, serverType);
            mConnectionStringKeywordMap.Add(keywordInfo);
        }

    }
}
