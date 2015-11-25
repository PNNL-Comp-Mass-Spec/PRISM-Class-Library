Option Strict On

Imports System.IO
Imports System.Collections.Specialized
Imports System.Security.Principal
Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports System.Text.RegularExpressions

Namespace Logging

#Region "Logger Interface"
    ''' <summary>Defines the logging interface.</summary>
    Public Interface ILogger
        ''' <summary>The type of log message.</summary>
        Enum logMsgType
            ''' <summary>The message is informational.</summary>
            logNormal
            ''' <summary>The message represents an error.</summary>
            logError
            ''' <summary>The message represents a warning.</summary>
            logWarning
            ''' <summary>The message is only for debugging purposes.</summary>
            logDebug
            ''' <summary>The mesaage does not apply (to what?).</summary>
            logNA
            ''' <summary>The message is an indicator of (in)correct operation.</summary>
            logHealth
        End Enum

        ReadOnly Property CurrentLogFilePath() As String
        ReadOnly Property MostRecentLogMessage() As String
        ReadOnly Property MostRecentErrorMessage() As String

        ''' <summary>Posts a message to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Sub PostEntry(message As String, EntryType As logMsgType, localOnly As Boolean)

        ''' <summary>Posts an error to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="e">The exception associated with the error.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Sub PostError(message As String, e As Exception, localOnly As Boolean)
    End Interface
#End Region

