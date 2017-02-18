using System;
using System.Collections.Generic;
using System.Globalization;

namespace PRISM
{
    /// <summary>
    /// This class can be used to read or write settings in an Xml settings file
    ///   Based on a class from the DMS Analysis Manager software written by Dave Clark and Gary Kiebel (PNNL, Richland, WA)
    ///   Additional features added by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in October 2003
    /// Updated in October 2004 to truly be case-insensitive if isCaseSensitive = False when calling LoadSettings()
    /// Updated in August 2007 to remove the PRISM.Logging functionality and to include class XMLFileReader inside class XmlSettingsFileAccessor
    /// Updated in December 2010 to rename vars from Ini to XML
    /// </summary>
    public class XmlSettingsFileAccessor
    {

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

        private struct udtRecentSectionType
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

        // When mCaseSensitive = False, then dtSectionNames stores mapping between lowercase section name and actual section name stored in file
        //   If section is present more than once in file, then only grabs the last occurence of the section
        // When mCaseSensitive = True, then the mappings in dtSectionNames are effectively not used
        private readonly Dictionary<string, string> dtSectionNames;

        private udtRecentSectionType mCachedSection;
        public event InformationMessageEventHandler InformationMessage;
        public delegate void InformationMessageEventHandler(string msg);

        /// <summary>
        /// Loads the settings for the defined Xml Settings File.  Assumes names are not case sensitive
        /// </summary>
        /// <return>The function returns a boolean that shows if the file was successfully loaded.</return>
        public bool LoadSettings()
        {
            return LoadSettings(m_XMLFilePath, false);
        }

        /// <summary>
        /// Loads the settings for the defined Xml Settings File.   Assumes names are not case sensitive
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

            m_XMLFileAccessor.InformationMessage += m_XMLFileAccessor_InformationMessage;

            if (m_XMLFileAccessor.Initialized)
            {
                CacheSectionNames();
                return true;
            }

            return false;
        }

        public bool ManualParseXmlOrIniFile(string strFilePath)
        {
            m_XMLFilePath = strFilePath;

            // Note: Always set isCaseSensitive = True for XMLFileReader's constructor since this class handles 
            //       case sensitivity mapping internally
            m_XMLFileAccessor = new XMLFileReader(string.Empty, true);

            if (m_XMLFileAccessor == null)
            {
                return false;
            }

            if (m_XMLFileAccessor.ManualParseXmlOrIniFile(strFilePath))
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
            var strSections = m_XMLFileAccessor.AllSections;

            for (var intIndex = 0; intIndex <= strSections.Count - 1; intIndex++)
            {
                if (SetNameCase(strSections[intIndex]) == SetNameCase(sectionName))
                    return true;
            }

            return false;

        }

        private bool CacheKeyNames(string sectionName)
        {
            // Looks up the Key Names for the given section, storing them in mCachedSection
            // This is done so that this class will know the correct capitalization for the key names

            List<string> strKeys;

            // Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
            var sectionNameInFile = GetCachedSectionName(sectionName);
            if (sectionNameInFile.Length == 0)
                return false;

            try
            {
                // Grab the keys for sectionName
                strKeys = m_XMLFileAccessor.AllKeysInSection(sectionNameInFile);
            }
            catch
            {
                // Invalid section name; do not update anything
                return false;
            }

            if (strKeys == null)
            {
                return false;
            }

            // Update mCachedSection with the key names for the given section
            {
                mCachedSection.SectionName = sectionNameInFile;
                mCachedSection.dtKeys.Clear();

                for (var intIndex = 0; intIndex <= strKeys.Count - 1; intIndex++)
                {
                    string strKeyNameToStore;
                    if (mCaseSensitive)
                    {
                        strKeyNameToStore = string.Copy(strKeys[intIndex]);
                    }
                    else
                    {
                        strKeyNameToStore = string.Copy(strKeys[intIndex].ToLower());
                    }

                    if (!mCachedSection.dtKeys.ContainsKey(strKeyNameToStore))
                    {
                        mCachedSection.dtKeys.Add(strKeyNameToStore, strKeys[intIndex]);
                    }

                }
            }

            return true;

        }

