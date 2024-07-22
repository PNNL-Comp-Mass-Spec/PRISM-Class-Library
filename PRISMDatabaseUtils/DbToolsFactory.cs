using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Npgsql;
using PRISMDatabaseUtils.MSSQLServer;
using PRISMDatabaseUtils.PostgreSQL;

// ReSharper disable UnusedMember.Global

namespace PRISMDatabaseUtils
{
    // Ignore Spelling: dms, gigasax, proto, Utils, svc

    /// <summary>
    /// Enum of supported relational database systems
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
        /// Postgres
        /// </summary>
        PostgreSQL,
    }

    /// <summary>
    /// Factory class for creating Database interaction objects
    /// </summary>
    public static class DbToolsFactory
    {
        // Ignore Spelling: pgpass, Postgres, PostgreSQL, Sql, Username, Utils

        /// <summary>
        /// Set to true once mConnectionStringKeywordMap has been initialized
        /// </summary>
        private static bool mConnectionStringKeywordMapInitialized;

        private static readonly Regex mDbServerTypeMatcher = new (@"DbServerType\s*=(?<ServerType>[a-z]+)\s*;?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Map between RegEx matchers and the server type for each RegEx
        /// </summary>
        private static readonly List<KeyValuePair<Regex, DbServerTypes>> mConnectionStringKeywordMap = new();

        /// <summary>
        /// Add / update the application name in a connection string
        /// </summary>
        /// <remarks>
        /// <para>
        /// Example input connection strings:
        ///   Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=True
        ///   Host=prismdb1;Port=5432;Database=dms;UserId=svc-dms
        ///   Host=prismdb1;Port=5432;Database=dms;Username=svc-dms
        /// </para>
        /// <para>
        /// Example return values:
        ///   Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=True;Application Name=Proto-6_DIM
        ///   Host=prismdb1;Port=5432;Database=dms;Username=svc-dms;Application Name=Proto-6_DIM
        /// </para>
        /// </remarks>
        /// <param name="connectionString">Connection string</param>
        /// <param name="applicationName">Application name</param>
        /// <param name="serverType">If undefined, this method will auto-determine the connection string type</param>
        /// <returns>Updated connection string, with the application name appended (if not an empty string)</returns>
        public static string AddApplicationNameToConnectionString(string connectionString, string applicationName, DbServerTypes serverType = DbServerTypes.Undefined)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
                return connectionString;

            if (serverType == DbServerTypes.Undefined)
            {
                serverType = GetServerTypeFromConnectionString(connectionString);
            }

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (serverType)
            {
                case DbServerTypes.MSSQLServer:
                    {
                        try
                        {
                            var builder = new SqlConnectionStringBuilder(connectionString)
                            {
                                ApplicationName = applicationName
                            };

                            return builder.ConnectionString;
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(string.Format(
                                "Invalid connection string: [{0}]; SqlConnectionStringBuilder reports {1}",
                                connectionString, ex.Message), ex);
                        }
                    }

                case DbServerTypes.PostgreSQL:
                    {
                        try
                        {
                            var builder = new NpgsqlConnectionStringBuilder(connectionString)
                            {
                                ApplicationName = applicationName
                            };

                            return builder.ConnectionString;
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(string.Format(
                                "Invalid connection string: [{0}]; NpgsqlConnectionStringBuilder reports {1}",
                                connectionString, ex.Message), ex);
                        }
                    }

                default:
                    throw new Exception("GetServerTypeFromConnectionString was unable to determine the server type for the connection string");
            }
        }

        /// <summary>
        /// Get a connection string for the given database and server, using integrated authentication
        /// </summary>
        public static string GetConnectionString(DbServerTypes serverType, string serverName, string databaseName, string applicationName = "")
        {
            return GetConnectionString(serverType, serverName, databaseName, string.Empty, string.Empty, applicationName);
        }

        /// <summary>
        /// Get a connection string for the given database and server, using the specified username
        /// </summary>
        /// <remarks>See method ValidatePgPass in class MgrSettingsDB for info on pgpass files</remarks>
        /// <param name="serverType">Server type (DbServerTypes.MSSQLServer or DbServerTypes.PostgreSQL)</param>
        /// <param name="serverName">Server name</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="userName">If this is an empty string, sets IntegratedSecurity to true</param>
        /// <param name="password">For PostgreSQL, this can be an empty string, provided the username is defined in a .pgpass or pgpass.conf file</param>
        /// <param name="applicationName">Application name</param>
        /// <param name="useIntegratedSecurity">
        /// <para>Value to use for the "Integrated Security" setting (only applicable for SQL Server)</para>
        /// <para>If null, auto-determine based on whether userName is defined</para>
        /// <para>If password is not an empty string, useIntegratedSecurity will be set to false by this method</para>
        /// </param>
        /// <returns>Connection string</returns>
        public static string GetConnectionString(
            DbServerTypes serverType,
            string serverName,
            string databaseName,
            string userName,
            string password,
            string applicationName = "",
            bool? useIntegratedSecurity = null)
        {
            // Example SQL Server connection string:
            // "Data Source=MyServer;Initial Catalog=MyDatabase;Integrated Security=SSPI;Application Name=Analysis Manager"
            // "Data Source=MyServer;Initial Catalog=MyDatabase;Integrated Security=False;User ID=MyUser;Password=pass;Application Name=Analysis Manager"

            // Example PostgreSQL connection strings:
            // "Host=MyServer;Username=MyUser;Password=pass;Database=MyDatabase"
            // "DbServerType=Postgres;Server=MyServer;Username=MyUser;Password=pass;Database=MyDatabase;Application Name=Analysis Manager"

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (serverType)
            {
                case DbServerTypes.MSSQLServer:
                    {
                        var builder = new SqlConnectionStringBuilder
                        {
                            DataSource = serverName
                        };

                        if (!string.IsNullOrWhiteSpace(databaseName))
                        {
                            builder.InitialCatalog = databaseName;
                        }

                        if (string.IsNullOrWhiteSpace(userName))
                        {
                            builder.IntegratedSecurity = true;
                        }
                        else
                        {
                            builder.UserID = userName;

                            if (string.IsNullOrWhiteSpace(password))
                            {
                                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                                if (useIntegratedSecurity.HasValue)
                                    builder.IntegratedSecurity = useIntegratedSecurity.Value;
                                else
                                    builder.IntegratedSecurity = true;
                            }
                            else
                            {
                                builder.IntegratedSecurity = false;
                                builder.Password = password;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(applicationName))
                        {
                            builder.ApplicationName = applicationName;
                        }

                        return builder.ConnectionString;
                    }

                case DbServerTypes.PostgreSQL:
                    {
                        var builder = new NpgsqlConnectionStringBuilder
                        {
                            Host = serverName
                        };

                        if (!string.IsNullOrWhiteSpace(databaseName))
                        {
                            builder.Database = databaseName;
                        }

                        if (!string.IsNullOrWhiteSpace(userName))
                        {
                            builder.Username = userName;

                            if (!string.IsNullOrWhiteSpace(password))
                            {
                                builder.Password = password;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(applicationName))
                        {
                            builder.ApplicationName = applicationName;
                        }

                        return builder.ConnectionString;
                    }

                default:
                    throw new InvalidEnumArgumentException(nameof(serverType));
            }
        }

        /// <summary>
        /// Get a SQL Server or PostgreSQL DBTools instance, depending on the contents of the connection string
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="timeoutSeconds">Query timeout, in seconds</param>
        /// <param name="debugMode">When true, show queries and procedure calls using OnDebugEvent</param>
        public static IDBTools GetDBTools(
            string connectionString,
            int timeoutSeconds = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC,
            bool debugMode = false)
        {
            var serverType = GetServerTypeFromConnectionString(connectionString);

            // Remove "DbServerType=...;" if present
            var standardConnectionString = mDbServerTypeMatcher.Replace(connectionString, string.Empty);

            return serverType switch
            {
                DbServerTypes.PostgreSQL => new PostgresDBTools(standardConnectionString, timeoutSeconds, debugMode),
                _ => new SQLServerDBTools(standardConnectionString, timeoutSeconds, debugMode)      // Includes DbServerTypes.MSSQLServer
            };
        }

        /// <summary>
        /// Get a SQL Server or PostgreSQL DBTools instance, as specified by serverType
        /// </summary>
        /// <param name="serverType">Server type (DbServerTypes.MSSQLServer or DbServerTypes.PostgreSQL)</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="timeoutSeconds">Query timeout, in seconds</param>
        /// <param name="debugMode">When true, show queries and procedure calls using OnDebugEvent</param>
        public static IDBTools GetDBTools(
            DbServerTypes serverType,
            string connectionString,
            int timeoutSeconds = DbUtilsConstants.DEFAULT_SP_TIMEOUT_SEC,
            bool debugMode = false)
        {
            return serverType switch
            {
                DbServerTypes.Undefined => GetDBTools(connectionString, timeoutSeconds, debugMode),
                DbServerTypes.PostgreSQL => new PostgresDBTools(connectionString, timeoutSeconds, debugMode),
                _ => new SQLServerDBTools(connectionString, timeoutSeconds, debugMode)
            };
        }

        /// <summary>
        /// Get the preferred SQL type for a .NET type
        /// </summary>
        /// <param name="type">.NET type</param>
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

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (type == typeof(string))
                return SqlType.VarChar;

            return SqlType.VarChar;
        }

        /// <summary>
        /// Checks elements in the connection string to determine which database engine it refers to
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public static DbServerTypes GetServerTypeFromConnectionString(string connectionString)
        {
            // Example SQL Server connection string:
            // "Data Source=MyServer;Initial Catalog=MyDatabase;Integrated Security=SSPI;Application Name=Analysis Manager"

            // Example PostgreSQL connection strings:
            // "Host=MyServer;Username=MyUser;Password=pass;Database=MyDatabase"
            // "DbServerType=Postgres;Server=MyServer;Username=MyUser;Password=pass;Database=MyDatabase;Application Name=Analysis Manager"

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
                    return DbServerTypes.PostgreSQL;
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
                    return DbServerTypes.PostgreSQL;
                }
            }
            catch
            {
                // Ignore errors here
            }

            // Assume SQL Server
            return DbServerTypes.MSSQLServer;
        }

        private static void InitializeConnectionStringKeywordMap()
        {
            mConnectionStringKeywordMap.Clear();

            // This is a special case that will be resolved by GetServerTypeFromConnectionString
            // DbServerType is a non-standard connection string keyword, which allows the user to explicitly specify the database server type
            mConnectionStringKeywordMap.Add(new KeyValuePair<Regex, DbServerTypes>(mDbServerTypeMatcher, DbServerTypes.Undefined));
            // Microsoft.Data.SqlClient.SqlConnection connection string keywords
            // Note that "Database=DbName" is also supported by SqlClient.SqlConnection, but we assume PostgreSQL
            InitializeKeywordInfo(@"\bData Source\s*=", DbServerTypes.MSSQLServer);
            InitializeKeywordInfo(@"\bInitial Catalog\s*=", DbServerTypes.MSSQLServer);
            InitializeKeywordInfo(@"\bIntegrated Security\s*=", DbServerTypes.MSSQLServer);

            // Npgsql connection string keywords
            InitializeKeywordInfo(@"\bHost\s*=", DbServerTypes.PostgreSQL);
            InitializeKeywordInfo(@"\bPort\s*=", DbServerTypes.PostgreSQL);
            InitializeKeywordInfo(@"\bDatabase\s*=", DbServerTypes.PostgreSQL);


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