#Region "Logger Aware Interface"
    ''' <summary>Defines the logging aware interface.</summary>
    ''' <remarks>This interface is used by any class that wants to optionally support 
    ''' logging to a logger that implements the ILogger interface.  The key
    ''' here is the phrase optionally.  The class allows, but does not
    ''' require the class user to supply an ILogger.  If the Logger is not
    ''' specified, the class throw Exceptions and raises Events in the usual
    ''' way.  If an ILogger is specified, the user has the option of just logging,
    ''' or logging and throwing/raising Exceptions/Events in the usual way as well.
    ''' </remarks>
    Public Interface ILoggerAware
        ''' <summary>Register an ILogger with a class to have it log any exception that might occur.</summary>
        ''' <param name="logger">A logger object to be used when logging is desired.</param>
        Sub RegisterExceptionLogger(logger As ILogger)

        ''' <summary>Register an ILogger with a class to have it log any event that might occur.</summary>
        ''' <param name="logger">A logger object to be used when logging is desired.</param>
        Sub RegisterEventLogger(logger As ILogger)
        ''' <summary>Set true and the class will raise events.  Set false and it will not.</summary>
        ''' <remarks>A function like the one shown below can be placed in ILoggerAware class that will only raise the event in the
        ''' event of one needing to be raised.
        ''' </remarks>
        Property NotifyOnEvent() As Boolean
        ' Private Sub RaiseConditionalProgChangedEvent(obj As clsProgRunner)
        '     If m_NotifyOnEvent Then
        '         If Not m_EventLogger Is Nothing Then
        '             m_EventLogger.PostEntry("Raising ProgChanged event for " & obj.m_name & ".", ILogger.logMsgType.logHealth, True)
        '         End If
        '         RaiseEvent ProgChanged(obj)
        '     End If
        ' End Sub
        '
        ' 

        ''' <summary>Set true and the class will throw exceptions.  Set false and it will not</summary>
        ''' <remarks>A function like this can be place in ILoggerAware class that will only throw an exception in the
        ''' event of one needing to be thrown.
        ''' </remarks>
        Property NotifyOnException() As Boolean
        ' Private Sub ThrowConditionalException(ByRef ex As Exception, loggerMessage As String)
        '     If Not m_ExceptionLogger Is Nothing Then
        '         m_ExceptionLogger.PostError(loggerMessage, ex, True)
        '     End If
        '     If m_NotifyOnException Then
        '         If Not m_ExceptionLogger Is Nothing Then
        '             m_ExceptionLogger.PostError("Rethrowing exception", ex, True)
        '             Throw ex
        '         End If
        '     End If
        ' End Sub
        ' 

    End Interface
#End Region

    Public Class Utilities

        ''' <summary>
        ''' Parses the .StackTrace text of the given expression to return a compact description of the current stack
        ''' </summary>
        ''' <param name="objException"></param>
        ''' <returns>String similar to "Stack trace: clsCodeTest.Test-:-clsCodeTest.TestException-:-clsCodeTest.InnerTestException in clsCodeTest.vb:line 86"</returns>
        ''' <remarks></remarks>
        Public Shared Function GetExceptionStackTrace(objException As Exception) As String
            Const REGEX_FUNCTION_NAME = "at ([^(]+)\("
            Const REGEX_FILE_NAME = "in .+\\(.+)"

            Dim intIndex As Integer

            Dim lstFunctions = New List(Of String)

            Dim strCurrentFunction As String
            Dim strFinalFile As String = String.Empty

            Dim strLine As String
            Dim strStackTrace As String

            Dim reFunctionName As New Regex(REGEX_FUNCTION_NAME, RegexOptions.Compiled Or RegexOptions.IgnoreCase)
            Dim reFileName As New Regex(REGEX_FILE_NAME, RegexOptions.Compiled Or RegexOptions.IgnoreCase)
            Dim objMatch As Match

            ' Process each line in objException.StackTrace
            ' Populate strFunctions() with the function name of each line
            Using trTextReader = New StringReader(objException.StackTrace)

                Do While trTextReader.Peek > -1
                    strLine = trTextReader.ReadLine()

                    If Not String.IsNullOrEmpty(strLine) Then
                        strCurrentFunction = String.Empty

                        objMatch = reFunctionName.Match(strLine)
                        If objMatch.Success AndAlso objMatch.Groups.Count > 1 Then
                            strCurrentFunction = objMatch.Groups(1).Value
                        Else
                            ' Look for the word " in "
                            intIndex = strLine.ToLower().IndexOf(" in ", StringComparison.Ordinal)
                            If intIndex = 0 Then
                                ' " in" not found; look for the first space after startIndex 4
                                intIndex = strLine.IndexOf(" ", 4, StringComparison.Ordinal)
                            End If
                            If intIndex = 0 Then
                                ' Space not found; use the entire string
                                intIndex = strLine.Length - 1
                            End If

                            If intIndex > 0 Then
                                strCurrentFunction = strLine.Substring(0, intIndex)
                            End If

                        End If

                        If Not String.IsNullOrEmpty(strCurrentFunction) Then
                            lstFunctions.Add(strCurrentFunction)
                        End If

                        If strFinalFile.Length = 0 Then
                            ' Also extract the file name where the Exception occurred
                            objMatch = reFileName.Match(strLine)
                            If objMatch.Success AndAlso objMatch.Groups.Count > 1 Then
                                strFinalFile = objMatch.Groups(1).Value
                            End If
                        End If

                    End If
                Loop

            End Using

            strStackTrace = String.Empty
            For intIndex = lstFunctions.Count - 1 To 0 Step -1
                If strStackTrace.Length = 0 Then
                    strStackTrace = "Stack trace: " & lstFunctions(intIndex)
                Else
                    strStackTrace &= "-:-" & lstFunctions(intIndex)
                End If
            Next intIndex

            If Not String.IsNullOrEmpty(strStackTrace) AndAlso Not String.IsNullOrWhiteSpace(strFinalFile) Then
                strStackTrace &= " in " & strFinalFile
            End If

            Return strStackTrace

        End Function
    End Class

#Region "File Logger Class"
    ''' <summary>Provides logging to a local file.</summary>
    ''' <remarks>The actual log file name changes daily and is of the form "filePath_mm-dd-yyyy.txt".</remarks>
    Public Class clsFileLogger
        Implements ILogger

        Private m_logFileName As String = ""  ' log file path
        Private m_programName As String ' program name
        Private m_programVersion As String ' program version
        Protected logExecName As Boolean = False
        Protected logExecVersion As Boolean = False

        Protected m_CurrentLogFilePath As String = String.Empty
        Protected m_MostRecentLogMessage As String = String.Empty
        Protected m_MostRecentErrorMessage As String = String.Empty

        ''' <summary>Initializes a new instance of the clsFileLogger class.</summary>
        Public Sub New()
        End Sub

        ''' <summary>Initializes a new instance of the clsFileLogger class which logs to the specified file.</summary>
        ''' <param name="filePath">The name of the file to use for the log.</param>
        ''' <remarks>The actual log file name changes daily and is of the form "filePath_mm-dd-yyyy.txt".</remarks>
        Public Sub New(filePath As String)
            m_logFileName = filePath
        End Sub

        Public ReadOnly Property CurrentLogFilePath() As String Implements ILogger.CurrentLogFilePath
            Get
                Return m_CurrentLogFilePath
            End Get
        End Property

        ''' <summary> Set to true to have the executable's name entered in the log.</summary>
        Public Overridable Property LogExecutableName() As Boolean
            Get
                Return logExecName
            End Get
            Set(Value As Boolean)
                logExecName = Value
            End Set
        End Property

        ''' <summary> Set to true to have the executable's version entered in the log.</summary>
        Public Overridable Property LogExecutableVersion() As Boolean
            Get
                Return logExecVersion
            End Get
            Set(Value As Boolean)
                logExecVersion = Value
            End Set
        End Property

        ''' <summary>Gets the product version associated with this application.</summary>
        Public ReadOnly Property ExecutableVersion() As String
            Get
                If IsNothing(m_programVersion) Then
                    m_programVersion = Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString()
                End If
                Return m_programVersion
            End Get
        End Property

        ''' <summary>Gets the name of the executable file that started the application.</summary>
        Public ReadOnly Property ExecutableName() As String
            Get
                If IsNothing(m_programName) Then
                    m_programName = Path.GetFileName(Reflection.Assembly.GetEntryAssembly().Location)
                End If
                Return m_programName
            End Get
        End Property

        ''' <summary>The name of the file being used as the log.</summary>
        ''' <remarks>The actual log file name changes daily and is of the form "filePath_mm-dd-yyyy.txt".</remarks>
        Public Property LogFilePath() As String
            Get
                Return m_logFileName
            End Get
            Set(Value As String)
                m_logFileName = Value
            End Set
        End Property

        Public ReadOnly Property MostRecentLogMessage() As String Implements ILogger.MostRecentLogMessage
            Get
                Return m_MostRecentLogMessage
            End Get
        End Property

        Public ReadOnly Property MostRecentErrorMessage() As String Implements ILogger.MostRecentErrorMessage
            Get
                Return m_MostRecentErrorMessage
            End Get
        End Property

        ''' <summary>Writes a message to the log file.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        Private Sub LogToFile(message As String, EntryType As ILogger.logMsgType)
            Dim LogFile As StreamWriter = Nothing
            Dim FormattedLogMessage As String

            ' don't log to file if no file name given
            If m_logFileName = "" Then
                Exit Sub
            End If

            'Set up date values for file name

            'Create log file name by appending specified file name and date
            m_CurrentLogFilePath = m_logFileName & "_" & DateTime.Now.ToString("MM-dd-yyyy") & ".txt"

            Try
                If Not File.Exists(m_CurrentLogFilePath) Then
                    LogFile = File.CreateText(m_CurrentLogFilePath)
                Else
                    LogFile = File.AppendText(m_CurrentLogFilePath)
                End If

                FormattedLogMessage = DateTime.Now.ToString & ", "

                If LogExecutableName Then
                    FormattedLogMessage &= ExecutableName & ", "
                End If

                If LogExecutableVersion Then
                    FormattedLogMessage &= ExecutableVersion & ", "
                End If

                FormattedLogMessage &= message & ", " & TypeToString(EntryType) & ", "

                LogFile.WriteLine(FormattedLogMessage)
                LogFile.Close()

                If EntryType = ILogger.logMsgType.logError Then
                    m_MostRecentErrorMessage = FormattedLogMessage
                Else
                    m_MostRecentLogMessage = FormattedLogMessage
                End If

            Catch e As Exception
                If Not LogFile Is Nothing Then
                    LogFile.Close()
                End If
            End Try
        End Sub

        ''' <summary>Converts enumerated error type to string for logging output.</summary>
        ''' <param name="MyErrType">The ILogger error type.</param>
        Protected Function TypeToString(MyErrType As ILogger.logMsgType) As String
            Select Case MyErrType
                Case ILogger.logMsgType.logNormal
                    TypeToString = "Normal"
                Case ILogger.logMsgType.logError
                    TypeToString = "Error"
                Case ILogger.logMsgType.logWarning
                    TypeToString = "Warning"
                Case ILogger.logMsgType.logDebug
                    TypeToString = "Debug"
                Case ILogger.logMsgType.logNA
                    TypeToString = "na"
                Case ILogger.logMsgType.logHealth
                    TypeToString = "Health"
                Case Else
                    TypeToString = "??"
            End Select
        End Function

        ''' <summary>Posts a message to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Public Overridable Sub PostEntry(message As String, EntryType As ILogger.logMsgType, localOnly As Boolean) Implements ILogger.PostEntry
            LogToFile(message, EntryType)
        End Sub

        ''' <summary>Posts an error to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="ex">The exception associated with the error.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Public Overridable Sub PostError(message As String, ex As Exception, localOnly As Boolean) Implements ILogger.PostError
            LogToFile(message & ": " & ex.Message, ILogger.logMsgType.logError)
        End Sub

    End Class