        private void CacheSectionNames()
        {
            // Looks up the Section Names in the XML file
            // This is done so that this class will know the correct capitalization for the section names

            var strSections = m_XMLFileAccessor.AllSections;

            dtSectionNames.Clear();

            for (var intIndex = 0; intIndex <= strSections.Count - 1; intIndex++)
            {
                string strSectionNameToStore;
                if (mCaseSensitive)
                {
                    strSectionNameToStore = string.Copy(strSections[intIndex]);
                }
                else
                {
                    strSectionNameToStore = string.Copy(strSections[intIndex].ToLower());
                }

                if (!dtSectionNames.ContainsKey(strSectionNameToStore))
                {
                    dtSectionNames.Add(strSectionNameToStore, strSections[intIndex]);
                }

            }

        }

        private string GetCachedKeyName(string sectionName, string keyName)
        {
            // Looks up the correct capitalization for key keyName in section sectionName
            // Returns string.Empty if not found

            bool blnSuccess;

            // Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
            var sectionNameInFile = GetCachedSectionName(sectionName);
            if (sectionNameInFile.Length == 0)
                return string.Empty;

            if (mCachedSection.SectionName == sectionNameInFile)
            {
                blnSuccess = true;
            }
            else
            {
                // Update the keys for sectionName
                blnSuccess = CacheKeyNames(sectionName);
            }

            if (blnSuccess)
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
        /// <return>The function returns the name of the "value" attribute as a String.</return>
        public string GetParam(string sectionName, string keyName, string valueIfMissing, out bool valueNotPresent)
        {
            var strResult = string.Empty;
            var blnValueFound = false;

            if (mCaseSensitive)
            {
                strResult = m_XMLFileAccessor.GetXMLValue(sectionName, keyName);
                if (strResult != null)
                    blnValueFound = true;
            }
            else
            {
                var sectionNameInFile = GetCachedSectionName(sectionName);
                if (sectionNameInFile.Length > 0)
                {
                    var keyNameInFile = GetCachedKeyName(sectionName, keyName);
                    if (keyNameInFile.Length > 0)
                    {
                        strResult = m_XMLFileAccessor.GetXMLValue(sectionNameInFile, keyNameInFile);
                        if (strResult != null)
                            blnValueFound = true;
                    }
                }
            }

            if (strResult == null || !blnValueFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            return strResult;
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
            bool blnNotFound;

            var strResult = GetParam(sectionName, keyName, valueIfMissing.ToString(), out blnNotFound);
            if (strResult == null || blnNotFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            if (strResult.ToLower() == "true")
            {
                return true;
            }

            return false;
        }

        public short GetParam(string sectionName, string keyName, short valueIfMissing)
        {
            bool valueNotPresent;
            return GetParam(sectionName, keyName, valueIfMissing, out valueNotPresent);
        }

        public int GetParam(string sectionName, string keyName, int valueIfMissing)
        {
            bool valueNotPresent;
            return GetParam(sectionName, keyName, valueIfMissing, out valueNotPresent);
        }

        public long GetParam(string sectionName, string keyName, long valueIfMissing)
        {
            bool valueNotPresent;
            return GetParam(sectionName, keyName, valueIfMissing, out valueNotPresent);
        }

        public float GetParam(string sectionName, string keyName, float valueIfMissing)
        {
            bool valueNotPresent;
            return GetParam(sectionName, keyName, valueIfMissing, out valueNotPresent);
        }

        public double GetParam(string sectionName, string keyName, double valueIfMissing)
        {
            bool valueNotPresent;
            return GetParam(sectionName, keyName, valueIfMissing, out valueNotPresent);
        }

        public string GetParam(string sectionName, string keyName, string valueIfMissing)
        {
            bool valueNotPresent;
            return GetParam(sectionName, keyName, valueIfMissing, out valueNotPresent);
        }

        public bool GetParam(string sectionName, string keyName, bool valueIfMissing)
        {
            bool valueNotPresent;
            return GetParam(sectionName, keyName, valueIfMissing, out valueNotPresent);
        }

        /// <summary>
        /// The function gets the name of the "value" attribute in section "sectionName".
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        /// <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        /// <return>The function returns the name of the "value" attribute as a Short.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        public short GetParam(string sectionName, string keyName, short valueIfMissing, out bool valueNotPresent)
        {
            bool blnNotFound;

            var strResult = GetParam(sectionName, keyName, valueIfMissing.ToString(), out blnNotFound);
            if (strResult == null || blnNotFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                short result;
                if (short.TryParse(strResult, out result))
                {
                    return result;
                }

                if (strResult.ToLower() == "true")
                {
                    return -1;
                }

                if (strResult.ToLower() == "false")
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
        /// <return>The function returns the name of the "value" attribute as an Integer.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        public int GetParam(string sectionName, string keyName, int valueIfMissing, out bool valueNotPresent)
        {
            bool blnNotFound;

            var strResult = GetParam(sectionName, keyName, valueIfMissing.ToString(), out blnNotFound);
            if (strResult == null || blnNotFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                int result;
                if (int.TryParse(strResult, out result))
                {
                    return result;
                }

                if (strResult.ToLower() == "true")
                {
                    return -1;
                }

                if (strResult.ToLower() == "false")
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
        /// <return>The function returns the name of the "value" attribute as a Long.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        public long GetParam(string sectionName, string keyName, long valueIfMissing, out bool valueNotPresent)
        {
            bool blnNotFound;

            var strResult = GetParam(sectionName, keyName, valueIfMissing.ToString(), out blnNotFound);
            if (strResult == null || blnNotFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                long result;
                if (long.TryParse(strResult, out result))
                {
                    return result;
                }

                if (strResult.ToLower() == "true")
                {
                    return -1;
                }

                if (strResult.ToLower() == "false")
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
        /// <return>The function returns the name of the "value" attribute as a Single.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        public float GetParam(string sectionName, string keyName, float valueIfMissing, out bool valueNotPresent)
        {
            bool blnNotFound;

            var strResult = GetParam(sectionName, keyName, valueIfMissing.ToString(CultureInfo.InvariantCulture), out blnNotFound);
            if (strResult == null || blnNotFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                float result;
                if (float.TryParse(strResult, out result))
                {
                    return result;
                }

                if (strResult.ToLower() == "true")
                {
                    return -1;
                }

                if (strResult.ToLower() == "false")
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
        /// <return>The function returns the name of the "value" attribute as a Double.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        public double GetParam(string sectionName, string keyName, double valueIfMissing, out bool valueNotPresent)
        {
            bool blnNotFound;

            var strResult = GetParam(sectionName, keyName, valueIfMissing.ToString(CultureInfo.InvariantCulture), out blnNotFound);
            if (strResult == null || blnNotFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            try
            {
                double result;
                if (double.TryParse(strResult, out result))
                {
                    return result;
                }

                if (strResult.ToLower() == "true")
                {
                    return -1;
                }

                if (strResult.ToLower() == "false")
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

            // If we get here, then either mCaseSensitive = True or the section and key weren't found
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

            var strSectionName = GetCachedSectionName(sectionNameOld);
            if (strSectionName.Length > 0)
            {
                return m_XMLFileAccessor.SetXMLSection(strSectionName, sectionNameNew);
            }

            // If we get here, then either mCaseSensitive = True or the section wasn't found using GetCachedSectionName
            return m_XMLFileAccessor.SetXMLSection(sectionNameOld, sectionNameNew);

        }

        void m_XMLFileAccessor_InformationMessage(string msg)
        {
            InformationMessage?.Invoke(msg);
        }
        
    }

}
