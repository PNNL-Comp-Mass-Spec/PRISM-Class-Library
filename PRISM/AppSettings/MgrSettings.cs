using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using JetBrains.Annotations;
using PRISM.Logging;

// ReSharper disable UnusedMember.Global

namespace PRISM.AppSettings
{
    /// <summary>
    /// Class for loading, storing and accessing manager parameters
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loads initial settings from the local config file (AppName.exe.config)
    /// </para>
    /// <para>
    /// If MgrActive_Local is true, loads additional manager settings
    /// from the manager control database (when using a derived class)
    /// </para>
    /// </remarks>
    public class MgrSettings : EventNotifier
    {
        /// <summary>
        /// Enum for database schema prefix manager parameters
        /// </summary>
        /// <remarks>Settings names are in the format SchemaPrefix.[enum value name], for example SchemaPrefix.DMS</remarks>
        public enum SchemaPrefix
        {
            // ReSharper disable InconsistentNaming
            /// <summary>
            /// DMS main database schema prefix
            /// </summary>
            DMS,

            /// <summary>
            /// DMS analysis pipeline database schema prefix
            /// </summary>
            DMSPipeline,

            /// <summary>
            /// DMS data capture pipeline database schema prefix
            /// </summary>
            DMSCapture,

            /// <summary>
            /// DMS data package database schema prefix
            /// </summary>
            DMSDataPackage,

            /// <summary>
            /// DMS manager control database schema prefix
            /// </summary>
            ManagerControl,

            /// <summary>
            /// DMS protein collection/sequences database schema prefix
            /// </summary>
            ProteinCollection,

            /// <summary>
            /// DMS ontology lookup database schema prefix
            /// </summary>
            Ontology
            // ReSharper restore InconsistentNaming
        }

        /// <summary>
        /// Status message for when the manager is deactivated locally
        /// </summary>
        /// <remarks>Used when MgrActive_Local is False in AppName.exe.config</remarks>
        public const string DEACTIVATED_LOCALLY = "Manager deactivated locally";

        /// <summary>
        /// Manager parameter: manager config database connection string
        /// </summary>
        public const string MGR_PARAM_MGR_CFG_DB_CONN_STRING = "MgrCnfgDbConnectStr";

        /// <summary>
        /// Manager parameter: DMS database connection string
        /// </summary>
        public const string DEFAULT_DMS_CONN_STRING = "DefaultDMSConnString";

        /// <summary>
        /// Manager parameter: manager active
        /// </summary>
        /// <remarks>Defined in AppName.exe.config</remarks>
        public const string MGR_PARAM_MGR_ACTIVE_LOCAL = "MgrActive_Local";

        /// <summary>
        /// Manager parameter: manager name
        /// </summary>
        public const string MGR_PARAM_MGR_NAME = "MgrName";

        /// <summary>
        /// Manager parameter: using defaults flag
        /// </summary>
        public const string MGR_PARAM_USING_DEFAULTS = "UsingDefaults";

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrMsg { get; protected set; } = string.Empty;

        /// <summary>
        /// Manager name
        /// </summary>
        public string ManagerName => GetParam(MGR_PARAM_MGR_NAME, System.Net.Dns.GetHostName() + "_Undefined-Manager");

        /// <summary>
        /// This will be true after the manager settings have been successfully loaded from the manager control database
        /// </summary>
        public bool ParamsLoadedFromDB { get; private set; }

        /// <summary>
        /// Dictionary of manager parameters
        /// </summary>
        public Dictionary<string, string> MgrParams { get; }

        /// <summary>
        /// Schema prefix value dictionary. Subset of <see cref="MgrParams"/>
        /// </summary>
        public IReadOnlyDictionary<SchemaPrefix, string> SchemaPrefixes => schemaPrefixes;

        /// <summary>
        /// When true, show additional messages at the console
        /// </summary>
        public bool TraceMode { get; set; }

