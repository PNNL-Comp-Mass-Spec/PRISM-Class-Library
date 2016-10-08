Option Strict On

Imports System
Imports System.Collections.Specialized
Imports System.IO
Imports System.Xml
Imports System.Text
Imports PRISM.Logging

Namespace Files

    ''' <summary>
    ''' Exception thrown if a call is made to a method prior to initializing IniFileReader
    ''' </summary>
    ''' <remarks></remarks>
    Public Class IniFileReaderNotInitializedException
        Inherits ApplicationException
        Public Overrides ReadOnly Property Message() As String
            Get
                Return "The IniFileReader instance has not been properly initialized."
            End Get
        End Property
    End Class

    ''' <summary>Tools to manipulates INI files.</summary>
    Public Class IniFileReader
        Implements ILoggerAware

        Private Enum IniItemTypeEnum
            GetKeys = 0
            GetValues = 1
            GetKeysAndValues = 2
        End Enum

        Private m_IniFilename As String
        Private m_XmlDoc As XmlDocument
        Private sections As StringCollection = New StringCollection
        Private m_CaseSensitive As Boolean = False
        Private m_SaveFilename As String
        Private m_initialized As Boolean = False

        Private m_ExceptionLogger As ILogger
        Private m_EventLogger As ILogger
        Public Event InformationMessage(msg As String)

        ''' <summary>Initializes a new instance of the IniFileReader.</summary>
        ''' <param name="filename">The name of the ini file.</param>
        ''' <param name="logger">This is the logger.</param>
        Public Sub New(filename As String, ByRef logger As ILogger)
            RegisterExceptionLogger(logger)
            NotifyOnException = False
            InitIniFileReader(filename, False)
        End Sub

        ''' <summary>Initializes a new instance of the IniFileReader.</summary>
        ''' <param name="filename">The name of the ini file.</param>
        ''' <param name="IsCaseSensitive">Case sensitive as boolean.</param>
        Public Sub New(filename As String, IsCaseSensitive As Boolean)
            NotifyOnException = True
            InitIniFileReader(filename, IsCaseSensitive)
        End Sub

        ''' <summary>Initializes a new instance of the IniFileReader.</summary>
        ''' <param name="filename">The name of the ini file.</param>
        ''' <param name="logger">This is the logger.</param>
        ''' <param name="IsCaseSensitive">Case sensitive as boolean.</param>
        Public Sub New(filename As String, ByRef logger As ILogger, IsCaseSensitive As Boolean)
            RegisterExceptionLogger(logger)
            NotifyOnException = False
            InitIniFileReader(filename, IsCaseSensitive)
        End Sub

        ''' <summary>Initializes a new instance of the IniFileReader.</summary>
        ''' <param name="filename">The name of the ini file.</param>
        Public Sub New(filename As String)
            NotifyOnException = True
            InitIniFileReader(filename, False)
        End Sub

        ''' <summary>
        ''' This routine is called by each of the constructors to make the actual assignments.
        ''' </summary>
        Private Sub InitIniFileReader(filename As String, IsCaseSensitive As Boolean)
            Dim fi As FileInfo
            Dim s As String
            m_CaseSensitive = IsCaseSensitive
            m_XmlDoc = New XmlDocument

            If String.IsNullOrWhiteSpace(filename) Then
                Return
            End If

            ' try to load the file as an XML file
            Try
                m_XmlDoc.Load(filename)
                UpdateSections()
                m_IniFilename = filename
                m_initialized = True

            Catch
                ' load the default XML
                m_XmlDoc.LoadXml("<?xml version=""1.0"" encoding=""UTF-8""?><sections></sections>")
                Try
                    fi = New FileInfo(filename)
                    If (fi.Exists) Then
                        Using tr As TextReader = fi.OpenText
                            s = tr.ReadLine()
                            Do While Not s Is Nothing
                                ParseLineXml(s, m_XmlDoc)
                                s = tr.ReadLine()
                            Loop
                        End Using
                        m_IniFilename = filename
                        m_initialized = True
                    Else
                        m_XmlDoc.Save(filename)
                        m_IniFilename = filename
                        m_initialized = True
                    End If
                Catch e As Exception
                    If Not IsNothing(m_ExceptionLogger) Then
                        m_ExceptionLogger.PostError("Failed to read INI file.", e, True)
                    End If
                    If NotifyOnException Then
                        Throw New Exception("Failed to read INI file.")
                    End If
                End Try
            End Try
        End Sub

        ''' <summary>
        ''' This routine returns the name of the ini file.
        ''' </summary>
        ''' <return>The function returns the name of ini file.</return>
        Public ReadOnly Property IniFilename() As String
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Return (m_IniFilename)
            End Get
        End Property

        ''' <summary>
        ''' This routine returns a boolean showing if the file was initialized or not.
        ''' </summary>
        ''' <return>The function returns a Boolean.</return>
        Public ReadOnly Property Initialized() As Boolean
            Get
                Return m_initialized
            End Get
        End Property

        ''' <summary>
        ''' This routine returns a boolean showing if the name is case sensitive or not.
        ''' </summary>
        ''' <return>The function returns a Boolean.</return>
        Public ReadOnly Property CaseSensitive() As Boolean
            Get
                Return m_CaseSensitive
            End Get
        End Property

        ''' <summary>
        ''' This routine sets a name.
        ''' </summary>
        ''' <param name="aName">The name to be set.</param>
        ''' <return>The function returns a string.</return>
        Private Function SetNameCase(aName As String) As String
            If (CaseSensitive) Then
                Return aName
            Else
                Return aName.ToLower()
            End If
        End Function

        ''' <summary>
        ''' Returns the root element
        ''' </summary>
        Private Function GetRoot() As XmlElement
            Return m_XmlDoc.DocumentElement
        End Function

        ''' <summary>
        ''' The function gets the last section.
        ''' </summary>
        ''' <return>The function returns the last section as XmlElement.</return>
        Private Function GetLastSection() As XmlElement
            If sections.Count = 0 Then
                Return GetRoot()
            Else
                Return GetSection(sections(sections.Count - 1))
            End If
        End Function

        ''' <summary>
        ''' The function gets a section as XmlElement.
        ''' </summary>
        ''' <param name="sectionName">The name of a section.</param>
        ''' <return>The function returns a section as XmlElement.</return>
        Private Function GetSection(sectionName As String) As XmlElement
            If (Not (sectionName = Nothing)) AndAlso (sectionName <> "") Then
                sectionName = SetNameCase(sectionName)
                Return CType(m_XmlDoc.SelectSingleNode("//section[@name='" & sectionName & "']"), XmlElement)
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' The function gets an item.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <return>The function returns a XML element.</return>
        Private Function GetItem(sectionName As String, keyName As String) As XmlElement
            Dim section As XmlElement
            If (Not keyName Is Nothing) AndAlso (keyName <> "") Then
                keyName = SetNameCase(keyName)
                section = GetSection(sectionName)
                If (Not section Is Nothing) Then
                    Return CType(section.SelectSingleNode("item[@key='" + keyName + "']"), XmlElement)
                End If
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' The function sets the ini section name.
        ''' </summary>
        ''' <param name="oldSection">The name of the old ini section name.</param>
        ''' <param name="newSection">The new name for the ini section.</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetIniSection(oldSection As String, newSection As String) As Boolean
            Dim section As XmlElement
            If Not Initialized Then
                Throw New IniFileReaderNotInitializedException
            End If
            If (Not newSection Is Nothing) AndAlso (newSection <> "") Then
                section = GetSection(oldSection)
                If (Not (section Is Nothing)) Then
                    section.SetAttribute("name", SetNameCase(newSection))
                    UpdateSections()
                    Return True
                End If
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function sets a new value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetIniValue(sectionName As String, keyName As String, newValue As String) As Boolean
            Dim item As XmlElement
            Dim section As XmlElement
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            section = GetSection(sectionName)
            If section Is Nothing Then
                If CreateSection(sectionName) Then
                    section = GetSection(sectionName)
                    ' exit if keyName is Nothing or blank
                    If (keyName Is Nothing) OrElse (keyName = "") Then
                        Return True
                    End If
                Else
                    ' can't create section
                    Return False
                End If
            End If
            If keyName Is Nothing Then
                ' delete the section
                Return DeleteSection(sectionName)
            End If

            item = GetItem(sectionName, keyName)
            If Not item Is Nothing Then
                If newValue Is Nothing Then
                    ' delete this item
                    Return DeleteItem(sectionName, keyName)
                Else
                    ' add or update the value attribute
                    item.SetAttribute("value", newValue)
                    Return True
                End If
            Else
                ' try to create the item
                If (keyName <> "") AndAlso (Not newValue Is Nothing) Then
                    ' construct a new item (blank values are OK)
                    item = m_XmlDoc.CreateElement("item")
                    item.SetAttribute("key", SetNameCase(keyName))
                    item.SetAttribute("value", newValue)
                    section.AppendChild(item)
                    Return True
                End If
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function deletes a section in the file.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a boolean that shows if the delete was completed.</return>
        Private Function DeleteSection(sectionName As String) As Boolean
            Dim section As XmlElement = GetSection(sectionName)
            If Not section Is Nothing Then
                section.ParentNode.RemoveChild(section)
                UpdateSections()
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function deletes a item in a specific section.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <return>The function returns a boolean that shows if the delete was completed.</return>
        Private Function DeleteItem(sectionName As String, keyName As String) As Boolean
            Dim item As XmlElement = GetItem(sectionName, keyName)
            If Not item Is Nothing Then
                item.ParentNode.RemoveChild(item)
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function sets a new value for the "key" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "key".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetIniKey(sectionName As String, keyName As String, newValue As String) As Boolean
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim item As XmlElement = GetItem(sectionName, keyName)
            If Not item Is Nothing Then
                item.SetAttribute("key", SetNameCase(newValue))
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        '''<return>The function returns the name of the "value" attribute.</return>
        Public Function GetIniValue(sectionName As String, keyName As String) As String
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim N As XmlNode = GetItem(sectionName, keyName)
            If Not N Is Nothing Then
                Return (N.Attributes.GetNamedItem("value").Value)
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' The function gets the comments for a section name.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        '''<return>The function returns a string collection with comments</return>
        Public Function GetIniComments(sectionName As String) As StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim sc = New StringCollection
            Dim target As XmlNode
            Dim nodes As XmlNodeList
            Dim N As XmlNode
            If sectionName Is Nothing Then
                target = m_XmlDoc.DocumentElement
            Else
                target = GetSection(sectionName)
            End If
            If Not target Is Nothing Then
                nodes = target.SelectNodes("comment")
                If nodes.Count > 0 Then
                    For Each N In nodes
                        sc.Add(N.InnerText)
                    Next
                End If
            End If
            Return sc
        End Function

        ''' <summary>
        ''' The function sets a the comments for a section name.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="comments">A string collection.</param>
        '''<return>The function returns a Boolean that shows if the change was done.</return>
        Public Function SetIniComments(sectionName As String, comments As StringCollection) As Boolean
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim target As XmlNode
            Dim nodes As XmlNodeList
            Dim N As XmlNode
            Dim s As String
            Dim NLastComment As XmlElement
            If sectionName Is Nothing Then
                target = m_XmlDoc.DocumentElement
            Else
                target = GetSection(sectionName)
            End If
            If Not target Is Nothing Then
                nodes = target.SelectNodes("comment")
                For Each N In nodes
                    target.RemoveChild(N)
                Next
                For Each s In comments
                    N = m_XmlDoc.CreateElement("comment")
                    N.InnerText = s
                    NLastComment = CType(target.SelectSingleNode("comment[last()]"), XmlElement)
                    If NLastComment Is Nothing Then
                        target.PrependChild(N)
                    Else
                        target.InsertAfter(N, NLastComment)
                    End If
                Next
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The subroutine updades the sections.
        ''' </summary>
        Private Sub UpdateSections()
            sections = New StringCollection
            Dim N As XmlElement
            For Each N In m_XmlDoc.SelectNodes("sections/section")
                sections.Add(N.GetAttribute("name"))
            Next
        End Sub

        ''' <summary>
        ''' The subroutine gets the sections.
        ''' </summary>
        ''' <return>The subroutine returns a strin collection of sections.</return>
        Public ReadOnly Property AllSections() As StringCollection
            Get
                If Not Initialized Then
                    Throw New IniFileReaderNotInitializedException
                End If
                Return sections
            End Get
        End Property

        ''' <summary>
        ''' The function gets a collection of items for a section name.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="itemType">Item type.</param>
        ''' <return>The function returns a string colection of items in a section.</return>
        Private Function GetItemsInSection(sectionName As String, itemType As IniItemTypeEnum) As StringCollection
            Dim nodes As XmlNodeList
            Dim items = New StringCollection
            Dim section As XmlNode = GetSection(sectionName)
            Dim N As XmlNode
            If section Is Nothing Then
                Return Nothing
            Else
                nodes = section.SelectNodes("item")
                If nodes.Count > 0 Then
                    For Each N In nodes
                        Select Case itemType
                            Case IniItemTypeEnum.GetKeys
                                items.Add(N.Attributes.GetNamedItem("key").Value)
                            Case IniItemTypeEnum.GetValues
                                items.Add(N.Attributes.GetNamedItem("value").Value)
                            Case IniItemTypeEnum.GetKeysAndValues
                                items.Add(N.Attributes.GetNamedItem("key").Value & "=" &
                                N.Attributes.GetNamedItem("value").Value)
                        End Select
                    Next
                End If
                Return items
            End If
        End Function

        ''' <summary>The funtions gets a collection of keys in a section.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a string colection of all the keys in a section.</return>
        Public Function AllKeysInSection(sectionName As String) As StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Return GetItemsInSection(sectionName, IniItemTypeEnum.GetKeys)
        End Function

        ''' <summary>The funtions gets a collection of values in a section.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a string colection of all the values in a section.</return>
        Public Function AllValuesInSection(sectionName As String) As StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Return GetItemsInSection(sectionName, IniItemTypeEnum.GetValues)
        End Function

        ''' <summary>The funtions gets a collection of items in a section.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a string colection of all the items in a section.</return>
        Public Function AllItemsInSection(sectionName As String) As StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Return (GetItemsInSection(sectionName, IniItemTypeEnum.GetKeysAndValues))
        End Function

        ''' <summary>The funtions gets a custom attribute name.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="attributeName">The name of the attribute.</param>
        ''' <return>The function returns a string.</return>
        Public Function GetCustomIniAttribute(sectionName As String, keyName As String, attributeName As String) As String
            Dim N As XmlElement
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            If (Not attributeName Is Nothing) AndAlso (attributeName <> "") Then
                N = GetItem(sectionName, keyName)
                If Not N Is Nothing Then
                    attributeName = SetNameCase(attributeName)
                    Return N.GetAttribute(attributeName)
                End If
            End If
            Return Nothing
        End Function

        ''' <summary>The funtions sets a custom attribute name.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="attributeName">The name of the attribute.</param>
        ''' <param name="attributeValue">The value of the attribute.</param>
        ''' <return>The function returns a Boolean.</return>
        Public Function SetCustomIniAttribute(sectionName As String, keyName As String, attributeName As String, attributeValue As String) As Boolean
            Dim N As XmlElement
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            If attributeName <> "" Then
                N = GetItem(sectionName, keyName)
                If Not N Is Nothing Then
                    Try
                        If attributeValue Is Nothing Then
                            ' delete the attribute
                            N.RemoveAttribute(attributeName)
                            Return True
                        Else
                            attributeName = SetNameCase(attributeName)
                            N.SetAttribute(attributeName, attributeValue)
                            Return True
                        End If

                    Catch e As Exception
                        If Not IsNothing(m_ExceptionLogger) Then
                            m_ExceptionLogger.PostError("Failed to create item.", e, True)
                        End If
                        If NotifyOnException Then
                            Throw New Exception("Failed to create item.")
                        End If
                    End Try
                End If
            End If
            Return False
        End Function

        ''' <summary>The funtions creates a section name.</summary>
        ''' <param name="sectionName">The name of the section to be created.</param>
        ''' <return>The function returns a Boolean.</return>
        Private Function CreateSection(sectionName As String) As Boolean
            Dim N As XmlElement
            Dim Natt As XmlAttribute
            If (Not sectionName Is Nothing) AndAlso (sectionName <> "") Then
                sectionName = SetNameCase(sectionName)
                Try
                    N = m_XmlDoc.CreateElement("section")
                    Natt = m_XmlDoc.CreateAttribute("name")
                    Natt.Value = SetNameCase(sectionName)
                    N.Attributes.SetNamedItem(Natt)
                    m_XmlDoc.DocumentElement.AppendChild(N)
                    sections.Add(Natt.Value)
                    Return True
                Catch e As Exception
                    If Not IsNothing(m_ExceptionLogger) Then
                        m_ExceptionLogger.PostError("Failed to create item.", e, True)
                    End If
                    If NotifyOnException Then
                        Throw New Exception("Failed to create item.")
                    End If
                    Return False
                End Try
            End If
            Return False
        End Function

        ''' <summary>The funtions creates a section name.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value to be created.</param>
        ''' <return>The function returns a Boolean.</return>
        Private Function CreateItem(sectionName As String, keyName As String, newValue As String) As Boolean
            Dim item As XmlElement
            Dim section As XmlElement
            Try
                section = GetSection(sectionName)
                If Not section Is Nothing Then
                    item = m_XmlDoc.CreateElement("item")
                    item.SetAttribute("key", keyName)
                    item.SetAttribute("newValue", newValue)
                    section.AppendChild(item)
                    Return True
                End If
                Return False
            Catch e As Exception
                If Not IsNothing(m_ExceptionLogger) Then
                    m_ExceptionLogger.PostError("Failed to create item.", e, True)
                End If
                If NotifyOnException Then
                    Throw New Exception("Failed to create item.")
                End If
                Return False
            End Try
        End Function

        ''' <summary>It parses a string and adds atribbutes to the XMLDocument.</summary>
        ''' <param name="s">The name of the string to be parse.</param>
        ''' <param name="doc">The name of the XmlDocument.</param>
        Private Sub ParseLineXml(s As String, doc As XmlDocument)
            Dim key As String
            Dim value As String
            Dim N As XmlElement
            Dim Natt As XmlAttribute
            Dim parts() As String

            s = s.TrimStart()

            If s.Length = 0 Then
                Return
            End If

            Select Case (s.Substring(0, 1))
                Case "["
                    ' this is a section
                    ' trim the first and last characters
                    s = s.TrimStart("["c)
                    s = s.TrimEnd("]"c)
                    ' create a new section element
                    CreateSection(s)
                Case ";"
                    ' new comment
                    N = doc.CreateElement("comment")
                    N.InnerText = s.Substring(1)
                    GetLastSection().AppendChild(N)
                Case Else
                    ' split the string on the "=" sign, if present
                    If (s.IndexOf("="c) > 0) Then
                        parts = s.Split("="c)
                        key = parts(0).Trim()
                        value = parts(1).Trim()
                    Else
                        key = s
                        value = ""
                    End If
                    N = doc.CreateElement("item")
                    Natt = doc.CreateAttribute("key")
                    Natt.Value = SetNameCase(key)
                    N.Attributes.SetNamedItem(Natt)
                    Natt = doc.CreateAttribute("value")
                    Natt.Value = value
                    N.Attributes.SetNamedItem(Natt)
                    GetLastSection().AppendChild(N)
            End Select

        End Sub

        ''' <summary>It Sets or Gets the output file name.</summary>
        Public Property OutputFilename() As String
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Return m_SaveFilename
            End Get
            Set(Value As String)
                Dim fi As FileInfo
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                fi = New FileInfo(Value)
                If Not fi.Directory.Exists Then
                    If Not IsNothing(m_ExceptionLogger) Then
                        m_ExceptionLogger.PostEntry("Invalid path for output file.", ILogger.logMsgType.logError, True)
                    End If
                    If NotifyOnException Then
                        Throw New Exception("Invalid path for output file.")
                    End If
                Else
                    m_SaveFilename = Value
                End If
            End Set
        End Property

        ''' <summary>Saves the data to the Xml output file</summary>
        Public Sub Save()
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            If Not OutputFilename Is Nothing AndAlso Not m_XmlDoc Is Nothing Then
                Dim fi = New FileInfo(OutputFilename)
                If Not fi.Directory.Exists Then
                    If Not IsNothing(m_ExceptionLogger) Then
                        m_ExceptionLogger.PostEntry("Invalid path.", ILogger.logMsgType.logError, True)
                    End If
                    If NotifyOnException Then
                        Throw New Exception("Invalid path.")
                    End If
                    Return
                End If
                If fi.Exists Then
                    fi.Delete()
                    m_XmlDoc.Save(OutputFilename)
                Else
                    m_XmlDoc.Save(OutputFilename)
                End If
                If Not IsNothing(m_EventLogger) Then
                    m_EventLogger.PostEntry("File save complete.", ILogger.logMsgType.logNormal, True)
                End If
                If NotifyOnEvent Then
                    RaiseEvent InformationMessage("File save complete.")
                End If
            Else
                If Not IsNothing(m_ExceptionLogger) Then
                    m_ExceptionLogger.PostEntry("Not Output File name specified.", ILogger.logMsgType.logError, True)
                End If
                If NotifyOnException Then
                    Throw New Exception("Not Output File name specified.")
                End If
            End If
        End Sub

        ' <summary>It transforms a XML file to an INI file.</summary>
        ' <return>The function returns document formatted as a string.</return>
        ''Public Function AsIniFile() As String
        ''    If Not Initialized Then Throw New IniFileReaderNotInitializedException
        ''    Try
        ''        Dim xsl As XslTransform = New XslTransform
        ''        Dim resolver As XmlUrlResolver = New XmlUrlResolver
        ''        xsl.Load("c:\\XMLToIni.xslt")
        ''        Dim sb As StringBuilder = New StringBuilder()
        ''        Dim sw As StringWriter = New StringWriter(sb)
        ''        xsl.Transform(m_XmlDoc, Nothing, sw, resolver)
        ''        sw.Close()
        ''        Return sb.ToString
        ''    Catch e As Exception
        ''        If Not IsNothing(m_ExceptionLogger) Then
        ''            m_ExceptionLogger.PostError("Error transforming XML to INI file.", e, True)
        ''        End If
        ''        If NotifyOnException Then
        ''            Throw New Exception("Error transforming XML to INI file.")
        ''        End If
        ''        Return Nothing
        ''    End Try
        ''End Function

        ''' <summary>It gets the XmlDocument.</summary>
        Public ReadOnly Property XmlDoc() As XmlDocument
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Return m_XmlDoc
            End Get
        End Property

        ''' <summary>Converts an XML document to a string.</summary>
        ''' <return>It returns the XML document formatted as a string.</return>
        Public ReadOnly Property XML() As String
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Dim sb = New StringBuilder()
                Dim sw = New StringWriter(sb)
                Dim xw = New XmlTextWriter(sw)
                xw.Indentation = 3
                xw.Formatting = Formatting.Indented
                m_XmlDoc.WriteContentTo(xw)
                xw.Close()
                sw.Close()
                Return sb.ToString()
            End Get
        End Property

        ''' <summary>Sets the name of the exception logger</summary>
        Public Sub RegisterExceptionLogger(logger As ILogger) Implements ILoggerAware.RegisterEventLogger
            m_ExceptionLogger = logger
        End Sub

        ''' <summary>Sets the name of the event logger</summary>
        Public Sub RegisterEventLogger(logger As ILogger) Implements ILoggerAware.RegisterExceptionLogger
            m_EventLogger = logger
        End Sub

        ''' <summary>Gets or Sets notify on event.</summary>
        Public Property NotifyOnEvent As Boolean Implements ILoggerAware.NotifyOnEvent


        ''' <summary>Gets or Sets notify on exception.</summary>
        Public Property NotifyOnException As Boolean Implements ILoggerAware.NotifyOnException

    End Class
End Namespace