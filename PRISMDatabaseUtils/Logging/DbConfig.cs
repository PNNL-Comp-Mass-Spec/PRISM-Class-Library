using System;
using PRISM.Logging;

// ReSharper disable UnusedMember.Global

namespace PRISMDatabaseUtils.Logging;

/// <summary>
/// This class initializes the database logger in static class PRISM.Logging.LogTools
/// </summary>
public static class DbConfig
{
    // Ignore Spelling: Postgres

    /// <summary>
    /// Initializes the database logger in static class PRISM.Logging.LogTools
    /// </summary>
    /// <remarks>Supports both SQL Server and Postgres connection strings</remarks>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="moduleName">Module name used by logger</param>
    /// <param name="logLevel">Log threshold level</param>
    /// <param name="traceMode">When true, show additional debug messages at the console</param>
    public static void CreateDbLogger(
        string connectionString,
        string moduleName,
        BaseLogger.LogLevels logLevel = BaseLogger.LogLevels.INFO,
        bool traceMode = false)
    {
        var databaseType = DbToolsFactory.GetServerTypeFromConnectionString(connectionString);

        DatabaseLogger dbLogger = databaseType switch
        {
            DbServerTypes.MSSQLServer => new SQLServerDatabaseLogger(),
            DbServerTypes.PostgreSQL => new PostgresDatabaseLogger(),
            _ => throw new Exception("Unsupported database connection string: should be SQL Server or Postgres")
        };

        dbLogger.ChangeConnectionInfo(moduleName, connectionString);

        LogTools.SetDbLogger(dbLogger, logLevel, traceMode);
    }
}
