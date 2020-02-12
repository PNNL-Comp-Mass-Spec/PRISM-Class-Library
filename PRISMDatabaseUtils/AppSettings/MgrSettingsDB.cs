using System.Collections.Generic;
using PRISM;
using PRISM.AppSettings;

namespace PRISMDatabaseUtils.AppSettings
{
    /// <summary>
    /// Supports reading settings from databases
    /// </summary>
    public class MgrSettingsDB : MgrSettings
    {
        /// <summary>
        /// Load manager settings from the database
        /// </summary>
        /// <param name="managerName">Manager name or manager group name</param>
        /// <param name="mgrSettingsFromDB">Output: manager settings</param>
        /// <param name="logConnectionErrors">When true, log connection errors</param>
        /// <param name="returnErrorIfNoParameters">When true, return an error if no parameters defined</param>
        /// <param name="retryCount">Number of times to retry (in case of a problem)</param>
        /// <returns>True if successful, otherwise false</returns>
        protected override bool LoadMgrSettingsFromDBWork(
            string managerName,
            out Dictionary<string, string> mgrSettingsFromDB,
            bool logConnectionErrors,
            bool returnErrorIfNoParameters,
            int retryCount = 3)
        {
            mgrSettingsFromDB = new Dictionary<string, string>();

            var dbConnectionString = GetParam(MGR_PARAM_MGR_CFG_DB_CONN_STRING, string.Empty);

            if (string.IsNullOrEmpty(dbConnectionString))
            {
                // MgrCnfgDbConnectStr parameter not defined defined in the AppName.exe.config file
                HandleParameterNotDefined(MGR_PARAM_MGR_CFG_DB_CONN_STRING);
                return false;
            }

            ShowTrace("LoadMgrSettingsFromDBWork using [" + dbConnectionString + "] for manager " + managerName);

            var sqlQuery = "SELECT ParameterName, ParameterValue FROM V_MgrParams WHERE ManagerName = '" + managerName + "'";

            // Query the database
            var dbTools = DbToolsFactory.GetDBTools(dbConnectionString);

            if (logConnectionErrors)
            {
                RegisterEvents(dbTools);
            }

            var success = dbTools.GetQueryResults(sqlQuery, out var queryResults, retryCount);

            if (!success)
            {
                // Log the message to the DB if the monthly Windows updates are not pending
                var criticalError = !WindowsUpdateStatus.ServerUpdatesArePending();

                ErrMsg = "MgrSettings.LoadMgrSettingsFromDB; Excessive failures attempting to retrieve manager settings from database for manager " + managerName;
                if (logConnectionErrors)
                    ReportError(ErrMsg, criticalError);

                return false;
            }

            // Verify at least one row returned
            if (queryResults.Count < 1 && returnErrorIfNoParameters)
            {
                // Wrong number of rows returned
                ErrMsg = string.Format("MgrSettings.LoadMgrSettingsFromDB; Manager '{0}' is not defined in the manager control database; using {1}",
                                       managerName, dbConnectionString);
                ReportError(ErrMsg);
                return false;
            }

            foreach (var item in queryResults)
            {
                mgrSettingsFromDB.Add(item[0], item[1]);
            }

            return true;
        }
    }
}
