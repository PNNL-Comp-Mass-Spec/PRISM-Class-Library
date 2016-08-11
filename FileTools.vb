Option Strict On

Imports System.IO
Imports System.Runtime.InteropServices    ' Required for call to GetDiskFreeSpaceEx
Imports System.Text.RegularExpressions

Namespace Files

    ''' <summary>Tools to manipulate paths and directories.</summary>
    ''' <remarks>
    ''' There is a set of functions to properly terminate directory paths.
    ''' There is a set of functions to copy an entire directory tree.
    ''' There is a set of functions to copy an entire directory tree and resume copying interrupted files.
    ''' There is a set of functions to get the size of an entire directory tree, including the number of files and directories.
    '''</remarks>
    Public Class clsFileTools

#Region "Events"

        ''' <summary>Event is raised before copying begins.</summary>
        ''' <param name="filename">The file's full path.</param>
        Public Event CopyingFile(filename As String)

        Public Event DebugEvent(currentTask As String, taskDetail As String)

        Public Event WarningEvent(warningMessage As String, warningDetail As String)

        ''' <summary>Event is raised before copying begins.</summary>
        ''' <param name="filename">The file's full path.</param>
        Public Event ResumingFileCopy(filename As String)

        ''' <summary>Event is raised before copying begins.</summary>
        ''' <param name="filename">The file name (not full path)</param>
        ''' <param name="percentComplete">Percent complete (value between 0 and 100)</param>
        Public Event FileCopyProgress(filename As String, percentComplete As Single)

        Public Event WaitingForLockQueue(sourceFilePath As String, targetFilePath As String, backlogSourceMB As Integer, backlogTargetMB As Integer)

        Public Event LockQueueTimedOut(sourceFilePath As String, targetFilePath As String, waitTimeMinutes As Double)

        Public Event LockQueueWaitComplete(sourceFilePath As String, targetFilePath As String, waitTimeMinutes As Double)

#End Region

#Region "Module constants and variables"
        'Private constants
        Private Const TERM_ADD As Boolean = True
        Private Const TERM_REMOVE As Boolean = False
        Private Const TERMCHAR_DOS As String = "\"
        Private Const TERMCHAR_UNIX As String = "/"
        Private Const COPY_OVERWRITE As Boolean = True
        Private Const COPY_NO_OVERWRITE As Boolean = False

        Private Const MAX_LOCKFILE_WAIT_TIME_MINUTES As Integer = 180

        Public Const LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB As Integer = 20
        Private Const LOCKFILE_TRANSFER_THRESHOLD_MB As Integer = 1000
        Private Const LOCKFILE_EXTENSION As String = ".lock"

        Private Const DEFAULT_VERSION_COUNT_TO_KEEP As Integer = 9

        Private mCopyStatus As CopyStatus = CopyStatus.Idle
        Private mCurrentSourceFilePath As String = String.Empty

        Private mChunkSizeMB As Integer = DEFAULT_CHUNK_SIZE_MB
        Private mFlushThresholdMB As Integer = DEFAULT_FLUSH_THRESHOLD_MB

        Private mDebugLevel As Integer = 1
        Private mManagerName As String = "Unknown-Manager"

        Private mLastGC As DateTime = DateTime.UtcNow

#End Region

#Region "Public constants"
        'Constants
        ''' <summary>Used to add the path separation character to the end of the directory path.</summary>
        Public Const TERMINATOR_ADD As Boolean = True
        ''' <summary>Used to remove the path separation character from the end of the directory path.</summary>
        Public Const TERMINATOR_REMOVE As Boolean = False

        ''' <summary>
        ''' Used by CopyFileWithResume and CopyDirectoryWithResume when copying a file byte-by-byte and supporting resuming the copy if interrupted
        ''' </summary>
        ''' <remarks></remarks>
        Public Const DEFAULT_CHUNK_SIZE_MB As Integer = 1

        ''' <summary>
        ''' Used by CopyFileWithResume; defines how often the data is flushed out to disk; must be larger than the ChunkSize
        ''' </summary>
        ''' <remarks></remarks>
        Public Const DEFAULT_FLUSH_THRESHOLD_MB As Integer = 25

#End Region

#Region "Enums"
        Public Enum FileOverwriteMode
            DoNotOverwrite = 0
            AlwaysOverwrite = 1
            OverwriteIfSourceNewer = 2                  ' Overwrite if source date newer (or if same date but length differs)
            OverWriteIfDateOrLengthDiffer = 3           ' Overwrite if any difference in size or date; note that newer files in target folder will get overwritten since their date doesn't match
        End Enum

        Public Enum CopyStatus
            Idle = 0                    ' Not copying a file
            NormalCopy = 1              ' File is geing copied via .NET and cannot be resumed
            BufferedCopy = 2            ' File is being copied in chunks and can be resumed
            BufferedCopyResume = 3      ' Resuming copying a file in chunks
        End Enum
#End Region

#Region "Properties"

        Public Property CopyChunkSizeMB As Integer
            Get
                Return mChunkSizeMB
            End Get
            Set(value As Integer)
                If value < 1 Then value = 1
                mChunkSizeMB = value
            End Set
        End Property

        Public Property CopyFlushThresholdMB As Integer
            Get
                Return mFlushThresholdMB
            End Get
            Set(value As Integer)
                If value < 1 Then value = 1
                mFlushThresholdMB = value
            End Set
        End Property

        Public ReadOnly Property CurrentCopyStatus As CopyStatus
            Get
                Return mCopyStatus
            End Get
        End Property

        Public ReadOnly Property CurrentSourceFile As String
            Get
                Return mCurrentSourceFilePath
            End Get
        End Property

        Public Property DebugLevel As Integer
            Get
                Return mDebugLevel
            End Get
            Set(value As Integer)
                mDebugLevel = value
            End Set
        End Property

        Public Property ManagerName As String
            Get
                Return mManagerName
            End Get
            Set(value As String)
                mManagerName = value
            End Set
        End Property

#End Region

#Region "Constructor"
        ''' <summary>
        ''' Constructor
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub New()
            mManagerName = "Unknown-Manager"
            mDebugLevel = 1
        End Sub

        Public Sub New(strManagerName As String, intDebugLevel As Integer)
            mManagerName = strManagerName
            mDebugLevel = intDebugLevel
        End Sub
#End Region

#Region "CheckTerminator function"
        'Functions
        ''' <summary>Modifies input directory path string depending on optional settings.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory separation character.</param>
        ''' <param name="TermChar">The specified directory separation character.</param>
        ''' <returns>The modified directory path.</returns>
        Public Shared Function CheckTerminator(InpFolder As String, AddTerm As Boolean, TermChar As String) As String

            'Overload for all parameters specified
            Return CheckTerminatorEX(InpFolder, AddTerm, TermChar)

        End Function

        ''' <summary>Adds or removes the DOS path separation character from the end of the directory path.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory separation character.</param>
        ''' <returns>The modified directory path.</returns>
        Public Shared Function CheckTerminator(InpFolder As String, AddTerm As Boolean) As String

            'Overload for using default termination character (DOS)
            Return CheckTerminatorEX(InpFolder, AddTerm, TERMCHAR_DOS)

        End Function

        ''' <summary>Assures the directory path ends with the specified path separation character.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="TermChar">The specified directory separation character.</param>
        ''' <returns>The modified directory path.</returns>
        Public Shared Function CheckTerminator(InpFolder As String, TermChar As String) As String

            'Overload for using "add character" as default
            Return CheckTerminatorEX(InpFolder, TERM_ADD, TermChar)

        End Function

        ''' <summary>Assures the directory path ends with the DOS path separation character.</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <returns>The modified directory path.</returns>
        Public Shared Function CheckTerminator(InpFolder As String) As String

            'Overload for using all defaults (add DOS terminator char)
            Return CheckTerminatorEX(InpFolder, TERM_ADD, TERMCHAR_DOS)

        End Function

        ''' <summary>Modifies input directory path string depending on AddTerm</summary>
        ''' <param name="InpFolder">The input directory path.</param>
        ''' <param name="AddTerm">Specifies whether the directory path should end with the specified directory separation character</param>
        ''' <param name="TermChar">The specified directory separation character.</param>
        ''' <returns>The modified directory path.</returns>
        ''' <remarks>AddTerm=True forces the path to end with specified TermChar while AddTerm=False will remove TermChar from the end if present</remarks>
        Private Shared Function CheckTerminatorEX(InpFolder As String, AddTerm As Boolean, TermChar As String) As String

            If String.IsNullOrWhiteSpace(InpFolder) OrElse String.IsNullOrWhiteSpace(TermChar) Then
                Return InpFolder
            End If

            If AddTerm Then
                If InpFolder.EndsWith(TermChar) Then
                    Return InpFolder
                Else
                    Return InpFolder & TermChar
                End If
            Else
                If InpFolder.EndsWith(TermChar) Then
                    Return InpFolder.TrimEnd(TermChar.Chars(0))
                Else
                    Return InpFolder
                End If
            End If

        End Function
#End Region

