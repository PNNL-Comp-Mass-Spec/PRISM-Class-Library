using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Xml;

namespace PRISM
{

    /// <summary>
    /// Exception thrown if a call is made to a method prior to initializing IniFileReader
    /// </summary>
    /// <remarks></remarks>
    public class IniFileReaderNotInitializedException : ApplicationException
    {
        public override string Message => "The IniFileReader instance has not been properly initialized.";
    }

    /// <summary>
    /// Tools to manipulates INI files.
    /// </summary>
    public class IniFileReader : ILoggerAware
    {

        private enum IniItemTypeEnum
        {
            GetKeys = 0,
            GetValues = 1,
            GetKeysAndValues = 2
        }

        private string m_IniFilename;
        private XmlDocument m_XmlDoc;
        private StringCollection m_SectionNames = new StringCollection();
        private bool m_CaseSensitive;
        private string m_SaveFilename;

        private bool m_initialized;
        private ILogger m_ExceptionLogger;
        private ILogger m_EventLogger;
        public event InformationMessageEventHandler InformationMessage;
        public delegate void InformationMessageEventHandler(string msg);

        /// <summary>
        /// Initializes a new instance of the IniFileReader.
        /// </summary>
        /// <param name="filename">The name of the ini file.</param>
        /// <param name="logger">This is the logger.</param>
        public IniFileReader(string filename, ref ILogger logger)
        {
            RegisterExceptionLogger(logger);
            NotifyOnException = false;
            InitIniFileReader(filename, false);
        }

        /// <summary>
        /// Initializes a new instance of the IniFileReader.
        /// </summary>
        /// <param name="filename">The name of the ini file.</param>
        /// <param name="IsCaseSensitive">Case sensitive as boolean.</param>
        public IniFileReader(string filename, bool IsCaseSensitive)
        {
            NotifyOnException = true;
            InitIniFileReader(filename, IsCaseSensitive);
        }

        /// <summary>
        /// Initializes a new instance of the IniFileReader.
        /// </summary>
        /// <param name="filename">The name of the ini file.</param>
        /// <param name="logger">This is the logger.</param>
        /// <param name="IsCaseSensitive">Case sensitive as boolean.</param>
        public IniFileReader(string filename, ref ILogger logger, bool IsCaseSensitive)
        {
            RegisterExceptionLogger(logger);
            NotifyOnException = false;
            InitIniFileReader(filename, IsCaseSensitive);
        }

        /// <summary>
        /// Initializes a new instance of the IniFileReader.
        /// </summary>
        /// <param name="filename">The name of the ini file.</param>
        public IniFileReader(string filename)
        {
            NotifyOnException = true;
            InitIniFileReader(filename, false);
        }

        /// <summary>
        /// This routine is called by each of the constructors to make the actual assignments.
        /// </summary>
        private void InitIniFileReader(string filename, bool IsCaseSensitive)
        {
            m_CaseSensitive = IsCaseSensitive;
            m_XmlDoc = new XmlDocument();

            if (string.IsNullOrWhiteSpace(filename)) {
                return;
            }

            // try to load the file as an XML file
            try {
                m_XmlDoc.Load(filename);
                UpdateSections();
                m_IniFilename = filename;
                m_initialized = true;

            } catch {
                // load the default XML
                m_XmlDoc.LoadXml("<?xml version=\"1.0\" encoding=\"UTF-8\"?><sections></sections>");
                try
                {
                    var fi = new FileInfo(filename);
                    if ((fi.Exists)) {
                        using (var tr = fi.OpenText())
                        {
                            var s = tr.ReadLine();
                            while ((s != null)) {
                                ParseLineXml(s, m_XmlDoc);
                                s = tr.ReadLine();
                            }
                        }
                        m_IniFilename = filename;
                        m_initialized = true;
                    } else {
                        m_XmlDoc.Save(filename);
                        m_IniFilename = filename;
                        m_initialized = true;
                    }
                } catch (Exception e) {
                    m_ExceptionLogger?.PostError("Failed to read INI file.", e, true);
                    if (NotifyOnException) {
                        throw new Exception("Failed to read INI file.");
                    }
                }
            }
        }