        /// <summary>
        /// Important error event (only raised if ParamsLoadedFromDB is false)
        /// </summary>
        public ErrorEventEventHandler CriticalErrorEvent;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>Call LoadSettings after instantiating this class</remarks>
        public MgrSettings()
        {
            ParamsLoadedFromDB = false;
            MgrParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private readonly Dictionary<SchemaPrefix, string> schemaPrefixes = new(7);

        /// <summary>
        /// Specifies the full name and path for the application config file
        /// </summary>
        /// <returns>String containing full name and path</returns>
        private static string GetConfigFileName()
        {
            return Path.GetFileName(GetConfigFilePath());
        }

        /// <summary>
        /// Specifies the full name and path for the application config file
        /// </summary>
        /// <returns>String containing full name and path</returns>
        protected static string GetConfigFilePath()
        {
            return AppUtils.GetAppPath() + ".config";
        }

        /// <summary>
        /// Initialize manager settings using the local settings, then load additional settings from the database
        /// </summary>
        /// <param name="localSettings">Manager settings from the AppName.exe.config file or from Properties.Settings.Default</param>
        /// <param name="loadSettingsFromDB">When true, also load settings from the database</param>
        /// <returns>True if successful; False on error</returns>
        public bool LoadSettings(Dictionary<string, string> localSettings, bool loadSettingsFromDB)
        {
            ErrMsg = string.Empty;

            MgrParams.Clear();

            // Copy the settings from localSettings to configFileSettings
            // Assures that the MgrName setting is defined and auto-updates it if it contains $ComputerName$
            var mgrSettingsFromFile = InitializeMgrSettings(localSettings);

            foreach (var item in mgrSettingsFromFile)
            {
                MgrParams.Add(item.Key, item.Value);
            }

            // Auto-add setting ApplicationPath, which is the directory with this applications .exe
            var appPath = AppUtils.GetAppPath();
            var appFile = new FileInfo(appPath);
            SetParam("ApplicationPath", appFile.DirectoryName);

            LoadSchemaPrefixesFromMgrParams();

            // Test the settings retrieved from the config file
            if (!CheckInitialSettings(MgrParams))
            {
                // Error logging was handled by CheckInitialSettings
                if (TraceMode)
                {
                    ShowDictionaryTrace(MgrParams);
                }
                return false;
            }

            if (!loadSettingsFromDB)
            {
                if (TraceMode)
                {
                    ShowDictionaryTrace(MgrParams);
                }
                return true;
            }

            // Get remaining settings from database
            if (!LoadMgrSettingsFromDB())
            {
                // Error logging was handled by LoadMgrSettingsFromDB
                return false;
            }

            // Set flag indicating params have been loaded from the manager control database
            ParamsLoadedFromDB = true;

            if (TraceMode)
            {
                ShowDictionaryTrace(MgrParams);
            }

            // No problems found
            return true;
        }

        private static Dictionary<string, string> InitializeMgrSettings(Dictionary<string, string> localSettings)
        {
            var mgrSettingsFromFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in localSettings)
            {
                mgrSettingsFromFile.Add(item.Key, item.Value);
            }

            // If MgrName value contains the text $ComputerName$, replace it with computer's name
            // This is a case-sensitive comparison
            if (mgrSettingsFromFile.TryGetValue(MGR_PARAM_MGR_NAME, out var mgrName))
            {
                if (mgrName.Contains("$ComputerName$"))
                {
                    mgrSettingsFromFile[MGR_PARAM_MGR_NAME] = mgrName.Replace("$ComputerName$", System.Net.Dns.GetHostName());
                }
            }
            else
            {
                mgrSettingsFromFile.Add(MGR_PARAM_MGR_NAME, System.Net.Dns.GetHostName() + "_Undefined-Manager");
            }

            return mgrSettingsFromFile;
        }

