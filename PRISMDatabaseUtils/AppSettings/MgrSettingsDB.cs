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
        /// Determine the expected path to the pgpass.conf file (or the .pgpass file if on Linux)
        /// </summary>
        /// <returns>FileInfo instance</returns>
        private static FileInfo GetPgPassFile()
        {
            if (SystemInfo.IsLinux)
            {
                // Standard location: ~/.pgpass
                return new FileInfo(Path.Combine("~", ".pgpass"));
            }

            // ReSharper disable once CommentTypo
            // Standard location: %APPDATA%\postgresql\pgpass.conf
            var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            return new FileInfo(Path.Combine(appDataDirectory, "postgresql", "pgpass.conf"));
        }

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

                var defaultErrorMessage = string.Format("MgrSettings.LoadMgrSettingsFromDBWork: Excessive failures attempting to retrieve manager settings from database for manager {0}", managerName);

                if (dbTools.DbServerType == DbServerTypes.PostgreSQL)
                {
                    if (RecentErrorMessage.IndexOf("No password has been provided but the backend requires one", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var pgPassFile = GetPgPassFile();

                        ErrMsg = string.Format("MgrSettings.LoadMgrSettingsFromDBWork: " +
                                               "user specified in the connection string is not defined in the .pgpass file for the user running this manager; " +
                                               "update file {0}; {1}", pgPassFile.FullName, RecentErrorMessage);
                    }
                    else if (RecentErrorMessage.IndexOf("LDAP authentication failed for user", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ErrMsg = string.Format("MgrSettings.LoadMgrSettingsFromDBWork: user specified in the connection string is not defined in the pg_hba.conf file on the PostgreSQL server; {0}", RecentErrorMessage);
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
                ErrMsg = string.Format("MgrSettings.LoadMgrSettingsFromDBWork; Manager '{0}' is not defined in the manager control database; using {1}",
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
        /// <param name="configFileSettings"></param>
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

                var pgPassFile = GetPgPassFile();

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