#Region "CopyFile function"

        ''' <summary>Copies a source file to the destination file. Does not allow overwriting.</summary>
        ''' <param name="SourcePath">The source file path.</param>
        ''' <param name="DestPath">The destination file path.</param>
        Public Sub CopyFile(SourcePath As String, DestPath As String)

            'Overload with overwrite set to default (FALSE)
            Const BackupDestFileBeforeCopy = False
            CopyFileEx(SourcePath, DestPath, COPY_NO_OVERWRITE, BackupDestFileBeforeCopy)

        End Sub

        Public Sub CopyFile(SourcePath As String, DestPath As String, OverWrite As Boolean)
            Const BackupDestFileBeforeCopy = False
            CopyFile(SourcePath, DestPath, OverWrite, BackupDestFileBeforeCopy)
        End Sub

        Public Sub CopyFile(SourcePath As String, DestPath As String, OverWrite As Boolean, BackupDestFileBeforeCopy As Boolean)
            CopyFile(SourcePath, DestPath, OverWrite, BackupDestFileBeforeCopy, DEFAULT_VERSION_COUNT_TO_KEEP)
        End Sub

        ''' <summary>Copies a source file to the destination file. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source file path.</param>
        ''' <param name="DestPath">The destination file path.</param>
        ''' <param name="Overwrite">True if the destination file can be overwritten; otherwise, false.</param>
        Public Sub CopyFile(SourcePath As String, DestPath As String, OverWrite As Boolean, BackupDestFileBeforeCopy As Boolean, VersionCountToKeep As Integer)

            'Overload with no defaults
            CopyFileEx(SourcePath, DestPath, OverWrite, BackupDestFileBeforeCopy, VersionCountToKeep)

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
        ''' <param name="Overwrite">True if the destination file can be overwritten; otherwise, false.</param>
        Private Sub CopyFileEx(
          SourcePath As String,
          DestPath As String,
          Overwrite As Boolean,
          BackupDestFileBeforeCopy As Boolean,
          Optional VersionCountToKeep As Integer = DEFAULT_VERSION_COUNT_TO_KEEP)

            Dim dirPath As String = Path.GetDirectoryName(DestPath)
            If Not Directory.Exists(dirPath) Then
                Directory.CreateDirectory(dirPath)
            End If

            If BackupDestFileBeforeCopy Then
                BackupFileBeforeCopy(DestPath, VersionCountToKeep)
            End If

            If mDebugLevel >= 3 Then
                RaiseEvent DebugEvent("Copying file with CopyFileEx", SourcePath & " to " & DestPath)
            End If

            UpdateCurrentStatus(CopyStatus.NormalCopy, SourcePath)
            File.Copy(SourcePath, DestPath, Overwrite)
            UpdateCurrentStatusIdle()
        End Sub

#End Region

#Region "Lock File Copying functions"

        ''' <summary>
        ''' Copy the source file to the target path; do not overwrite existing files
        ''' </summary>
        ''' <param name="strSourceFilePath">Source file path</param>
        ''' <param name="strTargetFilePath">Target file path</param>
        ''' <param name="strManagerName">Manager name (included in the lock file name)</param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks>If the file exists, will not copy the file but will still return true</remarks>
        Public Function CopyFileUsingLocks(strSourceFilePath As String, strTargetFilePath As String, strManagerName As String) As Boolean
            Return CopyFileUsingLocks(New FileInfo(strSourceFilePath), strTargetFilePath, strManagerName, Overwrite:=False)
        End Function

        ''' <summary>
        ''' Copy the source file to the target path
        ''' </summary>
        ''' <param name="strSourceFilePath">Source file path</param>
        ''' <param name="strTargetFilePath">Target file path</param>
        ''' <param name="strManagerName">Manager name (included in the lock file name)</param>
        ''' <param name="Overwrite">True to overwrite existing files</param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks>If the file exists yet Overwrite is false, will not copy the file but will still return true</remarks>
        Public Function CopyFileUsingLocks(strSourceFilePath As String, strTargetFilePath As String, strManagerName As String, Overwrite As Boolean) As Boolean
            Return CopyFileUsingLocks(New FileInfo(strSourceFilePath), strTargetFilePath, strManagerName, Overwrite)
        End Function

        ''' <summary>
        ''' Copy the source file to the target path
        ''' </summary>
        ''' <param name="fiSource">Source file object</param>
        ''' <param name="strTargetFilePath">Target file path</param>
        ''' <param name="strManagerName">Manager name (included in the lock file name)</param>
        ''' <param name="Overwrite">True to overwrite existing files</param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks>If the file exists yet Overwrite is false, will not copy the file but will still return true</remarks>
        Public Function CopyFileUsingLocks(
          fiSource As FileInfo,
          strTargetFilePath As String,
          strManagerName As String,
          Overwrite As Boolean) As Boolean

            Dim blnUseLockFile = False
            Dim blnSuccess As Boolean

            If Not Overwrite AndAlso File.Exists(strTargetFilePath) Then
                Return True
            End If

            Dim fiTarget = New FileInfo(strTargetFilePath)

            Dim strLockFolderPathSource As String = GetLockFolder(fiSource)
            Dim strLockFolderPathTarget As String = GetLockFolder(fiTarget)

            If Not String.IsNullOrEmpty(strLockFolderPathSource) OrElse Not String.IsNullOrEmpty(strLockFolderPathTarget) Then
                blnUseLockFile = True
            End If

            If blnUseLockFile Then
                blnSuccess = CopyFileUsingLocks(strLockFolderPathSource, strLockFolderPathTarget, fiSource, strTargetFilePath, strManagerName, Overwrite)
            Else
                RaiseEvent WarningEvent("Lock file folder not found on the source or target", strLockFolderPathSource & " and " & strLockFolderPathTarget)
                Const BackupDestFileBeforeCopy = False
                CopyFileEx(fiSource.FullName, strTargetFilePath, Overwrite, BackupDestFileBeforeCopy)
                blnSuccess = True
            End If

            Return blnSuccess

        End Function

        ''' <summary>
        ''' Given a file path, return the lock file folder if it exsists
        ''' </summary>
        ''' <param name="fiFile"></param>
        ''' <returns></returns>
        Public Function GetLockFolder(fiFile As FileInfo) As String

            If Path.IsPathRooted(fiFile.FullName) Then
                If fiFile.Directory.Root.FullName.StartsWith("\\") Then
                    Dim strLockFolderPath = Path.Combine(GetServerShareBase(fiFile.Directory.Root.FullName), "DMS_LockFiles")
                    If Directory.Exists(strLockFolderPath) Then
                        Return strLockFolderPath
                    End If
                End If
            End If

            Return String.Empty

        End Function

        ''' <summary>
        ''' Copy the source file to the target path
        ''' </summary>
        ''' <param name="strLockFolderPathSource">Path to the lock folder for the source file; can be an empty string</param>
        ''' <param name="strLockFolderPathTarget">Path to the lock folder for the target file; can be an empty string</param>
        ''' <param name="fiSource">Source file object</param>
        ''' <param name="strTargetFilePath">Target file path</param>
        ''' <param name="strManagerName">Manager name (included in the lock file name)</param>
        ''' <param name="Overwrite">True to overwrite existing files</param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks>If the file exists yet Overwrite is false, will not copy the file but will still return true</remarks>
        Public Function CopyFileUsingLocks(
          strLockFolderPathSource As String,
          strLockFolderPathTarget As String,
          fiSource As FileInfo,
          strTargetFilePath As String,
          strManagerName As String,
          Overwrite As Boolean) As Boolean

            Dim intSourceFileSizeMB As Integer

            If Not Overwrite AndAlso File.Exists(strTargetFilePath) Then
                If mDebugLevel >= 2 Then
                    RaiseEvent DebugEvent("Skipping file since target exists", strTargetFilePath)
                End If
                Return True
            End If

            ' Examine the size of the source file
            ' If less than LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB then
            ' copy the file normally
            intSourceFileSizeMB = CInt(fiSource.Length / 1024.0 / 1024.0)
            If intSourceFileSizeMB < LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB OrElse
               (String.IsNullOrWhiteSpace(strLockFolderPathSource) AndAlso String.IsNullOrWhiteSpace(strLockFolderPathTarget)) Then
                Const BackupDestFileBeforeCopy = False
                CopyFileEx(fiSource.FullName, strTargetFilePath, Overwrite, BackupDestFileBeforeCopy)
                Return True
            End If


            Dim strLockFilePathSource As String = String.Empty
            Dim strLockFilePathTarget As String = String.Empty

            Try
                ' Create a new lock file on the source and/or target server
                ' This file indicates an intent to copy a file

                Dim diLockFolderSource As DirectoryInfo = Nothing
                Dim diLockFolderTarget As DirectoryInfo = Nothing
                Dim lockFileTimestamp As Int64 = GetLockFileTimeStamp()

                If Not String.IsNullOrWhiteSpace(strLockFolderPathSource) Then
                    diLockFolderSource = New DirectoryInfo(strLockFolderPathSource)
                    strLockFilePathSource = CreateLockFile(diLockFolderSource, lockFileTimestamp, fiSource, strTargetFilePath, strManagerName)
                End If

                If Not String.IsNullOrWhiteSpace(strLockFolderPathTarget) Then
                    diLockFolderTarget = New DirectoryInfo(strLockFolderPathTarget)
                    strLockFilePathTarget = CreateLockFile(diLockFolderTarget, lockFileTimestamp, fiSource, strTargetFilePath, strManagerName)
                End If

                WaitForLockFileQueue(
                  lockFileTimestamp,
                  diLockFolderSource,
                  diLockFolderTarget,
                  fiSource,
                  strTargetFilePath,
                  MAX_LOCKFILE_WAIT_TIME_MINUTES)

                If mDebugLevel >= 1 Then
                    RaiseEvent DebugEvent("Copying file using Locks", fiSource.FullName & " to " & strTargetFilePath)
                End If

                ' Perform the copy
                Const BackupDestFileBeforeCopy = False
                CopyFileEx(fiSource.FullName, strTargetFilePath, Overwrite, BackupDestFileBeforeCopy)

                ' Delete the lock file(s)
                DeleteFileIgnoreErrors(strLockFilePathSource)
                DeleteFileIgnoreErrors(strLockFilePathTarget)

            Catch ex As Exception
                ' Error occurred
                ' Delete the lock file then throw the exception
                DeleteFileIgnoreErrors(strLockFilePathSource)
                DeleteFileIgnoreErrors(strLockFilePathTarget)

                Throw
            End Try

            Return True

        End Function

        ''' <summary>
        ''' Create a lock file in the specified lock folder
        ''' </summary>
        ''' <param name="diLockFolder"></param>
        ''' <param name="fiSource"></param>
        ''' <param name="strTargetFilePath"></param>
        ''' <param name="strManagerName"></param>
        ''' <returns>Full path to the lock file; empty string if an error or if diLockFolder is null</returns>
        ''' <remarks></remarks>
        Public Function CreateLockFile(
          diLockFolder As DirectoryInfo,
          lockFileTimestamp As Int64,
          fiSource As FileInfo,
          strTargetFilePath As String,
          strManagerName As String) As String

            If diLockFolder Is Nothing Then
                Return String.Empty
            End If

            If String.IsNullOrWhiteSpace(strManagerName) Then
                strManagerName = "UnknownManager"
            End If

            ' Define the lock file name
            Dim strLockFileName = GenerateLockFileName(lockFileTimestamp, fiSource, strManagerName)
            Dim strLockFilePath = Path.Combine(diLockFolder.FullName, strLockFileName)
            Do While File.Exists(strLockFilePath)
                ' File already exists for this manager; append a dash to the path
                strLockFileName = Path.GetFileNameWithoutExtension(strLockFileName) & "-" & Path.GetExtension(strLockFileName)
                strLockFilePath = Path.Combine(diLockFolder.FullName, strLockFileName)
            Loop

            Try
                ' Create the lock file
                Using swLockFile = New StreamWriter(New FileStream(strLockFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    swLockFile.WriteLine("Date: " & DateTime.Now.ToString())
                    swLockFile.WriteLine("Source: " & fiSource.FullName)
                    swLockFile.WriteLine("Target: " & strTargetFilePath)
                    swLockFile.WriteLine("Size_Bytes: " & fiSource.Length)
                    swLockFile.WriteLine("Manager: " & strManagerName)
                End Using

                RaiseEvent DebugEvent("Created lock file in " + diLockFolder.FullName, strLockFilePath)

            Catch ex As Exception
                ' Error creating the lock file
                ' Return an empty string
                RaiseEvent WarningEvent("Error creating lock file in " + diLockFolder.FullName, ex.Message)
                Return String.Empty
            End Try

            Return strLockFilePath

        End Function

        ''' <summary>
        '''  Deletes the specified directory and all subdirectories
        ''' </summary>
        ''' <param name="strDirectoryPath"></param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks></remarks>
        Public Function DeleteDirectory(strDirectoryPath As String) As Boolean
            Return DeleteDirectory(strDirectoryPath, ignoreErrors:=False)
        End Function

        ''' <summary>
        '''  Deletes the specified directory and all subdirectories
        ''' </summary>
        ''' <param name="strDirectoryPath"></param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks></remarks>
        Public Function DeleteDirectory(strDirectoryPath As String, ignoreErrors As Boolean) As Boolean

            Dim diLocalDotDFolder = New DirectoryInfo(strDirectoryPath)

            Try
                diLocalDotDFolder.Delete(True)
            Catch ex As Exception
                ' Problems deleting one or more of the files
                If Not ignoreErrors Then Throw

                ' Collect garbage, then delete the files one-by-one
                Processes.clsProgRunner.GarbageCollectNow()

                Const blnDeleteFolderIfEmpty = True
                Return DeleteDirectoryFiles(strDirectoryPath, blnDeleteFolderIfEmpty)
            End Try

            Return True

        End Function

        ''' <summary>
        ''' Deletes the specified directory and all subdirectories; does not delete the target folder
        ''' </summary>
        ''' <param name="strDirectoryPath"></param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks>Deletes each file individually.  Deletion errors are reported but are not treated as a fatal error</remarks>
        Public Function DeleteDirectoryFiles(strDirectoryPath As String) As Boolean
            Const blnDeleteFolderIfEmpty = False
            Return DeleteDirectoryFiles(strDirectoryPath, blnDeleteFolderIfEmpty)
        End Function

        ''' <summary>
        ''' Deletes the specified directory and all subdirectories
        ''' </summary>
        ''' <param name="strDirectoryPath"></param>
        ''' <param name="blnDeleteFolderIfEmpty">Set to True to delete the folder, if it is empty</param>
        ''' <returns>True if success, false if an error</returns>
        ''' <remarks>Deletes each file individually.  Deletion errors are reported but are not treated as a fatal error</remarks>
        Public Function DeleteDirectoryFiles(strDirectoryPath As String, blnDeleteFolderIfEmpty As Boolean) As Boolean

            Dim diFolderToDelete = New DirectoryInfo(strDirectoryPath)
            Dim errorCount = 0

            For Each fiFile In diFolderToDelete.GetFiles("*", SearchOption.AllDirectories)
                If Not DeleteFileIgnoreErrors(fiFile.FullName) Then
                    errorCount += 1
                End If
            Next

            If errorCount = 0 AndAlso blnDeleteFolderIfEmpty Then
                Try
                    diFolderToDelete.Delete(True)
                Catch ex As Exception
                    RaiseEvent WarningEvent("Error removing empty directory", "Unable to delete directory " & diFolderToDelete.FullName & ": " & ex.Message)
                    errorCount += 1
                End Try
            End If

            If errorCount = 0 Then
                Return True
            Else
                Return False
            End If

        End Function

        ''' <summary>
        ''' Delete the specified file
        ''' </summary>
        ''' <param name="strFilePath"></param>
        ''' <returns>True if successfully deleted (or if the file doesn't exist); false if an error</returns>
        ''' <remarks>If the initial attempt fails, then checks the readonly bit and tries again.  If not readonly, then performs a garbage collection (every 500 msec)</remarks>
        Private Function DeleteFileIgnoreErrors(strFilePath As String) As Boolean

            If String.IsNullOrWhiteSpace(strFilePath) Then Return True

            Dim fiFile = New FileInfo(strFilePath)

            Try
                If (fiFile.Exists) Then
                    fiFile.Delete()
                End If
                Return True
            Catch ex As Exception
                ' Ignore errors here
            End Try

            Try
                ' The file might be readonly; check for this then re-try the delete
                If fiFile.IsReadOnly Then
                    fiFile.IsReadOnly = False
                Else
                    If DateTime.UtcNow.Subtract(mLastGC).TotalMilliseconds >= 500 Then
                        mLastGC = DateTime.UtcNow
                        Processes.clsProgRunner.GarbageCollectNow()
                    End If
                End If

                fiFile.Delete()

            Catch ex As Exception
                ' Ignore errors here
                RaiseEvent WarningEvent("Error deleting file", "Unable to delete file " & fiFile.FullName & ": " & ex.Message)

                Return False
            End Try

            Return True

        End Function

        ''' <summary>
        ''' Finds lock files with a timestamp less than
        ''' </summary>
        ''' <param name="diLockFolder"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function FindLockFiles(diLockFolder As DirectoryInfo, lockFileTimestamp As Int64) As Generic.List(Of Integer)
            Static reParseLockFileName As Regex = New Regex("^(\d+)_(\d+)_", RegexOptions.Compiled)

            Dim reMatch As Match
            Dim intQueueTimeMSec As Int64
            Dim intFileSizeMB As Int32

            Dim lstLockFiles As Generic.List(Of Integer)
            lstLockFiles = New Generic.List(Of Integer)

            If diLockFolder Is Nothing Then
                Return lstLockFiles
            End If

            diLockFolder.Refresh()

            For Each fiLockFile As FileInfo In diLockFolder.GetFiles("*" & LOCKFILE_EXTENSION)
                reMatch = reParseLockFileName.Match(fiLockFile.Name)

                If reMatch.Success Then
                    If Int64.TryParse(reMatch.Groups(1).Value, intQueueTimeMSec) Then
                        If Int32.TryParse(reMatch.Groups(2).Value, intFileSizeMB) Then

                            If intQueueTimeMSec < lockFileTimestamp Then
                                ' Lock file fiLockFile was created prior to the current one
                                ' Make sure it's less than 1 hour old
                                If Math.Abs((lockFileTimestamp - intQueueTimeMSec) / 1000.0 / 60.0) < MAX_LOCKFILE_WAIT_TIME_MINUTES Then
                                    lstLockFiles.Add(intFileSizeMB)
                                End If
                            End If
                        End If
                    End If

                End If
            Next

            Return lstLockFiles

        End Function

        ''' <summary>
        ''' Generate the lock file name, which starts with a msec-based timestamp, then has the source file size (in MB), then has information on the machine creating the file
        ''' </summary>
        ''' <param name="fiSource"></param>
        ''' <param name="strManagerName"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function GenerateLockFileName(lockFileTimestamp As Int64, fiSource As FileInfo, strManagerName As String) As String
            Static reInvalidDosChars As Regex = New Regex("[\\/:*?""<>| ]", RegexOptions.Compiled)

            If String.IsNullOrWhiteSpace(strManagerName) Then
                strManagerName = "UnknownManager"
            End If

            Dim strLockFileName As String
            strLockFileName = lockFileTimestamp.ToString() & "_" &
              (fiSource.Length / 1024.0 / 1024.0).ToString("0000") & "_" & Environment.MachineName & "_" &
              strManagerName & LOCKFILE_EXTENSION

            ' Replace any invalid characters (including spaces) with an underscore
            Return reInvalidDosChars.Replace(strLockFileName, "_")

        End Function

        Public Function GetLockFileTimeStamp() As Int64
            Return CType(Math.Round(DateTime.UtcNow.Subtract(New DateTime(2010, 1, 1)).TotalMilliseconds, 0), Int64)
        End Function

        ''' <summary>
        ''' Returns the first portion of a network share path, for example \\MyServer is returned for \\MyServer\Share\Filename.txt
        ''' </summary>
        ''' <param name="strServerSharePath"></param>
        ''' <returns></returns>
        ''' <remarks>Treats \\picfs as a special share since DMS-related files are at \\picfs\projects\DMS</remarks>
        Public Function GetServerShareBase(strServerSharePath As String) As String
            If strServerSharePath.StartsWith("\\") Then
                Dim intSlashIndex As Integer
                intSlashIndex = strServerSharePath.IndexOf("\"c, 2)
                If intSlashIndex > 0 Then
                    Dim strServerShareBase = strServerSharePath.Substring(0, intSlashIndex)
                    If strServerShareBase.ToLower() = "\\picfs" Then
                        strServerShareBase = "\\picfs\projects\DMS"
                    End If
                    Return strServerShareBase
                Else
                    Return strServerSharePath
                End If
            Else
                Return String.Empty
            End If
        End Function
#End Region

#Region "CopyDirectory function"

        ''' <summary>Copies a source directory to the destination directory. Does not allow overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        Public Sub CopyDirectory(SourcePath As String, DestPath As String)

            'Overload with overwrite set to default=FALSE
            CopyDirectory(SourcePath, DestPath, COPY_NO_OVERWRITE)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Does not allow overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          strManagerName As String)

            'Overload with overwrite set to default=FALSE
            CopyDirectory(SourcePath, DestPath, COPY_NO_OVERWRITE, strManagerName)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Does not allow overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          FileNamesToSkip As Generic.List(Of String))

            'Overload with overwrite set to default=FALSE
            CopyDirectory(SourcePath, DestPath, COPY_NO_OVERWRITE, FileNamesToSkip)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          OverWrite As Boolean)

            'Overload with no defaults
            Const bReadOnly = False
            CopyDirectory(SourcePath, DestPath, OverWrite, bReadOnly)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          OverWrite As Boolean,
          strManagerName As String)

            'Overload with no defaults
            Const bReadOnly = False
            CopyDirectory(SourcePath, DestPath, OverWrite, bReadOnly, New Generic.List(Of String), strManagerName)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          OverWrite As Boolean,
          FileNamesToSkip As Generic.List(Of String))

            'Overload with no defaults
            Dim bReadOnly = False
            CopyDirectory(SourcePath, DestPath, OverWrite, bReadOnly, FileNamesToSkip)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        ''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          OverWrite As Boolean,
          bReadOnly As Boolean)

            'Overload with no defaults
            Const SetAttribute = True
            CopyDirectoryEx(SourcePath, DestPath, OverWrite, SetAttribute, bReadOnly, New Generic.List(Of String), mManagerName)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        ''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          OverWrite As Boolean,
          bReadOnly As Boolean,
          FileNamesToSkip As Generic.List(Of String))

            'Overload with no defaults
            Const SetAttribute = True
            CopyDirectoryEx(SourcePath, DestPath, OverWrite, SetAttribute, bReadOnly, FileNamesToSkip, mManagerName)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        ''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        Public Sub CopyDirectory(SourcePath As String,
          DestPath As String,
          OverWrite As Boolean,
          bReadOnly As Boolean,
          FileNamesToSkip As Generic.List(Of String),
          strManagerName As String)

            'Overload with no defaults
            Const SetAttribute = True
            CopyDirectoryEx(SourcePath, DestPath, OverWrite, SetAttribute, bReadOnly, FileNamesToSkip, strManagerName)

        End Sub

        ''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
        ''' <remarks>Usage: CopyDirectory("C:\Misc", "D:\MiscBackup")
        ''' Original code obtained from vb2themax.com
        ''' </remarks>
        ''' <param name="SourcePath">The source directory path.</param>
        ''' <param name="DestPath">The destination directory path.</param>
        ''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
        ''' <param name="SetAttribute">true if the read-only attribute of the destination file is to be modified, false otherwise.</param>
        ''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        ''' <param name="strManagerName">Name of the calling program; used when calling CopyFileUsingLocks</param>
        Private Sub CopyDirectoryEx(SourcePath As String,
            DestPath As String,
            Overwrite As Boolean,
            SetAttribute As Boolean,
            bReadOnly As Boolean,
            ByRef FileNamesToSkip As Generic.List(Of String),
            strManagerName As String)

            Dim SourceDir = New DirectoryInfo(SourcePath)
            Dim DestDir = New DirectoryInfo(DestPath)

            Dim dctFileNamesToSkip As Generic.Dictionary(Of String, String)

            Dim blnCopyFile As Boolean

            ' the source directory must exist, otherwise throw an exception
            If SourceDir.Exists Then
                ' if destination SubDir's parent SubDir does not exist throw an exception
                If Not DestDir.Parent.Exists Then
                    Throw New DirectoryNotFoundException("Destination directory does not exist: " + DestDir.Parent.FullName)
                End If

                If Not DestDir.Exists Then
                    DestDir.Create()
                End If

                ' Populate dctFileNamesToSkip
                dctFileNamesToSkip = New Generic.Dictionary(Of String, String)(StringComparer.CurrentCultureIgnoreCase)
                If Not FileNamesToSkip Is Nothing Then
                    For Each strItem As String In FileNamesToSkip
                        dctFileNamesToSkip.Add(strItem, "")
                    Next
                End If

                ' Copy all the files of the current directory
                Dim ChildFile As FileInfo
                Dim sTargetFilePath As String

                For Each ChildFile In SourceDir.GetFiles()

                    ' Look for both the file name and the full path in dctFileNamesToSkip
                    ' If either matches, then to not copy the file
                    If dctFileNamesToSkip.ContainsKey(ChildFile.Name) Then
                        blnCopyFile = False
                    ElseIf dctFileNamesToSkip.ContainsKey(ChildFile.FullName) Then
                        blnCopyFile = False
                    Else
                        blnCopyFile = True
                    End If

                    If blnCopyFile Then

                        sTargetFilePath = Path.Combine(DestDir.FullName, ChildFile.Name)

                        If Overwrite Then
                            UpdateCurrentStatus(CopyStatus.NormalCopy, ChildFile.FullName)
                            CopyFileUsingLocks(ChildFile, sTargetFilePath, strManagerName, Overwrite:=True)
                        Else
                            ' If Overwrite = false, copy the file only if it does not exist
                            ' this is done to avoid an IOException if a file already exists
                            ' this way the other files can be copied anyway...
                            If Not File.Exists(sTargetFilePath) Then
                                UpdateCurrentStatus(CopyStatus.NormalCopy, ChildFile.FullName)
                                CopyFileUsingLocks(ChildFile, sTargetFilePath, strManagerName, Overwrite:=False)
                            End If
                        End If

                        If SetAttribute Then
                            UpdateReadonlyAttribute(ChildFile, sTargetFilePath, bReadOnly)
                        End If

                        UpdateCurrentStatusIdle()
                    End If
                Next

                ' copy all the sub-directories by recursively calling this same routine
                For Each SubDir As DirectoryInfo In SourceDir.GetDirectories()
                    CopyDirectoryEx(SubDir.FullName, Path.Combine(DestDir.FullName, SubDir.Name),
                       Overwrite, SetAttribute, bReadOnly, FileNamesToSkip, strManagerName)
                Next
            Else
                Throw New DirectoryNotFoundException("Source directory does not exist: " + SourceDir.FullName)
            End If

        End Sub

        ''' <summary>
        ''' Copies the file attributes from a source file to a target file, explicitly updating the read-only bit based on bReadOnly
        ''' </summary>
        ''' <param name="fiSourceFile">Source FileInfo</param>
        ''' <param name="sTargetFilePath">Target file path</param>
        ''' <param name="bReadOnly">True to force the ReadOnly bit on, False to force it off</param>
        ''' <remarks></remarks>
        Protected Sub UpdateReadonlyAttribute(fiSourceFile As FileInfo, sTargetFilePath As String, bReadOnly As Boolean)

            ' Get the file attributes from the source file
            Dim fa As FileAttributes = fiSourceFile.Attributes()
            Dim faNew As FileAttributes

            ' Change the read-only attribute to the desired value
            If bReadOnly Then
                faNew = fa Or FileAttributes.ReadOnly
            Else
                faNew = fa And Not FileAttributes.ReadOnly
            End If

            If fa <> faNew Then
                ' Set the attributes of the destination file
                File.SetAttributes(sTargetFilePath, fa)
            End If

        End Sub
#End Region

#Region "CopyDirectoryWithResume function"

        ''' <summary>
        ''' Copies a source directory to the destination directory.
        ''' Overwrites existing files if they differ in modification time or size.
        ''' Copies large files in chunks and allows resuming copying a large file if interrupted.
        ''' </summary>
        ''' <param name="SourceFolderPath">The source directory path.</param>
        ''' <param name="TargetFolderPath">The destination directory path.</param>
        ''' <returns>True if success; false if an error</returns>
        ''' <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        Public Function CopyDirectoryWithResume(SourceFolderPath As String, TargetFolderPath As String) As Boolean

            Const Recurse = False
            Const eFileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer
            Dim FileNamesToSkip As New Generic.List(Of String)

            Return CopyDirectoryWithResume(SourceFolderPath, TargetFolderPath, Recurse, eFileOverwriteMode, FileNamesToSkip)
        End Function

        ''' <summary>
        ''' Copies a source directory to the destination directory.
        ''' Overwrites existing files if they differ in modification time or size.
        ''' Copies large files in chunks and allows resuming copying a large file if interrupted.
        ''' </summary>
        ''' <param name="SourceFolderPath">The source directory path.</param>
        ''' <param name="TargetFolderPath">The destination directory path.</param>
        ''' <param name="Recurse">True to copy subdirectories</param>
        ''' <returns>True if success; false if an error</returns>
        ''' <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        Public Function CopyDirectoryWithResume(SourceFolderPath As String, TargetFolderPath As String, Recurse As Boolean) As Boolean

            Const eFileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer
            Dim FileNamesToSkip As New Generic.List(Of String)

            Return CopyDirectoryWithResume(SourceFolderPath, TargetFolderPath, Recurse, eFileOverwriteMode, FileNamesToSkip)
        End Function

        ''' <summary>
        ''' Copies a source directory to the destination directory. 
        ''' Overwrite behavior is governed by eFileOverwriteMode
        ''' Copies large files in chunks and allows resuming copying a large file if interrupted.
        ''' </summary>
        ''' <param name="SourceFolderPath">The source directory path.</param>
        ''' <param name="TargetFolderPath">The destination directory path.</param>
        ''' <param name="Recurse">True to copy subdirectories</param>
        ''' <param name="eFileOverwriteMode">Behavior when a file already exists at the destination</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        ''' <returns>True if success; false if an error</returns>
        ''' <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        Public Function CopyDirectoryWithResume(SourceFolderPath As String,
          TargetFolderPath As String,
          Recurse As Boolean,
          eFileOverwriteMode As FileOverwriteMode,
          FileNamesToSkip As Generic.List(Of String)) As Boolean

            Dim FileCountSkipped = 0
            Dim FileCountResumed = 0
            Dim FileCountNewlyCopied = 0
            Const SetAttribute = False
            Const bReadOnly = False

            Return CopyDirectoryWithResume(SourceFolderPath, TargetFolderPath, Recurse, eFileOverwriteMode, SetAttribute, bReadOnly, FileNamesToSkip, FileCountSkipped, FileCountResumed, FileCountNewlyCopied)

        End Function

        ''' <summary>
        ''' Copies a source directory to the destination directory. 
        ''' Overwrite behavior is governed by eFileOverwriteMode
        ''' Copies large files in chunks and allows resuming copying a large file if interrupted.
        ''' </summary>
        ''' <param name="SourceFolderPath">The source directory path.</param>
        ''' <param name="TargetFolderPath">The destination directory path.</param>
        ''' <param name="Recurse">True to copy subdirectories</param>
        ''' <param name="eFileOverwriteMode">Behavior when a file already exists at the destination</param>
        ''' <param name="FileCountSkipped">Number of files skipped (output)</param>
        ''' <param name="FileCountResumed">Number of files resumed (output)</param>
        ''' <param name="FileCountNewlyCopied">Number of files newly copied (output)</param>
        ''' <returns>True if success; false if an error</returns>
        ''' <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        Public Function CopyDirectoryWithResume(SourceFolderPath As String,
          TargetFolderPath As String,
          Recurse As Boolean,
          eFileOverwriteMode As FileOverwriteMode,
          <Out> ByRef FileCountSkipped As Integer,
          <Out> ByRef FileCountResumed As Integer,
          <Out> ByRef FileCountNewlyCopied As Integer) As Boolean

            Const SetAttribute = False
            Const bReadOnly = False
            Dim FileNamesToSkip As New Generic.List(Of String)

            Return CopyDirectoryWithResume(SourceFolderPath, TargetFolderPath, Recurse, eFileOverwriteMode, SetAttribute, bReadOnly, FileNamesToSkip, FileCountSkipped, FileCountResumed, FileCountNewlyCopied)

        End Function

        ''' <summary>
        ''' Copies a source directory to the destination directory. 
        ''' Overwrite behavior is governed by eFileOverwriteMode
        ''' Copies large files in chunks and allows resuming copying a large file if interrupted.
        ''' </summary>
        ''' <param name="SourceFolderPath">The source directory path.</param>
        ''' <param name="TargetFolderPath">The destination directory path.</param>
        ''' <param name="Recurse">True to copy subdirectories</param>
        ''' <param name="eFileOverwriteMode">Behavior when a file already exists at the destination</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        ''' <param name="FileCountSkipped">Number of files skipped (output)</param>
        ''' <param name="FileCountResumed">Number of files resumed (output)</param>
        ''' <param name="FileCountNewlyCopied">Number of files newly copied (output)</param>
        ''' <returns>True if success; false if an error</returns>
        ''' <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        Public Function CopyDirectoryWithResume(SourceFolderPath As String,
          TargetFolderPath As String,
          Recurse As Boolean,
          eFileOverwriteMode As FileOverwriteMode,
          FileNamesToSkip As Generic.List(Of String),
          <Out> ByRef FileCountSkipped As Integer,
          <Out> ByRef FileCountResumed As Integer,
          <Out> ByRef FileCountNewlyCopied As Integer) As Boolean

            Const SetAttribute = False
            Const bReadOnly = False

            Return CopyDirectoryWithResume(SourceFolderPath, TargetFolderPath, Recurse, eFileOverwriteMode, SetAttribute, bReadOnly, FileNamesToSkip, FileCountSkipped, FileCountResumed, FileCountNewlyCopied)

        End Function

        ''' <summary>
        ''' Copies a source directory to the destination directory. 
        ''' Overwrite behavior is governed by eFileOverwriteMode
        ''' Copies large files in chunks and allows resuming copying a large file if interrupted.
        ''' </summary>
        ''' <param name="SourceFolderPath">The source directory path.</param>
        ''' <param name="TargetFolderPath">The destination directory path.</param>
        ''' <param name="Recurse">True to copy subdirectories</param>
        ''' <param name="eFileOverwriteMode">Behavior when a file already exists at the destination</param>
        ''' <param name="SetAttribute">True if the read-only attribute of the destination file is to be modified, false otherwise.</param>
        ''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
        ''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
        ''' <param name="FileCountSkipped">Number of files skipped (output)</param>
        ''' <param name="FileCountResumed">Number of files resumed (output)</param>
        ''' <param name="FileCountNewlyCopied">Number of files newly copied (output)</param>
        ''' <returns>True if success; false if an error</returns>
        ''' <remarks>Usage: CopyDirectoryWithResume("C:\Misc", "D:\MiscBackup")</remarks>
        Public Function CopyDirectoryWithResume(SourceFolderPath As String,
          TargetFolderPath As String,
          Recurse As Boolean,
          eFileOverwriteMode As FileOverwriteMode,
          SetAttribute As Boolean,
          bReadOnly As Boolean,
          FileNamesToSkip As Generic.List(Of String),
          <Out> ByRef FileCountSkipped As Integer,
          <Out> ByRef FileCountResumed As Integer,
          <Out> ByRef FileCountNewlyCopied As Integer) As Boolean

            Dim diSourceFolder As DirectoryInfo
            Dim diTargetFolder As DirectoryInfo
            Dim dctFileNamesToSkip As Generic.Dictionary(Of String, String)

            Dim blnCopyFile As Boolean
            Dim bSuccess = True

            FileCountSkipped = 0
            FileCountResumed = 0
            FileCountNewlyCopied = 0

            diSourceFolder = New DirectoryInfo(SourceFolderPath)
            diTargetFolder = New DirectoryInfo(TargetFolderPath)

            ' The source directory must exist, otherwise throw an exception
            If Not diSourceFolder.Exists Then
                Throw New DirectoryNotFoundException("Source directory does not exist: " + diSourceFolder.FullName)
            End If

            ' If destination SubDir's parent directory does not exist throw an exception
            If Not diTargetFolder.Parent.Exists Then
                Throw New DirectoryNotFoundException("Destination directory does not exist: " + diTargetFolder.Parent.FullName)
            End If

            If diSourceFolder.FullName = diTargetFolder.FullName Then
                Throw New IOException("Source and target directories cannot be the same: " + diTargetFolder.FullName)
            End If


            Try
                ' Create the target folder if necessary
                If Not diTargetFolder.Exists Then
                    diTargetFolder.Create()
                End If

                ' Populate objFileNamesToSkipCaseInsensitive
                dctFileNamesToSkip = New Generic.Dictionary(Of String, String)(StringComparer.CurrentCultureIgnoreCase)
                If Not FileNamesToSkip Is Nothing Then
                    ' Copy the values from FileNamesToSkip to dctFileNamesToSkip so that we can perform case-insensitive searching
                    For Each strItem As String In FileNamesToSkip
                        dctFileNamesToSkip.Add(strItem, String.Empty)
                    Next
                End If

                ' Copy all the files of the current directory
                For Each fiSourceFile As FileInfo In diSourceFolder.GetFiles()

                    ' Look for both the file name and the full path in dctFileNamesToSkip
                    ' If either matches, then do not copy the file
                    If dctFileNamesToSkip.ContainsKey(fiSourceFile.Name) Then
                        blnCopyFile = False
                    ElseIf dctFileNamesToSkip.ContainsKey(fiSourceFile.FullName) Then
                        blnCopyFile = False
                    Else
                        blnCopyFile = True
                    End If

                    If blnCopyFile Then
                        ' Does file already exist?
                        Dim fiExistingFile As FileInfo
                        fiExistingFile = New FileInfo(Path.Combine(diTargetFolder.FullName, fiSourceFile.Name))

                        If fiExistingFile.Exists Then
                            Select Case eFileOverwriteMode
                                Case FileOverwriteMode.AlwaysOverwrite
                                    blnCopyFile = True

                                Case FileOverwriteMode.DoNotOverwrite
                                    blnCopyFile = False

                                Case FileOverwriteMode.OverwriteIfSourceNewer
                                    If fiSourceFile.LastWriteTimeUtc < fiExistingFile.LastWriteTimeUtc OrElse
                                      (NearlyEqualFileTimes(fiSourceFile.LastWriteTimeUtc, fiExistingFile.LastWriteTimeUtc) AndAlso fiExistingFile.Length = fiSourceFile.Length) Then
                                        blnCopyFile = False
                                    End If

                                Case FileOverwriteMode.OverWriteIfDateOrLengthDiffer
                                    ' File exists; if size and last modified time are the same then don't copy

                                    If NearlyEqualFileTimes(fiSourceFile.LastWriteTimeUtc, fiExistingFile.LastWriteTimeUtc) AndAlso fiExistingFile.Length = fiSourceFile.Length Then
                                        blnCopyFile = False
                                    End If

                                Case Else
                                    ' Unknown mode; assume DoNotOverwrite
                                    blnCopyFile = False
                            End Select

                        End If
                    End If

                    If Not blnCopyFile Then
                        FileCountSkipped += 1
                    Else

                        Dim blnResumed = False
                        Try
                            Dim strTargetFilePath As String = Path.Combine(diTargetFolder.FullName, fiSourceFile.Name)
                            bSuccess = CopyFileWithResume(fiSourceFile, strTargetFilePath, blnResumed)
                        Catch ex As Exception
                            Throw
                        End Try

                        If Not bSuccess Then Exit For

                        If blnResumed Then
                            FileCountResumed += 1
                        Else
                            FileCountNewlyCopied += 1
                        End If

                        If SetAttribute Then
                            Dim sTargetFilePath As String = Path.Combine(diTargetFolder.FullName, fiSourceFile.Name)
                            UpdateReadonlyAttribute(fiSourceFile, sTargetFilePath, bReadOnly)
                        End If

                    End If

                Next

                If bSuccess AndAlso Recurse Then
                    ' Process each subdirectory
                    For Each fiSourceFolder As DirectoryInfo In diSourceFolder.GetDirectories()
                        Dim strSubDirTargetFolderPath As String
                        strSubDirTargetFolderPath = Path.Combine(TargetFolderPath, fiSourceFolder.Name)
                        bSuccess = CopyDirectoryWithResume(fiSourceFolder.FullName, strSubDirTargetFolderPath,
                          Recurse, eFileOverwriteMode, SetAttribute, bReadOnly,
                          FileNamesToSkip, FileCountSkipped, FileCountResumed, FileCountNewlyCopied)
                    Next
                End If

            Catch ex As Exception
                Throw New IOException("Exception copying directory with resume: " + ex.Message, ex)
            End Try

            Return bSuccess

        End Function

        ''' <summary>
        ''' Copy a file using chunks, thus allowing for resuming
        ''' </summary>
        ''' <param name="SourceFilePath"></param>
        ''' <param name="strTargetFilePath"></param>
        ''' <param name="blnResumed"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function CopyFileWithResume(SourceFilePath As String, strTargetFilePath As String, <Out> ByRef blnResumed As Boolean) As Boolean
            Dim fiSourceFile As FileInfo

            fiSourceFile = New FileInfo(SourceFilePath)
            Return CopyFileWithResume(fiSourceFile, strTargetFilePath, blnResumed)

        End Function

        ''' <summary>
        ''' Copy fiSourceFile to diTargetFolder
        ''' Copies the file using chunks, thus allowing for resuming
        ''' </summary>
        ''' <param name="fiSourceFile"></param>
        ''' <param name="strTargetFilePath"></param>
        ''' <param name="blnResumed">Output parameter; true if copying was resumed</param>
        ''' <returns>True if success; false if an error</returns>
        ''' <remarks></remarks>
        Public Function CopyFileWithResume(
          fiSourceFile As FileInfo,
          strTargetFilePath As String,
          <Out> ByRef blnResumed As Boolean) As Boolean

            Const FILE_PART_TAG = ".#FilePart#"
            Const FILE_PART_INFO_TAG = ".#FilePartInfo#"

            Dim intChunkSizeBytes As Integer
            Dim intFlushThresholdBytes As Integer

            Dim lngFileOffsetStart As Int64 = 0
            Dim blnResumeCopy As Boolean
            Dim strSourceFileLastWriteTime As String

            Dim swFilePart As FileStream = Nothing

            Try
                If mChunkSizeMB < 1 Then mChunkSizeMB = 1
                intChunkSizeBytes = mChunkSizeMB * 1024 * 1024

                If mFlushThresholdMB < mChunkSizeMB Then
                    mFlushThresholdMB = mChunkSizeMB
                End If
                intFlushThresholdBytes = mFlushThresholdMB * 1024 * 1024

                blnResumeCopy = False

                If fiSourceFile.Length <= intChunkSizeBytes Then
                    ' Simply copy the file

                    UpdateCurrentStatus(CopyStatus.NormalCopy, fiSourceFile.FullName)
                    fiSourceFile.CopyTo(strTargetFilePath, True)

                    UpdateCurrentStatusIdle()
                    blnResumed = False
                    Return True

                End If

                ' Delete the target file if it already exists
                If File.Exists(strTargetFilePath) Then
                    File.Delete(strTargetFilePath)
                    Threading.Thread.Sleep(25)
                End If

                ' Check for a #FilePart# file
                Dim fiFilePart As FileInfo
                fiFilePart = New FileInfo(strTargetFilePath & FILE_PART_TAG)

                Dim fiFilePartInfo As FileInfo
                fiFilePartInfo = New FileInfo(strTargetFilePath & FILE_PART_INFO_TAG)

                Dim dtSourceFileLastWriteTimeUTC = fiSourceFile.LastWriteTimeUtc
                strSourceFileLastWriteTime = dtSourceFileLastWriteTimeUTC.ToString("yyyy-MM-dd hh:mm:ss.fff tt")

                If fiFilePart.Exists Then
                    ' Possibly resume copying
                    ' First inspect the FilePartInfo file

                    If fiFilePartInfo.Exists Then
                        ' Open the file and read the file length and file modification time
                        ' If they match fiSourceFile then set blnResumeCopy to true and update lngFileOffsetStart

                        Using srFilePartInfo = New StreamReader(New FileStream(fiFilePartInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))

                            Dim lstSourceLines = New Generic.List(Of String)

                            Do While Not srFilePartInfo.EndOfStream
                                lstSourceLines.Add(srFilePartInfo.ReadLine())
                            Loop

                            If lstSourceLines.Count >= 3 Then
                                ' The first line contains the source file path
                                ' The second contains the file length, in bytes
                                ' The third contains the file modification time (UTC)

                                If lstSourceLines(0) = fiSourceFile.FullName AndAlso
                                   lstSourceLines(1) = fiSourceFile.Length.ToString Then

                                    ' Name and size are the same
                                    ' See if the timestamps agree within 2 seconds (need to allow for this in case we're comparing NTFS and FAT32)

                                    Dim dtCachedLastWriteTimeUTC As DateTime
                                    If DateTime.TryParse(lstSourceLines(2), dtCachedLastWriteTimeUTC) Then
                                        If NearlyEqualFileTimes(dtSourceFileLastWriteTimeUTC, dtCachedLastWriteTimeUTC) Then

                                            ' Source file is unchanged; safe to resume

                                            lngFileOffsetStart = fiFilePart.Length
                                            blnResumeCopy = True

                                        End If
                                    End If

                                End If

                            End If
                        End Using

                    End If

                End If

                If blnResumeCopy Then
                    UpdateCurrentStatus(CopyStatus.BufferedCopyResume, fiSourceFile.FullName)
                    swFilePart = New FileStream(fiFilePart.FullName, FileMode.Append, FileAccess.Write, FileShare.Read)
                    blnResumed = True
                Else
                    UpdateCurrentStatus(CopyStatus.BufferedCopy, fiSourceFile.FullName)

                    ' Delete FilePart file in the target folder if it already exists
                    If fiFilePart.Exists Then
                        fiFilePart.Delete()
                        Threading.Thread.Sleep(25)
                    End If

                    ' Create the FILE_PART_INFO_TAG file
                    Using swFilePartInfo = New StreamWriter(New FileStream(fiFilePartInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Read))

                        ' The first line contains the source file path
                        ' The second contains the file length, in bytes
                        ' The third contains the file modification time (UTC)
                        swFilePartInfo.WriteLine(fiSourceFile.FullName)
                        swFilePartInfo.WriteLine(fiSourceFile.Length)
                        swFilePartInfo.WriteLine(strSourceFileLastWriteTime)
                    End Using

                    ' Open the FilePart file
                    swFilePart = New FileStream(fiFilePart.FullName, FileMode.Create, FileAccess.Write, FileShare.Read)
                    blnResumed = False
                End If

                ' Now copy the file, appending data to swFilePart
                ' Open the source and seek to lngFileOffsetStart if > 0
                Using srSourceFile = New FileStream(fiSourceFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)

                    If lngFileOffsetStart > 0 Then
                        srSourceFile.Seek(lngFileOffsetStart, SeekOrigin.Begin)
                    End If

                    Dim intBytesRead As Integer
                    Dim buffer() As Byte
                    Dim intBytesSinceLastFlush As Int64

                    Dim lngBytesWritten As Int64 = lngFileOffsetStart
                    Dim sngTotalBytes As Single = srSourceFile.Length

                    ReDim buffer(intChunkSizeBytes)
                    intBytesSinceLastFlush = 0

                    Do
                        ' Read data in 1MB chunks and append to swFilePart
                        intBytesRead = srSourceFile.Read(buffer, 0, intChunkSizeBytes)
                        swFilePart.Write(buffer, 0, intBytesRead)
                        lngBytesWritten += intBytesRead

                        ' Flush out the data periodically 
                        intBytesSinceLastFlush += intBytesRead
                        If intBytesSinceLastFlush >= intFlushThresholdBytes Then
                            swFilePart.Flush()
                            intBytesSinceLastFlush = 0

                            ' Value between 0 and 100
                            Dim sngProgress = lngBytesWritten / sngTotalBytes * 100
                            RaiseEvent FileCopyProgress(fiSourceFile.Name, sngProgress)
                        End If

                        If intBytesRead < intChunkSizeBytes Then
                            Exit Do
                        End If
                    Loop While intBytesRead > 0

                    RaiseEvent FileCopyProgress(fiSourceFile.Name, 100)

                End Using

                swFilePart.Close()

                UpdateCurrentStatusIdle()

                ' Copy is complete
                ' Update last write time UTC to match source UTC
                fiFilePart.Refresh()
                fiFilePart.LastWriteTimeUtc = dtSourceFileLastWriteTimeUTC

                ' Rename fiFilePart to strTargetFilePath
                fiFilePart.MoveTo(strTargetFilePath)

                ' Delete fiFilePartInfo
                fiFilePartInfo.Delete()

            Catch ex As Exception
                If Not swFilePart Is Nothing Then
                    swFilePart.Close()
                End If
                Processes.clsProgRunner.GarbageCollectNow()

                Throw New IOException("Exception copying file with resume: " & ex.Message, ex)
            End Try

            Return True

        End Function

        ''' <summary>
        ''' Compares two timestamps (typically the LastWriteTime for a file)
        ''' If they agree within 2 seconds, then returns True, otherwise false
        ''' </summary>
        ''' <param name="dtTime1">First file time</param>
        ''' <param name="dtTime2">Second file time</param>
        ''' <returns>True if the times agree within 2 seconds</returns>
        ''' <remarks></remarks>
        Protected Function NearlyEqualFileTimes(dtTime1 As DateTime, dtTime2 As DateTime) As Boolean
            If Math.Abs(dtTime1.Subtract(dtTime2).TotalSeconds) <= 2.05 Then
                Return True
            Else
                Return False
            End If
        End Function

        Protected Sub UpdateCurrentStatusIdle()
            UpdateCurrentStatus(CopyStatus.Idle, String.Empty)
        End Sub

        Protected Sub UpdateCurrentStatus(eStatus As CopyStatus, sSourceFilePath As String)
            mCopyStatus = eStatus

            If eStatus = CopyStatus.Idle Then
                mCurrentSourceFilePath = String.Empty
            Else
                mCurrentSourceFilePath = String.Copy(sSourceFilePath)

                If eStatus = CopyStatus.BufferedCopyResume Then
                    RaiseEvent ResumingFileCopy(sSourceFilePath)
                ElseIf eStatus = CopyStatus.NormalCopy Then
                    RaiseEvent CopyingFile(sSourceFilePath)
                Else
                    ' Unknown status; do not raise an event
                End If

            End If
        End Sub

#End Region

#Region "GetDirectorySize function"
        ''' <summary>Get the directory size.</summary>
        ''' <param name="DirPath">The path to the directory.</param>
        ''' <returns>The directory size.</returns>
        Public Function GetDirectorySize(DirPath As String) As Long

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
        Public Function GetDirectorySize(DirPath As String, ByRef FileCount As Long, ByRef SubDirCount As Long) As Long

            'Overload for returning directory size, file count and directory count for entire directory tree
            Return GetDirectorySizeEX(DirPath, FileCount, SubDirCount)

        End Function

        ''' <summary>Get the directory size, file count, and directory count for the entire directory tree.</summary>
        ''' <param name="DirPath">The path to the directory.</param>
        ''' <param name="FileCount">The number of files in the entire directory tree.</param>
        ''' <param name="SubDirCount">The number of directories in the entire directory tree.</param>
        ''' <returns>The directory size.</returns>
        Private Function GetDirectorySizeEX(DirPath As String, ByRef FileCount As Long, ByRef SubDirCount As Long) As Long

            ' Returns the size of the specified directory, number of files in the directory tree, and number of subdirectories
            ' - Note: requires Imports System.IO
            ' - Usage: Dim DirSize As Long = GetDirectorySize("D:\Projects")
            '
            ' Original code obtained from vb2themax.com
            Dim DirSize As Long
            Dim Dir = New DirectoryInfo(DirPath)

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

            Return DirSize

        End Function
#End Region

#Region "MoveDirectory Function"

        Public Function MoveDirectory(SourceFolderPath As String, TargetFolderPath As String, OverwriteFiles As Boolean) As Boolean
            Return MoveDirectory(SourceFolderPath, TargetFolderPath, OverwriteFiles, mManagerName)
        End Function

        Public Function MoveDirectory(SourceFolderPath As String, TargetFolderPath As String, OverwriteFiles As Boolean, strManagerName As String) As Boolean
            Dim diSourceFolder As DirectoryInfo
            Dim blnSuccess As Boolean

            diSourceFolder = New DirectoryInfo(SourceFolderPath)

            ' Recursively call this function for each subdirectory
            For Each fiFolder As DirectoryInfo In diSourceFolder.GetDirectories()
                blnSuccess = MoveDirectory(fiFolder.FullName, Path.Combine(TargetFolderPath, fiFolder.Name), OverwriteFiles, strManagerName)
                If Not blnSuccess Then
                    Throw New Exception("Error moving directory " & fiFolder.FullName & " to " & TargetFolderPath & "; MoveDirectory returned False")
                End If
            Next

            For Each fiFile As FileInfo In diSourceFolder.GetFiles()
                blnSuccess = CopyFileUsingLocks(fiFile.FullName, Path.Combine(TargetFolderPath, fiFile.Name), strManagerName, OverwriteFiles)
                If Not blnSuccess Then
                    Throw New Exception("Error copying file " & fiFile.FullName & " to " & TargetFolderPath & "; CopyFileUsingLocks returned False")
                Else
                    ' Delete the source file
                    DeleteFileIgnoreErrors(fiFile.FullName)
                End If
            Next

            diSourceFolder.Refresh()
            If diSourceFolder.GetFileSystemInfos("*", SearchOption.AllDirectories).Count = 0 Then
                ' This folder is now empty; delete it
                Try
                    diSourceFolder.Delete(True)
                Catch ex As Exception
                    ' Ignore errors here
                End Try
            End If

            Return True

        End Function
#End Region

#Region "Utility Functions"

        ''' <summary>
        ''' Renames strTargetFilePath to have _Old1 before the file extension
        ''' Also looks for and renames other backed up versions of the file (those with _Old2, _Old3, etc.)
        ''' Use this function to backup old versions of a file before copying a new version to a target folder
        ''' Keeps up to 9 old versions of a file
        ''' </summary>
        ''' <param name="strTargetFilePath">Full path to the file to backup</param>
        ''' <returns>True if the file was successfully renamed (also returns True if the target file does not exist)</returns>
        ''' <remarks></remarks>
        Public Shared Function BackupFileBeforeCopy(strTargetFilePath As String) As Boolean
            Return BackupFileBeforeCopy(strTargetFilePath, DEFAULT_VERSION_COUNT_TO_KEEP)
        End Function

        ''' <summary>
        ''' Renames strTargetFilePath to have _Old1 before the file extension
        ''' Also looks for and renames other backed up versions of the file (those with _Old2, _Old3, etc.)
        ''' Use this function to backup old versions of a file before copying a new version to a target folder
        ''' </summary>
        ''' <param name="strTargetFilePath">Full path to the file to backup</param>
        ''' <param name="VersionCountToKeep">Maximum backup copies of the file to keep</param>
        ''' <returns>True if the file was successfully renamed (also returns True if the target file does not exist)</returns>
        ''' <remarks></remarks>
        Public Shared Function BackupFileBeforeCopy(strTargetFilePath As String, VersionCountToKeep As Integer) As Boolean

            Dim fiTargetFile = New FileInfo(strTargetFilePath)

            If Not fiTargetFile.Exists Then
                ' Target file does not exist; nothing to backup
                Return True
            End If

            If VersionCountToKeep = 0 Then VersionCountToKeep = 2
            If VersionCountToKeep < 1 Then VersionCountToKeep = 1

            Dim strBaseName = Path.GetFileNameWithoutExtension(fiTargetFile.Name)
            Dim strExtension = Path.GetExtension(fiTargetFile.Name)
            If String.IsNullOrEmpty(strExtension) Then
                strExtension = ".bak"
            End If

            Dim strTargetFolderPath = fiTargetFile.Directory.FullName

            ' Backup any existing copies of strTargetFilePath
            For intRevision = VersionCountToKeep - 1 To 0 Step -1

                Dim strBaseNameCurrent = String.Copy(strBaseName)
                If intRevision > 0 Then
                    strBaseNameCurrent &= "_Old" & intRevision.ToString()
                End If
                strBaseNameCurrent &= strExtension

                Dim ioFileToRename = New FileInfo(Path.Combine(strTargetFolderPath, strBaseNameCurrent))
                Dim strNewFilePath = Path.Combine(strTargetFolderPath, strBaseName & "_Old" & (intRevision + 1).ToString() & strExtension)

                ' Confirm that strNewFilePath doesn't exist; delete it if it does
                If File.Exists(strNewFilePath) Then
                    File.Delete(strNewFilePath)
                End If

                ' Rename the current file to strNewFilePath
                If ioFileToRename.Exists Then
                    ioFileToRename.MoveTo(strNewFilePath)
                End If

            Next intRevision

            Return True

        End Function

        Public Shared Function CompactPathString(strPathToCompact As String, Optional intMaxLength As Integer = 40) As String
            ' Recursive function to shorten strPathToCompact to a maximum length of intMaxLength

            ' The following is example output
            ' Note that when drive letters or subdirectories are present, the a minimum length is imposed
            ' For "C:\My Documents\Readme.txt"
            '   Minimum string returned=  C:\M..\Rea..
            '   Length for 20 characters= C:\My D..\Readme.txt
            '   Length for 25 characters= C:\My Docume..\Readme.txt
            '
            ' For "C:\My Documents\Word\Business\Finances.doc"
            '   Minimum string returned=  C:\...\B..\Fin..
            '   Length for 20 characters= C:\...\B..\Finance..
            '   Length for 25 characters= C:\...\Bus..\Finances.doc
            '   Length for 32 characters= C:\...\W..\Business\Finances.doc
            '   Length for 40 characters= C:\My Docum..\Word\Business\Finances.doc

            Dim pathSepChars(1) As Char
            pathSepChars(0) = "\"c
            pathSepChars(1) = "/"c

            Dim pathSepCharPreferred = "\"c

            Dim strPath(4) As String        ' 0-based array
            Dim intPartCount As Integer

            Dim strLeadingChars As String
            Dim strShortenedPath As String

            Dim intCharIndex As Integer
            Dim intLoopCount, intFileNameIndex As Integer
            Dim intShortLength, intOverLength As Integer
            Dim intLeadingCharsLength As Integer
            Dim intMultiPathCorrection As Short

            If intMaxLength < 3 Then intMaxLength = 3

            For intPartCount = 0 To strPath.Length - 1
                strPath(intPartCount) = String.Empty
            Next intPartCount

            If String.IsNullOrWhiteSpace(strPathToCompact) Then
                Return String.Empty
            End If

            Dim intFirstPathSepChar = strPathToCompact.IndexOfAny(pathSepChars)
            If intFirstPathSepChar >= 0 Then
                pathSepCharPreferred = strPathToCompact.Chars(intFirstPathSepChar)
            End If

            strPathToCompact = strPathToCompact.Trim()
            If strPathToCompact.Length <= intMaxLength Then
                Return strPathToCompact
            End If

            intPartCount = 1
            strLeadingChars = String.Empty

            If strPathToCompact.StartsWith("\\") Then
                strLeadingChars = "\\"
                intCharIndex = strPathToCompact.IndexOfAny(pathSepChars, 2)

                If intCharIndex > 0 Then
                    strLeadingChars = "\\" & strPathToCompact.Substring(2, intCharIndex - 1)
                    strPath(0) = strPathToCompact.Substring(intCharIndex + 1)
                Else
                    strPath(0) = strPathToCompact.Substring(2)
                End If
            ElseIf strPathToCompact.StartsWith("\") OrElse strPathToCompact.StartsWith("/") Then
                strLeadingChars = strPathToCompact.Substring(0, 1)
                strPath(0) = strPathToCompact.Substring(1)
            ElseIf strPathToCompact.StartsWith(".\") OrElse strPathToCompact.StartsWith("./") Then
                strLeadingChars = strPathToCompact.Substring(0, 2)
                strPath(0) = strPathToCompact.Substring(2)
            ElseIf strPathToCompact.StartsWith("..\") OrElse strPathToCompact.Substring(1, 2) = ":\" OrElse
             strPathToCompact.StartsWith("../") OrElse strPathToCompact.Substring(1, 2) = ":/" Then
                strLeadingChars = strPathToCompact.Substring(0, 3)
                strPath(0) = strPathToCompact.Substring(3)
            Else
                strPath(0) = strPathToCompact
            End If

            ' Examine strPath(0) to see if there are 1, 2, or more subdirectories
            intLoopCount = 0
            Do
                intCharIndex = strPath(intPartCount - 1).IndexOfAny(pathSepChars)
                If intCharIndex >= 0 Then
                    strPath(intPartCount) = strPath(intPartCount - 1).Substring(intCharIndex + 1)
                    strPath(intPartCount - 1) = strPath(intPartCount - 1).Substring(0, intCharIndex + 1)
                    intPartCount += 1
                Else
                    Exit Do
                End If
                intLoopCount += 1
            Loop While intLoopCount < 3

            If intPartCount = 1 Then
                ' No \ or / found, we're forced to shorten the filename (though if a UNC, then can shorten part of the UNC)

                If strLeadingChars.StartsWith("\\") Then
                    intLeadingCharsLength = strLeadingChars.Length
                    If intLeadingCharsLength > 5 Then
                        ' Can shorten the server name as needed
                        intShortLength = intMaxLength - strPath(0).Length - 3
                        If intShortLength < intLeadingCharsLength Then
                            If intShortLength < 3 Then intShortLength = 3
                            strLeadingChars = strLeadingChars.Substring(0, intShortLength) & "..\"
                        End If

                    End If
                End If

                intShortLength = intMaxLength - strLeadingChars.Length - 2
                If intShortLength < 3 Then intShortLength = 3
                If intShortLength < strPath(0).Length - 2 Then
                    If intShortLength < 4 Then
                        strShortenedPath = strLeadingChars & strPath(0).Substring(0, intShortLength) & ".."
                    Else
                        ' Shorten by removing the middle portion of the filename
                        Dim leftLength = CInt(Math.Ceiling(intShortLength / 2))
                        Dim rightLength = intShortLength - leftLength
                        strShortenedPath = strLeadingChars & strPath(0).Substring(0, leftLength) & ".." & strPath(0).Substring(strPath(0).Length - rightLength)
                    End If
                Else
                    strShortenedPath = strLeadingChars & strPath(0)
                End If
            Else
                ' Found one (or more) subdirectories

                ' First check if strPath(1) = "...\" or ".../"
                If strPath(0) = "...\" OrElse strPath(0) = ".../" Then
                    intMultiPathCorrection = 4
                    strPath(0) = strPath(1)
                    strPath(1) = strPath(2)
                    strPath(2) = strPath(3)
                    strPath(3) = String.Empty
                    intPartCount = 3
                Else
                    intMultiPathCorrection = 0
                End If

                ' Shorten the first to as little as possible
                ' If not short enough, replace the first with ... and call this function again
                intShortLength = intMaxLength - strLeadingChars.Length - strPath(3).Length - strPath(2).Length - strPath(1).Length - 3 - intMultiPathCorrection
                If intShortLength < 1 And strPath(2).Length > 0 Then
                    ' Not short enough, but other subdirectories are present
                    ' Thus, can call this function recursively
                    strShortenedPath = strLeadingChars & "..." & pathSepCharPreferred & strPath(1) & strPath(2) & strPath(3)
                    strShortenedPath = CompactPathString(strShortenedPath, intMaxLength)
                Else
                    If strLeadingChars.StartsWith("\\") Then
                        intLeadingCharsLength = strLeadingChars.Length
                        If intLeadingCharsLength > 5 Then
                            ' Can shorten the server name as needed
                            intShortLength = intMaxLength - strPath(3).Length - strPath(2).Length - strPath(1).Length - 7 - intMultiPathCorrection
                            If intShortLength < intLeadingCharsLength - 3 Then
                                If intShortLength < 3 Then intShortLength = 3
                                strLeadingChars = strLeadingChars.Substring(0, intShortLength) & "..\"
                            End If

                            ' Recompute intShortLength
                            intShortLength = intMaxLength - strLeadingChars.Length - strPath(3).Length - strPath(2).Length - strPath(1).Length - 3 - intMultiPathCorrection
                        End If
                    End If

                    If intMultiPathCorrection > 0 Then
                        strLeadingChars = strLeadingChars & "..." & pathSepCharPreferred
                    End If

                    If intShortLength < 1 Then intShortLength = 1
                    strPath(0) = strPath(0).Substring(0, intShortLength) & ".." & pathSepCharPreferred
                    strShortenedPath = strLeadingChars & strPath(0) & strPath(1) & strPath(2) & strPath(3)

                    ' See if still too long
                    ' If it is, then will need to shorten the filename too
                    intOverLength = strShortenedPath.Length - intMaxLength
                    If intOverLength > 0 Then
                        ' Need to shorten filename too
                        ' Determine which index the filename is in
                        For intFileNameIndex = intPartCount - 1 To 0 Step -1
                            If strPath(intFileNameIndex).Length > 0 Then Exit For
                        Next intFileNameIndex

                        intShortLength = strPath(intFileNameIndex).Length - intOverLength - 2
                        If intShortLength < 4 Then
                            strPath(intFileNameIndex) = strPath(intFileNameIndex).Substring(0, 3) & ".."
                        Else
                            ' Shorten by removing the middle portion of the filename
                            Dim leftLength = CInt(Math.Ceiling(intShortLength / 2))
                            Dim rightLength = intShortLength - leftLength
                            strPath(intFileNameIndex) = strPath(intFileNameIndex).Substring(0, leftLength) & ".." & strPath(intFileNameIndex).Substring(strPath(intFileNameIndex).Length - rightLength)
                        End If

                        strShortenedPath = strLeadingChars & strPath(0) & strPath(1) & strPath(2) & strPath(3)
                    End If

                End If
            End If

            Return strShortenedPath
        End Function

        ''' <summary>
        ''' Delete the file, retrying up to 3 times
        ''' </summary>
        ''' <param name="fiFile">File to delete</param>
        ''' <param name="errorMessage">Output message: error message if unable to delete the file</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function DeleteFileWithRetry(fiFile As FileInfo, <Out> ByRef errorMessage As String) As Boolean
            Return DeleteFileWithRetry(fiFile, 3, errorMessage)
        End Function

        ''' <summary>
        ''' Delete the file, retrying up to retryCount times
        ''' </summary>
        ''' <param name="fiFile">File to delete</param>
        ''' <param name="retryCount">Maximum number of times to retry the deletion, waiting 500 msec, then 750 msec between deletion attempts</param>
        ''' <param name="errorMessage">Output message: error message if unable to delete the file</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function DeleteFileWithRetry(fiFile As FileInfo, retryCount As Integer, <Out> ByRef errorMessage As String) As Boolean

            Dim fileDeleted = False
            Dim sleepTimeMsec = 500

            Dim retriesRemaining = retryCount - 1
            If retriesRemaining < 0 Then retriesRemaining = 0

            While Not fileDeleted AndAlso retriesRemaining >= 0
                retriesRemaining -= 1

                Try
                    fiFile.Delete()
                    fileDeleted = True
                Catch ex As Exception
                    If IsVimSwapFile(fiFile.Name) Then
                        ' Ignore this error
                        errorMessage = String.Empty
                        Return True
                    End If

                    ' Make sure the readonly bit is not set
                    If (fiFile.IsReadOnly) Then
                        Dim attributes = fiFile.Attributes
                        fiFile.Attributes = attributes And (Not FileAttributes.ReadOnly)

                        Try
                            ' Retry the delete
                            fiFile.Delete()
                            fileDeleted = True
                        Catch ex2 As Exception
                            errorMessage = "Error deleting file " & fiFile.FullName & ": " & ex2.Message
                        End Try
                    Else
                        errorMessage = "Error deleting file " & fiFile.FullName & ": " & ex.Message
                    End If
                End Try

                If Not fileDeleted Then
                    ' Sleep for 0.5 second (or longer) then try again
                    Threading.Thread.Sleep(sleepTimeMsec)

                    ' Increase sleepTimeMsec so that we sleep longer the next time, but cap the sleep time at 5.7 seconds
                    If sleepTimeMsec < 5 Then
                        sleepTimeMsec = CInt(Math.Round(sleepTimeMsec * 1.5, 0))
                    End If
                End If

            End While

            If fileDeleted Then
                errorMessage = String.Empty
            ElseIf String.IsNullOrWhiteSpace(errorMessage) Then
                errorMessage = "Unknown error deleting file " & fiFile.FullName
            End If

            ' ReSharper disable once NotAssignedOutParameter
            Return True

        End Function

        ''' <summary>
        ''' Returns true if the file is _.swp or starts with a . and ends with .swp
        ''' </summary>
        ''' <param name="filePath"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function IsVimSwapFile(filePath As String) As Boolean

            Dim fileName = Path.GetFileName(filePath)

            If fileName.ToLower() = "_.swp" OrElse fileName.StartsWith(".") AndAlso fileName.ToLower().EndsWith(".swp") Then
                Return True
            Else
                Return False
            End If
        End Function

        ''' <summary>
        ''' Confirms that the drive for the target output file has a minimum amount of free disk space
        ''' </summary>
        ''' <param name="outputFilePath">Path to output file; defines the drive or server share for which we will determine the disk space</param>
        ''' <param name="minimumFreeSpaceMB">Minimum free disk space, in MB.  Will default to 150 MB if zero or negative</param>
        ''' <param name="ErrorMessage">Output message if there is not enough free space (or if the path is invalid)</param>
        ''' <returns>True if more than minimumFreeSpaceMB is available; otherwise false</returns>
        ''' <remarks></remarks>
        Public Shared Function ValidateFreeDiskSpace(outputFilePath As String, minimumFreeSpaceMB As Double, ByRef errorMessage As String) As Boolean
            Dim outputFileExpectedSizeMB As Double = 0

            Return (ValidateFreeDiskSpace(outputFilePath, outputFileExpectedSizeMB, minimumFreeSpaceMB, errorMessage))
        End Function

        ''' <summary>
        ''' Confirms that the drive for the target output file has a minimum amount of free disk space
        ''' </summary>
        ''' <param name="outputFilePath">Path to output file; defines the drive or server share for which we will determine the disk space</param>
        ''' <param name="outputFileExpectedSizeMB">Expected size of the output file</param>
        ''' <param name="minimumFreeSpaceMB">Minimum free disk space, in MB.  Will default to 150 MB if zero or negative.  Takes into account outputFileExpectedSizeMB</param>
        ''' <param name="ErrorMessage">Output message if there is not enough free space (or if the path is invalid)</param>
        ''' <returns>True if more than minimumFreeSpaceMB is available; otherwise false</returns>
        ''' <remarks></remarks>
        Public Shared Function ValidateFreeDiskSpace(outputFilePath As String, outputFileExpectedSizeMB As Double, minimumFreeSpaceMB As Double, ByRef errorMessage As String) As Boolean

            Const DEFAULT_DATASET_STORAGE_MIN_FREE_SPACE_MB = 150

            Dim diFolderInfo As DirectoryInfo

            Dim freeBytesAvailableToUser As Long
            Dim totalDriveCapacityBytes As Long
            Dim totalNumberOfFreeBytes As Long

            errorMessage = String.Empty

            Try
                If minimumFreeSpaceMB <= 0 Then minimumFreeSpaceMB = DEFAULT_DATASET_STORAGE_MIN_FREE_SPACE_MB
                If outputFileExpectedSizeMB < 0 Then outputFileExpectedSizeMB = 0

                diFolderInfo = New FileInfo(outputFilePath).Directory

                Do While Not diFolderInfo.Exists AndAlso Not diFolderInfo.Parent Is Nothing
                    diFolderInfo = diFolderInfo.Parent
                Loop

                If GetDiskFreeSpace(diFolderInfo.FullName, freeBytesAvailableToUser, totalDriveCapacityBytes, totalNumberOfFreeBytes) Then
                    Dim freeSpaceMB As Double = totalNumberOfFreeBytes / 1024.0 / 1024.0

                    If outputFileExpectedSizeMB > 0 Then

                        If freeSpaceMB - outputFileExpectedSizeMB < minimumFreeSpaceMB Then
                            errorMessage = "Target drive will have less than " & minimumFreeSpaceMB.ToString("0") & " MB free after creating a " & outputFileExpectedSizeMB.ToString("0") & " MB file : " & freeSpaceMB.ToString("0.0") & " MB available prior to file creation"

                            Return False
                        End If

                    ElseIf freeSpaceMB < minimumFreeSpaceMB Then
                        errorMessage = "Target drive has less than " & minimumFreeSpaceMB.ToString("0") & " MB free: " & freeSpaceMB.ToString("0.0") & " MB available"

                        Return False

                    End If

                Else
                    errorMessage = "Error validating target drive free space (GetDiskFreeSpaceEx returned false): " & diFolderInfo.FullName
                    Return False

                End If


            Catch ex As Exception
                errorMessage = "Exception validating target drive free space for " & outputFilePath & ": " & ex.Message
                Return False
            End Try

            Return True
        End Function

        Public Sub WaitForLockFileQueue(
          lockFileTimestamp As Int64,
          diLockFolderSource As DirectoryInfo,
          fiSourceFile As FileInfo,
          maxWaitTimeMinutes As Integer)

            WaitForLockFileQueue(lockFileTimestamp, diLockFolderSource, Nothing, fiSourceFile, "Unknown_Target_File_Path", maxWaitTimeMinutes)

        End Sub

        Public Sub WaitForLockFileQueue(
          lockFileTimestamp As Int64,
          diLockFolderSource As DirectoryInfo,
          diLockFolderTarget As DirectoryInfo,
          fiSourceFile As FileInfo,
          strTargetFilePath As String,
          maxWaitTimeMinutes As Integer)

            ' Find the recent LockFiles present in the source and/or target lock folders
            ' These lists contain the sizes of the lock files with timestamps less than lockFileTimestamp
            Dim lstLockFileMBSource As Generic.List(Of Integer)
            Dim lstLockFileMBTarget As Generic.List(Of Integer)

            Dim intMBBacklogSource As Integer
            Dim intMBBacklogTarget As Integer

            Dim dtWaitTimeStart = DateTime.UtcNow

            Dim intSourceFileSizeMB = CInt(fiSourceFile.Length / 1024.0 / 1024.0)

            ' Wait for up to 180 minutes (3 hours) for the server resources to free up

            ' However, if retrieving files from aurora.emsl.pnl.gov only wait for a maximum of 30 minutes
            ' because sometimes that folder's permissions get messed up and we can create files there, but cannot delete them

            Dim maxWaitTimeSource = MAX_LOCKFILE_WAIT_TIME_MINUTES
            Dim maxWaitTimeTarget = MAX_LOCKFILE_WAIT_TIME_MINUTES

			' Switched from a2.emsl.pnl.gov to aurora.emsl.pnl.gov in June 2016
            If Not diLockFolderSource Is Nothing AndAlso diLockFolderSource.FullName.ToLower().StartsWith("\\aurora.emsl.pnl.gov\") Then
                maxWaitTimeSource = 30
            End If

            If Not diLockFolderTarget Is Nothing AndAlso diLockFolderTarget.FullName.ToLower().StartsWith("\\aurora.emsl.pnl.gov\") Then
                maxWaitTimeTarget = 30
            End If

            Do While True

                ' Refresh the lock files list by finding recent lock files with a timestamp less than lockFileTimestamp
                lstLockFileMBSource = FindLockFiles(diLockFolderSource, lockFileTimestamp)
                lstLockFileMBTarget = FindLockFiles(diLockFolderTarget, lockFileTimestamp)

                Dim stopWaiting = False

                If lstLockFileMBSource.Count <= 1 AndAlso lstLockFileMBTarget.Count <= 1 Then
                    stopWaiting = True
                Else

                    intMBBacklogSource = lstLockFileMBSource.Sum()
                    intMBBacklogTarget = lstLockFileMBTarget.Sum()

                    If intMBBacklogSource + intSourceFileSizeMB < LOCKFILE_TRANSFER_THRESHOLD_MB OrElse WaitedTooLong(dtWaitTimeStart, maxWaitTimeSource) Then
                        ' The source server has enough resources available to allow the copy
                        If intMBBacklogTarget + intSourceFileSizeMB < LOCKFILE_TRANSFER_THRESHOLD_MB OrElse WaitedTooLong(dtWaitTimeStart, maxWaitTimeTarget) Then
                            ' The target server has enough resources available to allow the copy
                            ' Copy the file
                            stopWaiting = True
                        End If
                    End If
                End If

                If stopWaiting Then
                    RaiseEvent LockQueueWaitComplete(fiSourceFile.FullName, strTargetFilePath, DateTime.UtcNow.Subtract(dtWaitTimeStart).TotalMinutes)
                    Exit Do
                End If

                ' Server resources exceed the thresholds
                ' Sleep for 1 to 30 seconds, depending on intMBBacklogSource and intMBBacklogTarget
                ' We compute intSleepTimeMsec using the assumption that data can be copied to/from the server at a rate of 200 MB/sec
                ' This is faster than reality, but helps minimize waiting too long between checking

                Dim dblSleepTimeSec = Math.Max(intMBBacklogSource, intMBBacklogTarget) / 200.0

                If dblSleepTimeSec < 1 Then dblSleepTimeSec = 1
                If dblSleepTimeSec > 30 Then dblSleepTimeSec = 30

                RaiseEvent WaitingForLockQueue(fiSourceFile.FullName, strTargetFilePath, intMBBacklogSource, intMBBacklogTarget)

                Threading.Thread.Sleep(CInt(dblSleepTimeSec) * 1000)

                If WaitedTooLong(dtWaitTimeStart, MAX_LOCKFILE_WAIT_TIME_MINUTES) Then
                    RaiseEvent LockQueueTimedOut(fiSourceFile.FullName, strTargetFilePath, DateTime.UtcNow.Subtract(dtWaitTimeStart).TotalMinutes)
                    Exit Do
                End If
            Loop

        End Sub

        Private Function WaitedTooLong(dtWaitTimeStart As Date, maxLockfileWaitTimeMinutes As Integer) As Boolean
            If DateTime.UtcNow.Subtract(dtWaitTimeStart).TotalMinutes < maxLockfileWaitTimeMinutes Then
                Return False
            Else
                Return True
            End If
        End Function

#End Region

#Region "GetDiskFreeSpace"

        <DllImport("Kernel32.dll", EntryPoint:="GetDiskFreeSpaceEx", SetLastError:=True, CharSet:=CharSet.Auto)>
        Private Shared Function GetDiskFreeSpaceEx(
          lpDirectoryName As String,
          ByRef lpFreeBytesAvailable As UInt64,
          ByRef lpTotalNumberOfBytes As UInt64,
          ByRef lpTotalNumberOfFreeBytes As UInt64) As Boolean
        End Function

        Protected Shared Function GetDiskFreeSpace(directoryPath As String, ByRef freeBytesAvailableToUser As Long, ByRef totalDriveCapacityBytes As Long, ByRef totalNumberOfFreeBytes As Long) As Boolean

            Dim freeAvailableUser As ULong
            Dim totalDriveCapacity As ULong
            Dim totalFree As ULong

            Dim bResult As Boolean

            ' Make sure directoryPath ends in a forward slash
            If Not directoryPath.EndsWith(Path.DirectorySeparatorChar) Then directoryPath &= Path.DirectorySeparatorChar

            bResult = GetDiskFreeSpaceEx(directoryPath, freeAvailableUser, totalDriveCapacity, totalFree)

            If Not bResult Then
                freeBytesAvailableToUser = 0
                totalDriveCapacityBytes = 0
                totalNumberOfFreeBytes = 0

                Return False
            Else
                freeBytesAvailableToUser = CLng(freeAvailableUser)
                totalDriveCapacityBytes = CLng(totalDriveCapacity)
                totalNumberOfFreeBytes = CLng(totalFree)

                Return True
            End If

        End Function

#End Region

    End Class
End Namespace
