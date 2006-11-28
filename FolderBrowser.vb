Option Strict On

Imports System.ComponentModel

Namespace Files

    ' Wrapper class to call Files.Forms.ShellFolderBrowser and highlight an initial folder
    ' Also defines numerous default BrowseFlags
    '
    ' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) (ShellFolderBrowser class provided by Gary Kiebel)
    ' Copyright 2005, Battelle Memorial Institute
    ' Started October 23, 2004
    ' Last modified October 30, 2004
    ''' <summary>TBD</summary>
    Public Class FolderBrowser

#Region "Classwide Variables"

        Private WithEvents mFolderBrowserDialog As Files.Forms.ShellFolderBrowser

        Private mCurrentFolderPath As String
        Private mBrowseFlags As Files.Forms.BrowseFlags
        Private mTitle As String

#End Region

#Region "Interface Functions"
        ''' <summary>TBD</summary>
        Public Property BrowseFlags() As Files.Forms.BrowseFlags
            Get
                Return mBrowseFlags
            End Get
            Set(ByVal Value As Files.Forms.BrowseFlags)
                mBrowseFlags = Value
            End Set
        End Property

        ''' <summary>TBD</summary>
        Public Property FolderPath() As String
            Get
                Return mCurrentFolderPath
            End Get
            Set(ByVal Value As String)
                mCurrentFolderPath = Value
            End Set
        End Property

        ''' <summary>TBD</summary>
        Public Property Title() As String
            Get
                Return mTitle
            End Get
            Set(ByVal Value As String)
                mTitle = Value
            End Set
        End Property
