Option Strict On

Imports System.IO

Namespace Files
    ''' <summary>Tools to manipulate paths and directories.</summary>
    ''' <remarks>
    ''' There is a set of functions to properly terminate directory paths.
    ''' There is a set of functions to copy an entire directory tree.
    ''' There is a set of functions to get the size of an entire directory tree, including the number of files and directories.
    '''</remarks>
    Public Class clsFileTools

        ''' <summary>Event is raised before copying begins.</summary>
        ''' <param name="filename">The file's full path.</param>
        Public Shared Event CopyingFile(ByVal filename As String)
  
#Region "Module constants and variables"
        'Private constants
        Private Const TERM_ADD As Boolean = True
        Private Const TERM_REMOVE As Boolean = False
        Private Const TERMCHAR_DOS As String = "\"
        Private Const TERMCHAR_UNIX As String = "/"
        Private Const COPY_OVERWRITE As Boolean = True
        Private Const COPY_NO_OVERWRITE As Boolean = False
#End Region

#Region "Public constants"
        'Constants
        ''' <summary>Used to add the path seperation character to the end of the directory path.</summary>
        Public Const TERMINATOR_ADD As Boolean = True
        ''' <summary>Used to remove the path seperation character from the end of the directory path.</summary>
        Public Const TERMINATOR_REMOVE As Boolean = False
#End Region

