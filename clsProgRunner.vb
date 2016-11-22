Option Strict On

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading

Namespace Processes

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

        ' overall state of this object
        Private m_state As States = States.NotMonitoring

        ''' <summary>
        ''' Used to start and monitor the external program
        ''' </summary>
        Private ReadOnly m_Process As New Process()

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
        Public Event ProgChanged(obj As clsProgRunner)

        ''' <summary>
        ''' This event is raised when the external program writes text to the console
        ''' </summary>
        ''' <param name="NewText"></param>
        ''' <remarks></remarks>
        Public Event ConsoleOutputEvent(NewText As String)

        ''' <summary>
        ''' This event is raised when the external program writes text to the console's error stream
        ''' </summary>
        ''' <param name="NewText"></param>
        ''' <remarks></remarks>
        Public Event ConsoleErrorEvent(NewText As String)

#End Region

#Region "Properties"

        ''' <summary>
        ''' Arguments supplied to external program when it is run
        ''' </summary>
        Public Property Arguments As String

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
        Public Property CacheStandardOutput As Boolean

        ''' <summary>
        ''' When true, the program name and command line arguments will be added to the top of the console output file
        ''' </summary>
        Public Property ConsoleOutputFileIncludesCommandLine As Boolean

        ''' <summary>
        ''' File path to which the console output will be written if WriteConsoleOutputToFile is true
        ''' If blank, then file path will be auto-defined in the WorkDir  when program execution starts
        ''' </summary>
        Public Property ConsoleOutputFilePath As String = String.Empty

        ''' <summary>
        ''' Determine if window should be displayed
        ''' Will be forced to True if CacheStandardOutput = True
        ''' </summary>
        Public Property CreateNoWindow As Boolean

        ''' <summary>
        ''' When true, then echoes, in real time, text written to the Console by the external program 
        ''' Ignored if CreateNoWindow = False
        ''' </summary>
        Public Property EchoOutputToConsole As Boolean

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
            Set(Value As Integer)
                If Value < MINIMUM_MONITOR_INTERVAL_MSEC Then Value = MINIMUM_MONITOR_INTERVAL_MSEC
                m_monitorInterval = Value
            End Set
        End Property

        ''' <summary>
        ''' Name of this progrunner
        ''' </summary>
        Public Property Name As String

        ''' <summary>
        ''' When true, raises event ProgChanged
        ''' </summary>
        Public Property NotifyOnEvent As Boolean Implements Logging.ILoggerAware.NotifyOnEvent

        ''' <summary>
        ''' When true, and if m_ExceptionLogger is defined, re-throws the exception
        ''' </summary>
        Public Property NotifyOnException As Boolean Implements Logging.ILoggerAware.NotifyOnException


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
        Public Property Program As String

        ''' <summary>
        ''' Whether prog runner will restart external program after it exits
        ''' </summary>
        Public Property Repeat As Boolean = False

        ''' <summary>
        ''' Time (in seconds) that prog runner waits to restart the external program after it exits
        ''' </summary>
        Public Property RepeatHoldOffTime() As Double

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
        Public Property WindowStyle As ProcessWindowStyle

        ''' <summary>
        ''' Working directory for process execution
        ''' Not necessarily the same as the directory that contains the program we're running
        ''' </summary>
        Public Property WorkDir As String

        ''' <summary>
        ''' When true then will write the standard output to a file in real-time
        ''' Will also fire event ConsoleOutputEvent as new text is written to the console
        ''' Define the path to the file using property ConsoleOutputFilePath; if not defined, the file will be created in the WorkDir
        ''' </summary>
        ''' <remarks>If this is true, then no window will be shown, even if CreateNoWindow=False</remarks>
        Public Property WriteConsoleOutputToFile As Boolean

#End Region

