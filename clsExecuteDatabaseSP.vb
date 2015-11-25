Option Strict On

' Simplified version of clsDBTask.vb, which was written by Dave Clark for the DMS Analysis Manager in 2007
Imports System.Threading
Imports System.Data.SqlClient
Imports System.Runtime.InteropServices

Namespace DataBase

    ''' <summary>Tools to execute a stored procedure</summary>
    Public Class clsExecuteDatabaseSP

#Region "Constants"
        Public Const RET_VAL_OK As Integer = 0
        Public Const RET_VAL_EXCESSIVE_RETRIES As Integer = -5           ' Timeout expired
        Public Const RET_VAL_DEADLOCK As Integer = -4                    ' Transaction (Process ID 143) was deadlocked on lock resources with another process and has been chosen as the deadlock victim

        Public Const DEFAULT_SP_RETRY_COUNT As Integer = 3
        Public Const DEFAULT_SP_RETRY_DELAY_SEC As Integer = 20

        Public Const DEFAULT_SP_TIMEOUT_SEC As Integer = 30

#End Region

#Region "Module variables"

        Protected m_ConnStr As String
        Protected mTimeoutSeconds As Integer = DEFAULT_SP_TIMEOUT_SEC

        Public Event DebugEvent(Message As String)
        Public Event DBErrorEvent(Message As String)

#End Region

#Region "Properties"
        Public Property DBConnectionString() As String
            Get
                Return m_ConnStr
            End Get
            Set(value As String)
                If String.IsNullOrWhiteSpace(value) Then
                    Throw New Exception("Connection string cannot be empty")
                End If
                m_ConnStr = value
            End Set
        End Property

        Public Property DebugMessagesEnabled As Boolean

        Public Property TimeoutSeconds() As Integer
            Get
                Return mTimeoutSeconds
            End Get
            Set(value As Integer)
                If value = 0 Then value = DEFAULT_SP_TIMEOUT_SEC
                If value < 10 Then value = 10

                mTimeoutSeconds = value
            End Set
        End Property
#End Region

#Region "Methods"
        ''' <summary>
        ''' Constructor
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub New(ConnectionString As String)

            m_ConnStr = String.Copy(ConnectionString)

        End Sub

        ''' <summary>
        ''' Constructor
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub New(connectionString As String, timoutSeconds As Integer)

            m_ConnStr = String.Copy(connectionString)
            mTimeoutSeconds = timoutSeconds

        End Sub

        ''' <summary>
        ''' Event handler for InfoMessage event
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="args"></param>
        ''' <remarks>Errors and warnings from SQL Server are caught here</remarks>
        Private Sub OnInfoMessage(sender As Object, args As SqlInfoMessageEventArgs)

            Dim err As SqlError
            Dim s As String

            For Each err In args.Errors
                s = "Message: " & err.Message &
                    ", Source: " & err.Source &
                    ", Class: " & err.Class &
                    ", State: " & err.State &
                    ", Number: " & err.Number &
                    ", LineNumber: " & err.LineNumber &
                    ", Procedure:" & err.Procedure &
                    ", Server: " & err.Server

                RaiseEvent DBErrorEvent(s)
            Next

        End Sub

        ''' <summary>
        ''' Method for executing a db stored procedure if a data table is to be returned; will retry the call to the procedure up to DEFAULT_SP_RETRY_COUNT=3 times
        ''' </summary>
        ''' <param name="spCmd">SQL command object containing stored procedure params</param>
        ''' <param name="outTable">If SP successful, contains data table on return</param>
        ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        ''' <remarks></remarks>
        Public Function ExecuteSP(
          spCmd As SqlCommand,
          <Out> ByRef outTable As DataTable) As Integer
            Return ExecuteSP(spCmd, outTable, DEFAULT_SP_RETRY_COUNT, DEFAULT_SP_RETRY_DELAY_SEC)
        End Function

        ''' <summary>
        ''' Method for executing a db stored procedure if a data table is to be returned
        ''' </summary>
        ''' <param name="spCmd">SQL command object containing stored procedure params</param>
        ''' <param name="outTable">If SP successful, contains data table on return</param>
        ''' <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        ''' <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        ''' <remarks></remarks>
        Public Function ExecuteSP(
          spCmd As SqlCommand,
          <Out> ByRef outTable As DataTable,
          maxRetryCount As Integer,
          retryDelaySeconds As Integer) As Integer

            Dim resultCode As Integer = -9999  'If this value is in error msg, then exception occurred before resultCode was set
            Dim errorMessage As String
            Dim dtStartTime = DateTime.UtcNow
            Dim retryCount As Integer = maxRetryCount
            Dim blnDeadlockOccurred As Boolean

            outTable = New DataTable("EmptyTable")

            If retryCount < 1 Then
                retryCount = 1
            End If

            If retryDelaySeconds < 1 Then
                retryDelaySeconds = 1
            End If

            While retryCount > 0    'Multiple retry loop for handling SP execution failures
                blnDeadlockOccurred = False
                Try
                    Using Cn = New SqlConnection(m_ConnStr)
                        AddHandler Cn.InfoMessage, New SqlInfoMessageEventHandler(AddressOf OnInfoMessage)
                        Using Da = New SqlDataAdapter(), Ds = New DataSet
                            'NOTE: The connection has to be added here because it didn't exist at the time the command object was created
                            spCmd.Connection = Cn

                            spCmd.CommandTimeout = TimeoutSeconds
                            Da.SelectCommand = spCmd

                            dtStartTime = DateTime.UtcNow
                            Da.Fill(Ds)

                            resultCode = CInt(Da.SelectCommand.Parameters("@Return").Value)

                            If Ds.Tables.Count > 0 Then
                                outTable = Ds.Tables.Item(0)
                            End If

                        End Using  'Ds
                        RemoveHandler Cn.InfoMessage, AddressOf OnInfoMessage
                    End Using  'Cn

                    Exit While
                Catch ex As Exception

                    retryCount -= 1
                    errorMessage = "Exception filling data adapter for " & spCmd.CommandText & ": " & ex.Message
                    errorMessage &= "; resultCode = " & resultCode.ToString & "; Retry count = " & retryCount.ToString
                    errorMessage &= "; " & Logging.Utilities.GetExceptionStackTrace(ex)

                    RaiseEvent DBErrorEvent(errorMessage)
                    Console.WriteLine(errorMessage)

                    If ex.Message.StartsWith("Could not find stored procedure " & spCmd.CommandText) Then
                        Exit While
                    ElseIf ex.Message.Contains("was deadlocked") Then
                        blnDeadlockOccurred = True
                    End If

                Finally
                    If DebugMessagesEnabled Then
                        Dim debugMessage = "SP execution time: " & DateTime.UtcNow.Subtract(dtStartTime).TotalSeconds.ToString("##0.000") & " seconds for SP " & spCmd.CommandText
                        RaiseEvent DebugEvent(debugMessage)
                    End If
                End Try

                If retryCount > 0 Then
                    Thread.Sleep(retryDelaySeconds * 1000)
                End If
            End While

            If retryCount < 1 Then
                'Too many retries, log and return error
                errorMessage = "Excessive retries"
                If blnDeadlockOccurred Then
                    errorMessage &= " (including deadlock)"
                End If
                errorMessage &= " executing SP " & spCmd.CommandText

                RaiseEvent DBErrorEvent(errorMessage)
                Console.WriteLine(errorMessage)

                If blnDeadlockOccurred Then
                    Return RET_VAL_DEADLOCK
                Else
                    Return RET_VAL_EXCESSIVE_RETRIES
                End If
            End If

            Return resultCode

        End Function

        ''' <summary>
        ''' Method for executing a db stored procedure, assuming no data table is returned; will retry the call to the procedure up to DEFAULT_SP_RETRY_COUNT=3 times
        ''' </summary>
        ''' <param name="spCmd">SQL command object containing stored procedure params</param>
        ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        ''' <remarks></remarks>
        Public Function ExecuteSP(spCmd As SqlCommand) As Integer

            Return ExecuteSP(spCmd, DEFAULT_SP_RETRY_COUNT)

        End Function

        ''' <summary>
        ''' Method for executing a db stored procedure, assuming no data table is returned
        ''' </summary>
        ''' <param name="spCmd">SQL command object containing stored procedure params</param>
        ''' <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        ''' <remarks></remarks>
        Public Function ExecuteSP(
          spCmd As SqlCommand,
          maxRetryCount As Integer) As Integer

            Return ExecuteSP(spCmd, maxRetryCount, DEFAULT_SP_RETRY_DELAY_SEC)

        End Function

        ''' <summary>
        ''' Method for executing a db stored procedure, assuming no data table is returned
        ''' </summary>
        ''' <param name="spCmd">SQL command object containing stored procedure params</param>
        ''' <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        ''' <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        ''' <remarks></remarks>
        Public Function ExecuteSP(
          spCmd As SqlCommand,
          maxRetryCount As Integer,
          retryDelaySeconds As Integer) As Integer

            Dim errorMessage As String = String.Empty
            Return ExecuteSP(spCmd, maxRetryCount, errorMessage, retryDelaySeconds)

        End Function

        ''' <summary>
        ''' Method for executing a db stored procedure when a data table is not returned
        ''' </summary>
        ''' <param name="spCmd">SQL command object containing stored procedure params</param>
        ''' <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        ''' <param name="errorMessage">Error message (output)</param>
        ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        ''' <remarks>No logging is performed by this procedure</remarks>
        Public Function ExecuteSP(
          spCmd As SqlCommand,
          maxRetryCount As Integer,
          <Out> ByRef errorMessage As String) As Integer
            Return ExecuteSP(spCmd, maxRetryCount, errorMessage, DEFAULT_SP_RETRY_DELAY_SEC)
        End Function

        ''' <summary>
        ''' Method for executing a db stored procedure when a data table is not returned
        ''' </summary>
        ''' <param name="spCmd">SQL command object containing stored procedure params</param>
        ''' <param name="maxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
        ''' <param name="errorMessage">Error message (output)</param>
        ''' <param name="retryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
        ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
        ''' <remarks>No logging is performed by this procedure</remarks>
        Public Function ExecuteSP(
          spCmd As SqlCommand,
          maxRetryCount As Integer,
          <Out> ByRef errorMessage As String,
          retryDelaySeconds As Integer) As Integer

            Dim resultCode As Integer = -9999  'If this value is in error msg, then exception occurred before resultCode was set			
            Dim dtStartTime = DateTime.UtcNow
            Dim retryCount As Integer = maxRetryCount
            Dim blnDeadlockOccurred As Boolean

            errorMessage = String.Empty

            If retryCount < 1 Then
                retryCount = 1
            End If

            If retryDelaySeconds < 1 Then
                retryDelaySeconds = 1
            End If

            While retryCount > 0    'Multiple retry loop for handling SP execution failures
                blnDeadlockOccurred = False
                Try
                    Using Cn = New SqlConnection(m_ConnStr)

                        Cn.Open()

                        spCmd.Connection = Cn
                        spCmd.CommandTimeout = TimeoutSeconds

                        dtStartTime = DateTime.UtcNow
                        spCmd.ExecuteNonQuery()

                        resultCode = CInt(spCmd.Parameters("@Return").Value)

                    End Using

                    errorMessage = String.Empty

                    Exit While
                Catch ex As Exception
                    retryCount -= 1
                    errorMessage = "Exception calling stored procedure " & spCmd.CommandText & ": " & ex.Message
                    errorMessage &= "; resultCode = " & resultCode.ToString & "; Retry count = " & retryCount.ToString
                    errorMessage &= "; " & Logging.Utilities.GetExceptionStackTrace(ex)

                    RaiseEvent DBErrorEvent(errorMessage)
                    Console.WriteLine(errorMessage)

                    If ex.Message.StartsWith("Could not find stored procedure " & spCmd.CommandText) Then
                        Exit While
                    ElseIf ex.Message.Contains("was deadlocked") Then
                        blnDeadlockOccurred = True
                    End If
                Finally
                    If DebugMessagesEnabled Then
                        Dim debugMessage = "SP execution time: " & DateTime.UtcNow.Subtract(dtStartTime).TotalSeconds.ToString("##0.000") & " seconds for SP " & spCmd.CommandText
                        RaiseEvent DebugEvent(debugMessage)
                    End If
                End Try

                If retryCount > 0 Then
                    Thread.Sleep(retryDelaySeconds * 1000)
                End If
            End While

            If retryCount < 1 Then
                'Too many retries, log and return error
                errorMessage = "Excessive retries"
                If blnDeadlockOccurred Then
                    errorMessage &= " (including deadlock)"
                End If
                errorMessage &= " executing SP " & spCmd.CommandText

                RaiseEvent DBErrorEvent(errorMessage)
                Console.WriteLine(errorMessage)

                If blnDeadlockOccurred Then
                    Return RET_VAL_DEADLOCK
                Else
                    Return RET_VAL_EXCESSIVE_RETRIES
                End If
            End If

            Return resultCode

        End Function

#End Region

    End Class

End Namespace