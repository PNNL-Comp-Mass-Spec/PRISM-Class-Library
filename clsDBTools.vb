Option Strict On

Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Runtime.InteropServices
Imports System.Threading
Imports PRISM.Logging

Namespace DataBase

    ''' <summary>
    ''' Tools to manipulates the database.
    ''' </summary>
    Public Class clsDBTools

#Region "Member Variables"

        ' DB access
        Private m_connection_str As String
        Private m_DBCn As SqlConnection

        Public Event ErrorEvent(errorMessage As String)

#End Region

        ''' <summary>
        ''' Constructor
        ''' </summary>
        ''' <param name="connectionString">Database connection string</param>
        Public Sub New(connectionString As String)
            m_connection_str = connectionString
        End Sub

        ''' <summary>
        ''' Deprecated Constructor
        ''' </summary>
        ''' <param name="logger">This is the logger.</param>
        ''' <param name="ConnectStr">This is a connection string.</param>
        <Obsolete("Use the constructor that does not take a logger")>
        Public Sub New(logger As ILogger, ConnectStr As String)
            m_connection_str = ConnectStr
        End Sub

        ''' <summary>
        ''' The property sets and gets a connection string.
        ''' </summary>
        Public Property ConnectStr() As String
            Get
                Return m_connection_str
            End Get
            Set(Value As String)
                m_connection_str = Value
            End Set
        End Property

        ''' <summary>
        ''' The function opens a database connection.
        ''' </summary>
        ''' <return>True if the connection was successfully opened</return>
        ''' <remarks>Retries the connection up to 3 times</remarks>
        Private Function OpenConnection() As Boolean
            Dim retryCount = 3
            Dim sleepTimeMsec = 300
            While retryCount > 0
                Try
                    m_DBCn = New SqlConnection(m_connection_str)
                    AddHandler m_DBCn.InfoMessage, New SqlInfoMessageEventHandler(AddressOf OnInfoMessage)
                    m_DBCn.Open()
                    retryCount = 0
                    Return True
                Catch e As SqlException
                    retryCount -= 1
                    m_DBCn.Close()
                    OnError("Connection problem", e)
                    Thread.Sleep(sleepTimeMsec)
                    sleepTimeMsec *= 2
                End Try
            End While

            OnError("Unable to open connection after multiple tries")
            Return False
        End Function

        ''' <summary>
        ''' The subroutine closes the database connection.
        ''' </summary>
        Private Sub CloseConnection()
            If Not m_DBCn Is Nothing Then
                m_DBCn.Close()
            End If
        End Sub

        ''' <summary>
        ''' The subroutine is an event handler for InfoMessage event.
        ''' </summary>
        ''' <remarks>
        ''' The errors and warnings sent from the SQL server are caught here
        ''' </remarks>
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
                OnError(s)
            Next
        End Sub

        ''' <summary>
        ''' The function gets a disconnected dataset as specified by the SQL statement.
        ''' </summary>
        ''' <param name="SQL">A SQL string.</param>
        ''' <param name="DS">A dataset.</param>
        ''' <param name="RowCount">A row counter.</param>
        ''' <return>Returns a disconnected dataset as specified by the SQL statement.</return>
        Public Function GetDiscDataSet(SQL As String, ByRef DS As DataSet, ByRef RowCount As Integer) As Boolean

            'Returns a disconnected dataset as specified by the SQL statement
            Dim Adapter As SqlDataAdapter

            'Verify database connection is open
            If Not OpenConnection() Then Return False

            Try
                'Get the dataset
                Adapter = New SqlDataAdapter(SQL, m_DBCn)
                DS = New DataSet
                RowCount = Adapter.Fill(DS)
                Return True
            Catch ex As Exception
                'If error happened, log it
                OnError("Error reading database", ex)
                Return False
            Finally
                'Be sure connection is closed
                m_DBCn.Close()
            End Try

        End Function

        ''' <summary>
        ''' Run a query against a SQL Server database, return the results as a list of strings
        ''' </summary>
        ''' <param name="sqlQuery">Query to run</param>
        ''' <param name="lstResults">Results (list of list of strings)</param>
        ''' <param name="callingFunction">Name of the calling function (for logging purposes)</param>
        ''' <param name="retryCount">Number of times to retry (in case of a problem)</param>
        ''' <param name="timeoutSeconds">Query timeout (in seconds); minimum is 5 seconds; suggested value is 30 seconds</param>
        ''' <param name="maxRowsToReturn">Maximum rows to return; 0 to return all rows</param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks>
        ''' Uses the connection string passed to the constructor of this class
        ''' Null values are converted to empty strings
        ''' Numbers are converted to their string equivalent
        ''' By default, retries the query up to 3 times
        ''' </remarks>
        Public Function GetQueryResults(
          sqlQuery As String,
          <Out()> ByRef lstResults As List(Of List(Of String)),
          callingFunction As String,
          Optional retryCount As Short = 3,
          Optional timeoutSeconds As Integer = 30,
          Optional maxRowsToReturn As Integer = 0) As Boolean

            If retryCount < 1 Then retryCount = 1
            If timeoutSeconds < 5 Then timeoutSeconds = 5

            lstResults = New List(Of List(Of String))

            While retryCount > 0
                Try
                    Using dbConnection = New SqlConnection(m_connection_str)
                        Using cmd = New SqlCommand(sqlQuery, dbConnection)

                            cmd.CommandTimeout = timeoutSeconds

                            dbConnection.Open()

                            Dim reader = cmd.ExecuteReader()

                            While reader.Read()
                                Dim lstCurrentRow = New List(Of String)

                                For columnIndex = 0 To reader.FieldCount - 1
                                    Dim value = reader.GetValue(columnIndex)

                                    If DBNull.Value.Equals(value) Then
                                        lstCurrentRow.Add(String.Empty)
                                    Else
                                        lstCurrentRow.Add(value.ToString())
                                    End If

                                Next

                                lstResults.Add(lstCurrentRow)

                                If maxRowsToReturn > 0 AndAlso lstResults.Count >= maxRowsToReturn Then
                                    Exit While
                                End If
                            End While

                        End Using
                    End Using

                    Return True

                Catch ex As Exception
                    retryCount -= 1S
                    If String.IsNullOrWhiteSpace(callingFunction) Then
                        callingFunction = "Unknown"
                    End If
                    Dim errorMessage = String.Format("Exception querying database (called from {0}): {1}; " +
                                                     "ConnectionString: {2}, RetryCount = {3}, Query {4}",
                                                     callingFunction, ex.Message, m_connection_str, retryCount, sqlQuery)

                    OnError(errorMessage)
                    Thread.Sleep(5000)              'Delay for 5 seconds before trying again
                End Try
            End While

            Return False

        End Function

        Private Sub OnError(errorMessage As String)
            If Not String.IsNullOrWhiteSpace(errorMessage) Then
                RaiseEvent ErrorEvent(errorMessage)
            End If
        End Sub

        Private Sub OnError(errorMessage As String, ex As Exception)
            If Not String.IsNullOrWhiteSpace(errorMessage) Then
                RaiseEvent ErrorEvent(errorMessage & ": " & ex.Message)
            End If
        End Sub

        ''' <summary>
        ''' The function updates a database table as specified in the SQL statement.
        ''' </summary>
        ''' <param name="SQL">A SQL string.</param>
        ''' <param name="AffectedRows">Affected Rows to be updated.</param>
        ''' <return>Returns Boolean shwoing if the database was updated.</return>
        <Obsolete("Functionality of this method has been disabled for safety; an exception will be raised if it is called")>
        Public Function UpdateDatabase(SQL As String, ByRef AffectedRows As Integer) As Boolean

            Throw New Exception("This method is obsolete (because it blindly executes the SQL); do not use")

            'Updates a database table as specified in the SQL statement
            Dim Cmd As SqlCommand

            AffectedRows = 0

            'Verify database connection is open
            If Not OpenConnection() Then Return False

            Try
                Cmd = New SqlCommand(SQL, m_DBCn)
                AffectedRows = Cmd.ExecuteNonQuery()
                Return True
            Catch ex As Exception
                'If error happened, log it
                OnError("Error updating database", ex)
                Return False
            Finally
                m_DBCn.Close()
            End Try

        End Function
    End Class
End Namespace