#Region "Methods"

        ''' <summary>
        ''' Constructor
        ''' </summary>
        Public Sub New()
            WorkDir = String.Empty
            CreateNoWindow = False
            m_ExitCode = -123454321  ' Unreasonable value
            m_monitorInterval = DEFAULT_MONITOR_INTERVAL_MSEC
            NotifyOnEvent = True
            NotifyOnException = True
            CacheStandardOutput = False
            EchoOutputToConsole = True
            WriteConsoleOutputToFile = False
            ConsoleOutputFileIncludesCommandLine = True
            ConsoleOutputFilePath = String.Empty
        End Sub

        ''' <summary>
        ''' Clears any console output text that is currently cached
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub ClearCachedConsoleOutput()

            If m_CachedConsoleOutput Is Nothing Then
                m_CachedConsoleOutput = New StringBuilder()
            Else
                m_CachedConsoleOutput.Clear()
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
                m_CachedConsoleError = New StringBuilder()
            Else
                m_CachedConsoleError.Clear()
            End If

        End Sub

        ''' <summary>
        ''' Asynchronously handles the error stream from m_Process
        ''' </summary>
        Private Sub ConsoleErrorHandler(sendingProcess As Object,
                                         outLine As DataReceivedEventArgs)

            ' Handle the error data
            If Not String.IsNullOrEmpty(outLine.Data) Then

                ' Send to the console output stream to maximize the chance of somebody noticing this error
                ConsoleOutputHandler(sendingProcess, outLine)

                RaiseEvent ConsoleErrorEvent(outLine.Data)

                If Not m_CachedConsoleError Is Nothing Then
                    m_CachedConsoleError.Append(outLine.Data)
                End If

            End If

        End Sub

        ''' <summary>
        ''' Asynchronously handles the console output from m_Process
        ''' </summary>
        Private Sub ConsoleOutputHandler(sendingProcess As Object,
                                         outLine As DataReceivedEventArgs)

            ' Collect the console output
            If Not outLine.Data Is Nothing Then

                RaiseEvent ConsoleOutputEvent(outLine.Data)

                If EchoOutputToConsole Then
                    Console.WriteLine(outLine.Data)
                End If

                If CacheStandardOutput Then
                    ' Add the text to the collected output
                    m_CachedConsoleOutput.AppendLine(outLine.Data)
                End If

                If WriteConsoleOutputToFile AndAlso Not m_ConsoleOutputStreamWriter Is Nothing Then
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
            Const intMaxWaitTimeMSec = 1000
            GarbageCollectNow(intMaxWaitTimeMSec)
        End Sub

        ''' <summary>
        ''' Force garbage collection
        ''' </summary>
        ''' <remarks></remarks>
        Public Shared Sub GarbageCollectNow(intMaxWaitTimeMSec As Integer)
            Const THREAD_SLEEP_TIME_MSEC = 100

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
        ''' Returns the full path to the console output file that will be created if WriteConsoleOutputToFile is true
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>Before calling this function, define WorkDir (working directory folder) and Program (full path to the .exe to run)</remarks>
        Public Function GetConsoleOutputFilePath() As String

            Dim consoleOutputFileName As String
            If String.IsNullOrEmpty(Program) Then
                consoleOutputFileName = "ProgRunner_ConsoleOutput.txt"
            Else
                consoleOutputFileName = Path.GetFileNameWithoutExtension(Program) & "_ConsoleOutput.txt"
            End If

            If String.IsNullOrEmpty(WorkDir) Then
                Return consoleOutputFileName
            End If

            Return Path.Combine(WorkDir, consoleOutputFileName)
        End Function

        ''' <summary>
        ''' Returns the number of cores
        ''' </summary>
        ''' <returns>The number of cores on this computer</returns>
        ''' <remarks>Should not be affected by hyperthreading, so a computer with two 4-core chips will report 8 cores</remarks>
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
        ''' <returns>Number of cores in use; 0 if the process is terminated.  Exception is thrown if a problem</returns>
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
        ''' <returns>Number of cores in use; 0 if the process is terminated. Exception is thrown if a problem</returns>
        ''' <remarks>Core count is typically an integer, but can be a fractional number if not using a core 100%</remarks>
        Public Shared Function GetCoreUsageByProcessID(processId As Integer, ByRef processIdInstanceName As String) As Single

            Try

                If mCachedCoreCount = 0 Then
                    mCachedCoreCount = GetCoreCount()
                End If

                Dim perfCounterContainer As KeyValuePair(Of String, PerformanceCounter) = Nothing
                Dim getNewPerfCounter = True
                Dim maxAttempts = 2

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
                    Else
                        ' Existing performance counter found
                        maxAttempts = 1
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

                    mCachedPerfCounters.TryGetValue(processId, perfCounterContainer)
                End If

                Dim cpuUsage = GetCoreUsageForPerfCounter(perfCounterContainer.Value, maxAttempts)

                Dim coresInUse = cpuUsage / 100.0

                Return CSng(coresInUse)

            Catch ex As InvalidOperationException
                ' The process is likely terminated
                Return 0
            Catch ex As Exception
                Throw New Exception("Exception in GetCoreUsageByProcessID for processId " & processId & ": " & ex.Message, ex)
            End Try

        End Function

        ''' <summary>
        ''' Sample the given performance counter to determine the CPU usage
        ''' </summary>
        ''' <param name="perfCounter">Performance counter instance</param>
        ''' <param name="maxAttempts">Number of attempts</param>
        ''' <returns>Number of cores in use; 0 if the process is terminated. Exception is thrown if a problem</returns>
        ''' <remarks>
        ''' The first time perfCounter.NextSample() is called a Permissions exception is sometimes thrown
        ''' Set maxAttempts to 2 or higher to gracefully handle this
        ''' </remarks>
        Private Shared Function GetCoreUsageForPerfCounter(perfCounter As PerformanceCounter, maxAttempts As Integer) As Single

            If maxAttempts < 1 Then maxAttempts = 1
            For iteration = 1 To maxAttempts

                Try

                    ' Take a sample, wait 1 second, then sample again
                    Dim sample1 = perfCounter.NextSample()
                    Thread.Sleep(1000)
                    Dim sample2 = perfCounter.NextSample()

                    ' Each core contributes "100" to the overall cpuUsage
                    Dim cpuUsage = CounterSample.Calculate(sample1, sample2)
                    Return cpuUsage

                Catch ex As InvalidOperationException
                    ' The process is likely terminated
                    Return 0
                Catch ex As Exception
                    If iteration = maxAttempts Then
                        Throw
                    Else
                        ' Wait 500 milliseconds then try again
                        Thread.Sleep(500)
                    End If
                End Try

            Next

            Return 0

        End Function

        ''' <summary>
        ''' Reports the number of cores in use by the given process
        ''' This method takes at least 1000 msec to execute
        ''' </summary>
        ''' <param name="processName">Process name, for example chrome (do not include .exe)</param>
        ''' <returns>Number of cores in use; -1 if process not found; exception is thrown if a problem</returns>
        ''' <remarks>
        ''' Core count is typically an integer, but can be a fractional number if not using a core 100%
        ''' If multiple processes are running with the given name then returns the total core usage for all of them
        ''' </remarks>
        Public Shared Function GetCoreUsageByProcessName(processName As String) As Single
            Dim processIDs As List(Of Integer) = Nothing
            Return GetCoreUsageByProcessName(processName, processIDs)
        End Function

        ''' <summary>
        ''' Reports the number of cores in use by the given process
        ''' This method takes at least 1000 msec to execute
        ''' </summary>
        ''' <param name="processName">Process name, for example chrome (do not include .exe)</param>
        ''' <param name="processIDs">List of ProcessIDs matching the given process name</param>
        ''' <returns>Number of cores in use; -1 if process not found; exception is thrown if a problem</returns>
        ''' <remarks>
        ''' Core count is typically an integer, but can be a fractional number if not using a core 100%
        ''' If multiple processes are running with the given name then returns the total core usage for all of them
        ''' </remarks>
        Public Shared Function GetCoreUsageByProcessName(processName As String, <Out()> ByRef processIDs As List(Of Integer)) As Single

            processIDs = New List(Of Integer)
            Dim processInstances = Process.GetProcessesByName(processName)
            If processInstances.Count = 0 Then Return -1

            Dim coreUsageOverall As Single = 0
            For Each runningProcess In processInstances
                Dim processID = runningProcess.Id
                processIDs.Add(processID)

                Dim coreUsage = GetCoreUsageByProcessID(processID, String.Empty)
                If coreUsage > 0 Then
                    coreUsageOverall += coreUsage
                End If
            Next

            Return coreUsageOverall

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
        Public Sub RegisterExceptionLogger(logger As Logging.ILogger) Implements Logging.ILoggerAware.RegisterEventLogger
            m_ExceptionLogger = logger
        End Sub

        ''' <summary>Sets the name of the event logger</summary>
        Public Sub RegisterEventLogger(logger As Logging.ILogger) Implements Logging.ILoggerAware.RegisterExceptionLogger
            m_EventLogger = logger
        End Sub

        Private Sub RaiseConditionalProgChangedEvent(obj As clsProgRunner)
            If NotifyOnEvent Then
                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Raising ProgChanged event for " & obj.Name & ".", Logging.ILogger.logMsgType.logHealth, True)
                End If
                RaiseEvent ProgChanged(obj)
            End If
        End Sub

        ''' <summary>
        ''' Start program as external process and monitor its state
        ''' </summary>
        Private Sub Start()

            Dim blnStandardOutputRedirected As Boolean

            ' set up parameters for external process
            '
            With m_Process.StartInfo
                .FileName = Program
                .WorkingDirectory = WorkDir
                .Arguments = Arguments
                .CreateNoWindow = CreateNoWindow
                If .CreateNoWindow Then
                    .WindowStyle = ProcessWindowStyle.Hidden
                Else
                    .WindowStyle = WindowStyle
                End If

                If .CreateNoWindow OrElse CacheStandardOutput OrElse WriteConsoleOutputToFile Then
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
                ' Add event handlers to asynchronously read the console output and error stream
                AddHandler m_Process.OutputDataReceived, AddressOf ConsoleOutputHandler
                AddHandler m_Process.ErrorDataReceived, AddressOf ConsoleErrorHandler

                If WriteConsoleOutputToFile Then
                    Try
                        If String.IsNullOrEmpty(ConsoleOutputFilePath) Then
                            ' Need to auto-define m_ConsoleOutputFilePath
                            ConsoleOutputFilePath = GetConsoleOutputFilePath()
                        End If

                        m_ConsoleOutputStreamWriter = New StreamWriter(New FileStream(ConsoleOutputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        m_ConsoleOutputStreamWriter.AutoFlush = True

                        If ConsoleOutputFileIncludesCommandLine Then
                            m_ConsoleOutputStreamWriter.WriteLine(Path.GetFileName(Program) & " " & Trim(Arguments))
                            m_ConsoleOutputStreamWriter.WriteLine(New String("-"c, 80))
                        End If
                    Catch ex As Exception
                        ' Report the error, but continue processing
                        ThrowConditionalException(ex, "Caught exception while trying to create the console output file, " & ConsoleOutputFilePath)
                    End Try
                End If
            End If

            ' Make sure the cached output StringBuilders are initialized
            ClearCachedConsoleOutput()
            ClearCachedConsoleError()

            Do
                ' Start the program as an external process
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
                        ' Initiate asynchronously reading the console output and error streams

                        m_Process.BeginOutputReadLine()
                        m_Process.BeginErrorReadLine()

                    Catch ex As Exception
                        ' Exception attaching the standard output
                        blnStandardOutputRedirected = False
                    End Try
                End If

                RaiseConditionalProgChangedEvent(Me)

                ' Wait for program to exit (loop on interval)
                '
                ' We wait until the external process exits or 
                ' the class is instructed to stop monitoring the process (m_doCleanup = true)
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

                Try
                    m_Process.Close()
                Catch ex As Exception
                    ' Exception closing the process; ignore
                End Try

                If Not m_EventLogger Is Nothing Then
                    m_EventLogger.PostEntry("Process " & Name & " terminated with exit code " & m_ExitCode,
                      Logging.ILogger.logMsgType.logHealth, True)

                    If Not m_CachedConsoleError Is Nothing AndAlso m_CachedConsoleError.Length > 0 Then
                        m_EventLogger.PostEntry("Cached error text for process " & Name & ": " & m_CachedConsoleError.ToString,
                          Logging.ILogger.logMsgType.logError, True)
                    End If
                End If

                If Not m_ConsoleOutputStreamWriter Is Nothing Then
                    ' Give the other threads time to write any additional info to m_ConsoleOutputStreamWriter
                    Dim maxWaitTimeMSec = 1000
                    GarbageCollectNow(maxWaitTimeMSec)
                    m_ConsoleOutputStreamWriter.Close()
                End If

                ' Decide whether or not to repeat starting
                ' the external process again, or quit
                '
                If Repeat And Not m_doCleanup Then
                    ' Repeat starting the process
                    ' after waiting for minimum hold off time interval
                    '
                    m_state = States.Waiting

                    RaiseConditionalProgChangedEvent(Me)

                    Dim holdoffMilliseconds = CInt(RepeatHoldOffTime * 1000)
                    Thread.Sleep(holdoffMilliseconds)

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
        Public Sub StopMonitoringProgram(Optional Kill As Boolean = False)

            If Me.StartingOrMonitoring() AndAlso Kill Then   'Program is running, kill it and abort thread
                Try
                    m_Process.Kill()
                    m_Thread.Abort()
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

        Private Sub ThrowConditionalException(ex As Exception, loggerMessage As String)
            If Not m_ExceptionLogger Is Nothing Then
                m_ExceptionLogger.PostError(loggerMessage, ex, True)
            End If

            If NotifyOnException Then
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