#End Region

#Region "Database Logger Class"
    ''' <summary>Provides logging to a database and local file.</summary>
    ''' <remarks>The module name identifies the logging process, but if not specified, it is filled in as
    ''' ExecutableName:ExecutableVersion:MachineName:UserName.</remarks>
    Public Class clsDBLogger
        Inherits clsFileLogger

        Private m_connection_str As String ' connection string
        Private ReadOnly m_error_list As New StringCollection ' db error list
        Protected m_moduleName As String  ' module name
        Private moduleNameConstructed As Boolean  ' gets set to true if ConstructModuleName is run

        ''' <summary>Initializes a new instance of the clsDBLogger class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub
        ''' <summary>Initializes a new instance of the clsDBLogger class which logs to the specified database.</summary>
        ''' <param name="connectionStr">The connection string used to access the database.</param>
        Public Sub New(connectionStr As String)
            m_connection_str = connectionStr
        End Sub

        ''' <summary>Initializes a new instance of the clsDBLogger class which logs to the specified database and file.</summary>
        ''' <param name="connectionStr">The connection string used to access the database.</param>
        ''' <param name="filePath">The name of the file to use for the log.</param>
        Public Sub New(connectionStr As String, filePath As String)
            MyBase.New(filePath)
            m_connection_str = connectionStr
        End Sub

        ''' <summary>Initializes a new instance of the clsDBLogger class which logs to the specified database and file.</summary>
        ''' <param name="modName">The string used to identify the posting process.</param>
        ''' <param name="connectionStr">The connection string used to access the database.</param>
        ''' <param name="filePath">The name of the file to use for the log.</param>
        ''' <remarks>The module name identifies the logging process, but if not specified, it is filled in as
        ''' ExecutableName:ExecutableVersion:MachineName:UserName.</remarks>
        Public Sub New(modName As String, connectionStr As String, filePath As String)
            MyBase.New(filePath)
            m_connection_str = connectionStr
            m_moduleName = modName
        End Sub

        ''' <summary> Set to true to have the executable's name entered in the log.</summary>
        Public Overrides Property LogExecutableName() As Boolean
            Get
                Return logExecName
            End Get
            Set(Value As Boolean)
                logExecName = Value
                If moduleNameConstructed Then
                    m_moduleName = Nothing 'This will cause m_moduleName to be reconstructed.
                End If
            End Set
        End Property

        ''' <summary> Set to true to have the executable's version entered in the log.</summary>
        Public Overrides Property LogExecutableVersion() As Boolean
            Get
                Return logExecVersion
            End Get
            Set(Value As Boolean)
                logExecVersion = Value
                If moduleNameConstructed Then
                    m_moduleName = Nothing 'This will cause m_moduleName to be reconstructed.
                End If
            End Set
        End Property

        ''' <summary>The connection string used to access the database.</summary>
        Public Property ConnectionString() As String
            Get
                Return m_connection_str
            End Get
            Set(Value As String)
                m_connection_str = Value
            End Set
        End Property

        ''' <summary>The module name identifies the logging process.</summary>
        Public ReadOnly Property MachineName() As String
            Get
                Dim host As String = Net.Dns.GetHostName

                If host.Contains(".") Then
                    host = host.Substring(0, host.IndexOf("."c))
                End If

                Return host
            End Get
        End Property

        ''' <summary>The module name identifies the logging process.</summary>
        Public ReadOnly Property UserName() As String
            Get
                Return WindowsIdentity.GetCurrent().Name
            End Get
        End Property

        ''' <summary>COnstruct the string ExecutableName:ExecutableVersion:MachineName:UserName.</summary>
        Private Function ConstructModuleName() As String
            ' TODO: Make sure that the concatenated string is less than 64 characters and 
            ' that each piece gets a proportionate share of the space.
            Dim retVal As String
            'If LogExecutableName Then
            '    retVal = ExecutableName
            'End If
            'If LogExecutableVersion Then
            '    retVal &= ":" & ExecutableVersion
            'End If
            retVal = ":" & MachineName & ":" & UserName
            moduleNameConstructed = True
            Return retVal
        End Function

        ''' <summary>The module name identifies the logging process.</summary>
        ''' <remarks>If the module name is not specified, it is filled in as
        ''' ExecutableName:ExecutableVersion:MachineName:UserName.</remarks>
        Public Property ModuleName() As String
            Get
                If IsNothing(m_moduleName) Then
                    m_moduleName = ConstructModuleName()
                End If
                Return m_moduleName
            End Get
            Set(Value As String)
                m_moduleName = Value
            End Set
        End Property

        ''' <summary>Writes a message to the log table.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        Protected Overridable Sub LogToDB(message As String, EntryType As ILogger.logMsgType)
            PostLogEntry(TypeToString(EntryType), message)
        End Sub

        ''' <summary>Posts a message to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Public Overrides Sub PostEntry(message As String, EntryType As ILogger.logMsgType, localOnly As Boolean)
            If Not IsNothing(MyBase.LogFilePath) Then
                MyBase.PostEntry(message, EntryType, localOnly)
            End If
            If Not localOnly Then
                LogToDB(message, EntryType)
            End If
        End Sub

        ''' <summary>Posts an error to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="e">The exception associated with the error.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Public Overrides Sub PostError(message As String, e As Exception, localOnly As Boolean)
            If Not IsNothing(MyBase.LogFilePath) Then
                MyBase.PostError(message, e, localOnly)
            End If
            If Not localOnly Then
                LogToDB(message & ": " & e.Message, ILogger.logMsgType.logError)
            End If
        End Sub

        ''' <summary>Writes a message to the log table via the stored procedure.</summary>
        ''' <param name="type">The ILogger error type.</param>
        ''' <param name="message">The message to post.</param>
        Private Sub PostLogEntry(type As String, message As String)
            Dim sc As SqlCommand

            Try
                m_error_list.Clear()
                ' create the database connection
                '
                Dim cnStr As String = m_connection_str
                Using dbCn = New SqlConnection(cnStr)
                    AddHandler dbCn.InfoMessage, New SqlInfoMessageEventHandler(AddressOf OnInfoMessage)
                    dbCn.Open()

                    ' create the command object
                    '
                    sc = New SqlCommand("PostLogEntry", dbCn)
                    sc.CommandType = CommandType.StoredProcedure

                    ' define parameters for command object
                    '
                    Dim myParm As SqlParameter
                    '
                    ' define parameter for stored procedure's return value
                    '
                    myParm = sc.Parameters.Add("@Return", SqlDbType.Int)
                    myParm.Direction = ParameterDirection.ReturnValue
                    '
                    ' define parameters for the stored procedure's arguments
                    '
                    myParm = sc.Parameters.Add("@type", SqlDbType.VarChar, 50)
                    myParm.Direction = ParameterDirection.Input
                    myParm.Value = type

                    myParm = sc.Parameters.Add("@message", SqlDbType.VarChar, 500)
                    myParm.Direction = ParameterDirection.Input
                    myParm.Value = message

                    myParm = sc.Parameters.Add("@postedBy", SqlDbType.VarChar, 50)
                    myParm.Direction = ParameterDirection.Input
                    myParm.Value = ModuleName

                    ' execute the stored procedure
                    '
                    sc.ExecuteNonQuery()

                End Using

                ' if we made it this far, we succeeded
                '

            Catch ex As Exception
                PostError("Failed to post log entry in database.", ex, True)
            End Try

            Return
        End Sub

        ''' <summary>Event handler for InfoMessage event.</summary>
        ''' <remarks>Errors and warnings sent from the SQL Server database engine are caught here.</remarks>
        Private Sub OnInfoMessage(sender As Object, args As SqlInfoMessageEventArgs)
            Dim err As SqlError
            Dim s As String
            For Each err In args.Errors
                s = ""
                s &= "Message: " & err.Message
                s &= ", Source: " & err.Source
                s &= ", Class: " & err.Class
                s &= ", State: " & err.State
                s &= ", Number: " & err.Number
                s &= ", LineNumber: " & err.LineNumber
                s &= ", Procedure:" & err.Procedure
                s &= ", Server: " & err.Server
                m_error_list.Add(s)
            Next
        End Sub

    End Class
