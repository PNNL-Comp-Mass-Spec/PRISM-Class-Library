Option Strict On

' This class can be used to read or write settings in an Xml settings file
' Based on a class from the DMS Analysis Manager software written by Dave Clark and Gary Kiebel (PNNL, Richland, WA)
' Additional features added by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in October 2003
' Copyright 2005, Battelle Memorial Institute
'
' Updated in October 2004 to truly be case-insensitive if IsCaseSensitive = False when calling LoadSettings()

Imports PRISM.Logging
Namespace Files
    Public Class XmlSettingsFileAccessor
        Implements ILoggerAware

        Private Structure udtRecentSectionType
            Public SectionName As String            ' Stores the section name whose keys are cached; the section name is capitalized identically to that actually present in the Xml file
            Public htKeys As Hashtable
        End Structure

        ' Ini file reader
        ' Call LoadSettings to initialize, even if simply saving settings
        Private m_IniFilePath As String = ""
        Private WithEvents m_iniFileAccessor As IniFileReader

        Private mCaseSensitive As Boolean

        ' When mCaseSensitive = False, then htSectionNames stores mapping between lowercase section name and actual section name stored in file
        '   If section is present more than once in file, then only grabs the last occurence of the section
        ' When mCaseSensitive = True, then the mappings in htSectionNames are effectively not used
        Private htSectionNames As Hashtable
        Private mCachedSection As udtRecentSectionType

        Public Event InformationMessage(ByVal msg As String)

        ''' <summary>
        ''' Loads the settings for the defined Xml Settings File.  Assumes names are not case sensitive
        ''' </summary>
        ''' <return>The function returns a boolean that shows if the file was successfully loaded.</return>
        Public Function LoadSettings() As Boolean
            Return LoadSettings(m_IniFilePath, False)
        End Function

        ''' <summary>
        ''' Loads the settings for the defined Xml Settings File.   Assumes names are not case sensitive
        ''' </summary>
        ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        ''' <return>The function returns a boolean that shows if the file was successfully loaded.</return>
        Public Function LoadSettings(ByVal XmlSettingsFilePath As String) As Boolean
            Return LoadSettings(XmlSettingsFilePath, False)
        End Function

        ''' <summary>
        ''' Loads the settings for the defined Xml Settings File
        ''' </summary>
        ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        ''' <param name="IsCaseSensitive">Case sensitive names if True. Non-case sensitive if false.</param>
        ''' <return>The function returns a boolean that shows if the file was successfully loaded.</return>
        Public Function LoadSettings(ByVal XmlSettingsFilePath As String, ByVal IsCaseSensitive As Boolean) As Boolean

            mCaseSensitive = IsCaseSensitive

            m_IniFilePath = XmlSettingsFilePath

            ' Note: Always set IsCaseSensitive = True for IniFileReader's constructor since this class handles 
            '       case sensitivity mapping internally
            m_iniFileAccessor = New IniFileReader(m_IniFilePath, True)
            If m_iniFileAccessor Is Nothing Then
                Return False
            ElseIf m_iniFileAccessor.Initialized Then
                CacheSectionNames()
                Return True
            Else
                Return False
            End If

        End Function

        ''' <summary>
        ''' Loads the settings for the defined Xml Settings File.  Assumes names are not case sensitive
        ''' </summary>
        ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        ''' <param name="logger">This is the logger.</param>
        Public Function LoadSettings(ByVal XmlSettingsFilePath As String, ByRef logger As ILogger) As Boolean
            Return LoadSettings(XmlSettingsFilePath, logger, False)
        End Function

        ''' <summary>
        ''' Loads the settings for the defined Xml Settings File
        ''' </summary>
        ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        ''' <param name="logger">This is the logger.</param>
        ''' <param name="IsCaseSensitive">Case sensitive names if True.  Non-case sensitive if false.</param>
        Public Function LoadSettings(ByVal XmlSettingsFilePath As String, ByRef logger As ILogger, ByVal IsCaseSensitive As Boolean) As Boolean
            mCaseSensitive = IsCaseSensitive

            m_IniFilePath = XmlSettingsFilePath

            ' Note: Always set IsCaseSensitive = True for IniFileReader's constructor since this class handles 
            '       case sensitivity mapping internally
            m_iniFileAccessor = New IniFileReader(m_IniFilePath, logger, True)
            If m_iniFileAccessor Is Nothing Then
                Return False
            ElseIf m_iniFileAccessor.Initialized Then
                CacheSectionNames()
                Return True
            Else
                Return False
            End If

        End Function

        ''' <summary>
        ''' Saves the settings for the defined Xml Settings File.  Note that you must call LoadSettings to initialize the class prior to setting any values.
        ''' </summary>
        ''' <return>The function returns a boolean that shows if the file was successfully saved.</return>
        Public Function SaveSettings() As Boolean

            If m_iniFileAccessor Is Nothing Then
                Return False
            ElseIf m_iniFileAccessor.Initialized Then
                m_iniFileAccessor.OutputFilename = m_IniFilePath
                m_iniFileAccessor.Save()
                Return True
            Else
                Return False
            End If

        End Function

        ''' <summary>Checks if a section is present in the settings file.</summary>
        ''' <param name="sectionName">The name of the section to look for.</param>
        ''' <return>The function returns a boolean that shows if the section is present.</return>
        Public Function SectionPresent(ByVal sectionName As String) As Boolean
            Dim strSections As System.Collections.Specialized.StringCollection
            Dim intIndex As Integer

            strSections = m_iniFileAccessor.AllSections

            For intIndex = 0 To strSections.Count - 1
                If SetNameCase(strSections.Item(intIndex)) = SetNameCase(sectionName) Then Return True
            Next intIndex

            Return False

        End Function

        Private Function CacheKeyNames(ByVal sectionName As String) As Boolean
            ' Looks up the Key Names for the given section, storing them in mCachedSection
            ' This is done so that this class will know the correct capitalization for the key names

            Dim strKeys As System.Collections.Specialized.StringCollection
            Dim intIndex As Integer

            Dim sectionNameInFile As String
            Dim strKeyNameToStore As String

            ' Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
            sectionNameInFile = GetCachedSectionName(sectionName)
            If sectionNameInFile.Length = 0 Then Return False

            Try
                ' Grab the keys for sectionName
                strKeys = m_iniFileAccessor.AllKeysInSection(sectionNameInFile)
            Catch ex As Exception
                ' Invalid section name; do not update anything
                Return False
            End Try

            If strKeys Is Nothing Then
                Return False
            End If

            ' Update mCachedSection with the key names for the given section
            With mCachedSection
                .SectionName = sectionNameInFile
                .htKeys.Clear()

                For intIndex = 0 To strKeys.Count - 1
                    If mCaseSensitive Then
                        strKeyNameToStore = String.Copy(strKeys.Item(intIndex))
                    Else
                        strKeyNameToStore = String.Copy(strKeys.Item(intIndex).ToLower)
                    End If

                    If Not .htKeys.Contains(strKeyNameToStore) Then
                        .htKeys.Add(strKeyNameToStore, strKeys.Item(intIndex))
                    End If

                Next intIndex
            End With

            Return True

        End Function

        Private Sub CacheSectionNames()
            ' Looks up the Section Names in the XML file
            ' This is done so that this class will know the correct capitalization for the section names

            Dim strSections As System.Collections.Specialized.StringCollection
            Dim strSectionNameToStore As String

            Dim intIndex As Integer

            strSections = m_iniFileAccessor.AllSections

            htSectionNames.Clear()

            For intIndex = 0 To strSections.Count - 1
                If mCaseSensitive Then
                    strSectionNameToStore = String.Copy(strSections.Item(intIndex))
                Else
                    strSectionNameToStore = String.Copy(strSections.Item(intIndex).ToLower)
                End If

                If Not htSectionNames.Contains(strSectionNameToStore) Then
                    htSectionNames.Add(strSectionNameToStore, strSections.Item(intIndex))
                End If

            Next intIndex

        End Sub

        Private Function GetCachedKeyName(ByVal sectionName As String, ByVal keyName As String) As String
            ' Looks up the correct capitalization for key keyName in section sectionName
            ' Returns String.Empty if not found

            Dim blnSuccess As Boolean
            Dim sectionNameInFile As String
            Dim keyNameToFind As String

            ' Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
            sectionNameInFile = GetCachedSectionName(sectionName)
            If sectionNameInFile.Length = 0 Then Return String.Empty

            If mCachedSection.SectionName = sectionNameInFile Then
                blnSuccess = True
            Else
                ' Update the keys for sectionName
                blnSuccess = CacheKeyNames(sectionName)
            End If

            If blnSuccess Then
                With mCachedSection
                    keyNameToFind = SetNameCase(keyName)
                    If .htKeys.ContainsKey(keyNameToFind) Then
                        Return CStr(.htKeys(keyNameToFind))
                    Else
                        Return String.Empty
                    End If
                End With
            Else
                Return String.Empty
            End If
        End Function

        Private Function GetCachedSectionName(ByVal sectionName As String) As String
            ' Looks up the correct capitalization for sectionName
            ' Returns String.Empty if not found

            Dim sectionNameToFind As String

            sectionNameToFind = SetNameCase(sectionName)
            If htSectionNames.ContainsKey(sectionNameToFind) Then
                Return CStr(htSectionNames(sectionNameToFind))
            Else
                Return String.Empty
            End If

        End Function

        Private Function SetNameCase(ByVal aName As String) As String
            ' Changes aName to lowercase if mCaseSensitive = False

            If mCaseSensitive Then
                Return aName
            Else
                Return aName.ToLower()
            End If
        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute in section "sectionName".
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        ''' <return>The function returns the name of the "value" attribute as a String.</return>
        Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As String, Optional ByRef valueNotPresent As Boolean = False) As String
            Dim strResult As String
            Dim sectionNameInFile As String
            Dim keyNameInFile As String

            If mCaseSensitive Then
                strResult = m_iniFileAccessor.GetIniValue(sectionName, keyName)
            Else
                sectionNameInFile = GetCachedSectionName(sectionName)
                If sectionNameInFile.Length > 0 Then
                    keyNameInFile = GetCachedKeyName(sectionName, keyName)
                    If keyNameInFile.Length > 0 Then
                        strResult = m_iniFileAccessor.GetIniValue(sectionNameInFile, keyNameInFile)
                    End If
                End If
            End If

            If strResult Is Nothing Then
                valueNotPresent = True
                Return valueIfMissing
            Else
                valueNotPresent = False
                Return strResult
            End If
        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute in section "sectionName".
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        ''' <return>The function returns boolean True if the "value" attribute is "true".  Otherwise, returns boolean False.</return>
        Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Boolean, Optional ByRef valueNotPresent As Boolean = False) As Boolean
            Dim strResult As String
            Dim blnNotFound As Boolean = False

            strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
            If strResult Is Nothing OrElse blnNotFound Then
                valueNotPresent = True
                Return valueIfMissing
            Else
                valueNotPresent = False
                If strResult.ToLower = "true" Then
                    Return True
                Else
                    Return False
                End If
            End If
        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute in section "sectionName".
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        ''' <return>The function returns the name of the "value" attribute as a Short.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Short, Optional ByRef valueNotPresent As Boolean = False) As Short
            Dim strResult As String
            Dim blnNotFound As Boolean = False

            strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
            If strResult Is Nothing OrElse blnNotFound Then
                valueNotPresent = True
                Return valueIfMissing
            Else
                valueNotPresent = False
                Try
                    If IsNumeric(strResult) Then
                        Return CShort(strResult)
                    ElseIf strResult.ToLower = "true" Then
                        Return -1
                    ElseIf strResult.ToLower = "false" Then
                        Return 0
                    Else
                        valueNotPresent = True
                        Return valueIfMissing
                    End If
                Catch ex As Exception
                    valueNotPresent = True
                    Return valueIfMissing
                End Try
            End If

        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute in section "sectionName".
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        ''' <return>The function returns the name of the "value" attribute as an Integer.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Integer, Optional ByRef valueNotPresent As Boolean = False) As Integer
            Dim strResult As String
            Dim blnNotFound As Boolean = False

            strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
            If strResult Is Nothing OrElse blnNotFound Then
                valueNotPresent = True
                Return valueIfMissing
            Else
                valueNotPresent = False
                Try
                    If IsNumeric(strResult) Then
                        Return CInt(strResult)
                    ElseIf strResult.ToLower = "true" Then
                        Return -1
                    ElseIf strResult.ToLower = "false" Then
                        Return 0
                    Else
                        valueNotPresent = True
                        Return valueIfMissing
                    End If
                Catch ex As Exception
                    valueNotPresent = True
                    Return valueIfMissing
                End Try
            End If

        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute in section "sectionName".
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        ''' <return>The function returns the name of the "value" attribute as a Long.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Long, Optional ByRef valueNotPresent As Boolean = False) As Long
            Dim strResult As String
            Dim blnNotFound As Boolean = False

            strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
            If strResult Is Nothing OrElse blnNotFound Then
                valueNotPresent = True
                Return valueIfMissing
            Else
                valueNotPresent = False
                Try
                    If IsNumeric(strResult) Then
                        Return CLng(strResult)
                    ElseIf strResult.ToLower = "true" Then
                        Return -1
                    ElseIf strResult.ToLower = "false" Then
                        Return 0
                    Else
                        valueNotPresent = True
                        Return valueIfMissing
                    End If
                Catch ex As Exception
                    valueNotPresent = True
                    Return valueIfMissing
                End Try
            End If

        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute in section "sectionName".
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        ''' <return>The function returns the name of the "value" attribute as a Single.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Single, Optional ByRef valueNotPresent As Boolean = False) As Single
            Dim strResult As String
            Dim blnNotFound As Boolean = False

            strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
            If strResult Is Nothing OrElse blnNotFound Then
                valueNotPresent = True
                Return valueIfMissing
            Else
                valueNotPresent = False
                Try
                    If IsNumeric(strResult) Then
                        Return CSng(strResult)
                    ElseIf strResult.ToLower = "true" Then
                        Return -1
                    ElseIf strResult.ToLower = "false" Then
                        Return 0
                    Else
                        valueNotPresent = True
                        Return valueIfMissing
                    End If
                Catch ex As Exception
                    valueNotPresent = True
                    Return valueIfMissing
                End Try
            End If

        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute in section "sectionName".
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
        ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
        ''' <return>The function returns the name of the "value" attribute as a Double.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
        Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Double, Optional ByRef valueNotPresent As Boolean = False) As Double
            Dim strResult As String
            Dim blnNotFound As Boolean = False

            strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
            If strResult Is Nothing OrElse blnNotFound Then
                valueNotPresent = True
                Return valueIfMissing
            Else
                valueNotPresent = False
                Try
                    If IsNumeric(strResult) Then
                        Return CDbl(strResult)
                    ElseIf strResult.ToLower = "true" Then
                        Return -1
                    ElseIf strResult.ToLower = "false" Then
                        Return 0
                    Else
                        valueNotPresent = True
                        Return valueIfMissing
                    End If
                Catch ex As Exception
                    valueNotPresent = True
                    Return valueIfMissing
                End Try
            End If

        End Function

        ''' <summary>
        ''' The function sets the path to the Xml Settings File.
        ''' </summary>
        ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
        Public Sub SetIniFilePath(ByVal XmlSettingsFilePath As String)
            m_IniFilePath = XmlSettingsFilePath
        End Sub

        ''' <summary>
        ''' The function sets a new String value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As String) As Boolean
            Dim sectionNameInFile As String
            Dim keyNameInFile As String

            If Not mCaseSensitive Then
                sectionNameInFile = GetCachedSectionName(sectionName)
                If sectionNameInFile.Length > 0 Then
                    keyNameInFile = GetCachedKeyName(sectionName, keyName)
                    If keyNameInFile.Length > 0 Then
                        ' Section and Key are present; update them
                        Return m_iniFileAccessor.SetIniValue(sectionNameInFile, keyNameInFile, newValue)
                    Else
                        ' Section is present, but the Key isn't; add teh key
                        Return m_iniFileAccessor.SetIniValue(sectionNameInFile, keyName, newValue)
                    End If
                End If
            End If

            ' If we get here, then either mCaseSensitive = True or the section and key weren't found
            Return m_iniFileAccessor.SetIniValue(sectionName, keyName, newValue)

        End Function

        ''' <summary>
        ''' The function sets a new Boolean value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Boolean) As Boolean
            Return Me.SetParam(sectionName, keyName, CStr(newValue))
        End Function

        ''' <summary>
        ''' The function sets a new Short value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Short) As Boolean
            Return Me.SetParam(sectionName, keyName, CStr(newValue))
        End Function

        ''' <summary>
        ''' The function sets a new Integer value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Integer) As Boolean
            Return Me.SetParam(sectionName, keyName, CStr(newValue))
        End Function

        ''' <summary>
        ''' The function sets a new Long value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Long) As Boolean
            Return Me.SetParam(sectionName, keyName, CStr(newValue))
        End Function

        ''' <summary>
        ''' The function sets a new Single value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Single) As Boolean
            Return Me.SetParam(sectionName, keyName, CStr(newValue))
        End Function

        ''' <summary>
        ''' The function sets a new Double value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Double) As Boolean
            Return Me.SetParam(sectionName, keyName, CStr(newValue))
        End Function

        ''' <summary>
        ''' The function renames a section.
        ''' </summary>
        ''' <param name="sectionNameOld">The name of the old ini section name.</param>
        ''' <param name="sectionNameNew">The new name for the ini section.</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function RenameSection(ByVal sectionNameOld As String, ByVal sectionNameNew As String) As Boolean

            Dim strSectionName As String

            If Not mCaseSensitive Then
                strSectionName = GetCachedSectionName(sectionNameOld)
                If strSectionName.Length > 0 Then
                    Return m_iniFileAccessor.SetIniSection(strSectionName, sectionNameNew)
                End If
            End If

            ' If we get here, then either mCaseSensitive = True or the section wasn't found using GetCachedSectionName
            Return m_iniFileAccessor.SetIniSection(sectionNameOld, sectionNameNew)

        End Function

        Private Sub FileAccessorInfoMessageEvent(ByVal msg As String) Handles m_iniFileAccessor.InformationMessage
            RaiseEvent InformationMessage(msg)
        End Sub

        ''' <summary>Sets the name of the exception logger</summary>
        Public Sub RegisterExceptionLogger(ByVal logger As ILogger) Implements Logging.ILoggerAware.RegisterEventLogger
            If Not m_iniFileAccessor Is Nothing Then
                m_iniFileAccessor.RegisterExceptionLogger(logger)
            End If
        End Sub

        ''' <summary>Sets the name of the event logger</summary>
        Public Sub RegisterEventLogger(ByVal logger As ILogger) Implements Logging.ILoggerAware.RegisterExceptionLogger
            If Not m_iniFileAccessor Is Nothing Then
                m_iniFileAccessor.RegisterEventLogger(logger)
            End If
        End Sub

        ''' <summary>Gets or Sets notify on event.</summary>
        Public Property NotifyOnEvent() As Boolean Implements Logging.ILoggerAware.NotifyOnEvent
            Get
                If Not m_iniFileAccessor Is Nothing Then
                    Return m_iniFileAccessor.NotifyOnEvent
                Else
                    Return False
                End If
            End Get
            Set(ByVal Value As Boolean)
                If Not m_iniFileAccessor Is Nothing Then
                    m_iniFileAccessor.NotifyOnEvent = Value
                End If
            End Set
        End Property

        ''' <summary>Gets or Sets notify on exception.</summary>
        Public Property NotifyOnException() As Boolean Implements Logging.ILoggerAware.NotifyOnException
            Get
                If Not m_iniFileAccessor Is Nothing Then
                    Return m_iniFileAccessor.NotifyOnException
                Else
                    Return False
                End If
            End Get
            Set(ByVal Value As Boolean)
                If Not m_iniFileAccessor Is Nothing Then
                    m_iniFileAccessor.NotifyOnException = Value
                End If
            End Set
        End Property

        Public Sub New()
            mCaseSensitive = False
            htSectionNames = New Hashtable

            With mCachedSection
                .SectionName = String.Empty
                .htKeys = New Hashtable
            End With
        End Sub
    End Class
End Namespace
