﻿using System;
using System.Collections.Generic;
using System.Globalization;

// ReSharper disable UnusedMember.Global

// ReSharper disable once CheckNamespace
namespace PRISM
{
    // ReSharper disable once GrammarMistakeInComment

    /// <summary>
    /// This class can be used to read or write settings in an Xml settings file
    /// Based on a class from the DMS Analysis Manager software written by Dave Clark and Gary Kiebel (PNNL, Richland, WA)
    /// Additional features added by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in October 2003
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public class XmlSettingsFileAccessor
    {
        /// <summary>
        /// XML file path
        /// </summary>
        /// <remarks>Call LoadSettings to initialize, even if simply saving settings</remarks>
        public string XMLFilePath => mXMLFilePath;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>Call LoadSettings to initialize, even if simply saving settings</remarks>
        public XmlSettingsFileAccessor()
        {
            mCaseSensitive = false;
            mSectionNames = new Dictionary<string, string>();
            {
                mCachedSection.SectionName = string.Empty;
                mCachedSection.Keys = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Stores the section name whose keys are cached
        /// </summary>
        /// <remarks>The section name is capitalized identically to that actually present in the Xml file</remarks>
        private struct CachedSectionInfo
        {
            public string SectionName;
            public Dictionary<string, string> Keys;
        }

        private string mXMLFilePath = string.Empty;

        /// <summary>
        /// XML file reader
        /// </summary>
        private XMLFileReader mXMLFileAccessor;

        private bool mCaseSensitive;

        /// <summary>
        /// When mCaseSensitive = False, SectionNames stores mapping between lowercase section name and actual section name stored in file
        /// If section is present more than once in file, only grabs the last occurrence of the section
        /// When mCaseSensitive = True, the mappings in SectionNames are effectively not used
        /// </summary>
        private readonly Dictionary<string, string> mSectionNames;

        private CachedSectionInfo mCachedSection;

        /// <summary>
        /// Loads the settings for the defined Xml Settings File; assumes names are not case-sensitive
        /// </summary>
        /// <returns>True if successfully loaded, otherwise false</returns>
        public bool LoadSettings()
        {
            return LoadSettings(mXMLFilePath, false);
        }

        /// <summary>
        /// Loads the settings for the defined Xml Settings File; assumes names are not case-sensitive
        /// </summary>
        /// <param name="XmlSettingsFilePath">The path to the XML settings file</param>
        /// <returns>True if successfully loaded, otherwise false</returns>
        public bool LoadSettings(string XmlSettingsFilePath)
        {
            return LoadSettings(XmlSettingsFilePath, false);
        }

        /// <summary>
        /// Loads the settings for the defined Xml Settings File
        /// </summary>
        /// <remarks>If case-sensitive names are in place, all section and key names must be lowercase</remarks>
        /// <param name="XmlSettingsFilePath">The path to the XML settings file</param>
        /// <param name="isCaseSensitive">Case-sensitive names if True. Non-case sensitive if false</param>
        public bool LoadSettings(string XmlSettingsFilePath, bool isCaseSensitive)
        {
            mCaseSensitive = isCaseSensitive;

            mXMLFilePath = XmlSettingsFilePath;

            // Note: Always set isCaseSensitive = True for XMLFileReader's constructor since this class handles
            //       case sensitivity mapping internally
            mXMLFileAccessor = new XMLFileReader(mXMLFilePath, true);

            if (mXMLFileAccessor == null)
            {
                return false;
            }

            if (mXMLFileAccessor.Initialized)
            {
                CacheSectionNames();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parse an XML settings file
        /// </summary>
        /// <param name="filePath">Settings file path</param>
        [Obsolete("Use LoadSettings")]
        public bool ManualParseXmlOrIniFile(string filePath)
        {
            mXMLFilePath = filePath;

            // Note: Always set isCaseSensitive = True for XMLFileReader's constructor since this class handles
            //       case sensitivity mapping internally
            mXMLFileAccessor = new XMLFileReader(string.Empty, true);

            if (mXMLFileAccessor == null)
            {
                return false;
            }

            if (mXMLFileAccessor.ManualParseXmlOrIniFile(filePath))
            {
                if (mXMLFileAccessor.Initialized)
                {
                    CacheSectionNames();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Saves the settings for the defined Xml Settings File
        /// </summary>
        /// <remarks>You must call LoadSettings to initialize the class prior to setting any values</remarks>
        /// <returns>True if successfully saved, otherwise false</returns>
        public bool SaveSettings()
        {
            if (mXMLFileAccessor == null)
            {
                return false;
            }

            if (mXMLFileAccessor.Initialized)
            {
                mXMLFileAccessor.OutputFilename = mXMLFilePath;
                mXMLFileAccessor.Save();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a section is present in the settings file
        /// </summary>
        /// <param name="sectionName">The name of the section to look for</param>
        /// <returns>True if the section is present</returns>
        public bool SectionPresent(string sectionName)
        {
            var sections = mXMLFileAccessor.AllSections;

            foreach (var section in sections)
            {
                if (SetNameCase(section) == SetNameCase(sectionName))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Examines the Key Names for the given section, storing them in mCachedSection
        /// </summary>
        /// <remarks>This is done so that this class will know the correct capitalization for the key names</remarks>
        /// <param name="sectionName">Section name</param>
        private bool CacheKeyNames(string sectionName)
        {
            List<string> keys;

            // Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
            var sectionNameInFile = GetCachedSectionName(sectionName);

            if (sectionNameInFile.Length == 0)
                return false;

            try
            {
                // Grab the keys for sectionName
                keys = mXMLFileAccessor.AllKeysInSection(sectionNameInFile);
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
                mCachedSection.Keys.Clear();

                foreach (var keyName in keys)
                {
                    string keyNameToStore;

                    if (mCaseSensitive)
                    {
                        keyNameToStore = keyName;
                    }
                    else
                    {
                        keyNameToStore = keyName.ToLower();
                    }

                    if (!mCachedSection.Keys.ContainsKey(keyNameToStore))
                    {
                        mCachedSection.Keys.Add(keyNameToStore, keyName);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Examines the Section Names in the XML file
        /// </summary>
        /// <remarks>This is done so that this class will know the correct capitalization for the section names</remarks>
        private void CacheSectionNames()
        {
            var sections = mXMLFileAccessor.AllSections;

            mSectionNames.Clear();

            foreach (var section in sections)
            {
                string sectionNameToStore;

                if (mCaseSensitive)
                {
                    sectionNameToStore = section;
                }
                else
                {
                    sectionNameToStore = section.ToLower();
                }

                if (!mSectionNames.ContainsKey(sectionNameToStore))
                {
                    mSectionNames.Add(sectionNameToStore, section);
                }
            }
        }

        /// <summary>
        /// Looks up the correct capitalization for the given key in the given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Key name</param>
        /// <returns>Key name if found, or an empty string</returns>
        private string GetCachedKeyName(string sectionName, string keyName)
        {
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

                    if (mCachedSection.Keys.TryGetValue(keyNameToFind, out var value))
                    {
                        return value;
                    }

                    return string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Determines the correct capitalization for sectionName
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <returns>Name if found, or an empty string</returns>
        private string GetCachedSectionName(string sectionName)
        {
            var sectionNameToFind = SetNameCase(sectionName);

            if (mSectionNames.TryGetValue(sectionNameToFind, out var value))
            {
                return value;
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
        /// Get the value of the given parameter in section "sectionName"
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing</param>
        /// <param name="valueNotPresent">Output: True if "sectionName" or "keyName" is missing</param>
        /// <returns>The string tracked by the "value" attribute</returns>
        public string GetParam(string sectionName, string keyName, string valueIfMissing, out bool valueNotPresent)
        {
            var result = string.Empty;
            var valueFound = false;

            if (mCaseSensitive)
            {
                result = mXMLFileAccessor.GetXMLValue(sectionName, keyName);

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
                        result = mXMLFileAccessor.GetXMLValue(sectionNameInFile, keyNameInFile);

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
        /// Get the boolean value of the given parameter in section "sectionName"
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing</param>
        /// <param name="valueNotPresent">Output: True if "sectionName" or "keyName" is missing</param>
        /// <returns>True if the "value" attribute is "true"; Otherwise, returns boolean False</returns>
        public bool GetParam(string sectionName, string keyName, bool valueIfMissing, out bool valueNotPresent)
        {
            var result = GetParam(sectionName, keyName, valueIfMissing.ToString(), out var notFound);

            if (result == null || notFound)
            {
                valueNotPresent = true;
                return valueIfMissing;
            }

            valueNotPresent = false;
            return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
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
        /// Get the string value for a given parameter in a given section
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
        /// Get the boolean value for a given parameter in a given section
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
        /// Get the numeric value for a given parameter in a given section
        /// </summary>
        /// <remarks>If "value" is "true" returns -1; if "value" is "false" returns 0</remarks>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing</param>
        /// <param name="valueNotPresent">Output: True if "sectionName" or "keyName" is missing</param>
        /// <returns>
        /// The numeric value of the "value" attribute as a short
        /// </returns>
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

                if (string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }

                if (string.Equals(result, "false", StringComparison.OrdinalIgnoreCase))
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
        /// Get the numeric value for a given parameter in a given section
        /// </summary>
        /// <remarks>If "value" is "true" returns -1; if "value" is "false" returns 0</remarks>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing</param>
        /// <param name="valueNotPresent">Output: True if "sectionName" or "keyName" is missing</param>
        /// <returns>
        /// The numeric value of the "value" attribute as an integer
        /// </returns>
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

                if (string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }

                if (string.Equals(result, "false", StringComparison.OrdinalIgnoreCase))
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
        /// Get the numeric value for a given parameter in a given section
        /// </summary>
        /// <remarks>If "value" is "true" returns -1; if "value" is "false" returns 0</remarks>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing</param>
        /// <param name="valueNotPresent">Output: True if "sectionName" or "keyName" is missing</param>
        /// <returns>
        /// The numeric value of the "value" attribute as a long
        /// </returns>
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

                if (string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }

                if (string.Equals(result, "false", StringComparison.OrdinalIgnoreCase))
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
        /// Get the numeric value for a given parameter in a given section
        /// </summary>
        /// <remarks>If "value" is "true" returns -1; if "value" is "false" returns 0</remarks>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing</param>
        /// <param name="valueNotPresent">Output: True if "sectionName" or "keyName" is missing</param>
        /// <returns>
        /// The numeric value of the "value" attribute as a float
        /// </returns>
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

                if (string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }

                if (string.Equals(result, "false", StringComparison.OrdinalIgnoreCase))
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
        /// Get the numeric value for a given parameter in a given section
        /// </summary>
        /// <remarks>If "value" is "true" returns -1; if "value" is "false" returns 0</remarks>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing</param>
        /// <param name="valueNotPresent">Output: True if "sectionName" or "keyName" is missing</param>
        /// <returns>
        /// The numeric value of the "value" attribute as a double
        /// </returns>
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

                if (string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }

                if (string.Equals(result, "false", StringComparison.OrdinalIgnoreCase))
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
        /// Legacy method name; calls SetXMLFilePath
        /// </summary>
        public void SetIniFilePath(string XmlSettingsFilePath)
        {
            SetXMLFilePath(XmlSettingsFilePath);
        }

        /// <summary>
        /// Sets the path to the Xml Settings File
        /// </summary>
        /// <param name="XmlSettingsFilePath">The path to the XML settings file</param>
        public void SetXMLFilePath(string XmlSettingsFilePath)
        {
            mXMLFilePath = XmlSettingsFilePath;
        }

        /// <summary>
        /// Sets a new string value for the "value" attribute
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="newValue">The new value for the "value"</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetParam(string sectionName, string keyName, string newValue)
        {
            if (mCaseSensitive)
            {
                return mXMLFileAccessor.SetXMLValue(sectionName, keyName, newValue);
            }

            var sectionNameInFile = GetCachedSectionName(sectionName);

            if (sectionNameInFile.Length == 0)
            {
                return mXMLFileAccessor.SetXMLValue(sectionName, keyName, newValue);
            }

            var keyNameInFile = GetCachedKeyName(sectionName, keyName);

            if (keyNameInFile.Length > 0)
            {
                // Section and Key are present; update them
                return mXMLFileAccessor.SetXMLValue(sectionNameInFile, keyNameInFile, newValue);
            }

            // Section is present, but the Key isn't; add the key
            return mXMLFileAccessor.SetXMLValue(sectionNameInFile, keyName, newValue);

            // If we get here, either mCaseSensitive = True or the section and key weren't found
        }

        /// <summary>
        /// Sets a new boolean value for the "value" attribute
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="newValue">The new value for the "value"</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetParam(string sectionName, string keyName, bool newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// Sets a new short value for the "value" attribute
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="newValue">The new value for the "value"</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetParam(string sectionName, string keyName, short newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// Sets a new integer value for the "value" attribute
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="newValue">The new value for the "value"</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetParam(string sectionName, string keyName, int newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// Sets a new Long value for the "value" attribute
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="newValue">The new value for the "value"</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetParam(string sectionName, string keyName, long newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue));
        }

        /// <summary>
        /// Sets a new float value for the "value" attribute
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="newValue">The new value for the "value"</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetParam(string sectionName, string keyName, float newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Sets a new double value for the "value" attribute
        /// </summary>
        /// <param name="sectionName">The name of the section</param>
        /// <param name="keyName">The name of the key</param>
        /// <param name="newValue">The new value for the "value"</param>
        /// <returns>True if successful, false if an error</returns>
        public bool SetParam(string sectionName, string keyName, double newValue)
        {
            return SetParam(sectionName, keyName, Convert.ToString(newValue, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Rename a section
        /// </summary>
        /// <param name="sectionNameOld">The name of the old XML section name</param>
        /// <param name="sectionNameNew">The new name for the XML section</param>
        /// <returns>True if successful, false if an error</returns>
        public bool RenameSection(string sectionNameOld, string sectionNameNew)
        {
            if (mCaseSensitive)
            {
                return mXMLFileAccessor.SetXMLSection(sectionNameOld, sectionNameNew);
            }

            var sectionName = GetCachedSectionName(sectionNameOld);

            if (sectionName.Length > 0)
            {
                return mXMLFileAccessor.SetXMLSection(sectionName, sectionNameNew);
            }

            // If we get here, either mCaseSensitive = True or the section wasn't found using GetCachedSectionName
            return mXMLFileAccessor.SetXMLSection(sectionNameOld, sectionNameNew);
        }
    }
}