        /// <summary>
        /// This routine returns the name of the ini file.
        /// </summary>
        /// <return>The function returns the name of ini file.</return>
        public string IniFilename {
            get {
                if (!Initialized)
                    throw new IniFileReaderNotInitializedException();
                return (m_IniFilename);
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
        public bool CaseSensitive => m_CaseSensitive;

        /// <summary>
        /// This routine sets a name.
        /// </summary>
        /// <param name="aName">The name to be set.</param>
        /// <return>The function returns a string.</return>
        private string SetNameCase(string aName)
        {
            if ((CaseSensitive)) {
                return aName;
            }

            return aName.ToLower();
        }

        /// <summary>
        /// Returns the root element
        /// </summary>
        private XmlElement GetRoot()
        {
            return m_XmlDoc.DocumentElement;
        }

        /// <summary>
        /// The function gets the last section.
        /// </summary>
        /// <return>The function returns the last section as XmlElement.</return>
        private XmlElement GetLastSection()
        {
            if (m_SectionNames.Count == 0) {
                return GetRoot();
            }

            return GetSection(m_SectionNames[m_SectionNames.Count - 1]);
        }

        /// <summary>
        /// The function gets a section as XmlElement.
        /// </summary>
        /// <param name="sectionName">The name of a section.</param>
        /// <return>The function returns a section as XmlElement.</return>
        private XmlElement GetSection(string sectionName)
        {
            if (!string.IsNullOrEmpty(sectionName)) {
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
            if (!string.IsNullOrEmpty(keyName)) {
                keyName = SetNameCase(keyName);
                var section = GetSection(sectionName);
                if (((section != null))) {
                    return (XmlElement)section.SelectSingleNode("item[@key='" + keyName + "']");
                }
            }
            return null;
        }

        /// <summary>
        /// The function sets the ini section name.
        /// </summary>
        /// <param name="oldSection">The name of the old ini section name.</param>
        /// <param name="newSection">The new name for the ini section.</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetIniSection(string oldSection, string newSection)
        {
            if (!Initialized) {
                throw new IniFileReaderNotInitializedException();
            }
            if (!string.IsNullOrEmpty(newSection))
            {
                var section = GetSection(oldSection);
                if (((section != null))) {
                    section.SetAttribute("name", SetNameCase(newSection));
                    UpdateSections();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The function sets a new value for the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "value".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetIniValue(string sectionName, string keyName, string newValue)
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();
            var section = GetSection(sectionName);
            if (section == null) {
                if (CreateSection(sectionName)) {
                    section = GetSection(sectionName);
                    // exit if keyName is Nothing or blank
                    if (string.IsNullOrEmpty(keyName)) {
                        return true;
                    }
                } else {
                    // can't create section
                    return false;
                }
            }
            if (keyName == null) {
                // delete the section
                return DeleteSection(sectionName);
            }

            var setting = GetItem(sectionName, keyName);
            if ((setting != null)) {
                if (newValue == null) {
                    // delete this item
                    return DeleteItem(sectionName, keyName);
                }

                // add or update the value attribute
                setting.SetAttribute("value", newValue);
                return true;
            }

            // try to create the item
            if ((!string.IsNullOrEmpty(keyName)) && ((newValue != null))) {
                // construct a new item (blank values are OK)
                var newSetting = m_XmlDoc.CreateElement("item");
                newSetting.SetAttribute("key", SetNameCase(keyName));
                newSetting.SetAttribute("value", newValue);
                section.AppendChild(newSetting);
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
            if (section != null) {
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
            var setting = GetItem(sectionName, keyName);
            if (setting != null)
            {
                setting.ParentNode?.RemoveChild(setting);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The function sets a new value for the "key" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        /// <param name="newValue">The new value for the "key".</param>
        /// <return>The function returns a boolean that shows if the change was done.</return>
        public bool SetIniKey(string sectionName, string keyName, string newValue)
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();
            var setting = GetItem(sectionName, keyName);
            if ((setting != null)) {
                setting.SetAttribute("key", SetNameCase(newValue));
                return true;
            }
            return false;
        }

        /// <summary>
        /// The function gets the name of the "value" attribute.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="keyName">The name of the key.</param>
        ///<return>The function returns the name of the "value" attribute.</return>
        public string GetIniValue(string sectionName, string keyName)
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();

            string value = null;
            XmlNode setting = GetItem(sectionName, keyName);
            if (setting?.Attributes != null)
                value = setting.Attributes.GetNamedItem("value").Value;

            if (string.IsNullOrEmpty(value))
            {
                // Setting not present or does not have attribute value
                return string.Empty;
            }

            return value;
        }

        /// <summary>
        /// The function gets the comments for a section name.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        ///<return>The function returns a string collection with comments</return>
        public StringCollection GetIniComments(string sectionName)
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();

            var sectionComments = new StringCollection();
            XmlNode target;
            if (sectionName == null) {
                target = m_XmlDoc.DocumentElement;
            } else {
                target = GetSection(sectionName);
            }

            var nodes = target?.SelectNodes("comment");
            if (nodes != null && nodes.Count > 0) {
                foreach (XmlNode commentNode in nodes) {
                    sectionComments.Add(commentNode.InnerText);
                }
            }
            return sectionComments;
        }

        /// <summary>
        /// The function sets a the comments for a section name.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <param name="comments">A string collection.</param>
        ///<return>The function returns a Boolean that shows if the change was done.</return>
        public bool SetIniComments(string sectionName, StringCollection comments)
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();
            XmlNode target;

            if (sectionName == null) {
                target = m_XmlDoc.DocumentElement;
            } else {
                target = GetSection(sectionName);
            }

            if (target != null) {
                var nodes = target.SelectNodes("comment");

                if (nodes != null)
                {
                    foreach (XmlNode commentNode in nodes)
                    {
                        target.RemoveChild(commentNode);
                    }
                }

                foreach (var s in comments) {
                    var comment = m_XmlDoc.CreateElement("comment");
                    comment.InnerText = s;
                    var lastComment = (XmlElement)target.SelectSingleNode("comment[last()]");
                    if (lastComment == null) {
                        target.PrependChild(comment);
                    } else {
                        target.InsertAfter(comment, lastComment);
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// The subroutine updates the sections.
        /// </summary>
        private void UpdateSections()
        {
            m_SectionNames = new StringCollection();

            var sectionNodes = m_XmlDoc?.SelectNodes("sections/section");
            if (sectionNodes == null)
                return;

            foreach (XmlElement item in sectionNodes) {
                m_SectionNames.Add(item.GetAttribute("name"));
            }
        }

        /// <summary>
        /// The subroutine gets the sections.
        /// </summary>
        /// <return>The subroutine returns a strin collection of sections.</return>
        public StringCollection AllSections {
            get {
                if (!Initialized) {
                    throw new IniFileReaderNotInitializedException();
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
        private StringCollection GetItemsInSection(string sectionName, IniItemTypeEnum itemType)
        {
            var items = new StringCollection();
            XmlNode section = GetSection(sectionName);
            if (section == null) {
                return null;
            }

            var nodes = section.SelectNodes("item");
            if (nodes != null && nodes.Count > 0) {
                foreach (XmlNode setting in nodes)
                {

                    if (setting.Attributes == null)
                        continue;

                    switch (itemType) {
                        case IniItemTypeEnum.GetKeys:
                            items.Add(setting.Attributes.GetNamedItem("key").Value);
                            break;
                        case IniItemTypeEnum.GetValues:
                            items.Add(setting.Attributes.GetNamedItem("value").Value);
                            break;
                        case IniItemTypeEnum.GetKeysAndValues:
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
                throw new IniFileReaderNotInitializedException();
            return GetItemsInSection(sectionName, IniItemTypeEnum.GetKeys);
        }

        /// <summary>
        /// Gets a collection of values in a section.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <return>The function returns a string colection of all the values in a section.</return>
        public StringCollection AllValuesInSection(string sectionName)
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();
            return GetItemsInSection(sectionName, IniItemTypeEnum.GetValues);
        }

        /// <summary>
        /// Gets a collection of items in a section.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        /// <return>The function returns a string colection of all the items in a section.</return>
        public StringCollection AllItemsInSection(string sectionName)
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();
            return (GetItemsInSection(sectionName, IniItemTypeEnum.GetKeysAndValues));
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
                throw new IniFileReaderNotInitializedException();
            if (!string.IsNullOrEmpty(attributeName))
            {
                var setting = GetItem(sectionName, keyName);
                if ((setting != null)) {
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
                throw new IniFileReaderNotInitializedException();
            if (!string.IsNullOrEmpty(attributeName))
            {
                var setting = GetItem(sectionName, keyName);
                if ((setting != null)) {
                    try {
                        if (attributeValue == null) {
                            // delete the attribute
                            setting.RemoveAttribute(attributeName);
                            return true;
                        }

                        attributeName = SetNameCase(attributeName);
                        setting.SetAttribute(attributeName, attributeValue);
                        return true;
                    } catch (Exception e) {
                        m_ExceptionLogger?.PostError("Failed to create item.", e, true);
                        if (NotifyOnException) {
                            throw new Exception("Failed to create item.");
                        }
                    }
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
            if (string.IsNullOrEmpty(sectionName))
                return false;

            sectionName = SetNameCase(sectionName);
            try {
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
                
            } catch (Exception e) {
                m_ExceptionLogger?.PostError("Failed to create item.", e, true);
                if (NotifyOnException) {
                    throw new Exception("Failed to create item.");
                }
                return false;
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
            try {
                var section = GetSection(sectionName);
                if ((section != null)) {
                    var item = m_XmlDoc.CreateElement("item");
                    item.SetAttribute("key", keyName);
                    item.SetAttribute("newValue", newValue);
                    section.AppendChild(item);
                    return true;
                }
                return false;
            } catch (Exception e) {
                m_ExceptionLogger?.PostError("Failed to create item.", e, true);
                if (NotifyOnException) {
                    throw new Exception("Failed to create item.");
                }
                return false;
            }
        }

        /// <summary>
        /// It parses a string and adds atribbutes to the XMLDocument.
        /// </summary>
        /// <param name="s">The name of the string to be parse.</param>
        /// <param name="doc">The name of the XmlDocument.</param>
        private void ParseLineXml(string s, XmlDocument doc)
        {
            s = s.TrimStart();

            if (s.Length == 0) {
                return;
            }

            switch ((s.Substring(0, 1))) {
                case "[":
                    // this is a section
                    // trim the first and last characters
                    s = s.TrimStart('[');
                    s = s.TrimEnd(']');
                    // create a new section element
                    CreateSection(s);
                    break;
                case ";":
                    // new comment
                    var commentElement = doc.CreateElement("comment");
                    commentElement.InnerText = s.Substring(1);
                    GetLastSection().AppendChild(commentElement);
                    break;
                default:
                    // split the string on the "=" sign, if present
                    string key;
                    string value;
                    if ((s.IndexOf('=') > 0)) {
                        var parts = s.Split('=');
                        key = parts[0].Trim();
                        value = parts[1].Trim();
                    } else {
                        key = s;
                        value = "";
                    }
                    var item = doc.CreateElement("item");

                    var keyAttribute= doc.CreateAttribute("key");
                    keyAttribute.Value = SetNameCase(key);
                    item.Attributes.SetNamedItem(keyAttribute);

                    var valueAttribute = doc.CreateAttribute("value");
                    valueAttribute.Value = value;
                    item.Attributes.SetNamedItem(valueAttribute);
                    GetLastSection().AppendChild(item);
                    break;
            }

        }

        /// <summary>
        /// Sets or Gets the output file name.
        /// </summary>
        public string OutputFilename {
            get {
                if (!Initialized)
                    throw new IniFileReaderNotInitializedException();
                return m_SaveFilename;
            }
            set {
                if (!Initialized)
                    throw new IniFileReaderNotInitializedException();
                var fi = new FileInfo(value);
                if (fi.Directory == null || !fi.Directory.Exists) {
                    m_ExceptionLogger?.PostEntry("Invalid path for output file.", logMsgType.logError, true);
                    if (NotifyOnException) {
                        throw new Exception("Invalid path for output file.");
                    }
                } else {
                    m_SaveFilename = value;
                }
            }
        }

        /// <summary>
        /// Saves the data to the Xml output file
        /// </summary>
        public void Save()
        {
            if (!Initialized)
                throw new IniFileReaderNotInitializedException();
            if ((OutputFilename != null) && (m_XmlDoc != null)) {
                var fi = new FileInfo(OutputFilename);
                if (fi.Directory == null || !fi.Directory.Exists) {
                    m_ExceptionLogger?.PostEntry("Invalid path.", logMsgType.logError, true);
                    if (NotifyOnException) {
                        throw new Exception("Invalid path.");
                    }
                    return;
                }
                if (fi.Exists) {
                    fi.Delete();
                    m_XmlDoc.Save(OutputFilename);
                } else {
                    m_XmlDoc.Save(OutputFilename);
                }
                m_EventLogger?.PostEntry("File save complete.", logMsgType.logNormal, true);
                if (NotifyOnEvent)
                {
                    InformationMessage?.Invoke("File save complete.");
                }
            } else {
                m_ExceptionLogger?.PostEntry("Not Output File name specified.", logMsgType.logError, true);
                if (NotifyOnException) {
                    throw new Exception("Not Output File name specified.");
                }
            }
        }

        // <summary>It transforms a XML file to an INI file.</summary>
        // <return>The function returns document formatted as a string.</return>
        //'Public Function AsIniFile() As String
        //'    If Not Initialized Then Throw New IniFileReaderNotInitializedException
        //'    Try
        //'        Dim xsl As XslTransform = New XslTransform
        //'        Dim resolver As XmlUrlResolver = New XmlUrlResolver
        //'        xsl.Load("c:\\XMLToIni.xslt")
        //'        Dim sb As StringBuilder = New StringBuilder()
        //'        Dim sw As StringWriter = New StringWriter(sb)
        //'        xsl.Transform(m_XmlDoc, Nothing, sw, resolver)
        //'        sw.Close()
        //'        Return sb.ToString
        //'    Catch e As Exception
        //'        If Not IsNothing(m_ExceptionLogger) Then
        //'            m_ExceptionLogger.PostError("Error transforming XML to INI file.", e, True)
        //'        End If
        //'        If NotifyOnException Then
        //'            Throw New Exception("Error transforming XML to INI file.")
        //'        End If
        //'        Return Nothing
        //'    End Try
        //'End Function

        /// <summary>
        /// It gets the XmlDocument.
        /// </summary>
        public XmlDocument XmlDoc {
            get {
                if (!Initialized)
                    throw new IniFileReaderNotInitializedException();
                return m_XmlDoc;
            }
        }

        /// <summary>
        /// Converts an XML document to a string.
        /// </summary>
        /// <return>It returns the XML document formatted as a string.</return>
        public string XML {
            get {
                if (!Initialized)
                    throw new IniFileReaderNotInitializedException();
                var sb = new StringBuilder();
                using (var sw = new StringWriter(sb))
                using (var xw = new XmlTextWriter(sw))
                {
                    xw.Indentation = 3;
                    xw.Formatting = Formatting.Indented;
                    m_XmlDoc.WriteContentTo(xw);
                }
                
                return sb.ToString();
            }
        }

        /// <summary>
        /// Associates an exception logger with this class
        /// </summary>
        public void RegisterExceptionLogger(ILogger logger)
        {
            m_ExceptionLogger = logger;
        }

        void ILoggerAware.RegisterExceptionLogger(ILogger logger)
        {
            RegisterExceptionLogger(logger);
        }

        /// <summary>
        /// Sets the name of the event logger
        /// </summary>
        public void RegisterEventLogger(ILogger logger)
        {
            m_EventLogger = logger;
        }

        /// <summary>
        /// Associates an event logger with this class
        /// </summary>
        void ILoggerAware.RegisterEventLogger(ILogger logger)
        {
            RegisterEventLogger(logger);
        }


        /// <summary>
        /// Gets or Sets notify on event.
        /// </summary>
        public bool NotifyOnEvent { get; set; }


        /// <summary>
        /// Gets or Sets notify on exception.
        /// </summary>
        public bool NotifyOnException { get; set; }

    }
}