        /// <summary>
        /// Tests initial settings retrieved from config file
        /// </summary>
        /// <param name="paramDictionary"></param>
        /// <returns>
        /// True if settings were loaded and MgrActive_Local is true (or MgrActive_Local is missing)
        /// False if MgrActive_Local is false or if UsingDefaults is true
        /// </returns>
        private bool CheckInitialSettings(IReadOnlyDictionary<string, string> paramDictionary)
        {
            // Verify that UsingDefaults is false
            if (!paramDictionary.TryGetValue(MGR_PARAM_USING_DEFAULTS, out var usingDefaultsText))
            {
                HandleParameterNotDefined(MGR_PARAM_USING_DEFAULTS);
            }
            else
            {
                if (bool.TryParse(usingDefaultsText, out var usingDefaults) && usingDefaults)
                {
                    ErrMsg = string.Format(
                        "MgrSettings.CheckInitialSettings; Config file problem, {0} contains UsingDefaults=True",
                        GetConfigFileName());

                    ReportError(ErrMsg);
                    return false;
                }
            }

            // Determine if manager is deactivated locally
            if (!paramDictionary.TryGetValue(MGR_PARAM_MGR_ACTIVE_LOCAL, out var activeLocalText))
            {
                // Parameter is not defined; log an error but return true
                HandleParameterNotDefined(MGR_PARAM_MGR_ACTIVE_LOCAL);
                return true;
            }

            if (!bool.TryParse(activeLocalText, out var activeLocal) || !activeLocal)
            {
                ErrMsg = DEACTIVATED_LOCALLY;
                ReportError(DEACTIVATED_LOCALLY, false);
                return false;
            }

            // No problems found
            return true;
        }

        private void LoadSchemaPrefixesFromMgrParams()
        {
            schemaPrefixes.Clear();
            const string settingNameBase = "SchemaPrefix.";

            foreach (var entry in Enum.GetValues(typeof(SchemaPrefix)).Cast<SchemaPrefix>())
            {
                var settingName = settingNameBase + entry;
                // Always add every enum entry to the dictionary.
                schemaPrefixes.Add(entry,
                    MgrParams.TryGetValue(settingName, out var value) ? value : string.Empty);
            }
        }

        private static string GetGroupNameFromSettings(IReadOnlyDictionary<string, string> mgrSettings)
        {
            if (!mgrSettings.TryGetValue("MgrSettingGroupName", out var groupName))
                return string.Empty;

            return string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName;
        }

        /// <summary>
        /// Extract the value for the given setting from the given config files
        /// </summary>
        /// <remarks>Uses a simple text reader in case the file has malformed XML</remarks>
        /// <param name="configFilePaths">List of config files to check (in order)</param>
        /// <param name="settingName">Setting to find</param>
        /// <param name="settingValue">Output: the setting, if found</param>
        /// <returns>True if found, otherwise false</returns>
        public bool GetXmlConfigFileSetting(IReadOnlyList<string> configFilePaths, string settingName, out string settingValue)
        {
            // Supported config file format:
            //
            //  <setting name="SettingName" serializeAs="String">
            //    <value>SettingValue</value>
            //  </setting>

            settingValue = string.Empty;

            foreach (var configFilePath in configFilePaths)
            {
                ShowTrace("Looking for setting {0} in {1}", settingName, configFilePath);

                var valueFound = GetXmlConfigFileSetting(configFilePath, settingName, out var configFileExists, out settingValue);

                if (!configFileExists)
                {
                    ConsoleMsgUtils.ShowWarning("Config file not found: {0}", configFilePath);
                }
                else if (valueFound)
                {
                    return true;
                }
            }

            if (configFilePaths.Count == 0)
            {
                OnErrorEvent("{0} setting not found in manager config file {1}", settingName, configFilePaths);
            }
            else
            {
                var fileNameList = new StringBuilder();
                foreach (var item in configFilePaths)
                {
                    if (fileNameList.Length > 0)
                        fileNameList.Append(", ");

                    fileNameList.Append(Path.GetFileName(item));
                }

                OnErrorEvent("{0} setting not found in the specified manager config files ({1})", settingName, fileNameList);
            }

            return false;
        }

