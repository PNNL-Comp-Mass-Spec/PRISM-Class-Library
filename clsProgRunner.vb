Option Strict On

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading

Namespace Processes
    ' This class runs a single program as an external process
    ' and monitors it with an internal thread
    '
    ''' <summary>
    ''' This class runs a single program as an external process and monitors it with an internal thread
    ''' </summary>
    Public Class clsProgRunner
        Implements Logging.ILoggerAware

#Region "Constants and Enums"

        Public Const DEFAULT_MONITOR_INTERVAL_MSEC As Integer = 5000
        Public Const MINIMUM_MONITOR_INTERVAL_MSEC As Integer = 250

        ''' <summary>
        ''' clsProgRunner states
        ''' </summary>
        Public Enum States
            NotMonitoring
            Monitoring
            Waiting
            CleaningUp
            Initializing
            StartingProcess
        End Enum
#End Region

#Region "Classwide Variables"

        ''' <summary>
        ''' Interface used for logging exceptions
        ''' </summary>
        Private m_ExceptionLogger As Logging.ILogger

        ''' <summary>
        ''' Interface used for logging errors and health related messages
        ''' </summary>
        Private m_EventLogger As Logging.ILogger

        ''' <summary>
        ''' True for logging behavior, else false
        ''' </summary>
        Private m_NotifyOnException As Boolean

        ''' <summary>
        ''' True for logging behavior, else false
        ''' </summary>
        Private m_NotifyOnEvent As Boolean

        ' overall state of this object
        Private m_state As States = States.NotMonitoring

        ''' <summary>
        ''' Used to start and monitor the external program
        ''' </summary>
        Private ReadOnly m_Process As New Process

        ''' <summary>
        ''' The process id of the currently running incarnation of the external program
        ''' </summary>
        Private m_pid As Integer

        ''' <summary>
        ''' The instance name of the most recent performance counter used by GetCoreUsageByProcessID
        ''' </summary>
        ''' <remarks></remarks>
        Private m_processIdInstanceName As String

        ''' <summary>
        ''' The internal thread used to run the monitoring code
        ''' </summary>
        ''' <remarks>
        ''' That starts and monitors the external program
        ''' </remarks>
        Private m_Thread As Thread

        ''' <summary>
        ''' Flag that tells internal thread to quit monitoring external program and exit
        ''' </summary>
        Private m_doCleanup As Boolean = False

        ''' <summary>
        ''' The interval, in milliseconds, for monitoring the thread to wake up and check m_doCleanup
        ''' </summary>
        ''' <remarks>Default is 5000 msec</remarks>
        Private m_monitorInterval As Integer

        ''' <summary>
        ''' Exit code returned by completed process
        ''' </summary>
        ''' <remarks>Initially set to -123454321</remarks>
        Private m_ExitCode As Integer

        ''' <summary>
        ''' Parameters for external program
        ''' </summary>
        Private m_name As String
        Private m_ProgName As String
        Private m_ProgArgs As String
        Private m_repeat As Boolean = False
        Private m_holdOffTime As Integer = 3000
        Private m_WorkDir As String
        Private m_CreateNoWindow As Boolean
        Private m_WindowStyle As ProcessWindowStyle

        Private m_CacheStandardOutput As Boolean
        Private m_EchoOutputToConsole As Boolean

        Private m_WriteConsoleOutputToFile As Boolean
        Private m_ConsoleOutputFilePath As String = String.Empty
        Private m_ConsoleOutputStreamWriter As StreamWriter

        ''' <summary>
        ''' Caches the text written to the Console by the external program
        ''' </summary>
        Private m_CachedConsoleOutput As StringBuilder

        ''' <summary>
        ''' Caches the text written to the Error buffer by the external program
        ''' </summary>
        Private m_CachedConsoleError As StringBuilder

        ''' <summary>
        ''' Number of cores on this computer
        ''' </summary>
        ''' <remarks></remarks>
        Private Shared mCachedCoreCount As Integer = 0

        ''' <summary>
        ''' Maps processId to a PerformanceCounter instance
        ''' </summary>
        ''' <remarks>The KeyValuePair tracks the performance counter instance name (could be empty string) and the PerformanceCounter instance</remarks>
        Private Shared ReadOnly mCachedPerfCounters As ConcurrentDictionary(Of Integer, KeyValuePair(Of String, PerformanceCounter)) =
            New ConcurrentDictionary(Of Integer, KeyValuePair(Of String, PerformanceCounter))

#End Region

