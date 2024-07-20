using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PRISM;
using PRISM.AppSettings;

// ReSharper disable UnusedMember.Global

namespace PRISMDatabaseUtils.AppSettings
{
    /// <summary>
    /// Supports reading settings from databases
    /// </summary>
    public class MgrSettingsDB : MgrSettings
    {
        // Ignore Spelling: App, Postgres, PostgreSQL, pgpass, Utils

        /// <summary>
        /// Most recent error message reported by the DB Tools Factory
        /// </summary>
        private string RecentErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Determine the expected path to the PgPass file (pgpass.conf on Windows or .pgpass on Linux)
        /// </summary>
        /// <param name="createOrUpdateMessage">Message that starts with "create" or "update" and includes the file path</param>
        /// <returns>FileInfo instance</returns>
        private static FileInfo GetPgPassFile(out string createOrUpdateMessage)
        {
            FileInfo pgPassFile;

            if (SystemInfo.IsLinux)
            {
                // Standard location: ~/.pgpass
                pgPassFile = new FileInfo(Path.Combine("~", ".pgpass"));
            }
            else
            {
                // ReSharper disable once CommentTypo
                // Standard location: %APPDATA%\postgresql\pgpass.conf
                var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                pgPassFile = new FileInfo(Path.Combine(appDataDirectory, "postgresql", "pgpass.conf"));
            }

            // Generate a message similar to either of these two messages:
            // - create file C:\Users\svc-dms\AppData\Roaming\postgresql\pgpass.conf
            // - update file C:\Users\svc-dms\AppData\Roaming\postgresql\pgpass.conf

            createOrUpdateMessage = string.Format("{0} file {1}", pgPassFile.Exists ? "update" : "create", pgPassFile.FullName);

            return pgPassFile; }