        /// <summary>
        /// Extract the value for the given setting from the given config file
        /// </summary>
        /// <remarks>Uses a simple text reader in case the file has malformed XML</remarks>
        /// <param name="configFilePath">Config file path</param>
        /// <param name="settingName">Setting to find</param>
        /// <param name="configFileExists">Output: true if the file exists</param>
        /// <param name="settingValue">Output: the setting, if found</param>
        /// <returns>True if found, otherwise false</returns>
        private bool GetXmlConfigFileSetting(string configFilePath, string settingName, out bool configFileExists, out string settingValue)
        {
            configFileExists = false;
            settingValue = string.Empty;

            try
            {
                var configFile = new FileInfo(configFilePath);

                configFileExists = configFile.Exists;

                if (!configFileExists)
                {
                    return false;
                }

                var configXml = new StringBuilder();

                // Open the config file using a simple text reader in case the file has malformed XML

                using (var reader = new StreamReader(new FileStream(configFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        configXml.Append(dataLine);
                    }
                }

                // This RegEx uses lazy matching to match the next <value>SettingValue</value> after the setting name
                var matcher = new Regex(settingName + ".+?<value>(?<ParamValue>.+?)</value>", RegexOptions.IgnoreCase);

                var match = matcher.Match(configXml.ToString());

                if (match.Success)
                {
                    settingValue = match.Groups["ParamValue"].Value;
                    return true;
                }

                ConsoleMsgUtils.ShowDebug("Setting {0} not found in {1}", settingName, configFilePath);
                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent(ex, "Exception reading setting {0} in {1}", settingName, configFilePath);
                return false;
            }
        }

        /// <summary>
        /// Reports errors caused by required parameters that are missing
        /// </summary>
        /// <param name="parameterName"></param>
        protected void HandleParameterNotDefined(string parameterName)
        {
            ErrMsg = string.Format(
                "Parameter '{0}' is not defined in file {1}",
                parameterName, GetConfigFileName());

            ReportError(ErrMsg);
        }

        /// <summary>
        /// Gets manager config settings from manager control DB (Manager_Control)
        /// </summary>
        /// <remarks>Performs retries if necessary</remarks>
        /// <returns>True if success, otherwise false</returns>
        public bool LoadMgrSettingsFromDB(bool logConnectionErrors = true, int retryCount = 3)
        {
            var managerName = GetParam(MGR_PARAM_MGR_NAME, string.Empty);

            if (string.IsNullOrEmpty(managerName))
            {
                // MgrName parameter not defined in the AppName.exe.config file
                HandleParameterNotDefined(MGR_PARAM_MGR_NAME);
                ShowTrace("LoadMgrSettingsFromDBWork: " + ErrMsg);
                return false;
            }

            var success = LoadMgrSettingsFromDBWork(
                managerName, managerName,
                out var mgrSettingsFromDB,
                logConnectionErrors, true, retryCount);

            if (!success)
            {
                return false;
            }

            success = StoreParameters(mgrSettingsFromDB, managerName, skipExistingParameters: false);

            var mgrSettingsGroup = GetGroupNameFromSettings(mgrSettingsFromDB);

            while (success && !string.IsNullOrEmpty(mgrSettingsGroup))
            {
                // This manager has group-based settings defined; load them now
                success = LoadMgrSettingsFromDBWork(
                    mgrSettingsGroup, managerName,
                    out var mgrGroupSettingsFromDB,
                    logConnectionErrors, false, retryCount);

                if (success)
                {
                    success = StoreParameters(mgrGroupSettingsFromDB, mgrSettingsGroup, skipExistingParameters: true);
                    mgrSettingsGroup = GetGroupNameFromSettings(mgrGroupSettingsFromDB);
                }
            }

            return success;
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
        protected virtual bool LoadMgrSettingsFromDBWork(
            string managerName,
            string managerNameForConnectionString,
            out Dictionary<string, string> mgrSettingsFromDB,
            bool logConnectionErrors,
            bool returnErrorIfNoParameters,
            int retryCount = 3)
        {
            mgrSettingsFromDB = new Dictionary<string, string>();
            return false;
        }

        /// <summary>
        /// Read settings from file AppName.exe.config
        /// If the file path ends with ".exe.config", other files with similar names are also read afterward
        /// (matching RegEx "AppName\.exe\..+config$")
        /// </summary>
        /// <remarks>Uses an XML reader instead of Properties.Settings.Default (to allow for non-standard .exe.config files)</remarks>
        /// <param name="configFilePath">Path to config file</param>
        /// <returns>Dictionary of settings as key/value pairs; null on error</returns>
        public Dictionary<string, string> LoadMgrSettingsFromFile(string configFilePath)
        {
            var configFile = new FileInfo(configFilePath);
            if (!configFile.Exists)
            {
                ReportError("LoadMgrSettingsFromFile; manager config file not found: " + configFilePath);
                return null;
            }

            var settings = LoadMgrSettingsFromFile(configFilePath, new Dictionary<string, string>());
            if (configFilePath.EndsWith(".exe.config", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = configFile.Name.Substring(0, configFile.Name.Length - 6);
                var dir = configFile.Directory ?? new DirectoryInfo(".");
                var files = dir.EnumerateFiles(baseName + "*config");
                foreach (var file in files.Where(x =>
                    x.Name.EndsWith("config", StringComparison.OrdinalIgnoreCase) &&
                    !x.Name.Equals(configFile.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    settings = LoadMgrSettingsFromFile(file.FullName, settings);
                }
            }

            return settings;
        }

        /// <summary>
        /// Read settings from file AppName.exe.config
        /// </summary>
        /// <remarks>Uses an XML reader instead of Properties.Settings.Default (to allow for non-standard .exe.config files)</remarks>
        /// <param name="configFilePath">Path to config file</param>
        /// <param name="existingSettings">Existing settings dictionary; new settings will add to the existing, and overwrite any that match</param>
        /// <returns>Dictionary of settings as key/value pairs; null on error</returns>
        public Dictionary<string, string> LoadMgrSettingsFromFile(string configFilePath, Dictionary<string, string> existingSettings)
        {
            XmlDocument configDoc;

            try
            {
                var configFile = new FileInfo(configFilePath);
                if (!configFile.Exists)
                {
                    ReportError("LoadMgrSettingsFromFile; manager config file not found: " + configFilePath);
                    return existingSettings;
                }

                // Load the config document
                configDoc = new XmlDocument();
                configDoc.Load(configFilePath);
            }
            catch (Exception ex)
            {
                ReportError("LoadMgrSettingsFromFile; exception loading settings file", ex);
                return existingSettings;
            }

            try
            {
                // Retrieve the settings node
                var appSettingsNode = configDoc.SelectSingleNode("//applicationSettings");

                if (appSettingsNode == null)
                {
                    ReportError("LoadMgrSettingsFromFile; applicationSettings node not found in " + configFilePath);
                    return existingSettings;
                }

                // Read each of the settings
                var settingNodes = appSettingsNode.SelectNodes("//setting[@name]");
                if (settingNodes == null)
                {
                    ReportError("LoadMgrSettingsFromFile; applicationSettings/*/setting nodes not found in " + configFilePath);
                    return existingSettings;
                }

                return ParseXMLSettings(settingNodes, TraceMode, existingSettings);
            }
            catch (Exception ex)
            {
                ReportError("LoadMgrSettingsFromFile; Exception reading settings file", ex);
                return existingSettings;
            }
        }

        /// <summary>
        /// Parse a list of XML nodes from AppName.exe.config or ManagerSettingsLocal.xml
        /// </summary>
        /// <param name="settingNodes">XML nodes, of the form </param>
        /// <param name="traceEnabled">If true, display trace statements</param>
        /// <param name="existingSettings">Existing settings dictionary; new settings will add to the existing, and overwrite any that match</param>
        /// <returns>Dictionary of settings</returns>
        public static Dictionary<string, string> ParseXMLSettings(IEnumerable settingNodes, bool traceEnabled, Dictionary<string, string> existingSettings = null)
        {
            // Example setting node:
            // <setting name="MgrName">
            //   <value>Pub-90-1</value>
            // </setting>

            var settings = existingSettings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (XmlNode settingNode in settingNodes)
            {
                if (settingNode.Attributes == null)
                {
                    if (traceEnabled)
                    {
                        ShowTraceMessage(string.Format("Skipping setting node because no attributes: {0}", settingNode));
                    }

                    continue;
                }

                var settingName = settingNode.Attributes["name"].Value;

                var valueNode = settingNode.SelectSingleNode("value");
                if (valueNode == null)
                {
                    if (traceEnabled)
                    {
                        ShowTraceMessage(string.Format("Skipping setting node because no value node: <setting name=\"{0}\"/>", settingName));
                    }

                    continue;
                }

                var value = valueNode.InnerText;

                // Don't use Add(...); this code will create non-existing entries, and will overwrite existing entries with new data
                settings[settingName] = value;
            }

            return settings;
        }

        /// <summary>
        /// Show contents of a dictionary
        /// </summary>
        /// <param name="settings"></param>
        public static void ShowDictionaryTrace(IReadOnlyDictionary<string, string> settings)
        {
            if (settings.Count == 0)
                return;

            // Find the longest key name
            var longestName = settings.Keys.Max(item => item.Length);

            Console.ForegroundColor = ConsoleMsgUtils.DebugFontColor;
            foreach (var key in from item in settings.Keys orderby item select item)
            {
                var value = settings[key] ?? string.Empty;

                int keyWidth;
                if (longestName < 45)
                    keyWidth = longestName;
                else
                    keyWidth = (int)Math.Max(45, Math.Ceiling(key.Length / 15.0) * 15);

                var formatString = "  {0,-" + keyWidth + "} {1}";
                Console.WriteLine(formatString, key, value);
            }
            Console.ResetColor();
        }

        /// <summary>
        /// If TraceMode is true, show a message at the console, preceded by a time stamp
        /// </summary>
        /// <param name="message"></param>
        protected void ShowTrace(string message)
        {
            if (!TraceMode)
                return;

            ShowTraceMessage(message);
        }

        /// <summary>
        /// If TraceMode is true, show a message at the console, preceded by a time stamp
        /// </summary>
        /// <param name="format">Message format string</param>
        /// <param name="args">Arguments to use with <paramref name="format" /> string</param>
        [StringFormatMethod("format")]
        protected void ShowTrace(string format, params string[] args)
        {
            if (!TraceMode)
                return;

            ShowTraceMessage(string.Format(format, args));
        }

        /// <summary>
        /// Show a message at the console, preceded with a timestamp
        /// </summary>
        /// <param name="message"></param>
        protected static void ShowTraceMessage(string message)
        {
            BaseLogger.ShowTraceMessage(message, false);
        }

        /// <summary>
        /// Store manager settings, optionally skipping existing parameters
        /// </summary>
        /// <param name="mgrSettings">Manager settings</param>
        /// <param name="managerOrGroupName">Manager name or manager group name</param>
        /// <param name="skipExistingParameters">When true, skip existing parameters</param>
        private bool StoreParameters(
            IReadOnlyDictionary<string, string> mgrSettings,
            string managerOrGroupName,
            bool skipExistingParameters)
        {
            bool success;

            try
            {
                foreach (var item in mgrSettings)
                {
                    if (MgrParams.ContainsKey(item.Key))
                    {
                        if (!skipExistingParameters)
                        {
                            MgrParams[item.Key] = item.Value;
                        }
                    }
                    else
                    {
                        MgrParams.Add(item.Key, item.Value);
                    }
                }
                success = true;
            }
            catch (Exception ex)
            {
                ErrMsg = string.Format(
                    "MgrSettings.StoreParameters; Exception storing settings for manager '{0}': {1}",
                    managerOrGroupName, ex.Message);

                ReportError(ErrMsg);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Gets a manager parameter
        /// </summary>
        /// <param name="itemKey"></param>
        /// <returns>Parameter value if found, otherwise an empty string</returns>
        public string GetParam(string itemKey)
        {
            return GetParam(itemKey, string.Empty);
        }

        /// <summary>
        /// Gets a manager parameter
        /// </summary>
        /// <param name="itemKey">Parameter name</param>
        /// <param name="valueIfMissing">Value to return if the parameter does not exist</param>
        /// <returns>Parameter value if found, otherwise valueIfMissing</returns>
        public string GetParam(string itemKey, string valueIfMissing)
        {
            if (MgrParams.TryGetValue(itemKey, out var itemValue))
            {
                return itemValue ?? string.Empty;
            }

            return valueIfMissing ?? string.Empty;
        }

        /// <summary>
        /// Gets a manager parameter
        /// </summary>
        /// <param name="itemKey">Parameter name</param>
        /// <param name="valueIfMissing">Value to return if the parameter does not exist</param>
        /// <returns>Parameter value if found, otherwise valueIfMissing</returns>
        public bool GetParam(string itemKey, bool valueIfMissing)
        {
            if (MgrParams.TryGetValue(itemKey, out var valueText))
            {
                if (string.IsNullOrEmpty(valueText))
                    return valueIfMissing;

                if (bool.TryParse(valueText, out var value))
                    return value;

                if (int.TryParse(valueText, out var integerValue))
                {
                    if (integerValue == 0)
                        return false;
                    if (integerValue == 1)
                        return true;
                }
            }

            return valueIfMissing;
        }

        /// <summary>
        /// Gets a manager parameter
        /// </summary>
        /// <param name="itemKey">Parameter name</param>
        /// <param name="valueIfMissing">Value to return if the parameter does not exist</param>
        /// <returns>Parameter value if found, otherwise valueIfMissing</returns>
        public int GetParam(string itemKey, int valueIfMissing)
        {
            if (MgrParams.TryGetValue(itemKey, out var valueText))
            {
                if (string.IsNullOrEmpty(valueText))
                    return valueIfMissing;

                if (int.TryParse(valueText, out var value))
                    return value;
            }

            return valueIfMissing;
        }

        /// <summary>
        /// Adds or updates a manager parameter
        /// </summary>
        /// <param name="itemKey"></param>
        /// <param name="itemValue"></param>
        // ReSharper disable once UnusedMember.Global
        public void SetParam(string itemKey, string itemValue)
        {
            MgrParams[itemKey] = itemValue;
        }

        /// <summary>
        /// Report an important error
        /// </summary>
        /// <param name="message"></param>
        private void OnCriticalErrorEvent(string message)
        {
            if (CriticalErrorEvent == null && WriteToConsoleIfNoListener)
            {
                ConsoleMsgUtils.ShowErrorCustom(message, false, false, EmptyLinesBeforeErrorMessages);
            }

            CriticalErrorEvent?.Invoke(message, null);
        }

        /// <summary>
        /// Raises a CriticalErrorEvent if criticalError is true and ParamsLoadedFromDB is false
        /// Otherwise, raises a normal error event
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="criticalError"></param>
        protected void ReportError(string errorMessage, bool criticalError = true)
        {
            if (!ParamsLoadedFromDB && criticalError)
            {
                OnCriticalErrorEvent(errorMessage);
            }
            else
            {
                OnErrorEvent(errorMessage);
            }
        }

        /// <summary>
        /// Raises an error event that includes an exception
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="ex"></param>
        protected void ReportError(string errorMessage, Exception ex)
        {
            OnErrorEvent(errorMessage, ex);
        }
    }
}
