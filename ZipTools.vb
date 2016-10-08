Imports System.IO
Imports PRISM.Processes
Imports PRISM.Logging

Namespace Files
	''' <summary>Makes using a file archiving program easier.</summary>
	''' <remarks>There are a routines to create an archive, extract files from an existing archive,
    ''' and to verify an existing archive.
	''' </remarks>
	Public Class ZipTools
		Implements ILoggerAware

		Private m_WorkDir As String
		Private m_ZipFilePath As String
        Private ReadOnly m_WaitInterval As Integer
		Private m_ExceptionLogger As ILogger
        Private m_EventLogger As ILogger
        Private m_CreateNoWindow As Boolean
        Private m_WindowStyle As ProcessWindowStyle

        ''' <summary>Create a zip file.</summary>
        ''' <param name="CmdOptions">The zip program command line arguments.</param>
        ''' <param name="OutputFile">The file path of the output zip file.</param>
        ''' <param name="InputSpec">The files and/or directorys to archive.</param>
        Public Function MakeZipFile(
          CmdOptions As String,
          OutputFile As String,
          InputSpec As String) As Boolean


            'Verify input file and output path have been specified
            If (m_ZipFilePath = "") Or (m_WorkDir = "") Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Input file path and/or working path not specified.", ILogger.logMsgType.logError, True)
                End If
                Return False
            End If

            'Setup the zip program
            Dim zipper = New clsProgRunner() With {
                .Arguments = "-Add " & CmdOptions & " """ & OutputFile & """ """ & InputSpec & """",
                .Program = m_ZipFilePath,
                .WorkDir = m_WorkDir,
                .MonitoringInterval = m_WaitInterval,
                .Name = "Zipper",
                .Repeat = False,
                .RepeatHoldOffTime = 0,
                .CreateNoWindow = m_CreateNoWindow
            }

            'Start the zip program
            zipper.StartAndMonitorProgram()

            'Wait for zipper program to complete
            While (zipper.State <> clsProgRunner.States.NotMonitoring)
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Waiting for zipper program.  Going to sleep for " & m_WaitInterval & " milliseconds.",
                      ILogger.logMsgType.logHealth, True)
                End If
                Threading.Thread.Sleep(m_WaitInterval)
            End While

            'Check for valid return value after completion
            If zipper.ExitCode <> 0 Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Zipper program exited with code: " & zipper.ExitCode, ILogger.logMsgType.logError, True)
                End If
                Return False
            Else
                Return True
            End If

        End Function

        ''' <summary>Extract files from a zip file.</summary>
        ''' <param name="CmdOptions">The zip program command line arguments.</param>
        ''' <param name="InputFile">The file path of the zip file from which to extract files.</param>
        ''' <param name="OutPath">The path where you want to put the extracted files.</param>
        Public Function UnzipFile(CmdOptions As String, InputFile As String, OutPath As String) As Boolean


            'Verify input file and output path have been specified
            If (m_ZipFilePath = "") Or (m_WorkDir = "") Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Input file path and/or working path not specified.", ILogger.logMsgType.logError, True)
                End If
                Return False
            End If

            'Verify input file exists
            If Not File.Exists(InputFile) Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Input file " & InputFile & " not found", ILogger.logMsgType.logError, True)
                End If
                Return False
            End If

            'Verify output path exists
            If Not Directory.Exists(OutPath) Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Output directory " & OutPath & " does not exist.", ILogger.logMsgType.logError, True)
                End If
                Return False
            End If

            'Setup the unzip program
            Dim zipper = New clsProgRunner() With {
                .Arguments = "-Extract " & CmdOptions & " """ & InputFile & """ """ & OutPath & """",
                .MonitoringInterval = m_WaitInterval,
                .Name = "Zipper",
                .Program = m_ZipFilePath,
                .WorkDir = m_WorkDir,
                .Repeat = False,
                .RepeatHoldOffTime = 0,
                .CreateNoWindow = m_CreateNoWindow,
                .WindowStyle = m_WindowStyle
            }

            'Start the unzip program
            zipper.StartAndMonitorProgram()

            'Wait for zipper program to complete
            While (zipper.State <> clsProgRunner.States.NotMonitoring)
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Waiting for zipper program.  Going to sleep for " & m_WaitInterval & " milliseconds.",
                       ILogger.logMsgType.logHealth, True)
                End If
                Threading.Thread.Sleep(m_WaitInterval)
            End While

            'Check for valid return value after completion
            If zipper.ExitCode <> 0 Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Zipper program exited with code: " & zipper.ExitCode, ILogger.logMsgType.logError, True)
                End If
                Return False
            Else
                Return True
            End If

        End Function

        ''' <summary>Defines whether a window is displayed when calling the zipping program.</summary>
        Public Property CreateNoWindow() As Boolean
            Get
                Return m_CreateNoWindow
            End Get
            Set(Value As Boolean)
                m_CreateNoWindow = Value
            End Set
        End Property

        ''' <summary>
        ''' Window style to use when CreateNoWindow is False.
        ''' </summary>
        Public Property WindowStyle() As ProcessWindowStyle
            Get
                Return m_WindowStyle
            End Get
            Set(Value As ProcessWindowStyle)
                m_WindowStyle = Value
            End Set
        End Property


        ''' <summary>The working directory for the zipping process.</summary>
        Public Property WorkDir() As String
            Get
                Return m_WorkDir
            End Get
            Set(Value As String)
                m_WorkDir = Value
            End Set
        End Property

        ''' <summary>The path to the zipping program.</summary>
        Public Property ZipFilePath() As String
            Get
                Return m_ZipFilePath
            End Get
            Set(Value As String)
                m_ZipFilePath = Value
            End Set
        End Property

        ''' <summary>Initializes a new instance of the ZipTools class.</summary>
        ''' <param name="WorkDir">The working directory for the zipping process.</param>
        ''' <param name="ZipFilePath">The path to the zipping program.</param>
        Public Sub New(WorkDir As String, ZipFilePath As String)
            m_WorkDir = WorkDir
            m_ZipFilePath = ZipFilePath
            m_WaitInterval = 2000  'msec
            NotifyOnEvent = True
            NotifyOnException = True
        End Sub

        ''' <summary>Verifies initial parameters have been set prior to performing operation.</summary>
        Private Function VerifyParams() As Boolean

            If (m_WorkDir = "") Or (m_ZipFilePath) = "" Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Working directory and/or zipfile's path not specified.", ILogger.logMsgType.logError, True)
                End If
                Return False
            Else
                Return True
            End If

        End Function

        ''' <summary>Verifies the integrity of a zip file.</summary>
        ''' <param name="FilePath">The file path of the zip file to verify.</param>
        Public Function VerifyZippedFile(FilePath As String) As Boolean


            'Verify test file exists
            If Not File.Exists(FilePath) Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("File path file " & FilePath & " not found", ILogger.logMsgType.logError, True)
                End If
                Return False
            End If

            'Verify Zip file and output path have been specified
            If (m_ZipFilePath = "") Or (m_WorkDir = "") Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Zip file path and/or working path not specified.", ILogger.logMsgType.logError, True)
                End If
                Return False
            End If

            'Setup the zip program
            Dim zipper = New clsProgRunner() With {
                .Arguments = "-test -nofix" & " " & FilePath,
                .Program = m_ZipFilePath,
                .WorkDir = m_WorkDir,
                .MonitoringInterval = m_WaitInterval,
                .Name = "Zipper",
                .Repeat = False,
                .RepeatHoldOffTime = 0,
                .CreateNoWindow = m_CreateNoWindow,
                .WindowStyle = m_WindowStyle
            }

            'Start the zip program
            zipper.StartAndMonitorProgram()

            'Wait for zipper program to complete
            While (zipper.State <> clsProgRunner.States.NotMonitoring)
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Waiting for zipper program.  Going to sleep for " & m_WaitInterval & " milliseconds.",
                       ILogger.logMsgType.logHealth, True)
                End If
                Threading.Thread.Sleep(m_WaitInterval)
            End While

            'Check for valid return value after completion
            If zipper.ExitCode <> 0 Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Zipper program exited with code: " & zipper.ExitCode, ILogger.logMsgType.logError, True)
                End If
                Return False
            Else
                Return True
            End If

        End Function

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