        /// <summary>
        /// Load manager settings from the database
        /// </summary>
        /// <param name="managerName">Manager name or manager group name</param>
        /// <param name="managerNameForConnectionString">Manager name to include in the database connection string</param>
        /// <param name="mgrSettingsFromDB">Output: manager settings</param>
        /// <param name="logConnectionErrors">When true, log connection errors</param>
        /// <param name="returnErrorIfNoParameters">When true, return an error if no parameters defined</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <returns>True if successful, otherwise false</returns>
        protected override bool LoadMgrSettingsFromDBWork(
            string managerName,
            string managerNameForConnectionString,
            out Dictionary<string, string> mgrSettingsFromDB,
            bool logConnectionErrors,
            bool returnErrorIfNoParameters,
            int retryCount = 3)
        {
            mgrSettingsFromDB = new Dictionary<string, string>();

            var dbConnectionString = GetParam(MGR_PARAM_MGR_CFG_DB_CONN_STRING, string.Empty);

            if (string.IsNullOrEmpty(dbConnectionString))
            {
                // MgrCnfgDbConnectStr parameter not defined in the AppName.exe.config file
                HandleParameterNotDefined(MGR_PARAM_MGR_CFG_DB_CONN_STRING);
                return false;
            }

            if (string.IsNullOrWhiteSpace(managerNameForConnectionString))
            {
                managerNameForConnectionString = managerName;
            }

            var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(dbConnectionString, managerNameForConnectionString);

            ShowTrace("LoadMgrSettingsFromDBWork using {0} for manager {1}", connectionStringToUse, managerName);

            var sqlQuery = "SELECT parameter_name, parameter_value " +
                           "FROM " + SchemaPrefixes[SchemaPrefix.ManagerControl] + "V_Mgr_Params " +
                           "WHERE manager_name = '" + managerName + "'";

            // Query the database
            var dbTools = DbToolsFactory.GetDBTools(connectionStringToUse);

            dbTools.ErrorEvent += OnDbToolsErrorEvent;

            if (logConnectionErrors)
            {
                RegisterEvents(dbTools);
            }

            var success = dbTools.GetQueryResults(sqlQuery, out var queryResults, retryCount);

            if (!success)
            {
                // Log the message to the DB if the monthly Windows updates are not pending
                var criticalError = !WindowsUpdateStatus.ServerUpdatesArePending();

                var defaultErrorMessage = string.Format(
                    "LoadMgrSettingsFromDBWork: Excessive failures attempting to retrieve manager settings from the database for manager {0}",
                    managerName);

                if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
                {
                    if (RecentErrorMessage.IndexOf("database", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        RecentErrorMessage.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ErrMsg = string.Format(
                            "LoadMgrSettingsFromDBWork: the database specified in the connection string is invalid; {0}", RecentErrorMessage);
                    }
                    else if (RecentErrorMessage.IndexOf("No such host is known", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ErrMsg = string.Format(
                            "LoadMgrSettingsFromDBWork: the host specified in the connection string is invalid; {0}", RecentErrorMessage);
                    }
                    else if (RecentErrorMessage.IndexOf("LDAP authentication failed", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ErrMsg = string.Format(
                            "LoadMgrSettingsFromDBWork: the user specified in the connection string is not defined in the pg_hba.conf file " +
                            "on the PostgreSQL server; {0}", RecentErrorMessage);
                    }
                    else if (RecentErrorMessage.IndexOf("No password has been provided but the backend requires one", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        GetPgPassFile(out var createOrUpdateMessage);

                        // ReSharper disable once UseStringInterpolation
                        ErrMsg = string.Format(
                            "LoadMgrSettingsFromDBWork: the user specified in the connection string is not defined in the PgPass file; " +
                            "{0}; {1}", createOrUpdateMessage, RecentErrorMessage);
                    }
                    else if (RecentErrorMessage.IndexOf("password authentication failed", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        GetPgPassFile(out var createOrUpdateMessage);

                        // ReSharper disable once UseStringInterpolation
                        ErrMsg = string.Format(
                            "LoadMgrSettingsFromDBWork: the PgPass file has the wrong password for the user specified in the connection string; " +
                            "{0}; {1}", createOrUpdateMessage, RecentErrorMessage);
                    }
                    else
                    {
                        ErrMsg = defaultErrorMessage;
                    }
                }
                else
                {
                    ErrMsg = defaultErrorMessage;
                }

                if (logConnectionErrors)
                    ReportError(ErrMsg, criticalError);

                return false;
            }

            // Verify at least one row returned
            if (queryResults.Count < 1 && returnErrorIfNoParameters)
            {
                // Wrong number of rows returned
                ErrMsg = string.Format("LoadMgrSettingsFromDBWork; Manager '{0}' is not defined in the manager control database; using {1}",
                                       managerName, connectionStringToUse);
                ReportError(ErrMsg);
                return false;
            }

            foreach (var item in queryResults)
            {
                mgrSettingsFromDB.Add(item[0], item[1]);
            }

            return true;
        }

        /// <summary>Report an error</summary>
        /// <param name="message">Error message</param>
        /// <param name="ex">Exception (allowed to be null)</param>
        private void OnDbToolsErrorEvent(string message, Exception ex)
        {
            RecentErrorMessage = string.Format("{0}{1}",
                message,
                ex == null ? string.Empty : string.Format("; {0}", ex.Message));
        }

        /// <summary>
        /// Examine the settings in MgrParams to look for parameters MgrCnfgDbConnectStr and/or DefaultDMSConnString
        /// If defined, and if pointing to a PostgreSQL server, look for a pgpass file for the current user
        /// </summary>
        public void ValidatePgPass()
        {
            ValidatePgPass(MgrParams);
        }

        /// <summary>
        /// Examine configFileSettings to look for parameters MgrCnfgDbConnectStr and/or DefaultDMSConnString
        /// If defined, and if pointing to a PostgreSQL server, look for a pgpass file for the current user
        /// </summary>
        /// <param name="configFileSettings">Dictionary of settings loaded from the config file</param>
        public void ValidatePgPass(IReadOnlyDictionary<string, string> configFileSettings)
        {
            var connectionStringSettingNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                MGR_PARAM_MGR_CFG_DB_CONN_STRING,
                DEFAULT_DMS_CONN_STRING
            };

            ValidatePgPass(configFileSettings, connectionStringSettingNames);
        }

        /// <summary>
        /// Examine configFileSettings to look for parameters in connectionStringParameterNames
        /// If defined, and if pointing to a PostgreSQL server, look for a pgpass file for the current user
        /// </summary>
        /// <param name="configFileSettings">Dictionary with config file settings</param>
        /// <param name="connectionStringParameterNames">Connection string parameter names</param>
        public void ValidatePgPass(IReadOnlyDictionary<string, string> configFileSettings, SortedSet<string> connectionStringParameterNames)
        {
            // This is used to look for Integrated Security=true
            var integratedSecurityMatcher = new Regex(@"Integrated Security\s*=\s*true", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            foreach (var settingName in connectionStringParameterNames)
            {
                if (!configFileSettings.TryGetValue(settingName, out var connectionString))
                    continue;

                var serverType = DbToolsFactory.GetServerTypeFromConnectionString(connectionString);

                if (serverType != DbServerTypes.PostgreSQL)
                    continue;

                if (integratedSecurityMatcher.IsMatch(connectionString))
                {
                    // Will connect to the PostgreSQL server using integrity security; do not look for a pgpass file
                    return;
                }

                var pgPassFile = GetPgPassFile(out var _);

                if (pgPassFile.Exists)
                {
                    Console.WriteLine("Postgres user password will be read from " + pgPassFile.FullName);
                    return;
                }

                OnWarningEvent("Postgres pgpass file not found; expected to find it at " + pgPassFile.FullName);
                return;
            }
        }
    }
}
