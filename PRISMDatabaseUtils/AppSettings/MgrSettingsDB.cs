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
        // Ignore Spelling: Postgres, PostgreSQL, pgpass

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
                managerNameForConnectionString = managerName;

            var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(dbConnectionString, managerNameForConnectionString);

            ShowTrace(string.Format("LoadMgrSettingsFromDBWork using {0} for manager {1}", connectionStringToUse, managerName));

            var sqlQuery = "SELECT parameter_name, parameter_value FROM V_Mgr_Params WHERE manager_name = '" + managerName + "'";

            // Query the database
            var dbTools = DbToolsFactory.GetDBTools(connectionStringToUse);

            if (logConnectionErrors)
            {
                RegisterEvents(dbTools);
            }

            var success = dbTools.GetQueryResults(sqlQuery, out var queryResults, retryCount);

            if (!success)
            {
                // Log the message to the DB if the monthly Windows updates are not pending
                var criticalError = !WindowsUpdateStatus.ServerUpdatesArePending();

                ErrMsg = "MgrSettings.LoadMgrSettingsFromDBWork; Excessive failures attempting to retrieve manager settings from database for manager " + managerName;
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
        /// <param name="configFileSettings"></param>
        /// <param name="connectionStringParameterNames"></param>
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

                FileInfo pgPassFile;
                if (SystemInfo.IsLinux)
                {
                    // Standard location: ~/.pgpass
                    var passwordFileName = ".pgpass";
                    pgPassFile = new FileInfo(Path.Combine("~", passwordFileName));
                }
                else
                {
                    // ReSharper disable once CommentTypo
                    // Standard location: %APPDATA%\postgresql\pgpass.conf
                    var passwordFileName = "pgpass.conf";
                    var appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    pgPassFile = new FileInfo(Path.Combine(appDataDirectory, "postgresql", passwordFileName));
                }

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