#End Region

#Region "Queue Logger Class"
    ''' <summary>Wraps a queuing mechanism around any object that implements ILogger interface.</summary>
    ''' <remarks>The posting member functions of this class put the log entry
    ''' onto the end of an internal queue and return very quickly to the caller.
    ''' A separate thread within the class is used to perform the actual output of
    ''' the log entries using the logging object that is specified
    ''' in the constructor for this class.</remarks>
    Public Class clsQueLogger
        Implements ILogger

        ''' <summary>A class to hold a log entry in the internal queue.</summary>
        ''' <remarks>It holds the three arguments to PostEntry.</remarks>
        Class clsLogEntry
            Public message As String
            Public entryType As ILogger.logMsgType
            Public localOnly As Boolean
        End Class

        ' queue to hold entries to be output
        Protected m_queue As Queue

        ' internal thread for outputting entries from queue
        Protected m_Thread As Thread
        Protected m_threadRunning As Boolean = False
        Protected m_ThreadStart As New ThreadStart(AddressOf Me.LogFromQueue)

        ' logger object to use for outputting entries from queue
        Protected m_logger As ILogger

        Public ReadOnly Property CurrentLogFilePath() As String Implements ILogger.CurrentLogFilePath
            Get
                If m_logger Is Nothing Then
                    Return String.Empty
                Else
                    Return m_logger.CurrentLogFilePath
                End If
            End Get
        End Property

        Public ReadOnly Property MostRecentLogMessage() As String Implements ILogger.MostRecentLogMessage
            Get
                If m_logger Is Nothing Then
                    Return String.Empty
                Else
                    Return m_logger.MostRecentLogMessage
                End If
            End Get
        End Property

        Public ReadOnly Property MostRecentErrorMessage() As String Implements ILogger.MostRecentErrorMessage
            Get
                If m_logger Is Nothing Then
                    Return String.Empty
                Else
                    Return m_logger.MostRecentErrorMessage
                End If
            End Get
        End Property

        ''' <summary>Initializes a new instance of the clsQueLogger class which logs to the ILogger.</summary>
        ''' <param name="logger">The target logger object.</param>
        Public Sub New(logger As ILogger)
            ' remember my logging object
            m_logger = logger

            ' create a thread safe queue for log entries
            Dim q As New Queue
            m_queue = Queue.Synchronized(q)
        End Sub

        ''' <summary>Start the log output thread if it isn't already running.</summary>
        Protected Sub KickTheOutputThread()
            If Not m_threadRunning Then
                m_threadRunning = True
                m_Thread = New Thread(m_ThreadStart)
                m_Thread.Start()
            End If
        End Sub

        ''' <summary>Pull all entries from the queue and output them to the log streams.</summary>
        Protected Sub LogFromQueue()
            Dim le As clsLogEntry

            While True
                If m_queue.Count = 0 Then Exit While
                le = CType(m_queue.Dequeue(), clsLogEntry)
                m_logger.PostEntry(le.message, le.entryType, le.localOnly)
            End While
            m_threadRunning = False
        End Sub

        ''' <summary>Writes a message to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        Public Sub PostEntry(message As String, EntryType As ILogger.logMsgType, localOnly As Boolean) Implements ILogger.PostEntry
            Dim le As New clsLogEntry
            le.message = message
            le.entryType = EntryType
            le.localOnly = localOnly
            m_queue.Enqueue(le)
            KickTheOutputThread()
        End Sub

        ''' <summary>Posts a message to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="e">The exception associated with the error.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Public Sub PostError(message As String, e As Exception, localOnly As Boolean) Implements ILogger.PostError
            Dim le As New clsLogEntry
            le.message = message & ": " & e.Message
            le.entryType = ILogger.logMsgType.logError
            le.localOnly = localOnly
            m_queue.Enqueue(le)
            KickTheOutputThread()
        End Sub
    End Class