#Region "Events"

        ''' <summary>
        ''' This event is raised at regular intervals while monitoring the program
        ''' </summary>
        ''' <remarks>Raised every m_monitorInterval milliseconds</remarks>
        Public Event ProgChanged(ByVal obj As clsProgRunner)

        ''' <summary>
        ''' This event is raised when the external program writes text to the console
        ''' </summary>
        ''' <param name="NewText"></param>
        ''' <remarks></remarks>
        Public Event ConsoleOutputEvent(ByVal NewText As String)

        ''' <summary>
        ''' This event is raised when the external program writes text to the console's error stream
        ''' </summary>
        ''' <param name="NewText"></param>
        ''' <remarks></remarks>
        Public Event ConsoleErrorEvent(ByVal NewText As String)

#End Region

#Region "Properties"

        ''' <summary>
        ''' Arguments supplied to external program when it is run
        ''' </summary>
        Public Property Arguments() As String
            Get
                Return m_ProgArgs
            End Get
            Set(ByVal Value As String)
                m_ProgArgs = Value
            End Set
        End Property

        ''' <summary>
        ''' Text written to the Console by the external program (including carriage returns)
        ''' </summary>
        Public ReadOnly Property CachedConsoleOutput() As String
            Get
                If m_CachedConsoleOutput Is Nothing Then
                    Return String.Empty
                Else
                    Return m_CachedConsoleOutput.ToString
                End If
            End Get
        End Property

        ''' <summary>
        ''' Any text written to the Error buffer by the external program
        ''' </summary>
        Public ReadOnly Property CachedConsoleError() As String
            Get
                If m_CachedConsoleError Is Nothing Then
                    Return String.Empty
                Else
                    Return m_CachedConsoleError.ToString
                End If
            End Get
        End Property

        ''' <summary>
        ''' When true then will cache the text the external program writes to the console
        ''' Can retrieve using the CachedConsoleOutput readonly property
        ''' Will also fire event ConsoleOutputEvent as new text is written to the console
        ''' </summary>
        ''' <remarks>If this is true, then no window will be shown, even if CreateNoWindow=False</remarks>
        Public Property CacheStandardOutput() As Boolean
            Get
                Return m_CacheStandardOutput
            End Get
            Set(ByVal value As Boolean)
                m_CacheStandardOutput = value
            End Set
        End Property

        ''' <summary>
        ''' File path to which the console output will be written if WriteConsoleOutputToFile is true
        ''' If blank, then file path will be auto-defined in the WorkDir  when program execution starts
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Property ConsoleOutputFilePath() As String
            Get
                Return m_ConsoleOutputFilePath
            End Get
            Set(ByVal value As String)
                If value Is Nothing Then value = String.Empty
                m_ConsoleOutputFilePath = value
            End Set
        End Property

        ''' <summary>
        ''' Determine if window should be displayed
        ''' Will be forced to True if CacheStandardOutput = True
        ''' </summary>
        Public Property CreateNoWindow() As Boolean
            Get
                Return m_CreateNoWindow
            End Get
            Set(ByVal Value As Boolean)
                m_CreateNoWindow = Value
            End Set
        End Property

        ''' <summary>
        ''' When true, then echoes, in real time, text written to the Console by the external program 
        ''' Ignored if CreateNoWindow = False
        ''' </summary>
        Public Property EchoOutputToConsole() As Boolean
            Get
                Return m_EchoOutputToConsole
            End Get
            Set(ByVal value As Boolean)
                m_EchoOutputToConsole = value
            End Set
        End Property

        ''' <summary>
        ''' Exit code when process completes
        ''' </summary>
        Public ReadOnly Property ExitCode() As Integer
            Get
                Return m_ExitCode
            End Get
        End Property

        ''' <summary>
        ''' How often (milliseconds) internal monitoring thread checks status of external program
        ''' </summary>
        ''' <remarks>Minimum allowed value is 100 milliseconds</remarks>
        Public Property MonitoringInterval() As Integer
            Get
                Return m_monitorInterval
            End Get
            Set(ByVal Value As Integer)
                If Value < MINIMUM_MONITOR_INTERVAL_MSEC Then Value = MINIMUM_MONITOR_INTERVAL_MSEC
                m_monitorInterval = Value
            End Set
        End Property

        ''' <summary>
        ''' Name of this progrunner
        ''' </summary>
        Public Property Name() As String
            Get
                Return m_name
            End Get
            Set(ByVal Value As String)
                m_name = Value
            End Set
        End Property

        ''' <summary>Gets or Sets notify on event</summary>
        Public Property NotifyOnEvent() As Boolean Implements Logging.ILoggerAware.NotifyOnEvent
            Get
                Return m_NotifyOnEvent
            End Get
            Set(ByVal Value As Boolean)
                m_NotifyOnEvent = Value
            End Set
        End Property

        ''' <summary>Gets or Sets notify on exception</summary>
        Public Property NotifyOnException() As Boolean Implements Logging.ILoggerAware.NotifyOnException
            Get
                Return m_NotifyOnException
            End Get
            Set(ByVal Value As Boolean)
                m_NotifyOnException = Value
            End Set
        End Property

        ''' <summary>
        ''' Process id of currently running external program's process
        ''' </summary>
        Public ReadOnly Property PID() As Integer
            Get
                Return m_pid
            End Get
        End Property

        ''' <summary>
        ''' External program that prog runner will run
        ''' This is the full path to the program file
        ''' </summary>
        Public Property Program() As String
            Get
                Return m_ProgName
            End Get
            Set(ByVal Value As String)
                m_ProgName = Value
            End Set
        End Property

        ''' <summary>
        ''' Whether prog runner will restart external program after it exits
        ''' </summary>
        Public Property Repeat() As Boolean
            Get
                Return m_repeat
            End Get
            Set(ByVal Value As Boolean)
                m_repeat = Value
            End Set
        End Property

        ''' <summary>
        ''' Time (seconds) that prog runner waits to restart external program after it exits
        ''' </summary>
        Public Property RepeatHoldOffTime() As Double
            Get
                Return m_holdOffTime / 1000.0
            End Get
            Set(ByVal Value As Double)
                m_holdOffTime = CType(Value * 1000, Integer)
            End Set
        End Property

        ''' <summary>
        ''' Current state of prog runner (as number)
        ''' </summary>
        Public ReadOnly Property State() As States
            Get
                Return m_state
            End Get
        End Property

        ''' <summary>
        ''' Current state of prog runner (as descriptive name)
        ''' </summary>
        Public ReadOnly Property StateName() As String
            Get
                Select Case m_state
                    Case States.NotMonitoring
                        StateName = "not monitoring"
                    Case States.Monitoring
                        StateName = "monitoring"
                    Case States.Waiting
                        StateName = "waiting to restart"
                    Case States.CleaningUp
                        StateName = "cleaning up"
                    Case States.Initializing
                        StateName = "initializing"
                    Case States.StartingProcess
                        StateName = "starting"
                    Case Else
                        StateName = "???"
                End Select
            End Get
        End Property

        ''' <summary>
        ''' Window style to use when CreateNoWindow is False
        ''' </summary>
        Public Property WindowStyle() As ProcessWindowStyle
            Get
                Return m_WindowStyle
            End Get
            Set(ByVal Value As ProcessWindowStyle)
                m_WindowStyle = Value
            End Set
        End Property

        ''' <summary>
        ''' Working directory for process execution
        ''' Not necessarily the same as the directory that contains the program we're running
        ''' </summary>
        Public Property WorkDir() As String
            Get
                Return m_WorkDir
            End Get
            Set(ByVal Value As String)
                m_WorkDir = Value
            End Set
        End Property

        ''' <summary>
        ''' When true then will write the standard output to a file in real-time
        ''' Will also fire event ConsoleOutputEvent as new text is written to the console
        ''' Define the path to the file using property ConsoleOutputFilePath; if not defined, the file will be created in the WorkDir
        ''' </summary>
        ''' <remarks>If this is true, then no window will be shown, even if CreateNoWindow=False</remarks>
        Public Property WriteConsoleOutputToFile() As Boolean
            Get
                Return m_WriteConsoleOutputToFile
            End Get
            Set(ByVal value As Boolean)
                m_WriteConsoleOutputToFile = value
            End Set
        End Property

