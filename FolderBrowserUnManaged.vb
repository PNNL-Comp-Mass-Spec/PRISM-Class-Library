Option Strict On

Imports System.Runtime.InteropServices

Namespace Files.Forms

    Delegate Function BrowseCallBackProc(ByVal hwnd As IntPtr, ByVal msg As Integer, ByVal lp As IntPtr, ByVal wp As IntPtr) As Integer

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure BrowseInfo
        Public hwndOwner As IntPtr
        Public pidlRoot As IntPtr
        <MarshalAs(UnmanagedType.LPTStr)>
        Public displayname As String
        <MarshalAs(UnmanagedType.LPTStr)>
        Public title As String
        Public flags As Integer
        <MarshalAs(UnmanagedType.FunctionPtr)>
        Public callback As BrowseCallBackProc
        Public lparam As IntPtr
    End Structure

    <ComImport(), Guid("00000002-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Friend Interface IMalloc
        <PreserveSig()>
        Function Alloc(ByVal cb As IntPtr) As IntPtr

        <PreserveSig()>
        Function Realloc(ByVal pv As IntPtr, ByVal cb As IntPtr) As IntPtr

        <PreserveSig()>
        Sub Free(ByVal pv As IntPtr)

        <PreserveSig()>
        Function GetSize(ByVal pv As IntPtr) As IntPtr

        <PreserveSig()>
        Function DidAlloc(ByVal pv As IntPtr) As Integer

        <PreserveSig()>
        Sub HeapMinimize()
    End Interface

    ''' <summary>
    ''' A class that defines all the unmanaged methods used in the assembly
    ''' </summary>
    Friend Class UnManagedMethods

        Friend Declare Auto Function SHBrowseForFolder Lib "Shell32.dll" (ByRef bi As BrowseInfo) As IntPtr


        Friend Declare Auto Function SHGetPathFromIDList Lib "Shell32.dll" (ByVal pidl As IntPtr,
            <MarshalAs(UnmanagedType.LPTStr)> ByVal pszPath As Text.StringBuilder) As <MarshalAs(UnmanagedType.Bool)> Boolean


        Friend Declare Auto Function SendMessage Lib "User32.Dll" (ByVal hwnd As IntPtr, ByVal msg As Integer,
            ByVal wp As IntPtr, ByVal lp As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean


        Friend Declare Auto Function SHGetMalloc Lib "Shell32.dll" (<MarshalAs(UnmanagedType.IUnknown)> ByRef shmalloc As Object) As Integer


        ' Helper routine to free memory allocated using shells malloc object
        Friend Shared Sub SHMemFree(ByVal ptr As IntPtr)
            Dim shmalloc As Object = Nothing

            If SHGetMalloc(shmalloc) = 0 Then
                Dim malloc = CType(shmalloc, IMalloc)

                malloc.Free(ptr)
            End If
        End Sub
    End Class
End Namespace