Option Strict On

'File:  ShareConnector.vb
'File Contents:  ShareConnector class that connects a machine to an SMB/CIFS share
'                using a password and user name.
'Author(s):  Nathan Trimble
'
'Modifed: DAC (7/2/2004) -- Provided overloading for constructor, added property for share name


Namespace Files
    ''' <summary>Connects to a file share using a password and user name.
    ''' </summary>
    ''' <remarks>
    ''' The default behaviour when connecting to SMB/CIFS file shares is for
    ''' the system to supply the user name and password used to logon to the local machine.
    ''' This class allows you to connect to SMB/CIFS file shares when the use of
    ''' SSPI isn't availabe and/or when you don't wish to use the default behaviour.
    ''' It's quite comparable to the "Connect using a different user name." option in the Map Network Drive
    ''' utility in Windows.  Much of this code came from Microsoft Knowledge Base Article - 173011.  It was
    ''' then modified to fit our needs.
    ''' </remarks>
    Public Class ShareConnector

		Private mErrorMessage As String = ""

		''' <summary>This structure is used to group a bunch of member variables.</summary>
		Private Structure udtNetResource
			Dim dwScope As Integer
			Dim dwType As Integer
			Dim dwDisplayType As Integer
			Dim dwUsage As Integer
			Dim lpLocalName As String
			Dim lpRemoteName As String
			Dim lpComment As String
			Dim lpProvider As String
		End Structure

		Private Const NO_ERROR As Short = 0
		Private Const CONNECT_UPDATE_PROFILE As Short = &H1S

		''' <summary> Constant that may be used by NETRESOURCE->dwScope </summary>
		Private Const RESOURCE_CONNECTED As Short = &H1S
		''' <summary> Constant that may be used by NETRESOURCE->dwScope </summary>
		Private Const RESOURCE_GLOBALNET As Short = &H2S

		''' <summary> Constant that may be used by NETRESOURCE->dwType </summary>
		Private Const RESOURCETYPE_DISK As Short = &H1S
		''' <summary> Constant that may be used by NETRESOURCE->dwType </summary>
		Private Const RESOURCETYPE_PRINT As Short = &H2S
		''' <summary> Constant that may be used by NETRESOURCE->dwType </summary>
		Private Const RESOURCETYPE_ANY As Short = &H0S

		''' <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
		Private Const RESOURCEDISPLAYTYPE_DOMAIN As Short = &H1S
		''' <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
		Private Const RESOURCEDISPLAYTYPE_GENERIC As Short = &H0S
		''' <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
		Private Const RESOURCEDISPLAYTYPE_SERVER As Short = &H2S
		''' <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
		Private Const RESOURCEDISPLAYTYPE_SHARE As Short = &H3S

		''' <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
		Private Const RESOURCEUSAGE_CONNECTABLE As Short = &H1S
		''' <summary> Constant that may be used by NETRESOURCE->dwUsage </summary>
		Private Const RESOURCEUSAGE_CONTAINER As Short = &H2S

		Private Declare Function WNetAddConnection2 Lib "mpr.dll" Alias "WNetAddConnection2A" (ByRef lpNetResource As udtNetResource, ByVal lpPassword As String, ByVal lpUserName As String, ByVal dwFlags As Integer) As Integer
		Private Declare Function WNetCancelConnection2 Lib "mpr.dll" Alias "WNetCancelConnection2A" (ByVal lpName As String, ByVal dwFlags As Integer, ByVal fForce As Integer) As Integer

		Private mNetResource As udtNetResource
		Private mUsername As String
		Private mPassword As String
		Private mShareName As String = String.Empty

		''' <summary>
		''' This version of the constructor requires you to specify the sharename by setting the <see cref="Share">Share</see> property.
		''' </summary>
		Public Sub New(ByVal userName As String, ByVal userPwd As String)
			RealNew(userName, userPwd)
		End Sub

		''' <summary>
		''' This version of the constructor allows you to specify the sharename as an argument.
		''' </summary>
		Public Sub New(ByVal share As String, ByVal userName As String, ByVal userPwd As String)
			DefineShareName(share)
			RealNew(userName, userPwd)
		End Sub

		''' <summary>
		''' This routine is called by each of the constructors to make the actual assignments in a consistent fashion.
		''' </summary>
		Private Sub RealNew(ByVal userName As String, ByVal userPwd As String)
			mUsername = userName
			mPassword = userPwd
			mNetResource.lpRemoteName = mShareName
			mNetResource.dwType = RESOURCETYPE_DISK
			mNetResource.dwScope = RESOURCE_GLOBALNET
			mNetResource.dwDisplayType = RESOURCEDISPLAYTYPE_SHARE
			mNetResource.dwUsage = RESOURCEUSAGE_CONNECTABLE
		End Sub

		''' <summary>
		''' Sets the name of the file share to which you will connect.
		''' </summary>
		Public Property Share() As String
			Get
				Return mShareName
			End Get
			Set(ByVal Value As String)
				DefineShareName(Value)
				mNetResource.lpRemoteName = mShareName
			End Set
		End Property

		''' <summary>
		''' Connects to specified share using account/password specified through the constructor and 
		''' the file share name passed as an argument.
		''' </summary>
		''' <param name="Share">The name of the file share to which you will connect.</param>
		Public Function Connect(ByVal Share As String) As Boolean

			DefineShareName(Share)
			mNetResource.lpRemoteName = mShareName
			Return RealConnect()

		End Function

		''' <summary>
		''' Connects to specified share using account/password specified through the constructor.
		''' Requires you to have specifyed the sharename by setting the <see cref="Share">Share</see> property.
		''' </summary>
		Public Function Connect() As Boolean

			If mNetResource.lpRemoteName = "" Then
				mErrorMessage = "Share name not specified"
				Return False
			End If
			Return RealConnect()

		End Function

		''' <summary>
		''' Updates class variable with the specified share path
		''' </summary>
		''' <param name="share"></param>
		''' <remarks>If the path ends in a forward slash then the slash will be removed</remarks>
		Private Sub DefineShareName(ByVal share As String)
			If share.EndsWith("\") Then
				mShareName = share.TrimEnd("\"c)
			Else
				mShareName = share
			End If
		End Sub

		''' <summary>
		''' Connects to specified share using account/password specified previously.
		''' This is the function that actually does the connection based on the setup 
		''' from the <see cref="Connect">Connect</see> functions.
		''' </summary>
		Private Function RealConnect() As Boolean

			Dim errorNum As Integer

			errorNum = WNetAddConnection2(mNetResource, mPassword, mUsername, 0)
			If errorNum = NO_ERROR Then
				Debug.WriteLine("Connected.")
				Return True
			Else
				mErrorMessage = errorNum.ToString()
				Debug.WriteLine("Got error: " & errorNum)
				Return False
			End If

		End Function

		''' <summary>
		''' Disconnects the files share.
		''' </summary>
		Public Function Disconnect() As Boolean
			Dim errorNum As Integer = WNetCancelConnection2(Me.mNetResource.lpRemoteName, 0, CInt(True))
			If errorNum = NO_ERROR Then
				Debug.WriteLine("Disconnected.")
				Return True
			Else
				mErrorMessage = errorNum.ToString()
				Debug.WriteLine("Got error: " & errorNum)
				Return False
			End If
		End Function

		''' <summary>
		''' Gets the error message returned by the <see cref="Connect">Connect</see> and <see cref="Disconnect">Disconnect</see> functions.
		''' </summary>
		Public ReadOnly Property ErrorMessage() As String
			Get
				Return mErrorMessage
			End Get
		End Property
    End Class
End Namespace