#End Region

#Region "Control Logger Class"
    ''' <summary>Provides logging to a control.</summary>
    ''' <remarks>The actual log control can be a textbox, listbox and a listview.</remarks>
    Public Class clsControlLogger
        Implements ILogger

        Private m_loglsBoxCtl As ListBox        ' log file path
        Private m_loglsViewCtl As ListView      ' log file path
        Private m_logtxtBoxCtl As TextBoxBase   ' log file path
        Private m_programName As String         ' program name
        Private m_programVersion As String      ' program version

        Protected m_MostRecentLogMessage As String = String.Empty
        Protected m_MostRecentErrorMessage As String = String.Empty

        ''' <summary>Initializes a new instance of the clsFileLogger class which logs to a listbox.</summary>
        ''' <param name="lsBox">The name of the listbox used to log message.</param>
        Public Sub New(lsBox As ListBox)
            m_loglsBoxCtl = lsBox
        End Sub

        ''' <summary>Initializes a new instance of the clsFileLogger class which logs to a listview.</summary>
        ''' <param name="lsView">The name of the listview used to log message.</param>
        Public Sub New(lsView As ListView)
            m_loglsViewCtl = lsView
            ' Set the view to show details.
            m_loglsViewCtl.View = View.Details
            ' Allow the user to edit item text.
            m_loglsViewCtl.LabelEdit = True
            ' Allow the user to rearrange columns.
            m_loglsViewCtl.AllowColumnReorder = True
            ' Display check boxes.
            m_loglsViewCtl.CheckBoxes = True
            ' Select the item and subitems when selection is made.
            m_loglsViewCtl.FullRowSelect = True
            ' Display grid lines.
            m_loglsViewCtl.GridLines = True
            ' Sort the items in the list in ascending order.
            m_loglsViewCtl.Sorting = Windows.Forms.SortOrder.Ascending

            ' Create columns for the items and subitems.
            m_loglsViewCtl.Columns.Add("Date", 100, HorizontalAlignment.Left)
            m_loglsViewCtl.Columns.Add("ProgramName", 100, HorizontalAlignment.Left)
            m_loglsViewCtl.Columns.Add("ProgramVersion", 100, HorizontalAlignment.Left)
            m_loglsViewCtl.Columns.Add("Message", 200, HorizontalAlignment.Left)
            m_loglsViewCtl.Columns.Add("EntryType", 100, HorizontalAlignment.Left)

        End Sub

        ''' <summary>Initializes a new instance of the clsFileLogger class which logs to a textbox.</summary>
        ''' <param name="txtBox">The name of the textbox used to log message.</param>
        Public Sub New(txtBox As TextBoxBase)
            If txtBox.Multiline = False Then
                Throw New Exception("The textBox is not multiline!")
            End If
            m_logtxtBoxCtl = txtBox
        End Sub

        Public ReadOnly Property CurrentLogFilePath() As String Implements ILogger.CurrentLogFilePath
            Get
                Return String.Empty
            End Get
        End Property

        ''' <summary>Gets the product version associated with this application.</summary>
        Public ReadOnly Property ExecutableVersion() As String
            Get
                If IsNothing(m_programVersion) Then
                    m_programVersion = Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString()
                End If
                Return m_programVersion
            End Get
        End Property

        ''' <summary>Gets the name of the executable file that started the application.</summary>
        Public ReadOnly Property ExecutableName() As String
            Get
                If IsNothing(m_programName) Then
                    m_programName = Path.GetFileName(Reflection.Assembly.GetEntryAssembly().Location)
                End If
                Return m_programName
            End Get
        End Property

        ''' <summary>Gets and Sets the name of the listbox control.</summary>
        Public Property LogListBox() As ListBox
            Get
                Return m_loglsBoxCtl
            End Get
            Set(Value As ListBox)
                m_loglsBoxCtl = Value
            End Set
        End Property

        ''' <summary>Gets and Sets the name of the listview control.</summary>
        Public Property LogListView() As ListView
            Get
                Return m_loglsViewCtl
            End Get
            Set(Value As ListView)
                m_loglsViewCtl = Value
            End Set
        End Property

        ''' <summary>Gets and Sets the name of the TextBoxBase control.</summary>
        Public Property LogTextBoxBase() As TextBoxBase
            Get
                Return m_logtxtBoxCtl
            End Get
            Set(Value As TextBoxBase)
                m_logtxtBoxCtl = Value
            End Set
        End Property

        Public ReadOnly Property MostRecentLogMessage() As String Implements ILogger.MostRecentLogMessage
            Get
                Return m_MostRecentLogMessage
            End Get
        End Property

        Public ReadOnly Property MostRecentErrorMessage() As String Implements ILogger.MostRecentErrorMessage
            Get
                Return m_MostRecentErrorMessage
            End Get
        End Property

        ''' <summary>Posts a message.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        Private Sub LogToControl(message As String, EntryType As ILogger.logMsgType)
            Dim localMsg As String
            localMsg = DateTime.Now() & ", " & ", " & ExecutableName & ", " &
                       ExecutableVersion & ", " & message & ", " & TypeToString(EntryType)

            If EntryType = ILogger.logMsgType.logError Then
                m_MostRecentErrorMessage = localMsg
            Else
                m_MostRecentLogMessage = localMsg
            End If

            PostEntryTextBox(localMsg)
            PostEntryListBox(localMsg)
            PostEntryListview(message, EntryType)
        End Sub

        ''' <summary>Posts a message to the textbox control.</summary>
        ''' <param name="message">The message to post.</param>
        Private Sub PostEntryTextBox(message As String)
            If IsNothing(m_logtxtBoxCtl) Then Exit Sub
            m_logtxtBoxCtl.Text = message
        End Sub

        ''' <summary>Posts a message to the listbox control.</summary>
        ''' <param name="message">The message to post.</param>
        Private Sub PostEntryListBox(message As String)
            If IsNothing(m_loglsBoxCtl) Then Exit Sub
            m_loglsBoxCtl.Items.Add(message)
        End Sub

        ''' <summary>Posts a message to the listview control.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        Private Sub PostEntryListview(message As String, EntryType As ILogger.logMsgType)
            If IsNothing(m_loglsViewCtl) Then Exit Sub
            ' Create three items and three sets of subitems for each item.
            Dim lvItem As New ListViewItem(DateTime.Now().ToString)
            lvItem.SubItems.Add(ExecutableName)
            lvItem.SubItems.Add(ExecutableVersion)
            lvItem.SubItems.Add(message)
            lvItem.SubItems.Add(TypeToString(EntryType))
            ' Add the items to the ListView.
            m_loglsViewCtl.Items.AddRange(New ListViewItem() {lvItem})
        End Sub


        ''' <summary>Converts enumerated error type to string for logging output.</summary>
        ''' <param name="MyErrType">The ILogger error type.</param>
        Protected Function TypeToString(MyErrType As ILogger.logMsgType) As String
            Select Case MyErrType
                Case ILogger.logMsgType.logNormal
                    TypeToString = "Normal"
                Case ILogger.logMsgType.logError
                    TypeToString = "Error"
                Case ILogger.logMsgType.logWarning
                    TypeToString = "Warning"
                Case ILogger.logMsgType.logDebug
                    TypeToString = "Debug"
                Case ILogger.logMsgType.logNA
                    TypeToString = "na"
                Case ILogger.logMsgType.logHealth
                    TypeToString = "Health"
                Case Else
                    TypeToString = "??"
            End Select
        End Function

        ''' <summary>Posts a message to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="EntryType">The ILogger error type.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Public Overridable Sub PostEntry(message As String, EntryType As ILogger.logMsgType, localOnly As Boolean) Implements ILogger.PostEntry
            LogToControl(message, EntryType)
        End Sub

        ''' <summary>Posts an error to the log.</summary>
        ''' <param name="message">The message to post.</param>
        ''' <param name="ex">The exception associated with the error.</param>
        ''' <param name="localOnly">Post message locally only.</param>
        Public Overridable Sub PostError(message As String, ex As Exception, localOnly As Boolean) Implements ILogger.PostError
            LogToControl(message & ": " & ex.Message, ILogger.logMsgType.logError)
        End Sub

    End Class
#End Region

End Namespace


