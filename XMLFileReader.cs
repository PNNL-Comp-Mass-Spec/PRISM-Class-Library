using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Xml;

namespace PRISM
{

    /// <summary>
    /// Tools for manipulating XML settings files
    /// </summary>
    internal class XMLFileReader
    {
        private enum XMLItemTypeEnum
        {
            GetKeys = 0,
            GetValues = 1,
            GetKeysAndValues = 2
        }

        private string m_XmlFilename;

        private readonly XmlDocument m_XmlDoc;

        private List<string> m_SectionNames = new List<string>();
        private string m_SaveFilename;

        private bool m_initialized;

        private readonly bool NotifyOnException;

        /// <summary>
        /// Initializes a new instance of the XMLFileReader (non case-sensitive)
        /// </summary>
        /// <param name="xmlFilename">The name of the XML file.</param>
        /// <param name="isCaseSensitive"></param>
        /// <param name="notifyOnException">When true, raise event InformationMessage if an exception occurs</param>
        public XMLFileReader(string xmlFilename, bool isCaseSensitive, bool notifyOnException = true)
        {
            NotifyOnException = notifyOnException;

            CaseSensitive = isCaseSensitive;
            m_XmlDoc = new XmlDocument();

            if (string.IsNullOrEmpty(xmlFilename))
            {
                return;
            }

            // Try to load the file as an XML file
            try
            {
                using (var settingsFile = new FileStream(xmlFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    m_XmlDoc.Load(settingsFile);
                }
                UpdateSections();
                m_XmlFilename = xmlFilename;
                m_initialized = true;

            }
            catch
            {
                // Exception occurred parsing XmlFilename
                // Manually parse the file line-by-line
                ManualParseXmlOrIniFile(xmlFilename);
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
        private bool CaseSensitive { get; }

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

#if !(NETSTANDARD2_0)
        /// <summary>
        /// Legacy function name; calls GetXmlSectionComments
        /// </summary>
        [Obsolete("Use method GetXmlSectionComments")]
        public StringCollection GetIniComments(string sectionName)
        {
            var sectionComments = GetXmlSectionComments(sectionName);

            var commentCollection = new StringCollection();
            foreach (var item in sectionComments)
                commentCollection.Add(item);

            return commentCollection;
        }
#endif

        /// <summary>
        /// The function gets the comments for a section name.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        ///<return>The function returns a string collection with comments</return>
        private List<string> GetXmlSectionComments(string sectionName)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            var sectionComments = new List<string>();
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

#if !(NETSTANDARD2_0)
        /// <summary>
        /// Legacy function name; calls SetXMLComments
        /// </summary>
        [Obsolete("Use method SetXMLComments")]
        public bool SetIniComments(string sectionName, StringCollection comments)
        {
            var commentList = new List<string>();
            foreach (var item in comments)
                commentList.Add(item);

            return SetXMLComments(sectionName, commentList);
        }
#endif

        /// <summary>
        /// The function sets a the comments for a section name.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="comments">A string collection.</param>
        ///<return>The function returns a Boolean that shows if the change was done.</return>
        private bool SetXMLComments(string sectionName, List<string> comments)
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
            m_SectionNames = new List<string>();
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
        public List<string> AllSections
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
        private List<string> GetItemsInSection(string sectionName, XMLItemTypeEnum itemType)
        {
            var items = new List<string>();
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
        public List<string> AllKeysInSection(string sectionName)
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
        public List<string> AllValuesInSection(string sectionName)
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
        public List<string> AllItemsInSection(string sectionName)
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
        /// Manually read a XML or .INI settings file line-by-line, extracting out any settings in the expected format
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool ManualParseXmlOrIniFile(string filePath)
        {

            // Create a new, blank XML document
            m_XmlDoc.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\"?><sections></sections>");

            try
            {
                var fileToFind = new FileInfo(filePath);
                if (fileToFind.Exists)
                {
                    // Read filePath line-by-line to see if it has any .Ini style settings
                    // For example:
                    //   [SectionName]
                    //   Setting1=ValueA
                    //   Setting2=ValueB

                    // Also look for XML-style entries
                    // For example:
                    //   <section name="SectionName">
                    //     <item key="Setting1" value="ValueA" />
                    //   </section>

                    using (var srInFile = new StreamReader(new FileStream(fileToFind.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                    {

                        while (!srInFile.EndOfStream)
                        {
                            var s = srInFile.ReadLine();

                            // Try to manually parse this line
                            ParseLineManual(s, m_XmlDoc);
                        }

                        m_XmlFilename = filePath;
                        m_initialized = true;

                    }
                }
                else
                {
                    // File doesn't exist; create a new, blank .XML file
                    m_XmlFilename = filePath;
                    using (var settingsFile = new FileStream(m_XmlFilename, FileMode.Create, FileAccess.Write))
                    {
                        m_XmlDoc.Save(settingsFile);
                    }
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
        /// <param name="dataLine">The name of the string to be parse.</param>
        /// <param name="doc">The name of the System.Xml.XmlDocument.</param>
        /// <returns>True if success, false if not a recognized line format</returns>
        private void ParseLineManual(string dataLine, XmlDocument doc)
        {
            const string SECTION_NAME_TAG = "<section name=";
            const string KEY_TAG = "key=";
            const string VALUE_TAG = "value=";

            dataLine = dataLine.TrimStart();
            if (dataLine.Length == 0)
            {
                return;
            }

            switch (dataLine.Substring(0, 1))
            {
                case "[":
                    // this is a section
                    // trim the first and last characters
                    dataLine = dataLine.TrimStart('[');
                    dataLine = dataLine.TrimEnd(']');
                    // create a new section element
                    CreateSection(dataLine);
                    break;
                case ";":
                    // new comment
                    var commentElement = doc.CreateElement("comment");
                    commentElement.InnerText = dataLine.Substring(1);
                    GetLastSection().AppendChild(commentElement);
                    break;
                default:
                    // Look for typical XML settings file elements

                    if (ParseLineManualCheckTag(dataLine, SECTION_NAME_TAG, out var keyName))
                    {
                        // This is an XML-style section

                        // Create a new section element
                        CreateSection(keyName);

                    }
                    else
                    {
                        string value;
                        if (ParseLineManualCheckTag(dataLine, KEY_TAG, out keyName))
                        {
                            // This is an XML-style key
                            ParseLineManualCheckTag(dataLine, VALUE_TAG, out value);

                        }
                        else
                        {
                            // split the string on the "=" sign, if present
                            if (dataLine.IndexOf('=') > 0)
                            {
                                var parts = dataLine.Split('=');
                                keyName = parts[0].Trim();
                                value = parts[1].Trim();
                            }
                            else
                            {
                                keyName = dataLine;
                                value = string.Empty;
                            }
                        }

                        if (string.IsNullOrEmpty(keyName))
                        {
                            keyName = string.Empty;
                        }

                        if (string.IsNullOrEmpty(value))
                        {
                            value = string.Empty;
                        }

                        bool addSetting;
                        if (keyName.Length > 0)
                        {
                            addSetting = true;

                            switch (keyName.ToLower().Trim())
                            {

                                case "<sections>":
                                case "</section>":
                                case "</sections>":
                                    // Do not add a new key
                                    if (string.IsNullOrEmpty(value))
                                    {
                                        addSetting = false;
                                    }

                                    break;
                            }

                        }
                        else
                        {
                            addSetting = false;
                        }

                        if (addSetting)
                        {
                            var newSetting = doc.CreateElement("item");
                            var keyAttribute = doc.CreateAttribute("key");
                            keyAttribute.Value = SetNameCase(keyName);
                            newSetting.Attributes.SetNamedItem(keyAttribute);

                            var valueAttribute = doc.CreateAttribute("value");
                            valueAttribute.Value = value;
                            newSetting.Attributes.SetNamedItem(valueAttribute);

                            GetLastSection().AppendChild(newSetting);

                        }

                    }

                    break;
            }

        }

        private bool ParseLineManualCheckTag(string dataLine, string tagTofind, out string tagValue)
        {
            tagValue = string.Empty;

            var matchIndex = dataLine.ToLower().IndexOf(tagTofind, StringComparison.Ordinal);

            if (matchIndex >= 0)
            {
                tagValue = dataLine.Substring(matchIndex + tagTofind.Length);

                if (tagValue.StartsWith('"'.ToString()))
                {
                    tagValue = tagValue.Substring(1);
                }

                var nextMatchIndex = tagValue.IndexOf('"');
                if (nextMatchIndex >= 0)
                {
                    tagValue = tagValue.Substring(0, nextMatchIndex);
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
                }

                using (var settingsFile = new FileStream(OutputFilename, FileMode.Create, FileAccess.Write))
                {
                    m_XmlDoc.Save(settingsFile);
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

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "   "
                };

                using (var xw = XmlWriter.Create(new StringWriter(sb), settings))
                {
                    m_XmlDoc.WriteContentTo(xw);
                }

                return sb.ToString();
            }
        }

    }

    /// <summary>
    /// Exception thrown when a method is accessed before the reader has been initialized
    /// </summary>
    public class XMLFileReaderNotInitializedException : Exception
    {
        /// <summary>
        /// Returns a message describing this exception
        /// </summary>
        public override string Message { get; } = "The XMLFileReader instance has not been properly initialized.";
    }

}
