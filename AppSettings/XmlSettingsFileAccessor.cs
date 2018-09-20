using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PRISM
{
    /// <summary>
    /// This class can be used to read or write settings in an Xml settings file
    /// Based on a class from the DMS Analysis Manager software written by Dave Clark and Gary Kiebel (PNNL, Richland, WA)
    /// Additional features added by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in October 2003
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class XmlSettingsFileAccessor
    {

        /// <summary>
        /// XML file path
        /// </summary>
        /// <remarks>Call LoadSettings to initialize, even if simply saving settings</remarks>
        public string XMLFilePath => m_XMLFilePath;

        /// <summary>
        /// Constructor
        /// </summary>
        public XmlSettingsFileAccessor()
        {
            mCaseSensitive = false;
            dtSectionNames = new Dictionary<string, string>();

            {
                mCachedSection.SectionName = string.Empty;
                mCachedSection.dtKeys = new Dictionary<string, string>();
            }
        }

        private struct CachedSectionInfo
        {
            // Stores the section name whose keys are cached; the section name is capitalized identically to that actually present in the Xml file
            public string SectionName;
            public Dictionary<string, string> dtKeys;
        }

        // XML file reader
        // Call LoadSettings to initialize, even if simply saving settings
        private string m_XMLFilePath = "";

        private XMLFileReader m_XMLFileAccessor;

        private bool mCaseSensitive;

        // When mCaseSensitive = False, dtSectionNames stores mapping between lowercase section name and actual section name stored in file
        // If section is present more than once in file, only grabs the last occurence of the section
        // When mCaseSensitive = True, the mappings in dtSectionNames are effectively not used
        private readonly Dictionary<string, string> dtSectionNames;

        private CachedSectionInfo mCachedSection;

        /// <summary>
        /// Loads the settings for the defined Xml Settings File.  Assumes names are not case sensitive
        /// </summary>
        /// <return>The function returns a boolean that shows if the file was successfully loaded.</return>
        public bool LoadSettings()
        {
            return LoadSettings(m_XMLFilePath, false);
        }

        /// <summary>
        /// Loads the settings for the defined Xml Settings File.  Assumes names are not case sensitive
        /// </summary>
        /// <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        /// <return>The function returns a boolean that shows if the file was successfully loaded.</return>
        public bool LoadSettings(string XmlSettingsFilePath)
        {
            return LoadSettings(XmlSettingsFilePath, false);
        }

        /// <summary>
        /// Loads the settings for the defined Xml Settings File
        /// </summary>
        /// <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        /// <param name="isCaseSensitive">Case sensitive names if True. Non-case sensitive if false.</param>
        /// <remarks>If case sensitive names are in place, all section and key names must be lowercase</remarks>
        public bool LoadSettings(string XmlSettingsFilePath, bool isCaseSensitive)
        {
            mCaseSensitive = isCaseSensitive;

            m_XMLFilePath = XmlSettingsFilePath;

            // Note: Always set isCaseSensitive = True for XMLFileReader's constructor since this class handles
            //       case sensitivity mapping internally
            m_XMLFileAccessor = new XMLFileReader(m_XMLFilePath, true);
            if (m_XMLFileAccessor == null)
            {
                return false;
            }

            if (m_XMLFileAccessor.Initialized)
            {
                CacheSectionNames();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parse an XML settings file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        [Obsolete("Use LoadSettings")]
        public bool ManualParseXmlOrIniFile(string filePath)
        {
            m_XMLFilePath = filePath;

            // Note: Always set isCaseSensitive = True for XMLFileReader's constructor since this class handles
            //       case sensitivity mapping internally
            m_XMLFileAccessor = new XMLFileReader(string.Empty, true);

            if (m_XMLFileAccessor == null)
            {
                return false;
            }

            if (m_XMLFileAccessor.ManualParseXmlOrIniFile(filePath))
            {
                if (m_XMLFileAccessor.Initialized)
                {
                    CacheSectionNames();
                    return true;
                }
            }

            return false;

        }

        /// <summary>
        /// Saves the settings for the defined Xml Settings File.  Note that you must call LoadSettings to initialize the class prior to setting any values.
        /// </summary>
        /// <return>The function returns a boolean that shows if the file was successfully saved.</return>
        public bool SaveSettings()
        {
            if (m_XMLFileAccessor == null)
            {
                return false;
            }

            if (m_XMLFileAccessor.Initialized)
            {
                m_XMLFileAccessor.OutputFilename = m_XMLFilePath;
                m_XMLFileAccessor.Save();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a section is present in the settings file.
        /// </summary>
        /// <param name="sectionName">The name of the section to look for.</param>
        /// <return>The function returns a boolean that shows if the section is present.</return>
        public bool SectionPresent(string sectionName)
        {
            var sections = m_XMLFileAccessor.AllSections;

            for (var index = 0; index <= sections.Count - 1; index++)
            {
                if (SetNameCase(sections[index]) == SetNameCase(sectionName))
                    return true;
            }

            return false;

        }

        private bool CacheKeyNames(string sectionName)
        {
            // Looks up the Key Names for the given section, storing them in mCachedSection
            // This is done so that this class will know the correct capitalization for the key names

            List<string> keys;

            // Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
            var sectionNameInFile = GetCachedSectionName(sectionName);
            if (sectionNameInFile.Length == 0)
                return false;

            try
            {
                // Grab the keys for sectionName
                keys = m_XMLFileAccessor.AllKeysInSection(sectionNameInFile);
            }
            catch
            {
                // Invalid section name; do not update anything
                return false;
            }

            if (keys == null)
            {
                return false;
            }

            // Update mCachedSection with the key names for the given section
            {
                mCachedSection.SectionName = sectionNameInFile;
                mCachedSection.dtKeys.Clear();

                for (var index = 0; index <= keys.Count - 1; index++)
                {
                    string keyNameToStore;
                    if (mCaseSensitive)
                    {
                        keyNameToStore = keys[index];
                    }
                    else
                    {
                        keyNameToStore = keys[index].ToLower();
                    }

                    if (!mCachedSection.dtKeys.ContainsKey(keyNameToStore))
                    {
                        mCachedSection.dtKeys.Add(keyNameToStore, keys[index]);
                    }

                }
            }

            return true;

        }

        private void CacheSectionNames()
        {
            // Looks up the Section Names in the XML file
            // This is done so that this class will know the correct capitalization for the section names

            var sections = m_XMLFileAccessor.AllSections;

            dtSectionNames.Clear();

            for (var index = 0; index <= sections.Count - 1; index++)
            {
                string sectionNameToStore;
                if (mCaseSensitive)
                {
                    sectionNameToStore = sections[index];
                }
                else
                {
                    sectionNameToStore = sections[index].ToLower();
                }

                if (!dtSectionNames.ContainsKey(sectionNameToStore))
                {
                    dtSectionNames.Add(sectionNameToStore, sections[index]);
                }

            }

        }

        private string GetCachedKeyName(string sectionName, string keyName)
        {
            // Looks up the correct capitalization for key keyName in section sectionName
            // Returns string.Empty if not found

            bool success;

            // Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
            var sectionNameInFile = GetCachedSectionName(sectionName);
            if (sectionNameInFile.Length == 0)
                return string.Empty;

            if (mCachedSection.SectionName == sectionNameInFile)
            {
                success = true;
            }
            else
            {
                // Update the keys for sectionName
                success = CacheKeyNames(sectionName);
            }

            if (success)
            {
                {
                    var keyNameToFind = SetNameCase(keyName);
                    if (mCachedSection.dtKeys.ContainsKey(keyNameToFind))
                    {
                        return mCachedSection.dtKeys[keyNameToFind];
                    }

                    return string.Empty;
                }
            }

            return string.Empty;
        }

        private string GetCachedSectionName(string sectionName)
        {
            // Looks up the correct capitalization for sectionName
            // Returns string.Empty if not found

            var sectionNameToFind = SetNameCase(sectionName);
            if (dtSectionNames.ContainsKey(sectionNameToFind))
            {
                return dtSectionNames[sectionNameToFind];
            }

            return string.Empty;
        }

        private string SetNameCase(string aName)
        {
            // Changes aName to lowercase if mCaseSensitive = False

            if (mCaseSensitive)
            {
                return aName;
            }

            return aName.ToLower();
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>The function returns the name of the "value" attribute as a string.</return>
        public string GetParam(string sectionName, string keyName, string valueIfMissing, out bool valueNotPresent)
        {
            var result = string.Empty;
            var valueFound = false;

            if (mCaseSensitive)
            {
                result = m_XMLFileAccessor.GetXMLValue(sectionName, keyName);
                if (result != null)
                    valueFound = true;
            }
            else
            {
                var sectionNameInFile = GetCachedSectionName(sectionName);
                if (sectionNameInFile.Length > 0)
                {
                    var keyNameInFile = GetCachedKeyName(sectionName, keyName);
                    if (keyNameInFile.Length > 0)
                    {
                        result = m_XMLFileAccessor.GetXMLValue(sectionNameInFile, keyNameInFile);
                        if (result != null)
                            valueFound = true;
                    }
                }
            }

            if (result == null || !valueFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            return result;
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>The function returns boolean True if the "value" attribute is "true".  Otherwise, returns boolean False.</return>
        public bool GetParam(string sectionName, string keyName, bool valueIfMissing, out bool valueNotPresent)
        {
            var result = GetParam(sectionName, keyName, valueIfMissing.ToString(), out var notFound);
            if (result == null || notFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            if (result.ToLower() == "true")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the value for a given parameter in a given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Parameter name</param>
        /// <param name="valueIfMissing">Value if missing</param>
        /// <returns>A short</returns>
        public short GetParam(string sectionName, string keyName, short valueIfMissing)
        {
            return GetParam(sectionName, keyName, valueIfMissing, out _);
        }

        /// <summary>
        /// Get the value for a given parameter in a given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Parameter name</param>
        /// <param name="valueIfMissing">Value if missing</param>
        /// <returns>An integer</returns>
        public int GetParam(string sectionName, string keyName, int valueIfMissing)
        {
            return GetParam(sectionName, keyName, valueIfMissing, out _);
        }

        /// <summary>
        /// Get the value for a given parameter in a given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Parameter name</param>
        /// <param name="valueIfMissing">Value if missing</param>
        /// <returns>A long</returns>
        public long GetParam(string sectionName, string keyName, long valueIfMissing)
        {
            return GetParam(sectionName, keyName, valueIfMissing, out _);
        }

        /// <summary>
        /// Get the value for a given parameter in a given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Parameter name</param>
        /// <param name="valueIfMissing">Value if missing</param>
        /// <returns>A float</returns>
        public float GetParam(string sectionName, string keyName, float valueIfMissing)
        {
            return GetParam(sectionName, keyName, valueIfMissing, out _);
        }

        /// <summary>
        /// Get the value for a given parameter in a given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Parameter name</param>
        /// <param name="valueIfMissing">Value if missing</param>
        /// <returns>A double</returns>
        public double GetParam(string sectionName, string keyName, double valueIfMissing)
        {
            return GetParam(sectionName, keyName, valueIfMissing, out _);
        }

        /// <summary>
        /// Get the value for a given parameter in a given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Parameter name</param>
        /// <param name="valueIfMissing">Value if missing</param>
        /// <returns>A string</returns>
        public string GetParam(string sectionName, string keyName, string valueIfMissing)
        {
            return GetParam(sectionName, keyName, valueIfMissing, out _);
        }

        /// <summary>
        /// Get the value for a given parameter in a given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Parameter name</param>
        /// <param name="valueIfMissing">Value if missing</param>
        /// <returns>A boolean</returns>
        public bool GetParam(string sectionName, string keyName, bool valueIfMissing)
        {
            return GetParam(sectionName, keyName, valueIfMissing, out _);
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>
        /// The function returns the name of the "value" attribute as a short.
        /// If "value" is "true" returns -1.  If "value" is "false" returns 0.
        /// </return>
        public short GetParam(string sectionName, string keyName, short valueIfMissing, out bool valueNotPresent)
        {
            var result = GetParam(sectionName, keyName, valueIfMissing.ToString(), out var notFound);
            if (result == null || notFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                if (short.TryParse(result, out var resultValue))
                {
                    return resultValue;
                }

                if (result.ToLower() == "true")
                {
                    return -1;
                }

                if (result.ToLower() == "false")
                {
                    return 0;
                }

                valueNotPresent = true;
                return valueIfMissing;
            }
            catch
            {
                valueNotPresent = true;
                return valueIfMissing;
            }
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>
        /// The function returns the name of the "value" attribute as an integer.
        /// If "value" is "true" returns -1.  If "value" is "false" returns 0.
        /// </return>
        public int GetParam(string sectionName, string keyName, int valueIfMissing, out bool valueNotPresent)
        {

            var result = GetParam(sectionName, keyName, valueIfMissing.ToString(), out var notFound);
            if (result == null || notFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                if (int.TryParse(result, out var resultValue))
                {
                    return resultValue;
                }

                if (result.ToLower() == "true")
                {
                    return -1;
                }

                if (result.ToLower() == "false")
                {
                    return 0;
                }

                valueNotPresent = true;
                return valueIfMissing;
            }
            catch
            {
                valueNotPresent = true;
                return valueIfMissing;
            }
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>
        /// The function returns the name of the "value" attribute as a long.
        /// If "value" is "true" returns -1.  If "value" is "false" returns 0.
        /// </return>
        public long GetParam(string sectionName, string keyName, long valueIfMissing, out bool valueNotPresent)
        {

            var result = GetParam(sectionName, keyName, valueIfMissing.ToString(), out var notFound);
            if (result == null || notFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                if (long.TryParse(result, out var resultValue))
                {
                    return resultValue;
                }

                if (result.ToLower() == "true")
                {
                    return -1;
                }

                if (result.ToLower() == "false")
                {
                    return 0;
                }

                valueNotPresent = true;
                return valueIfMissing;
            }
            catch
            {
                valueNotPresent = true;
                return valueIfMissing;
            }
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>
        /// The function returns the name of the "value" attribute as a float.
        /// If "value" is "true" returns -1.  If "value" is "false" returns 0.
        /// </return>
        public float GetParam(string sectionName, string keyName, float valueIfMissing, out bool valueNotPresent)
        {

            var result = GetParam(sectionName, keyName, valueIfMissing.ToString(CultureInfo.InvariantCulture), out var notFound);
            if (result == null || notFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                if (float.TryParse(result, out var resultValue))
                {
                    return resultValue;
                }

                if (result.ToLower() == "true")
                {
                    return -1;
                }

                if (result.ToLower() == "false")
                {
                    return 0;
                }

                valueNotPresent = true;
                return valueIfMissing;
            }
            catch
            {
                valueNotPresent = true;
                return valueIfMissing;
            }
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>
        /// The function returns the name of the "value" attribute as a double.
        /// If "value" is "true" returns -1.  If "value" is "false" returns 0.
        /// </return>
        public double GetParam(string sectionName, string keyName, double valueIfMissing, out bool valueNotPresent)
        {

            var result = GetParam(sectionName, keyName, valueIfMissing.ToString(CultureInfo.InvariantCulture), out var notFound);
            if (result == null || notFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                if (double.TryParse(result, out var resultValue))
                {
                    return resultValue;
                }

                if (result.ToLower() == "true")
                {
                    return -1;
                }

                if (result.ToLower() == "false")
                {
                    return 0;
                }

                valueNotPresent = true;
                return valueIfMissing;
            }
            catch
            {
                valueNotPresent = true;
                return valueIfMissing;
            }
        }

        /// <summary>
        /// Legacy function name; calls SetXMLFilePath
        /// </summary>
        public void SetIniFilePath(string XmlSettingsFilePath)
        {
            SetXMLFilePath(XmlSettingsFilePath);
        }

        /// <summary>
        /// The function sets the path to the Xml Settings File.
        /// </summary>
        /// <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        public void SetXMLFilePath(string XmlSettingsFilePath)
        {
            m_XMLFilePath = XmlSettingsFilePath;
        }

        /// <summary>
        /// The function sets a new String value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetParam(string sectionName, string keyName, string newValue)
        {
            if (mCaseSensitive)
            {
                return m_XMLFileAccessor.SetXMLValue(sectionName, keyName, newValue);
            }

            var sectionNameInFile = GetCachedSectionName(sectionName);
            if (sectionNameInFile.Length <= 0)
            {
                return m_XMLFileAccessor.SetXMLValue(sectionName, keyName, newValue);
            }

            var keyNameInFile = GetCachedKeyName(sectionName, keyName);
            if (keyNameInFile.Length > 0)
            {
                // Section and Key are present; update them
                return m_XMLFileAccessor.SetXMLValue(sectionNameInFile, keyNameInFile, newValue);
            }

            // Section is present, but the Key isn't; add the key
            return m_XMLFileAccessor.SetXMLValue(sectionNameInFile, keyName, newValue);

            // If we get here, either mCaseSensitive = True or the section and key weren't found
        }

        /// <summary>
        /// The function sets a new Boolean value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetParam(string sectionName, string keyName, bool newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// The function sets a new Short value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetParam(string sectionName, string keyName, short newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// The function sets a new Integer value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetParam(string sectionName, string keyName, int newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// The function sets a new Long value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetParam(string sectionName, string keyName, long newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// The function sets a new Single value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetParam(string sectionName, string keyName, float newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The function sets a new Double value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetParam(string sectionName, string keyName, double newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The function renames a section.
        /// </summary>
        /// <param name="sectionNameOld">The name of the old XML section name.</param>
        /// <param name="sectionNameNew">The new name for the XML section.</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool RenameSection(string sectionNameOld, string sectionNameNew)
        {
            if (mCaseSensitive)
            {
                return m_XMLFileAccessor.SetXMLSection(sectionNameOld, sectionNameNew);
            }

            var sectionName = GetCachedSectionName(sectionNameOld);
            if (sectionName.Length > 0)
            {
                return m_XMLFileAccessor.SetXMLSection(sectionName, sectionNameNew);
            }

            // If we get here, either mCaseSensitive = True or the section wasn't found using GetCachedSectionName
            return m_XMLFileAccessor.SetXMLSection(sectionNameOld, sectionNameNew);

        }

    }

}