#End Region

#Region "Methods"

        ''' <summary>
        ''' Constructor
        ''' </summary>
        Public Sub New()
            m_WorkDir = ""
            m_CreateNoWindow = False
            m_ExitCode = -123454321  ' Unreasonable value
            m_monitorInterval = DEFAULT_MONITOR_INTERVAL_MSEC
            m_NotifyOnEvent = True
            m_NotifyOnException = True
            m_CacheStandardOutput = False
            m_EchoOutputToConsole = True
            m_WriteConsoleOutputToFile = False
            m_ConsoleOutputFilePath = String.Empty

        End Sub

        ''' <summary>
        ''' Clears any console output text that is currently cached
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub ClearCachedConsoleOutput()

            If m_CachedConsoleOutput Is Nothing Then
                m_CachedConsoleOutput = New StringBuilder
            Else
                m_CachedConsoleOutput.Length = 0
            End If

        End Sub

        ''' <summary>
        ''' Clear any performance counters cached via a call to GetCoreUsage() or GetCoreUsageByProcessID()
        ''' </summary>
        ''' <remarks></remarks>
        Public Shared Sub ClearCachedPerformanceCounters()
            mCachedPerfCounters.Clear()
        End Sub

        ''' <summary>
        ''' Clear the performance counter cached for the given Process ID
        ''' </summary>
        ''' <remarks></remarks>
        Public Shared Sub ClearCachedPerformanceCounterForProcessID(processId As Integer)

            Try
                If Not mCachedPerfCounters.ContainsKey(processId) Then
                    Return
                End If

                Dim removedCounter As KeyValuePair(Of String, PerformanceCounter) = Nothing
                mCachedPerfCounters.TryRemove(processId, removedCounter)
            Catch ex As Exception
                ' Ignore errors
            End Try

        End Sub

        ''' <summary>
        ''' Clears any console error text that is currently cached
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub ClearCachedConsoleError()

            If m_CachedConsoleError Is Nothing Then
                m_CachedConsoleError = New StringBuilder
            Else
                m_CachedConsoleError.Length = 0
            End If

        End Sub

        ''' <summary>
        ''' Asynchronously handles the console output from the process running by m_Process
        ''' </summary>
        Private Sub ConsoleOutputHandler(ByVal sendingProcess As Object,
                                         ByVal outLine As DataReceivedEventArgs)

            ' Collect the console output
            If Not String.IsNullOrEmpty(outLine.Data) Then

                RaiseEvent ConsoleOutputEvent(outLine.Data)

                If m_EchoOutputToConsole Then
                    Console.WriteLine(outLine.Data)
                End If

                If m_CacheStandardOutput Then
                    ' Add the text to the collected output
                    m_CachedConsoleOutput.AppendLine(outLine.Data)
                End If

                If m_WriteConsoleOutputToFile AndAlso Not m_ConsoleOutputStreamWriter Is Nothing Then
                    ' Write the standard output to the console output file
                    Try
                        m_ConsoleOutputStreamWriter.WriteLine(outLine.Data)
                    Catch ex As Exception
                        ' Another thread is likely trying to write to a closed file
                        ' Ignore errors here
                    End Try
                End If
            End If
        End Sub

        ''' <summary>
        ''' Force garbage collection
        ''' </summary>
        ''' <remarks>Waits up to 1 second for the collection to finish</remarks>
        Public Shared Sub GarbageCollectNow()
            Const intMaxWaitTimeMSec As Integer = 1000
            GarbageCollectNow(intMaxWaitTimeMSec)
        End Sub

        ''' <summary>
        ''' Force garbage collection
        ''' </summary>
        ''' <remarks></remarks>
        Public Shared Sub GarbageCollectNow(ByVal intMaxWaitTimeMSec As Integer)
            Const THREAD_SLEEP_TIME_MSEC As Integer = 100

            Dim intTotalThreadWaitTimeMsec As Integer
            If intMaxWaitTimeMSec < 100 Then intMaxWaitTimeMSec = 100
            If intMaxWaitTimeMSec > 5000 Then intMaxWaitTimeMSec = 5000

            Thread.Sleep(100)

            Try
                Dim gcThread As New Thread(AddressOf GarbageCollectWaitForGC)
                gcThread.Start()

                intTotalThreadWaitTimeMsec = 0
                While gcThread.IsAlive AndAlso intTotalThreadWaitTimeMsec < intMaxWaitTimeMSec
                    Thread.Sleep(THREAD_SLEEP_TIME_MSEC)
                    intTotalThreadWaitTimeMsec += THREAD_SLEEP_TIME_MSEC
                End While
                If gcThread.IsAlive Then gcThread.Abort()

            Catch ex As Exception
                ' Ignore errors here
            End Try

        End Sub

        Protected Shared Sub GarbageCollectWaitForGC()
            Try
                GC.Collect()
                GC.WaitForPendingFinalizers()
            Catch
                ' Ignore errors here
            End Try
        End Sub

        ''' <summary>
        ''' Returns the number of cores
        ''' </summary>
        ''' <returns>The number of cores on this computer</returns>
        ''' <remarks>Should not affected by hyperthreading, so a computer with two 4-core chips will report 8 cores</remarks>
        Public Shared Function GetCoreCount() As Integer

            Try

                If mCachedCoreCount > 0 Then
                    Return mCachedCoreCount
                End If

                Dim result = New Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor")
                Dim coreCount = 0

                For Each item In result.Get()
                    coreCount += Integer.Parse(item("NumberOfCores").ToString())
                Next

                Interlocked.Exchange(mCachedCoreCount, coreCount)

                Return mCachedCoreCount

            Catch ex As Exception
                ' This value will be affected by hyperthreading
                Return Environment.ProcessorCount
            End Try

        End Function

        ''' <summary>
        ''' Reports the number of cores in use by the program started with StartAndMonitorProgram
        ''' This method takes at least 1000 msec to execute
        ''' </summary>
        ''' <returns>Number of cores in use; -1 if an error</returns>
        ''' <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        Public Function GetCoreUsage() As Single

            If m_pid = 0 Then
                Return 0
            End If

            Try
                Return GetCoreUsageByProcessID(m_pid, m_processIdInstanceName)
            Catch ex As Exception
                ThrowConditionalException(ex, "processId not recognized or permissions error")
                Return -1
            End Try

        End Function

        ''' <summary>
        ''' Reports the number of cores in use by the given process
        ''' This method takes at least 1000 msec to execute
        ''' </summary>
        ''' <param name="processId">Process ID for the program</param>
        ''' <returns>Number of cores in use</returns>
        ''' <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        Public Shared Function GetCoreUsageByProcessID(processId As Integer) As Single
            Return GetCoreUsageByProcessID(processId, String.Empty)
        End Function

        ''' <summary>
        ''' Reports the number of cores in use by the given process
        ''' This method takes at least 1000 msec to execute
        ''' </summary>
        ''' <param name="processId">Process ID for the program</param>
        ''' <param name="processIdInstanceName">Expected instance name for the given processId; ignored if empty string. Updated to actual instance name if a new performance counter is created</param>
        ''' <returns>Number of cores in use</returns>
        ''' <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        Public Shared Function GetCoreUsageByProcessID(processId As Integer, ByRef processIdInstanceName As String) As Single

            Try

                If mCachedCoreCount = 0 Then
                    mCachedCoreCount = GetCoreCount()
                End If

                Dim perfCounterContainer As KeyValuePair(Of String, PerformanceCounter) = Nothing
                Dim getNewPerfCounter = True

                ' Look for a cached performance counter instance
                If mCachedPerfCounters.TryGetValue(processId, perfCounterContainer) Then

                    Dim cachedProcessIdInstanceName = perfCounterContainer.Key

                    If String.IsNullOrEmpty(processIdInstanceName) OrElse String.IsNullOrEmpty(cachedProcessIdInstanceName) Then
                        ' Use the existing performance counter
                        getNewPerfCounter = False
                    Else
                        ' Confirm that the existing performance counter matches the expected instance name                        
                        If cachedProcessIdInstanceName.Equals(processIdInstanceName, StringComparison.InvariantCultureIgnoreCase) Then
                            getNewPerfCounter = False
                        End If
                    End If

                    If perfCounterContainer.Value Is Nothing Then
                        getNewPerfCounter = True
                    End If
                End If

                If getNewPerfCounter Then
                    Dim newProcessIdInstanceName As String = String.Empty
                    Dim perfCounter = GetPerfCounterForProcessID(processId, newProcessIdInstanceName)

                    If perfCounter Is Nothing Then
                        Throw New Exception("GetCoreUsageByProcessID: Performance counter not found for processId " & processId)
                    End If

                    processIdInstanceName = newProcessIdInstanceName

                    ClearCachedPerformanceCounterForProcessID(processId)

                    ' Cache this performance counter so that it is quickly available on the next call to this method
                    mCachedPerfCounters.TryAdd(processId, New KeyValuePair(Of String, PerformanceCounter)(newProcessIdInstanceName, perfCounter))

                End If

                ' Take a sample, wait 1 second, then sample again
                Dim sample1 = perfCounterContainer.Value.NextSample()
                Thread.Sleep(1000)
                Dim sample2 = perfCounterContainer.Value.NextSample()

                ' Each core contributes "100" to the overall cpuUsage
                Dim cpuUsage = CounterSample.Calculate(sample1, sample2)
                Dim coresInUse = cpuUsage / 100.0

                Return CSng(coresInUse)

            Catch ex As InvalidOperationException
                ' The process is likely terminated
                Return 0
            Catch ex As Exception
                Throw New Exception("Exception in GetCoreUsageByProcessID for processId " & processId, ex)
            End Try

        End Function

        ''' <summary>
        ''' Obtain the performance counter for the given process
        ''' </summary>
        ''' <param name="processId">Process ID</param>
        ''' <param name="instanceName">Output: instance name corresponding to processId</param>
        ''' <param name="processCounterName">Performance counter to return</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function GetPerfCounterForProcessID(
           processId As Integer,
           <Out()> ByRef instanceName As String,
           Optional processCounterName As String = "% Processor Time") As PerformanceCounter

            instanceName = GetInstanceNameForProcessId(processId)
            If String.IsNullOrEmpty(instanceName) Then
                Return Nothing
            End If

            Return New PerformanceCounter("Process", processCounterName, instanceName)

        End Function

        ''' <summary>
        ''' Get the specific Windows instance name for a program
        ''' </summary>
        ''' <param name="processId">Process ID</param>
        ''' <returns>Instance name if found, otherwise an empty string</returns>
        ''' <remarks>If multiple programs named Chrome.exe are running, the first is Chrome.exe, the second is Chrome.exe#1, etc.</remarks>
        Public Shared Function GetInstanceNameForProcessId(processId As Integer) As String

            Try
                Dim runningProcess = Process.GetProcessById(processId)

                Dim processName = Path.GetFileNameWithoutExtension(runningProcess.ProcessName)

                Dim processCategory = New PerformanceCounterCategory("Process")

                Dim perfCounterInstances = (From item In processCategory.GetInstanceNames() Where item.StartsWith(processName)).ToList()

                For Each instanceName In perfCounterInstances

                    Using counterInstance = New PerformanceCounter("Process", "ID Process", instanceName, True)
                        Dim instanceProcessID = CInt(counterInstance.RawValue)
                        If instanceProcessID = processId Then
                            Return instanceName
                        End If
                    End Using

                Next

            Catch ex As Exception
                Return String.Empty
            End Try

            Return String.Empty

        End Function

        ''' <summary>
        ''' Handles any new data in the console output and console error streams
        ''' </summary>
        Private Sub HandleOutputStreams(ByRef srConsoleError As StreamReader)

            Dim strNewText As String

            If Not srConsoleError Is Nothing AndAlso Not srConsoleError.EndOfStream Then
                strNewText = srConsoleError.ReadToEnd

                RaiseEvent ConsoleErrorEvent(strNewText)

                If Not m_CachedConsoleError Is Nothing Then
                    m_CachedConsoleError.Append(strNewText)
                End If
            End If

        End Sub

        Public Sub JoinThreadNow()

            If m_Thread Is Nothing Then Exit Sub

            Try
                ' Attempt to re-join the thread (wait for 5 seconds, at most)
                m_Thread.Join(5000)
            Catch ex As ThreadStateException
                ThrowConditionalException(CType(ex, Exception), "Caught ThreadStateException while trying to join thread.")
            Catch ex As ThreadInterruptedException
                ThrowConditionalException(CType(ex, Exception), "Caught ThreadInterruptedException while trying to join thread.")
            Catch ex As Exception
                ThrowConditionalException(ex, "Caught exception while trying to join thread.")
            End Try

        End Sub

        ''' <summary>Sets the name of the exception logger</summary>
        Public Sub RegisterExceptionLogger(ByVal logger As Logging.ILogger) Implements Logging.ILoggerAware.RegisterEventLogger
            m_ExceptionLogger = logger
        End Sub

        ''' <summary>Sets the name of the event logger</summary>
        Public Sub RegisterEventLogger(ByVal logger As Logging.ILogger) Implements Logging.ILoggerAware.RegisterExceptionLogger
            m_EventLogger = logger
        End Sub

        Private Sub RaiseConditionalProgChangedEvent(ByVal obj As clsProgRunner)
            If m_NotifyOnEvent Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Raising ProgChanged event for " & obj.m_name & ".", Logging.ILogger.logMsgType.logHealth, True)
                End If
                RaiseEvent ProgChanged(obj)
            End If
        End Sub

        ''' <summary>
        ''' Start program as external process and monitor its state
        ''' </summary>
        Private Sub Start()

            Dim srConsoleError As StreamReader = Nothing
            Dim blnStandardOutputRedirected As Boolean

            ' set up parameters for external process
            '
            With m_Process.StartInfo
                .FileName = m_ProgName
                .WorkingDirectory = m_WorkDir
                .Arguments = m_ProgArgs
                .CreateNoWindow = m_CreateNoWindow
                If .CreateNoWindow Then
                    .WindowStyle = ProcessWindowStyle.Hidden
                Else
                    .WindowStyle = m_WindowStyle
                End If

                If .CreateNoWindow OrElse m_CacheStandardOutput OrElse m_WriteConsoleOutputToFile Then
                    .UseShellExecute = False
                    .RedirectStandardOutput = True
                    .RedirectStandardError = True
                    blnStandardOutputRedirected = True
                Else
                    .UseShellExecute = True
                    .RedirectStandardOutput = False
                    blnStandardOutputRedirected = False
                End If

            End With

            If Not File.Exists(m_Process.StartInfo.FileName) Then
                ThrowConditionalException(New Exception("Process filename " & m_Process.StartInfo.FileName & " not found."),
                  "clsProgRunner m_ProgName was not set correctly.")
                m_state = States.NotMonitoring
                Exit Sub
            End If

            If Not Directory.Exists(m_Process.StartInfo.WorkingDirectory) Then
                ThrowConditionalException(New Exception("Process working directory " & m_Process.StartInfo.WorkingDirectory & " not found."),
                  "clsProgRunner m_WorkDir was not set correctly.")
                m_state = States.NotMonitoring
                Exit Sub
            End If

            If blnStandardOutputRedirected Then
                ' Add an event handler to asynchronously read the console output
                AddHandler m_Process.OutputDataReceived, AddressOf ConsoleOutputHandler

                If m_WriteConsoleOutputToFile Then
                    Try
                        If String.IsNullOrEmpty(m_ConsoleOutputFilePath) Then
                            ' Need to auto-define m_ConsoleOutputFilePath

                            m_ConsoleOutputFilePath = Path.Combine(m_Process.StartInfo.WorkingDirectory,
                                                      Path.GetFileNameWithoutExtension(m_Process.StartInfo.FileName) & "_ConsoleOutput.txt")

                        End If

                        m_ConsoleOutputStreamWriter = New StreamWriter(New FileStream(m_ConsoleOutputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        m_ConsoleOutputStreamWriter.AutoFlush = True

                    Catch ex As Exception
                        ' Report the error, but continue processing
                        ThrowConditionalException(ex, "Caught exception while trying to create the console output file, " & m_ConsoleOutputFilePath)
                    End Try
                End If
            End If

            ' Make sure the cached output StringBuilders are initialized
            ClearCachedConsoleOutput()
            ClearCachedConsoleError()

            Do
                ' start the program as an external process
                '
                Try
                    m_state = States.StartingProcess
                    m_Process.Start()
                Catch ex As Exception
                    ThrowConditionalException(ex, "Problem starting process. Parameters: " &
                      m_Process.StartInfo.WorkingDirectory & m_Process.StartInfo.FileName & " " &
                      m_Process.StartInfo.Arguments & ".")
                    m_ExitCode = -1234567
                    m_state = States.NotMonitoring
                    Exit Sub
                End Try

                Try
                    m_state = States.Monitoring
                    m_pid = m_Process.Id
                Catch ex As Exception
                    ' Exception looking up the process ID
                    m_pid = 999999999
                End Try

                m_processIdInstanceName = String.Empty

                If blnStandardOutputRedirected Then
                    Try
                        m_Process.BeginOutputReadLine()

                        ' Attach a StreamReader to m_Process.StandardError 
                        srConsoleError = m_Process.StandardError

                        ' Do not attach a reader to m_Process.StandardOutput
                        ' since we are asynchronously reading the console output

                    Catch ex As Exception
                        ' Exception attaching the standard output
                        blnStandardOutputRedirected = False
                    End Try
                End If

                RaiseConditionalProgChangedEvent(Me)

                ' wait for program to exit (loop on interval)
                ' until external process exits or class is commanded
                ' to stop monitoring the process (m_doCleanup = true)
                '
                Do While Not (m_doCleanup)

                    If m_monitorInterval < MINIMUM_MONITOR_INTERVAL_MSEC Then m_monitorInterval = MINIMUM_MONITOR_INTERVAL_MSEC

                    Try
                        m_Process.WaitForExit(m_monitorInterval)
                        If m_Process.HasExited Then Exit Do
                    Catch ex As Exception
                        ' Exception calling .WaitForExit or .HasExited; most likely the process has exited
                        Exit Do
                    End Try

                Loop

                ' Need to free up resources used to keep
                ' track of the external process
                '
                m_pid = 0
                m_processIdInstanceName = String.Empty

                Try
                    m_ExitCode = m_Process.ExitCode
                Catch ex As Exception
                    ' Exception looking up ExitCode; most likely the process has exited
                    m_ExitCode = 0
                End Try

                If blnStandardOutputRedirected Then
                    ' Read any console error text using srConsoleError
                    HandleOutputStreams(srConsoleError)

                    If Not srConsoleError Is Nothing Then srConsoleError.Close()
                End If

                Try
                    m_Process.Close()
                Catch ex As Exception
                    ' Exception closing the process; ignore
                End Try

                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Process " & m_name & " terminated with exit code " & m_ExitCode,
                      Logging.ILogger.logMsgType.logHealth, True)

                    If Not m_CachedConsoleError Is Nothing AndAlso m_CachedConsoleError.Length > 0 Then
                        m_EventLogger.PostEntry("Cached error text for process " & m_name & ": " & m_CachedConsoleError.ToString,
                          Logging.ILogger.logMsgType.logError, True)
                    End If
                End If

                If Not m_ConsoleOutputStreamWriter Is Nothing Then
                    ' Give the other threads time to write any additional info to m_ConsoleOutputStreamWriter
                    Dim intMaxWaitTimeMSec As Integer = 1000
                    GarbageCollectNow(intMaxWaitTimeMSec)
                    m_ConsoleOutputStreamWriter.Close()
                End If

                ' Decide whether or not to repeat starting
                ' the external process again, or quit
                '
                If m_repeat And Not m_doCleanup Then
                    ' Repeat starting the process
                    ' after waiting for minimum hold off time interval
                    '
                    m_state = States.Waiting

                    RaiseConditionalProgChangedEvent(Me)

                    Thread.Sleep(m_holdOffTime)

                    m_state = States.Monitoring
                Else
                    ' Don't repeat starting the process - just quit
                    '
                    m_state = States.NotMonitoring
                    RaiseConditionalProgChangedEvent(Me)
                    Exit Do
                End If
            Loop

        End Sub

        ''' <summary>
        ''' Creates a new thread and starts code that runs and monitors a program in it
        ''' </summary>
        Public Sub StartAndMonitorProgram()
            If m_state = States.NotMonitoring Then
                m_state = States.Initializing
                m_doCleanup = False

                ' arrange to start the program as an external process
                ' and monitor it in a separate internal thread
                '
                Try
                    Dim m_ThreadStart As New ThreadStart(AddressOf Me.Start)
                    m_Thread = New Thread(m_ThreadStart)
                    m_Thread.Start()
                Catch ex As Exception
                    ThrowConditionalException(ex, "Caught exception while trying to start thread.")

                End Try
            End If
        End Sub

        Protected Function StartingOrMonitoring() As Boolean
            If m_state = States.Initializing OrElse m_state = States.StartingProcess OrElse m_state = States.Monitoring Then
                Return True
            Else
                Return False
            End If
        End Function

        ''' <summary>
        ''' Causes monitoring thread to exit on its next monitoring cycle
        ''' </summary>
        Public Sub StopMonitoringProgram(Optional ByVal Kill As Boolean = False)

            If Me.StartingOrMonitoring() AndAlso Kill Then   'Program is running, kill it and abort thread
                Try
                    m_Process.Kill()
                    m_Thread.Abort()  'DAC added
                Catch ex As ThreadAbortException
                    ThrowConditionalException(CType(ex, Exception), "Caught ThreadAbortException while trying to abort thread.")
                Catch ex As ComponentModel.Win32Exception
                    ThrowConditionalException(CType(ex, Exception), "Caught Win32Exception while trying to kill thread.")
                Catch ex As InvalidOperationException
                    ThrowConditionalException(CType(ex, Exception), "Caught InvalidOperationException while trying to kill thread.")
                Catch ex As SystemException
                    ThrowConditionalException(CType(ex, Exception), "Caught SystemException while trying to kill thread.")
                Catch ex As Exception
                    ThrowConditionalException(ex, "Caught Exception while trying to kill or abort thread.")
                End Try
            End If

            If m_state = States.Waiting And Kill Then  'Program not running, just abort thread
                Try
                    m_Thread.Abort()
                Catch ex As ThreadAbortException
                    ThrowConditionalException(CType(ex, Exception), "Caught ThreadAbortException while trying to abort thread.")
                Catch ex As Exception
                    ThrowConditionalException(ex, "Caught exception while trying to abort thread.")
                End Try
            End If

            If Me.StartingOrMonitoring() OrElse m_state = States.Waiting Then
                m_state = States.CleaningUp
                m_doCleanup = True
                Try
                    ' Attempt to re-join the thread (wait for 5 seconds, at most)
                    m_Thread.Join(5000)
                Catch ex As ThreadStateException
                    ThrowConditionalException(CType(ex, Exception), "Caught ThreadStateException while trying to join thread.")
                Catch ex As ThreadInterruptedException
                    ThrowConditionalException(CType(ex, Exception), "Caught ThreadInterruptedException while trying to join thread.")
                Catch ex As Exception
                    ThrowConditionalException(ex, "Caught exception while trying to join thread.")
                End Try
                m_state = States.NotMonitoring
            End If
        End Sub

        Private Sub ThrowConditionalException(ByRef ex As Exception, ByVal loggerMessage As String)
            If Not m_ExceptionLogger Is Nothing Then
                m_ExceptionLogger.PostError(loggerMessage, ex, True)
            End If
            If m_NotifyOnException Then
                If m_ExceptionLogger Is Nothing Then
                    Console.WriteLine("Exception caught (but ignored): " & loggerMessage & "; " & ex.Message)
                Else
                    m_ExceptionLogger.PostError("Rethrowing exception", ex, True)
                    Throw ex
                End If
            End If
        End Sub

#End Region

    End Class

End Namespace