#Region "CheckTerminator function"
        'Functions
        ''' <summary>Modifies input directory path string depending on optional settings.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory seperation character.</param>
        ''' <param name="TermChar">The specified directory seperation character.</param>
        ''' <returns>The modified directory path.</returns>
        Public Overloads Shared Function CheckTerminator(ByVal InpFolder As String, ByVal AddTerm As Boolean, _
         ByVal TermChar As String) As String

            'Overload for all parameters specified
            Return CheckTerminatorEX(InpFolder, AddTerm, TermChar)

        End Function

        ''' <summary>Adds or removes the DOS path seperation character from the end of the directory path.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory seperation character.</param>
        ''' <returns>The modified directory path.</returns>
        Public Overloads Shared Function CheckTerminator(ByVal InpFolder As String, ByVal AddTerm As Boolean) As String

            'Overload for using default termination character (DOS)
            Return CheckTerminatorEX(InpFolder, AddTerm, TERMCHAR_DOS)

        End Function

        ''' <summary>Assures the directory path ends with the specified path seperation character.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="TermChar">The specified directory seperation character.</param>
        ''' <returns>The modified directory path.</returns>
        Public Overloads Shared Function CheckTerminator(ByVal InpFolder As String, ByVal TermChar As String) As String

            'Overload for using "add character" as default
            Return CheckTerminatorEX(InpFolder, TERM_ADD, TermChar)

        End Function

        ''' <summary>Assures the directory path ends with the DOS path seperation character.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <returns>The modified directory path.</returns>
        Public Overloads Shared Function CheckTerminator(ByVal InpFolder As String) As String

            'Overload for using all defaults (add DOS terminator char)
            Return CheckTerminatorEX(InpFolder, TERM_ADD, TERMCHAR_DOS)

        End Function

        ''' <summary>Modifies input directory path string depending on optional settings.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory seperation character.</param>
        ''' <param name="TermChar">The specified directory seperation character.</param>
        ''' <returns>The modified directory path.</returns>
        Private Shared Function CheckTerminatorEX(ByVal InpFolder As String, ByVal AddTerm As Boolean, _
         ByVal TermChar As String) As String

            'Modifies input folder string depending on optional settings.
            '		m_Addterm=True forces string to end with specified m_TermChar.
            '		m_Addterm=False removes specified m_TermChar from string if present

            Dim TempStr As String

            If AddTerm Then
                TempStr = CStr(IIf(Right(InpFolder, 1) <> TermChar, InpFolder & TermChar, InpFolder))
            Else
                TempStr = CStr(IIf(Right(InpFolder, 1) = TermChar, Left(InpFolder, Len(InpFolder) - 1), InpFolder))
            End If

            Return TempStr

        End Function
#End Region

#Region "CopyFile function"

        ''' <summary>Copies a source file to the destination file. Does not allow overwriting.</summary>
        ''' <param name="SourcePath">The source file path.</param>
        ''' <param name="DestPath">The destination file path.</param>
        Public Overloads Shared Sub CopyFile(ByVal SourcePath As String, ByVal DestPath As String)

            'Overload with overwrite set to default=FALSE
            CopyFileEx(SourcePath, DestPath, COPY_NO_OVERWRITE)

        End Sub

        ''' <summary>Copies a source file to the destination file. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source file path.</param>
        ''' <param name="DestPath">The destination file path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        Public Overloads Shared Sub CopyFile(ByVal SourcePath As String, ByVal DestPath As String, _
        ByVal OverWrite As Boolean)

            'Overload with no defaults
            CopyFileEx(SourcePath, DestPath, OverWrite)

        End Sub

        ''' <summary>Copies a source file to the destination file. Allows overwriting.</summary>
        ''' <remarks>
        ''' This function is unique in that it allows you to specify a destination path where
        ''' some of the directories do not already exist.  It will create them if they don't.
        ''' The last parameter specifies whether a file already present in the
        ''' destination directory will be overwritten
        ''' - Note: requires Imports System.IO
        ''' - Usage: CopyFile("C:\Misc\Bob.txt", "D:\MiscBackup\Bob.txt")
        ''' </remarks>
        ''' <param name="SourcePath">The source file path.</param>
        ''' <param name="DestPath">The destination file path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        Private Shared Sub CopyFileEx(ByVal SourcePath As String, ByVal DestPath As String, _
         ByVal Overwrite As Boolean)
            Dim dirPath As String = Path.GetDirectoryName(DestPath)
            If Not Directory.Exists(dirPath) Then
                Directory.CreateDirectory(dirPath)
            End If
            File.Copy(SourcePath, DestPath, Overwrite)
        End Sub
#End Region

#Region "CopyDirectory function"

        ''' <summary>Copies a source directory to the destination directory. Does not allow overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        Public Overloads Shared Sub CopyDirectory(ByVal SourcePath As String, ByVal DestPath As String)

            'Overload with overwrite set to default=FALSE
            CopyDirectoryEx(SourcePath, DestPath, COPY_NO_OVERWRITE, False, False)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        Public Overloads Shared Sub CopyDirectory(ByVal SourcePath As String, ByVal DestPath As String, _
        ByVal OverWrite As Boolean)

            'Overload with no defaults
            CopyDirectoryEx(SourcePath, DestPath, OverWrite, False, False)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        ''' <param name="bReadOnly">The value to be assgned to the read-only attribute of the destination file.</param>
        Public Overloads Shared Sub CopyDirectory(ByVal SourcePath As String, ByVal DestPath As String, _
        ByVal OverWrite As Boolean, ByVal bReadOnly As Boolean)

            'Overload with no defaults
            CopyDirectoryEx(SourcePath, DestPath, OverWrite, True, bReadOnly)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <remarks>The last parameter specifies whether the files already present in the
        ''' destination directory will be overwritten
        ''' - Note: requires Imports System.IO
        ''' - Usage: CopyDirectory("C:\Misc", "D:\MiscBackup")
        '''
        ''' Original code obtained from vb2themax.com
        ''' </remarks>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        ''' <param name="SetAttribute">true if the read-only attribute of the destination file is to be modified, false otherwise.</param>
        ''' <param name="bReadOnly">The value to be assgned to the read-only attribute of the destination file.</param>
        Private Shared Sub CopyDirectoryEx(ByVal SourcePath As String, ByVal DestPath As String, _
        ByVal Overwrite As Boolean, ByVal SetAttribute As Boolean, ByVal bReadOnly As Boolean)

            Dim SourceDir As DirectoryInfo = New DirectoryInfo(SourcePath)
            Dim DestDir As DirectoryInfo = New DirectoryInfo(DestPath)

            ' the source directory must exist, otherwise throw an exception
            If SourceDir.Exists Then
                ' if destination SubDir's parent SubDir does not exist throw an exception
                If Not DestDir.Parent.Exists Then
                    Throw New DirectoryNotFoundException _
                     ("Destination directory does not exist: " + DestDir.Parent.FullName)
                End If

                If Not DestDir.Exists Then
                    DestDir.Create()
                End If

                ' copy all the files of the current directory
                Dim ChildFile As FileInfo
                For Each ChildFile In SourceDir.GetFiles()
                    RaiseEvent CopyingFile(ChildFile.FullName)
                    If Overwrite Then
                        ChildFile.CopyTo(Path.Combine(DestDir.FullName, ChildFile.Name), True)
                    Else
                        ' if Overwrite = false, copy the file only if it does not exist
                        ' this is done to avoid an IOException if a file already exists
                        ' this way the other files can be copied anyway...
                        If Not File.Exists(Path.Combine(DestDir.FullName, ChildFile.Name)) Then
                            ChildFile.CopyTo(Path.Combine(DestDir.FullName, ChildFile.Name), _
                             False)
                        End If
                    End If
                    If SetAttribute Then
                        ' Get the file attributes from the source file
                        Dim fa As FileAttributes = ChildFile.Attributes()
                        ' Change the read-only attribute to the desired value
                        If bReadOnly Then
                            fa = fa Or FileAttributes.ReadOnly
                        Else
                            fa = fa And Not FileAttributes.ReadOnly
                        End If
                        ' Set the attributes of the destination file
                        File.SetAttributes(Path.Combine(DestDir.FullName, ChildFile.Name), fa)
                    End If
                Next

                ' copy all the sub-directories by recursively calling this same routine
                Dim SubDir As DirectoryInfo
                For Each SubDir In SourceDir.GetDirectories()
                    CopyDirectory(SubDir.FullName, Path.Combine(DestDir.FullName, _
                     SubDir.Name), Overwrite)
                Next
            Else
                Throw New DirectoryNotFoundException("Source directory does not exist: " + _
                 SourceDir.FullName)
            End If

        End Sub
#End Region

#Region "GetDirectorySize function"
        ''' <summary>Get the directory size.</summary>
        ''' <param name="DirPath">The path to the directory.</param>
        ''' <returns>The directory size.</returns>
        Public Overloads Shared Function GetDirectorySize(ByVal DirPath As String) As Long

            ' Overload for returning directory size only

            Dim DumFileCount As Long
            Dim DumDirCount As Long

            Return GetDirectorySizeEX(DirPath, DumFileCount, DumDirCount)

        End Function

        ''' <summary>Get the directory size, file count, and directory count for the entire directory tree.</summary>
        ''' <param name="DirPath">The path to the directory.</param>
        ''' <param name="FileCount">The number of files in the entire directory tree.</param>
        ''' <param name="SubDirCount">The number of directories in the entire directory tree.</param>
        ''' <returns>The directory size.</returns>
        Public Overloads Shared Function GetDirectorySize(ByVal DirPath As String, ByRef FileCount As Long, ByRef SubDirCount As Long) As Long

            'Overload for returning directory size, file count and directory count for entire directory tree
            Return GetDirectorySizeEX(DirPath, FileCount, SubDirCount)

        End Function

        ''' <summary>Get the directory size, file count, and directory count for the entire directory tree.</summary>
        ''' <param name="DirPath">The path to the directory.</param>
        ''' <param name="FileCount">The number of files in the entire directory tree.</param>
        ''' <param name="SubDirCount">The number of directories in the entire directory tree.</param>
        ''' <returns>The directory size.</returns>
        Private Shared Function GetDirectorySizeEX(ByVal DirPath As String, ByRef FileCount As Long, ByRef SubDirCount As Long) As Long

            ' Returns the size of the specified directory, number of files in the directory tree, and number of subdirectories
            ' - Note: requires Imports System.IO
            ' - Usage: Dim DirSize As Long = GetDirectorySize("D:\Projects")
            '
            ' Original code obtained from vb2themax.com
            Dim DirSize As Long
            Dim Dir As DirectoryInfo = New DirectoryInfo(DirPath)
            '		Dim InternalFileCount As Long
            '		Dim InternalDirCount As Long

            ' add the size of each file
            Dim ChildFile As FileInfo
            For Each ChildFile In Dir.GetFiles()
                DirSize += ChildFile.Length
                FileCount += 1
            Next

            ' add the size of each sub-directory, that is retrieved by recursively
            ' calling this same routine
            Dim SubDir As DirectoryInfo
            For Each SubDir In Dir.GetDirectories()
                DirSize += GetDirectorySizeEX(SubDir.FullName, FileCount, SubDirCount)
                SubDirCount += 1
            Next

            '		FileCount = InternalFileCount
            '		SubDirCount = InternalDirCount
            Return DirSize

        End Function
#End Region

    End Class
End Namespace
