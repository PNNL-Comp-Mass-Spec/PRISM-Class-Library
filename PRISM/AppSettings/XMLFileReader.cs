using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Tools for manipulating XML settings files
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal class XMLFileReader
    {
        private enum XMLItemTypeEnum
        {
            GetKeys = 0,
            GetValues = 1,
            GetKeysAndValues = 2
        }

        private readonly XmlDocument m_XmlDoc;

        /// <summary>
        /// Cached list of section names
        /// </summary>
        private List<string> m_SectionNames = new List<string>();

        private string m_SaveFilename;

        private readonly bool NotifyOnException;

        /// <summary>
        /// Initializes a new instance of the XMLFileReader (non case-sensitive)
        /// </summary>
        /// <param name="xmlFilename">XML file name</param>
        /// <param name="isCaseSensitive"></param>
        /// <param name="notifyOnException">When true, raise event InformationMessage if an exception occurs</param>
        public XMLFileReader(string xmlFilename, bool isCaseSensitive, bool notifyOnException = true)
        {
            NotifyOnException = notifyOnException;

            CaseSensitive = isCaseSensitive;
            m_XmlDoc = new XmlDocument();

            if (string.IsNullOrWhiteSpace(xmlFilename))
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
                XmlFilePath = xmlFilename;
                Initialized = true;
            }
            catch
            {
                // Exception occurred parsing XmlFilename
                // Manually parse the file line-by-line
                ManualParseXmlOrIniFile(xmlFilename);
            }
        }

        /// <summary>
        /// Path of the XML settings file
        /// </summary>
        public string XmlFilePath { get; private set; }

        /// <summary>
        /// This is set to True once the XML settings file has been successfully read
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// This is True if setting names are case sensitive
        /// </summary>
        private bool CaseSensitive { get; }

        /// <summary>
        /// Adjust the case of a setting name
        /// </summary>
        /// <param name="aName">Setting name</param>
        /// <return>Returns the name as-is if CaseSensitive is true; otherwise, changes the name to lowercase</return>
        private string SetNameCase(string aName)
        {
            if (CaseSensitive)
            {
                return aName;
            }

            return aName.ToLower();
        }

        /// <summary>
        /// Get the root element of the XML document
        /// </summary>
        private XmlElement GetRoot()
        {
            return m_XmlDoc.DocumentElement;
        }

        /// <summary>
        /// Get the last section in m_SectionNames
        /// </summary>
        /// <return>The last section as System.Xml.XmlElement.</return>
        private XmlElement GetLastSection()
        {
            if (m_SectionNames.Count == 0)
            {
                return GetRoot();
            }

            return GetSection(m_SectionNames[m_SectionNames.Count - 1]);
        }

        /// <summary>
        /// Get a section by name
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <return>The section as an XmlElement if found, otherwise null</return>
        private XmlElement GetSection(string sectionName)
        {
            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                sectionName = SetNameCase(sectionName);
                return (XmlElement)m_XmlDoc.SelectSingleNode("//section[@name='" + sectionName + "']");
            }
            return null;
        }

        /// <summary>
        /// Get the XML element for the given key in the given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Setting name</param>
        /// <return>XML element, or null if no match</return>
        private XmlElement GetItem(string sectionName, string keyName)
        {
            if (!string.IsNullOrWhiteSpace(keyName))
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
        /// Copies a section
        /// </summary>
        /// <param name="oldSection">The name of the section to copy</param>
        /// <param name="newSection">The new section name</param>
        /// <return>True if success, false if the old section was not found or if newSection is an empty string</return>
        public bool SetXMLSection(string oldSection, string newSection)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            if (string.IsNullOrWhiteSpace(newSection))
                return false;

            var section = GetSection(oldSection);
            if (section != null)
            {
                section.SetAttribute("name", SetNameCase(newSection));
                UpdateSections();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Store the value of a setting in the given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Key name</param>
        /// <param name="newValue">Value for the key</param>
        /// <return>True if success, false </return>
        public bool SetXMLValue(string sectionName, string keyName, string newValue)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            var section = GetSection(sectionName);
            if (section == null)
            {
                // Section not found; add it
                if (CreateSection(sectionName))
                {
                    section = GetSection(sectionName);

                    // exit if keyName is Nothing or blank
                    if (string.IsNullOrWhiteSpace(keyName))
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
            if (!string.IsNullOrWhiteSpace(keyName) && newValue != null)
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
        /// Delete a section by name
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <return>True if success, false if the section was not found</return>
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
        /// Delete an item from a section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Key name</param>
        /// <return>True if success, false if the section and/or key was not found</return>
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
        /// Stores the value for a given key in the given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Key name</param>
        /// <param name="newValue">The new value for the "key".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        [Obsolete("Unused: this class is used to read XML files, not update them")]
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
        /// Get the value for the given key in the given section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Key name</param>
        ///<return>The string in the "value" attribute of the key</return>
        public string GetXMLValue(string sectionName, string keyName)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            XmlNode setting = GetItem(sectionName, keyName);
            return setting?.Attributes?.GetNamedItem("value").Value;
        }

        /// <summary>
        /// Get the comments for a section name
        /// </summary>
        /// <param name="sectionName">Section name</param>
        ///<return>String collection with comments</return>
        public IEnumerable<string> GetXmlSectionComments(string sectionName)
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
            if (commentNodes?.Count > 0)
            {
                foreach (XmlElement commentNode in commentNodes)
                {
                    sectionComments.Add(commentNode.InnerText);
                }
            }

            return sectionComments;
        }

        /// <summary>
        /// Set the comments for a section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="comments">A string collection.</param>
        /// <return>The function returns a Boolean that shows if the change was done.</return>
        [Obsolete("Unused: this class is used to read XML files, not update them")]
        private bool SetXMLComments(string sectionName, IEnumerable<string> comments)
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
        /// Update the cached section names
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
        /// The method gets the sections.
        /// </summary>
        /// <return>A string collection of sections.</return>
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
        /// The method gets a collection of items for a section name.
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="itemType">Item type.</param>
        /// <return>The function returns a string collection of items in a section.</return>
        private List<string> GetItemsInSection(string sectionName, XMLItemTypeEnum itemType)
        {
            var items = new List<string>();
            XmlNode section = GetSection(sectionName);

            if (section == null)
            {
                return null;
            }

            var nodes = section.SelectNodes("item");
            if (nodes?.Count > 0)
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
        /// <param name="sectionName">Section name</param>
        /// <return>The function returns a string collection of all the keys in a section.</return>
        public List<string> AllKeysInSection(string sectionName)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            return GetItemsInSection(sectionName, XMLItemTypeEnum.GetKeys);
        }

        /// <summary>
        /// Gets a collection of values in a section.
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <return>A string collection of all the values in a section.</return>
        public List<string> AllValuesInSection(string sectionName)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            return GetItemsInSection(sectionName, XMLItemTypeEnum.GetValues);
        }

        /// <summary>
        /// Gets a collection of items in a section.
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <return>A string collection of all the items in a section.</return>
        public List<string> AllItemsInSection(string sectionName)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            return GetItemsInSection(sectionName, XMLItemTypeEnum.GetKeysAndValues);
        }

        /// <summary>
        /// Gets a custom attribute name.
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Key name</param>
        /// <param name="attributeName">Attribute name</param>
        /// <return>The function returns a string.</return>
        public string GetCustomIniAttribute(string sectionName, string keyName, string attributeName)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            if (!string.IsNullOrWhiteSpace(attributeName))
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
        /// Defines a custom attribute name
        /// If attributeValue is null, removes the attribute
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="keyName">Key name</param>
        /// <param name="attributeName">Attribute name</param>
        /// <param name="attributeValue">Value for the attribute</param>
        /// <return>The function returns a Boolean.</return>
        public bool SetCustomIniAttribute(string sectionName, string keyName, string attributeName, string attributeValue)
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            if (string.IsNullOrWhiteSpace(attributeName))
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
        /// Creates a section
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <return>True if successful, false if sectionName is empty or an error occurs</return>
        private bool CreateSection(string sectionName)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
                return false;

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

                        XmlFilePath = filePath;
                        Initialized = true;
                    }
                }
                else
                {
                    // File doesn't exist; create a new, blank .XML file
                    XmlFilePath = filePath;
                    using (var settingsFile = new FileStream(XmlFilePath, FileMode.Create, FileAccess.Write))
                    {
                        m_XmlDoc.Save(settingsFile);
                    }
                    Initialized = true;
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

        /// <summary>
        /// Manually parses a line to extract the settings information
        /// Supports the traditional .Ini file format
        /// Also supports the 'key="KeyName" value="Value"' method used in XML settings files
        /// If successful, adds attributes to the doc variable</summary>
        /// <param name="dataLine">Data line</param>
        /// <param name="doc">XmlDocument to track</param>
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

                        if (string.IsNullOrWhiteSpace(keyName))
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

        private bool ParseLineManualCheckTag(string dataLine, string tagToFind, out string tagValue)
        {
            tagValue = string.Empty;

            var matchIndex = dataLine.ToLower().IndexOf(tagToFind, StringComparison.Ordinal);

            if (matchIndex >= 0)
            {
                tagValue = dataLine.Substring(matchIndex + tagToFind.Length);

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
        /// Output file name used by the Save method
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
                if (fi.Directory?.Exists == false)
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
        /// Save the settings to the XML file specified by OutputFilename
        /// </summary>
        public void Save()
        {
            if (!Initialized)
                throw new XMLFileReaderNotInitializedException();

            if (OutputFilename != null && m_XmlDoc != null)
            {
                var outputFile = new FileInfo(OutputFilename);
                if (outputFile.Directory?.Exists == false)
                {
                    if (NotifyOnException)
                    {
                        throw new Exception("Invalid path.");
                    }
                    return;
                }

                if (outputFile.Exists)
                {
                    outputFile.Delete();
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
        /// Gets the System.Xml.XmlDocument
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
        /// <return>The XML document formatted as a string.</return>
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