#End Region

        ''' <summary>TBD</summary>
        Public Function BrowseForFolder(Optional ByVal strFolderStartPath As String = "") As Boolean
            ' Returns True if user selects a folder
            ' Retrieve the folder chosen using FolderPath

            Dim blnSuccess As Boolean

            mFolderBrowserDialog = New Files.Forms.ShellFolderBrowser

            Try
                blnSuccess = False

                If Not strFolderStartPath Is Nothing AndAlso strFolderStartPath.Length > 0 Then
                    mCurrentFolderPath = strFolderStartPath
                End If

                With mFolderBrowserDialog
                    .Title = mTitle
                    .BrowseFlags = mBrowseFlags

                    If .ShowDialog() Then
                        mCurrentFolderPath = .FolderPath
                        blnSuccess = True
                    Else
                        ' Do not update mCurrentFolderPath
                    End If
                End With

            Catch ex As Exception
                ' Do not update mCurrentFolderPath
            Finally
                mFolderBrowserDialog = Nothing
            End Try

            Return blnSuccess

        End Function


        ''' <summary>TBD</summary>
        Private Sub mFolderBrowserDialog_Initialized(ByVal sender As Object, ByVal e As System.EventArgs) Handles mFolderBrowserDialog.Initialized
            If Not mCurrentFolderPath Is Nothing AndAlso mCurrentFolderPath.Length > 0 Then
                Try
                    mFolderBrowserDialog.SetExpanded(mCurrentFolderPath)
                Catch ex As Exception
                    ' Ignore any errors here
                End Try
            End If
        End Sub

        ''' <summary>TBD</summary>
        Public Sub New()
            mCurrentFolderPath = String.Empty
            mTitle = "Select Folder"

            ' Define the default Browse Flags
            mBrowseFlags = Files.Forms.BrowseFlags.ReturnOnlyFSDirs Or Files.Forms.BrowseFlags.DontGoBelowDomain Or _
                           Files.Forms.BrowseFlags.ShowStatusText Or Files.Forms.BrowseFlags.EditBox Or _
                           Files.Forms.BrowseFlags.Validate Or Files.Forms.BrowseFlags.NewDialogStyle
        End Sub

    End Class

End Namespace


Namespace Files.Forms

#Region "BrowseFlags Enum"
    ''' <summary>
    ' Flags that control display and behaviour of folder browse dialog
    ''' </summary>
    <Flags()> _
   Public Enum BrowseFlags As Integer
        ''' <summary>
        ' Same as BIF_RETURNONLYFSDIRS 
        ''' </summary>
        ReturnOnlyFSDirs = &H1
        ''' <summary>
        ' Same as BIF_DONTGOBELOWDOMAIN 
        ''' </summary>
        DontGoBelowDomain = &H2
        ''' <summary>
        ' Same as BIF_STATUSTEXT 
        ''' </summary>
        ShowStatusText = &H4
        ''' <summary>
        ' Same as BIF_RETURNFSANCESTORS 
        ''' </summary>
        ReturnFSancestors = &H8
        ''' <summary>
        ' Same as BIF_EDITBOX 
        ''' </summary>
        EditBox = &H10
        ''' <summary>
        ' Same as BIF_VALIDATE 
        ''' </summary>
        Validate = &H20
        ''' <summary>
        ' Same as BIF_NEWDIALOGSTYLE
        ''' </summary>
        NewDialogStyle = &H40
        ''' <summary>
        ' Same as BIF_BROWSEINCLUDEURLS 
        ''' </summary>
        BrowseIncludeURLs = &H80
        ''' <summary>
        ' Same as BIF_UAHINT
        ''' </summary>
        AddUsageHint = &H100
        ''' <summary>
        ' Same as BIF_NONEWFOLDERBUTTON 
        ''' </summary>
        NoNewFolderButton = &H200
        ''' <summary>
        ' Same as BIF_BROWSEFORCOMPUTER
        ''' </summary>
        BrowseForComputer = &H1000
        ''' <summary>
        ' Same as BIF_BROWSEFORPRINTER 
        ''' </summary>
        BrowseForPrinter = &H2000
        ''' <summary>
        ' Same as BIF_BROWSEINCLUDEFILES 
        ''' </summary>
        IncludeFiles = &H4000
        ''' <summary>
        ' Same as BIF_SHAREABLE 
        ''' </summary>
        ShowShareable = &H8000
    End Enum
#End Region

#Region "Class ShellFolderBrowser"

    Public Delegate Sub ValidateFailedEventHandler(ByVal sender As Object, ByVal args As ValidateFailedEventArgs)

    ''' <summary>
    ' Encapsulates the shell folder browse dialog shown by SHBrowseForFolder
    ''' </summary>
    Public Class ShellFolderBrowser
        Inherits System.ComponentModel.Component
        Private titleValue As String
        Private pidlReturnedValue As IntPtr = IntPtr.Zero
        Private handleValue As IntPtr
        Private displayNameValue As String
        Private flagsValue As BrowseFlags

        Public Sub New()
        End Sub

        ''' <summary>
        ' String that is displayed above the tree view control in the dialog box. 
        ' This string can be used to specify instructions to the user. 
        ' Can only be modified if the dalog is not currently displayed.
        ''' </summary>
        <Description("String that is displayed above the tree view control in the dialog box. This string can be used to specify instructions to the user.")> _
        Public Property Title() As String
            Get
                Return titleValue
            End Get
            Set(ByVal Value As String)
                If IntPtr.op_Inequality(handleValue, IntPtr.Zero) Then
                    Throw New InvalidOperationException
                End If
                titleValue = Value
            End Set
        End Property

        ''' <summary>
        ' The display name of the folder selected by the user
        ''' </summary>
        <Description("The display name of the folder selected by the user")> _
        Public ReadOnly Property FolderDisplayName() As String
            Get
                Return displayNameValue
            End Get
        End Property

        ''' <summary>
        ' The folder path that was selected
        ''' </summary>
        Public ReadOnly Property FolderPath() As String
            Get
                If IntPtr.op_Equality(pidlReturnedValue, IntPtr.Zero) Then
                    Return String.Empty
                End If
                Dim pathReturned As New System.Text.StringBuilder(260)

                UnManagedMethods.SHGetPathFromIDList(pidlReturnedValue, pathReturned)
                Return pathReturned.ToString()
            End Get
        End Property

        ''' <summary>
        ' Sets the flags that control the behaviour of the dialog
        ''' </summary>
        Public Property BrowseFlags() As BrowseFlags
            Get
                Return flagsValue
            End Get
            Set(ByVal Value As BrowseFlags)
                flagsValue = Value
            End Set
        End Property

        Private Function ShowDialogInternal(ByRef bi As BrowseInfo) As Boolean '

            bi.title = Title
            bi.displayname = New String(ControlChars.NullChar, 260)
            bi.callback = New BrowseCallBackProc(AddressOf Me.CallBack)
            bi.flags = CInt(flagsValue)

            'Free any old pidls
            If IntPtr.op_Inequality(pidlReturnedValue, IntPtr.Zero) Then
                UnManagedMethods.SHMemFree(pidlReturnedValue)
            End If
            Dim ret As Boolean
            pidlReturnedValue = UnManagedMethods.SHBrowseForFolder(bi)
            ret = IntPtr.op_Inequality(pidlReturnedValue, IntPtr.Zero)

            If ret Then
                displayNameValue = bi.displayname
            End If

            ' Reset the handle
            handleValue = IntPtr.Zero

            Return ret
        End Function

        ''' <summary>
        ' Shows the dialog
        ''' </summary>
        ''' <param name="owner">The window to use as the owner</param>
        Public Overloads Function ShowDialog(ByVal owner As System.Windows.Forms.IWin32Window) As Boolean
            If IntPtr.op_Inequality(handleValue, IntPtr.Zero) Then
                Throw New InvalidOperationException
            End If
            Dim bi As New BrowseInfo

            If Not (owner Is Nothing) Then
                bi.hwndOwner = owner.Handle
            End If
            Return ShowDialogInternal(bi)
        End Function

        ''' <summary>
        ' Shows the dialog using active window as the owner
        ''' </summary>
        Public Overloads Function ShowDialog() As Boolean
            Return ShowDialog(System.Windows.Forms.Form.ActiveForm)
        End Function

        Private WM_USER As Integer = &H400
        Private BFFM_SETSTATUSTEXTA As Integer = WM_USER + 100
        Private BFFM_SETSTATUSTEXTW As Integer = WM_USER + 104

        ''' <summary>
        ' Sets the text of the staus area of the folder dialog
        ''' </summary>
        ''' <param name="text">Text to set</param>
        Public Sub SetStatusText(ByVal [text] As String)
            If IntPtr.op_Equality(handleValue, IntPtr.Zero) Then
                Throw New InvalidOperationException
            End If
            Dim msg As Integer
            If Environment.OSVersion.Platform = PlatformID.Win32NT Then
                msg = BFFM_SETSTATUSTEXTW
            Else
                msg = BFFM_SETSTATUSTEXTA
            End If
            Dim strptr As IntPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAuto([text])

            UnManagedMethods.SendMessage(handleValue, msg, IntPtr.Zero, strptr)

            System.Runtime.InteropServices.Marshal.FreeHGlobal(strptr)
        End Sub

        Private BFFM_ENABLEOK As Integer = WM_USER + 101

        ''' <summary>
        ' Enables or disables the ok button
        ''' </summary>
        ''' <param name="bEnable">true to enable false to diasble the OK button</param>
        Public Sub EnableOkButton(ByVal bEnable As Boolean)
            If IntPtr.op_Equality(handleValue, IntPtr.Zero) Then
                Throw New InvalidOperationException
            End If
            Dim lp As IntPtr
            If bEnable Then
                lp = New IntPtr(1)
            Else
                lp = IntPtr.Zero
            End If

            UnManagedMethods.SendMessage(handleValue, BFFM_ENABLEOK, IntPtr.Zero, lp)
        End Sub

        Private BFFM_SETSELECTIONA As Integer = WM_USER + 102
        Private BFFM_SETSELECTIONW As Integer = WM_USER + 103


        ''' <summary>
        ' Sets the selection the text specified
        ''' </summary>
        ''' <param name="newsel">The path of the folder which is to be selected</param>
        Public Sub SetSelection(ByVal newsel As String)
            If IntPtr.op_Equality(handleValue, IntPtr.Zero) Then
                Throw New InvalidOperationException
            End If
            Dim msg As Integer

            If Environment.OSVersion.Platform = PlatformID.Win32NT Then
                msg = BFFM_SETSELECTIONA
            Else
                msg = BFFM_SETSELECTIONW
            End If

            Dim strptr As IntPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAuto(newsel)

            UnManagedMethods.SendMessage(handleValue, msg, New IntPtr(1), strptr)

            System.Runtime.InteropServices.Marshal.FreeHGlobal(strptr)
        End Sub

        Private BFFM_SETOKTEXT As Integer = WM_USER + 105

        ''' <summary>
        ' Sets the text of the OK button in the dialog
        ''' </summary>
        ''' <param name="text">New text of the OK button</param>
        Public Sub SetOkButtonText(ByVal [text] As String)
            If IntPtr.op_Equality(handleValue, IntPtr.Zero) Then
                Throw New InvalidOperationException
            End If
            Dim strptr As IntPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalUni([text])

            UnManagedMethods.SendMessage(handleValue, BFFM_SETOKTEXT, New IntPtr(1), strptr)

            System.Runtime.InteropServices.Marshal.FreeHGlobal(strptr)
        End Sub

        Private BFFM_SETEXPANDED As Integer = WM_USER + 106

        ''' <summary>
        ' Expand a path in the folder
        ''' </summary>
        ''' <param name="path">The path to expand</param>
        Public Sub SetExpanded(ByVal path As String)
            Dim strptr As IntPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(path)

            UnManagedMethods.SendMessage(handleValue, BFFM_SETEXPANDED, New IntPtr(1), strptr)

            System.Runtime.InteropServices.Marshal.FreeHGlobal(strptr)
        End Sub

        ''' <summary>
        ' Fired when the dialog is initialized
        ''' </summary>
        Public Event Initialized As EventHandler
        ''' <summary>
        ' Fired when selection changes
        ''' </summary>
        Public Event SelChanged As FolderSelChangedEventHandler
        ''' <summary>
        ' Shell provides an IUnknown through this event. For details see documentation of SHBrowseForFolder
        ''' </summary>
        Public Event IUnknownObtained As IUnknownObtainedEventHandler
        ''' <summary>
        ' Fired when validation of text typed by user fails
        ''' </summary>
        Public Event ValidateFailed As ValidateFailedEventHandler

        Private BFFM_INITIALIZED As Integer = 1
        Private BFFM_SELCHANGED As Integer = 2
        Private BFFM_VALIDATEFAILEDA As Integer = 3
        Private BFFM_VALIDATEFAILEDW As Integer = 4
        Private BFFM_IUNKNOWN As Integer = 5

        Private Function CallBack(ByVal hwnd As IntPtr, ByVal msg As Integer, ByVal lp As IntPtr, ByVal lpData As IntPtr) As Integer
            Dim ret As Integer = 0

            Select Case msg
                Case BFFM_INITIALIZED
                    handleValue = hwnd
                    RaiseEvent Initialized(Me, Nothing)
                Case BFFM_IUNKNOWN
                    RaiseEvent IUnknownObtained(Me, New IUnknownObtainedEventArgs(System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(lp)))
                Case BFFM_SELCHANGED
                    Dim e As New FolderSelChangedEventArgs(lp)
                    RaiseEvent SelChanged(Me, e)
                Case BFFM_VALIDATEFAILEDA
                    Dim e As New ValidateFailedEventArgs(System.Runtime.InteropServices.Marshal.PtrToStringAnsi(lpData))
                    RaiseEvent ValidateFailed(Me, e)

                    If e.DismissDialog Then
                        ret = 0
                    Else
                        ret = 1
                    End If
                Case BFFM_VALIDATEFAILEDW
                    Dim e As New ValidateFailedEventArgs(System.Runtime.InteropServices.Marshal.PtrToStringUni(lpData))
                    RaiseEvent ValidateFailed(Me, e)

                    If e.DismissDialog Then
                        ret = 0
                    Else
                        msg = 1
                    End If
            End Select

            Return ret
        End Function

        Protected Overloads Overrides Sub Dispose(ByVal disposing As Boolean)
            If IntPtr.op_Inequality(pidlReturnedValue, IntPtr.Zero) Then
                UnManagedMethods.SHMemFree(pidlReturnedValue)
                pidlReturnedValue = IntPtr.Zero
            End If
        End Sub
    End Class
#End Region

#Region "Class FolderSelChangedEventArgs"
    Public Delegate Sub FolderSelChangedEventHandler(ByVal sender As Object, ByVal e As FolderSelChangedEventArgs)

    Public Class FolderSelChangedEventArgs
        Inherits EventArgs
        Implements IDisposable
        Private pidlNewSelectValue As IntPtr


        Friend Sub New(ByVal pidlNewSelect As IntPtr)
            pidlNewSelectValue = pidlNewSelect
        End Sub

        ''' <summary>
        ' Return ITEMIDLIST for the currently selected folder
        ''' </summary>
        Public ReadOnly Property SelectedFolderPidl() As IntPtr
            Get
                Return pidlNewSelectValue
            End Get
        End Property

        ''' <summary>
        ' Gets the path of the folder which is currently selected
        ''' </summary>
        Public ReadOnly Property SelectedFolderPath() As String
            Get
                Dim path As New System.Text.StringBuilder(260)
                UnManagedMethods.SHGetPathFromIDList(pidlNewSelectValue, path)

                Return path.ToString()
            End Get
        End Property


        Public Sub Dispose() Implements IDisposable.Dispose
            UnManagedMethods.SHMemFree(pidlNewSelectValue)
        End Sub
    End Class
#End Region

#Region "Class IUnknownObtainedEventArgs"
    Public Delegate Sub IUnknownObtainedEventHandler(ByVal sender As Object, ByVal args As IUnknownObtainedEventArgs)

    ''' <summary>
    ' Provides data for the IUnknownObtainedEvent.
    ''' </summary>
    Public Class IUnknownObtainedEventArgs
        Inherits EventArgs
        Private siteUnknownValue As Object


        Friend Sub New(ByVal siteUnknown As Object)
            Me.siteUnknownValue = siteUnknown
        End Sub

        ''' <summary>
        ' Object that corrensponds to the IUnknown obtained
        ''' </summary>
        Public ReadOnly Property SiteUnknown() As Object
            Get
                Return siteUnknownValue
            End Get
        End Property
    End Class
#End Region

#Region "Class ValidateFailedEventArgs"
    ''' <summary>
    ' Provides data for validation failed event.
    ''' </summary>
    Public Class ValidateFailedEventArgs
        Private invalidTextValue As String
        Private dismissDialogValue As Boolean = False


        Friend Sub New(ByVal invalidText As String)
            Me.invalidTextValue = invalidText
        End Sub

        ''' <summary>
        ' The text which called validation to fail
        ''' </summary>
        Public ReadOnly Property InvalidText() As String
            Get
                Return invalidTextValue
            End Get
        End Property

        ''' <summary>
        ' Sets whether the dialog needs to be dismissed or not
        ''' </summary>
        Public Property DismissDialog() As Boolean
            Get
                Return dismissDialogValue
            End Get
            Set(ByVal Value As Boolean)
                dismissDialogValue = Value
            End Set
        End Property
    End Class
#End Region

End Namespace