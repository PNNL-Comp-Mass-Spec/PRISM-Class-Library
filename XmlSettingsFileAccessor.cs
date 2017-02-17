using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Xml;

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

            StringCollection strKeys;

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

            // Section is present, but the Key isn't; add teh key
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

        /// <summary>
        /// Tools to manipulates XML Settings files.
        /// </summary>
        private class XMLFileReader
        {
            private enum XMLItemTypeEnum
            {
                GetKeys = 0,
                GetValues = 1,
                GetKeysAndValues = 2
            }

            private string m_XmlFilename;

            private XmlDocument m_XmlDoc;

            private StringCollection m_SectionNames = new StringCollection();
            private bool m_CaseSensitive;
            private string m_SaveFilename;

            private bool m_initialized;
            private readonly bool NotifyOnEvent;

            private readonly bool NotifyOnException;
            public event InformationMessageEventHandler InformationMessage;

            /// <summary>
            /// Initializes a new instance of the XMLFileReader (non case-sensitive)
            /// </summary>
            /// <param name="XmlFilename">The name of the XML file.</param>
            /// <param name="notifyOnEvent"></param>
            public XMLFileReader(string XmlFilename, bool notifyOnEvent)
            {
                NotifyOnEvent = notifyOnEvent;
                NotifyOnException = false;
                InitXMLFileReader(XmlFilename, isCaseSensitive: false);
            }

            /// <summary>
            /// This routine is called by each of the constructors to make the actual assignments.
            /// </summary>
            private void InitXMLFileReader(string strXmlFilename, bool isCaseSensitive)
            {
                m_CaseSensitive = isCaseSensitive;
                m_XmlDoc = new XmlDocument();

                if (string.IsNullOrEmpty(strXmlFilename))
                {
                    return;
                }

                // Try to load the file as an XML file
                try
                {
                    m_XmlDoc.Load(strXmlFilename);
                    UpdateSections();
                    m_XmlFilename = strXmlFilename;
                    m_initialized = true;

                }
                catch
                {
                    // Exception occurred parsing XmlFilename 
                    // Manually parse the file line-by-line
                    ManualParseXmlOrIniFile(strXmlFilename);
                }
            }

            /// <summary>
            /// Legacy property; calls XmlFilename
            /// </summary>
            [Obsolete("Use property XmlFilename")]
            public string IniFilename => XmlFilename;

            /// <summary>
            /// This routine returns the name of the ini file.
            /// </summary>
            /// <return>The function returns the name of ini file.</return>
            private string XmlFilename
            {
                get
                {
                    if (!Initialized)
                        throw new XMLFileReaderNotInitializedException();

                    return m_XmlFilename;
                }
            }

            /// <summary>
            /// This routine returns a boolean showing if the file was initialized or not.
            /// </summary>
            /// <return>The function returns a Boolean.</return>
            public bool Initialized => m_initialized;

            /// <summary>
            /// This routine returns a boolean showing if the name is case sensitive or not.
            /// </summary>
            /// <return>The function returns a Boolean.</return>
            private bool CaseSensitive => m_CaseSensitive;

            /// <summary>
            /// This routine sets a name.
            /// </summary>
            /// <param name="aName">The name to be set.</param>
            /// <return>The function returns a string.</return>
            private string SetNameCase(string aName)
            {
                if (CaseSensitive)
                {
                    return aName;
                }

                return aName.ToLower();
            }

            /// <summary>
            /// Returns the root element of the XML document
            /// </summary>
            private XmlElement GetRoot()
            {
                return m_XmlDoc.DocumentElement;
            }

            /// <summary>
            /// The function gets the last section.
            /// </summary>
            /// <return>The function returns the last section as System.Xml.XmlElement.</return>
            private XmlElement GetLastSection()
            {
                if (m_SectionNames.Count == 0)
                {
                    return GetRoot();
                }

                return GetSection(m_SectionNames[m_SectionNames.Count - 1]);
            }

            /// <summary>
            /// The function gets a section as System.Xml.XmlElement.
            /// </summary>
            /// <param name="sectionName">The name of a section.</param>
            /// <return>The function returns a section as System.Xml.XmlElement.</return>
            private XmlElement GetSection(string sectionName)
            {
                if (!string.IsNullOrEmpty(sectionName))
                {
                    sectionName = SetNameCase(sectionName);
                    return (XmlElement)m_XmlDoc.SelectSingleNode("//section[@name='" + sectionName + "']");
                }
                return null;
            }

            /// <summary>
            /// The function gets an item.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            /// <return>The function returns a XML element.</return>
            private XmlElement GetItem(string sectionName, string keyName)
            {
                if (!string.IsNullOrEmpty(keyName))
                {
                    keyName = SetNameCase(keyName);
                    var section = GetSection(sectionName);
                    if (section != null)
                    {
                        return (XmlElement)section.SelectSingleNode("item[@key='" + keyName + "']");
                    }
                }
                return null;
            }

            /// <summary>
            /// Legacy function name; calls SetXMLSection
            /// </summary>
            [Obsolete("Use method SetXMLSection")]
            public bool SetIniSection(string oldSection, string newSection)
            {
                return SetXMLSection(oldSection, newSection);
            }

            /// <summary>
            /// The function sets the ini section name.
            /// </summary>
            /// <param name="oldSection">The name of the old ini section name.</param>
            /// <param name="newSection">The new name for the ini section.</param>
            /// <return>The function returns a boolean that shows if the change was done.</return>
            public bool SetXMLSection(string oldSection, string newSection)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                if (!string.IsNullOrEmpty(newSection))
                {
                    var section = GetSection(oldSection);
                    if (section != null)
                    {
                        section.SetAttribute("name", SetNameCase(newSection));
                        UpdateSections();
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Legacy function name; calls SetXMLValue
            /// </summary>
            [Obsolete("Use method SetXMLValue")]
            public bool SetIniValue(string sectionName, string keyName, string newValue)
            {
                return SetXMLValue(sectionName, keyName, newValue);
            }

            /// <summary>
            /// The function sets a new value for the "value" attribute.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            /// <param name="newValue">The new value for the "value".</param>
            /// <return>The function returns a boolean that shows if the change was done.</return>
            public bool SetXMLValue(string sectionName, string keyName, string newValue)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                var section = GetSection(sectionName);
                if (section == null)
                {
                    if (CreateSection(sectionName))
                    {
                        section = GetSection(sectionName);
                        // exit if keyName is Nothing or blank
                        if (string.IsNullOrEmpty(keyName))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        // can't create section
                        return false;
                    }
                }
                if (keyName == null)
                {
                    // delete the section
                    return DeleteSection(sectionName);
                }

                var item = GetItem(sectionName, keyName);
                if (item != null)
                {
                    if (newValue == null)
                    {
                        // delete this item
                        return DeleteItem(sectionName, keyName);
                    }

                    // add or update the value attribute
                    item.SetAttribute("value", newValue);
                    return true;
                }

                // try to create the item
                if (!string.IsNullOrEmpty(keyName) && newValue != null)
                {
                    // construct a new item (blank values are OK)
                    item = m_XmlDoc.CreateElement("item");
                    item.SetAttribute("key", SetNameCase(keyName));
                    item.SetAttribute("value", newValue);
                    section.AppendChild(item);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// The function deletes a section in the file.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <return>The function returns a boolean that shows if the delete was completed.</return>
            private bool DeleteSection(string sectionName)
            {
                var section = GetSection(sectionName);
                if (section != null)
                {
                    section.ParentNode?.RemoveChild(section);
                    UpdateSections();
                    return true;
                }
                return false;
            }

            /// <summary>
            /// The function deletes a item in a specific section.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            /// <return>The function returns a boolean that shows if the delete was completed.</return>
            private bool DeleteItem(string sectionName, string keyName)
            {
                var item = GetItem(sectionName, keyName);
                if (item != null)
                {
                    item.ParentNode?.RemoveChild(item);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Legacy function name; calls SetXmlKey
            /// </summary>
            [Obsolete("Use method SetXmlKey")]
            public bool SetIniKey(string sectionName, string keyName, string newValue)
            {
                return SetXmlKey(sectionName, keyName, newValue);
            }

            /// <summary>
            /// The function sets a new value for the "key" attribute.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            /// <param name="newValue">The new value for the "key".</param>
            /// <return>The function returns a boolean that shows if the change was done.</return>
            private bool SetXmlKey(string sectionName, string keyName, string newValue)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                var item = GetItem(sectionName, keyName);
                if (item != null)
                {
                    item.SetAttribute("key", SetNameCase(newValue));
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Legacy function name; calls GetXMLValue
            /// </summary>
            [Obsolete("Use method GetXMLValue")]
            public string GetIniValue(string sectionName, string keyName)
            {
                return GetXMLValue(sectionName, keyName);
            }

            /// <summary>
            /// The function gets the name of the "value" attribute.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            ///<return>The function returns the name of the "value" attribute.</return>
            public string GetXMLValue(string sectionName, string keyName)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                XmlNode setting = GetItem(sectionName, keyName);
                return setting?.Attributes?.GetNamedItem("value").Value;
            }

            /// <summary>
            /// Legacy function name; calls GetXmlSectionComments
            /// </summary>
            [Obsolete("Use method GetXmlSectionComments")]
            public StringCollection GetIniComments(string sectionName)
            {
                return GetXmlSectionComments(sectionName);
            }

            /// <summary>
            /// The function gets the comments for a section name.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            ///<return>The function returns a string collection with comments</return>
            private StringCollection GetXmlSectionComments(string sectionName)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                var sectionComments = new StringCollection();
                XmlNode target;

                if (sectionName == null)
                {
                    target = m_XmlDoc.DocumentElement;
                }
                else
                {
                    target = GetSection(sectionName);
                }

                var commentNodes = target?.SelectNodes("comment");
                if (commentNodes != null && commentNodes.Count > 0)
                {
                    foreach (XmlElement commentNode in commentNodes)
                    {
                        sectionComments.Add(commentNode.InnerText);
                    }
                }

                return sectionComments;
            }

            /// <summary>
            /// Legacy function name; calls SetXMLComments
            /// </summary>
            [Obsolete("Use method SetXMLComments")]
            public bool SetIniComments(string sectionName, StringCollection comments)
            {
                return SetXMLComments(sectionName, comments);
            }

            /// <summary>
            /// The function sets a the comments for a section name.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="comments">A string collection.</param>
            ///<return>The function returns a Boolean that shows if the change was done.</return>
            private bool SetXMLComments(string sectionName, StringCollection comments)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                XmlNode targetSection;

                if (sectionName == null)
                {
                    targetSection = m_XmlDoc.DocumentElement;
                }
                else
                {
                    targetSection = GetSection(sectionName);
                }

                if (targetSection != null)
                {
                    var commentNodes = targetSection.SelectNodes("comment");
                    if (commentNodes != null)
                    {
                        foreach (XmlNode commentNode in commentNodes)
                        {
                            targetSection.RemoveChild(commentNode);
                        }
                    }

                    foreach (var s in comments)
                    {
                        var comment = m_XmlDoc.CreateElement("comment");
                        comment.InnerText = s;
                        var lastComment = (XmlElement)targetSection.SelectSingleNode("comment[last()]");
                        if (lastComment == null)
                        {
                            targetSection.PrependChild(comment);
                        }
                        else
                        {
                            targetSection.InsertAfter(comment, lastComment);
                        }
                    }
                    return true;
                }
                return false;
            }

            /// <summary>
            /// The subroutine updades the sections.
            /// </summary>
            private void UpdateSections()
            {
                m_SectionNames = new StringCollection();
                var sectionNodes = m_XmlDoc.SelectNodes("sections/section");

                if (sectionNodes != null)
                {
                    foreach (XmlElement item in sectionNodes)
                    {
                        m_SectionNames.Add(item.GetAttribute("name"));
                    }
                }
            }

            /// <summary>
            /// The subroutine gets the sections.
            /// </summary>
            /// <return>The subroutine returns a strin collection of sections.</return>
            public StringCollection AllSections
            {
                get
                {
                    if (!Initialized)
                    {
                        throw new XMLFileReaderNotInitializedException();
                    }
                    return m_SectionNames;
                }
            }

            /// <summary>
            /// The function gets a collection of items for a section name.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="itemType">Item type.</param>
            /// <return>The function returns a string colection of items in a section.</return>
            private StringCollection GetItemsInSection(string sectionName, XMLItemTypeEnum itemType)
            {
                var items = new StringCollection();
                XmlNode section = GetSection(sectionName);

                if (section == null)
                {
                    return null;
                }

                var nodes = section.SelectNodes("item");
                if (nodes != null && nodes.Count > 0)
                {
                    foreach (XmlNode setting in nodes)
                    {
                        if (setting.Attributes == null)
                            continue;

                        switch (itemType)
                        {
                            case XMLItemTypeEnum.GetKeys:
                                items.Add(setting.Attributes.GetNamedItem("key").Value);
                                break;
                            case XMLItemTypeEnum.GetValues:
                                items.Add(setting.Attributes.GetNamedItem("value").Value);
                                break;
                            case XMLItemTypeEnum.GetKeysAndValues:
                                items.Add(setting.Attributes.GetNamedItem("key").Value + "=" + setting.Attributes.GetNamedItem("value").Value);
                                break;
                        }
                    }
                }
                return items;
            }

            /// <summary>
            /// Gets a collection of keys in a section.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <return>The function returns a string colection of all the keys in a section.</return>
            public StringCollection AllKeysInSection(string sectionName)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                return GetItemsInSection(sectionName, XMLItemTypeEnum.GetKeys);
            }

            /// <summary>
            /// Gets a collection of values in a section.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <return>The function returns a string colection of all the values in a section.</return>
            public StringCollection AllValuesInSection(string sectionName)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                return GetItemsInSection(sectionName, XMLItemTypeEnum.GetValues);
            }

            /// <summary>
            /// Gets a collection of items in a section.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <return>The function returns a string colection of all the items in a section.</return>
            public StringCollection AllItemsInSection(string sectionName)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                return GetItemsInSection(sectionName, XMLItemTypeEnum.GetKeysAndValues);
            }

            /// <summary>
            /// Gets a custom attribute name.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            /// <param name="attributeName">The name of the attribute.</param>
            /// <return>The function returns a string.</return>
            public string GetCustomIniAttribute(string sectionName, string keyName, string attributeName)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                if (!string.IsNullOrEmpty(attributeName))
                {
                    var setting = GetItem(sectionName, keyName);
                    if (setting != null)
                    {
                        attributeName = SetNameCase(attributeName);
                        return setting.GetAttribute(attributeName);
                    }
                }
                return null;
            }

            /// <summary>
            /// Sets a custom attribute name.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            /// <param name="attributeName">The name of the attribute.</param>
            /// <param name="attributeValue">The value of the attribute.</param>
            /// <return>The function returns a Boolean.</return>
            public bool SetCustomIniAttribute(string sectionName, string keyName, string attributeName, string attributeValue)
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                if (string.IsNullOrEmpty(attributeName))
                {
                    return false;
                }

                var setting = GetItem(sectionName, keyName);
                if (setting == null)
                {
                    return false;
                }

                try
                {
                    if (attributeValue == null)
                    {
                        // delete the attribute
                        setting.RemoveAttribute(attributeName);
                        return true;
                    }

                    attributeName = SetNameCase(attributeName);
                    setting.SetAttribute(attributeName, attributeValue);
                    return true;
                }
                catch (Exception e)
                {
                    if (NotifyOnException)
                    {
                        throw new Exception("Failed to create item: " + e.Message);
                    }
                }
                return false;
            }

            /// <summary>
            /// Creates a section name.
            /// </summary>
            /// <param name="sectionName">The name of the section to be created.</param>
            /// <return>The function returns a Boolean.</return>
            private bool CreateSection(string sectionName)
            {
                if (!string.IsNullOrEmpty(sectionName))
                {
                    sectionName = SetNameCase(sectionName);
                    try
                    {
                        var newSection = m_XmlDoc.CreateElement("section");

                        var nameAttribute = m_XmlDoc.CreateAttribute("name");
                        nameAttribute.Value = SetNameCase(sectionName);
                        newSection.Attributes.SetNamedItem(nameAttribute);

                        if (m_XmlDoc.DocumentElement != null)
                        {
                            m_XmlDoc.DocumentElement.AppendChild(newSection);
                            m_SectionNames.Add(nameAttribute.Value);
                            return true;
                        }

                    }
                    catch (Exception e)
                    {
                        if (NotifyOnException)
                        {
                            throw new Exception("Failed to create item: " + e.Message);
                        }
                        return false;
                    }
                }
                return false;
            }

            /// <summary>
            /// Creates a section name.
            /// </summary>
            /// <param name="sectionName">The name of the section.</param>
            /// <param name="keyName">The name of the key.</param>
            /// <param name="newValue">The new value to be created.</param>
            /// <return>The function returns a Boolean.</return>
            private bool CreateItem(string sectionName, string keyName, string newValue)
            {
                try
                {
                    var section = GetSection(sectionName);
                    if (section != null)
                    {
                        var item = m_XmlDoc.CreateElement("item");
                        item.SetAttribute("key", keyName);
                        item.SetAttribute("newValue", newValue);
                        section.AppendChild(item);
                        return true;
                    }
                    return false;
                }
                catch (Exception e)
                {
                    if (NotifyOnException)
                    {
                        throw new Exception("Failed to create item: " + e.Message);
                    }
                    return false;
                }
            }

            /// <summary>
            /// Manually read a XML or .INI settings file line-by-line, extracting out any settings in the expected format
            /// </summary>
            /// <param name="strFilePath"></param>
            /// <returns></returns>
            /// <remarks></remarks>
            public bool ManualParseXmlOrIniFile(string strFilePath)
            {

                // Create a new, blank XML document
                m_XmlDoc.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\"?><sections></sections>");

                try
                {
                    var fi = new FileInfo(strFilePath);
                    if (fi.Exists)
                    {
                        // Read strFilePath line-by-line to see if it has any .Ini style settings
                        // For example:
                        //   [SectionName]
                        //   Setting1=ValueA
                        //   Setting2=ValueB

                        // Also look for XML-style entries
                        // For example:
                        //   <section name="SectionName">
                        //     <item key="Setting1" value="ValueA" />
                        //   </section>

                        using (var srInFile = new StreamReader(new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                        {

                            while (!srInFile.EndOfStream)
                            {
                                var s = srInFile.ReadLine();

                                // Try to manually parse this line
                                ParseLineManual(s, ref m_XmlDoc);
                            }

                            m_XmlFilename = strFilePath;
                            m_initialized = true;

                        }
                    }
                    else
                    {
                        // File doesn't exist; create a new, blank .XML file
                        m_XmlFilename = strFilePath;
                        m_XmlDoc.Save(m_XmlFilename);
                        m_initialized = true;
                    }

                    return true;

                }
                catch (Exception e)
                {
                    if (NotifyOnException)
                    {
                        throw new Exception("Failed to read XML file: " + e.Message);
                    }
                }

                return false;

            }

            /// <summary>Manually parses a line to extract the settings information
            /// Supports the traditional .Ini file format
            /// Also supports the 'key="KeyName" value="Value"' method used in XML settings files
            /// If success, then adds attributes to the doc var</summary>
            /// <param name="strLine">The name of the string to be parse.</param>
            /// <param name="doc">The name of the System.Xml.XmlDocument.</param>
            /// <returns>True if success, false if not a recognized line format</returns>
            private bool ParseLineManual(string strLine, ref XmlDocument doc)
            {
                const string SECTION_NAME_TAG = "<section name=";
                const string KEY_TAG = "key=";
                const string VALUE_TAG = "value=";

                strLine = strLine.TrimStart();
                if (strLine.Length == 0)
                {
                    return true;
                }

                switch (strLine.Substring(0, 1))
                {
                    case "[":
                        // this is a section
                        // trim the first and last characters
                        strLine = strLine.TrimStart('[');
                        strLine = strLine.TrimEnd(']');
                        // create a new section element
                        CreateSection(strLine);
                        break;
                    case ";":
                        // new comment
                        var commentElement = doc.CreateElement("comment");
                        commentElement.InnerText = strLine.Substring(1);
                        GetLastSection().AppendChild(commentElement);
                        break;
                    default:
                        // Look for typical XML settings file elements

                        string strKey;
                        if (ParseLineManualCheckTag(strLine, SECTION_NAME_TAG, out strKey))
                        {
                            // This is an XML-style section

                            // Create a new section element
                            CreateSection(strKey);

                        }
                        else
                        {
                            string strValue;
                            if (ParseLineManualCheckTag(strLine, KEY_TAG, out strKey))
                            {
                                // This is an XML-style key

                                ParseLineManualCheckTag(strLine, VALUE_TAG, out strValue);

                            }
                            else
                            {
                                // split the string on the "=" sign, if present
                                if (strLine.IndexOf('=') > 0)
                                {
                                    var parts = strLine.Split('=');
                                    strKey = parts[0].Trim();
                                    strValue = parts[1].Trim();
                                }
                                else
                                {
                                    strKey = strLine;
                                    strValue = string.Empty;
                                }
                            }

                            if (string.IsNullOrEmpty(strKey))
                            {
                                strKey = string.Empty;
                            }

                            if (string.IsNullOrEmpty(strValue))
                            {
                                strValue = string.Empty;
                            }

                            bool blnAddSetting;
                            if (strKey.Length > 0)
                            {
                                blnAddSetting = true;

                                switch (strKey.ToLower().Trim())
                                {

                                    case "<sections>":
                                    case "</section>":
                                    case "</sections>":
                                        // Do not add a new key
                                        if (string.IsNullOrEmpty(strValue))
                                        {
                                            blnAddSetting = false;
                                        }

                                        break;
                                }

                            }
                            else
                            {
                                blnAddSetting = false;
                            }

                            if (blnAddSetting)
                            {
                                var newSetting = doc.CreateElement("item");
                                var keyAttribute = doc.CreateAttribute("key");
                                keyAttribute.Value = SetNameCase(strKey);
                                newSetting.Attributes.SetNamedItem(keyAttribute);

                                var valueAttribute = doc.CreateAttribute("value");
                                valueAttribute.Value = strValue;
                                newSetting.Attributes.SetNamedItem(valueAttribute);

                                GetLastSection().AppendChild(newSetting);

                            }

                        }

                        break;
                }

                return false;
            }

            private bool ParseLineManualCheckTag(string strLine, string strTagTofind, out string strTagValue)
            {
                strTagValue = string.Empty;

                var intMatchIndex = strLine.ToLower().IndexOf(strTagTofind, StringComparison.Ordinal);

                if (intMatchIndex >= 0)
                {
                    strTagValue = strLine.Substring(intMatchIndex + strTagTofind.Length);

                    if (strTagValue.StartsWith('"'.ToString()))
                    {
                        strTagValue = strTagValue.Substring(1);
                    }

                    var intNextMatchIndex = strTagValue.IndexOf('"');
                    if (intNextMatchIndex >= 0)
                    {
                        strTagValue = strTagValue.Substring(0, intNextMatchIndex);
                    }

                    return true;
                }

                return false;
            }

            /// <summary>
            /// It Sets or Gets the output file name.
            /// </summary>
            public string OutputFilename
            {
                private get
                {
                    if (!Initialized)
                        throw new XMLFileReaderNotInitializedException();
                    return m_SaveFilename;
                }
                set
                {
                    if (!Initialized)
                        throw new XMLFileReaderNotInitializedException();

                    var fi = new FileInfo(value);
                    if (fi.Directory != null && !fi.Directory.Exists)
                    {
                        if (NotifyOnException)
                        {
                            throw new Exception("Invalid path for output file.");
                        }
                    }
                    else
                    {
                        m_SaveFilename = value;
                    }
                }
            }

            /// <summary>
            /// It saves the data to the Xml output file.
            /// </summary>
            public void Save()
            {
                if (!Initialized)
                    throw new XMLFileReaderNotInitializedException();

                if (OutputFilename != null && m_XmlDoc != null)
                {
                    var fi = new FileInfo(OutputFilename);
                    if (fi.Directory != null && !fi.Directory.Exists)
                    {
                        if (NotifyOnException)
                        {
                            throw new Exception("Invalid path.");
                        }
                        return;
                    }
                    if (fi.Exists)
                    {
                        fi.Delete();
                        m_XmlDoc.Save(OutputFilename);
                    }
                    else
                    {
                        m_XmlDoc.Save(OutputFilename);
                    }
                    if (NotifyOnEvent)
                    {
                        InformationMessage?.Invoke("File save complete.");
                    }
                }
                else
                {
                    if (NotifyOnException)
                    {
                        throw new Exception("Not Output File name specified.");
                    }
                }
            }

            /// <summary>
            /// Gets the System.Xml.XmlDocument.
            /// </summary>
            public XmlDocument XmlDoc
            {
                get
                {
                    if (!Initialized)
                        throw new XMLFileReaderNotInitializedException();

                    return m_XmlDoc;
                }
            }

            /// <summary>
            /// Converts an XML document to a string.
            /// </summary>
            /// <return>It returns the XML document formatted as a string.</return>
            public string XML
            {
                get
                {
                    if (!Initialized)
                        throw new XMLFileReaderNotInitializedException();

                    var sb = new System.Text.StringBuilder();

                    using (var xw = new XmlTextWriter(new StringWriter(sb)))
                    {
                        xw.Indentation = 3;
                        xw.Formatting = Formatting.Indented;

                        m_XmlDoc.WriteContentTo(xw);
                    }
                    return sb.ToString();
                }
            }

        }

        public class XMLFileReaderNotInitializedException : ApplicationException
        {
            public override string Message => "The XMLFileReader instance has not been properly initialized.";
        }

    }

}
