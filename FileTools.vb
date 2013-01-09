Option Strict On

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
		Public Event CopyingFile(ByVal filename As String)

		Public Event DebugEvent(ByVal CurrentTask As String, ByVal TaskDetail As String)

		''' <summary>Event is raised before copying begins.</summary>
		''' <param name="filename">The file's full path.</param>
		Public Event ResumingFileCopy(ByVal filename As String)

		''' <summary>Event is raised before copying begins.</summary>
		''' <param name="filename">The file name (not full path)</param>
		''' <param name="percentComplete">Percent complete (value between 0 and 100)</param>
		Public Event FileCopyProgress(ByVal filename As String, ByVal percentComplete As Single)

		Public Event WaitingForLockQueue(ByVal SourceFilePath As String, ByVal TargetFilePath As String, ByVal MBBacklogSource As Integer, ByVal MBBacklogTarget As Integer)

#End Region

#Region "Module constants and variables"
		'Private constants
		Private Const TERM_ADD As Boolean = True
		Private Const TERM_REMOVE As Boolean = False
		Private Const TERMCHAR_DOS As String = "\"
		Private Const TERMCHAR_UNIX As String = "/"
		Private Const COPY_OVERWRITE As Boolean = True
		Private Const COPY_NO_OVERWRITE As Boolean = False

		Private Const MAX_LOCKFILE_WAIT_TIME_MINUTES As Integer = 60

		Public Const LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB As Integer = 20
		Private Const LOCKFILE_TRANSFER_THRESHOLD_MB As Integer = 1000
		Private Const LOCKFILE_EXTENSION As String = ".lock"

		Private mCopyStatus As CopyStatus = CopyStatus.Idle
		Private mCurrentSourceFilePath As String = String.Empty

		Private mChunkSizeMB As Integer = DEFAULT_CHUNK_SIZE_MB
		Private mFlushThresholdMB As Integer = DEFAULT_FLUSH_THRESHOLD_MB

		Private mDebugLevel As Integer = 1
		Private mManagerName As String = "Unknown-Manager"

#End Region

#Region "Public constants"
		'Constants
		''' <summary>Used to add the path seperation character to the end of the directory path.</summary>
		Public Const TERMINATOR_ADD As Boolean = True
		''' <summary>Used to remove the path seperation character from the end of the directory path.</summary>
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
			OverwriteIfSourceNewer = 2					' Overwrite if source date newer (or if same date but length differs)
			OverWriteIfDateOrLengthDiffer = 3			' Overwrite if any difference in size or date; note that newer files in target folder will get overwritten since their date doesn't match
		End Enum

		Public Enum CopyStatus
			Idle = 0					' Not copying a file
			NormalCopy = 1				' File is geing copied via .NET and cannot be resumed
			BufferedCopy = 2			' File is being copied in chunks and can be resumed
			BufferedCopyResume = 3		' Resuming copying a file in chunks
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
		''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory seperation character.</param>
		''' <param name="TermChar">The specified directory seperation character.</param>
		''' <returns>The modified directory path.</returns>
		Public Shared Function CheckTerminator(ByVal InpFolder As String, ByVal AddTerm As Boolean, ByVal TermChar As String) As String

			'Overload for all parameters specified
			Return CheckTerminatorEX(InpFolder, AddTerm, TermChar)

		End Function

		''' <summary>Adds or removes the DOS path seperation character from the end of the directory path.</summary>
		''' <param name="InpFolder">The input directory path.</param>
		''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory seperation character.</param>
		''' <returns>The modified directory path.</returns>
		Public Shared Function CheckTerminator(ByVal InpFolder As String, ByVal AddTerm As Boolean) As String

			'Overload for using default termination character (DOS)
			Return CheckTerminatorEX(InpFolder, AddTerm, TERMCHAR_DOS)

		End Function

		''' <summary>Assures the directory path ends with the specified path seperation character.</summary>
		''' <param name="InpFolder">The input directory path.</param>
		''' <param name="TermChar">The specified directory seperation character.</param>
		''' <returns>The modified directory path.</returns>
		Public Shared Function CheckTerminator(ByVal InpFolder As String, ByVal TermChar As String) As String

			'Overload for using "add character" as default
			Return CheckTerminatorEX(InpFolder, TERM_ADD, TermChar)

		End Function

		''' <summary>Assures the directory path ends with the DOS path seperation character.</summary>
		''' <param name="InpFolder">The input directory path.</param>
		''' <returns>The modified directory path.</returns>
		Public Shared Function CheckTerminator(ByVal InpFolder As String) As String

			'Overload for using all defaults (add DOS terminator char)
			Return CheckTerminatorEX(InpFolder, TERM_ADD, TERMCHAR_DOS)

		End Function

		''' <summary>Modifies input directory path string depending on optional settings.</summary>
		''' <param name="InpFolder">The input directory path.</param>
		''' <param name="AddTerm">Specifies whether the directory path string ends with the specified directory seperation character.</param>
		''' <param name="TermChar">The specified directory seperation character.</param>
		''' <returns>The modified directory path.</returns>
		Private Shared Function CheckTerminatorEX(ByVal InpFolder As String, ByVal AddTerm As Boolean, ByVal TermChar As String) As String

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
		Public Sub CopyFile(ByVal SourcePath As String, ByVal DestPath As String)

			'Overload with overwrite set to default=FALSE
			CopyFileEx(SourcePath, DestPath, COPY_NO_OVERWRITE)

		End Sub

		''' <summary>Copies a source file to the destination file. Allows overwriting.</summary>
		''' <param name="SourcePath">The source file path.</param>
		''' <param name="DestPath">The destination file path.</param>
		''' <param name="Overwrite">True if the destination file can be overwritten; otherwise, false.</param>
		Public Sub CopyFile(ByVal SourcePath As String, ByVal DestPath As String, ByVal OverWrite As Boolean)

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
		''' <param name="Overwrite">True if the destination file can be overwritten; otherwise, false.</param>
		Private Sub CopyFileEx(ByVal SourcePath As String, ByVal DestPath As String, ByVal Overwrite As Boolean)

			Dim dirPath As String = System.IO.Path.GetDirectoryName(DestPath)
			If Not System.IO.Directory.Exists(dirPath) Then
				System.IO.Directory.CreateDirectory(dirPath)
			End If

			If mDebugLevel >= 3 Then
				RaiseEvent DebugEvent("Copying file with CopyFileEx", SourcePath & " to " & DestPath)
			End If

			UpdateCurrentStatus(CopyStatus.NormalCopy, SourcePath)
			System.IO.File.Copy(SourcePath, DestPath, Overwrite)
			UpdateCurrentStatusIdle()
		End Sub

#End Region

#Region "Lock File Copying functions"

		Public Function CopyFileUsingLocks(ByVal strSourceFilePath As String, ByVal strTargetFilePath As String, ByVal strManagerName As String) As Boolean
			Return CopyFileUsingLocks(New IO.FileInfo(strSourceFilePath), strTargetFilePath, strManagerName, Overwrite:=False)
		End Function

		Public Function CopyFileUsingLocks(ByVal strSourceFilePath As String, ByVal strTargetFilePath As String, ByVal strManagerName As String, ByVal Overwrite As Boolean) As Boolean
			Return CopyFileUsingLocks(New IO.FileInfo(strSourceFilePath), strTargetFilePath, strManagerName, Overwrite)
		End Function

		Public Function CopyFileUsingLocks(ByVal fiSource As System.IO.FileInfo, ByVal strTargetFilePath As String, ByVal strManagerName As String, ByVal Overwrite As Boolean) As Boolean

			Dim blnUseLockFile As Boolean = False
			Dim blnSuccess As Boolean = False

			Dim strLockFolderPathSource As String = String.Empty
			Dim strLockFolderPathTarget As String = String.Empty

			If Not Overwrite AndAlso System.IO.File.Exists(strTargetFilePath) Then
				Return True
			End If

			If IO.Path.IsPathRooted(fiSource.FullName) Then
				If fiSource.Directory.Root.FullName.StartsWith("\\") Then
					strLockFolderPathSource = IO.Path.Combine(GetServerShareBase(fiSource.Directory.Root.FullName), "DMS_LockFiles")
					If System.IO.Directory.Exists(strLockFolderPathSource) Then
						blnUseLockFile = True
					Else
						strLockFolderPathSource = String.Empty
					End If
				End If
			End If

			Dim fiTarget As System.IO.FileInfo
			fiTarget = New System.IO.FileInfo(strTargetFilePath)

			If IO.Path.IsPathRooted(fiTarget.FullName) Then
				If fiTarget.Directory.Root.FullName.StartsWith("\\") Then
					strLockFolderPathTarget = IO.Path.Combine(GetServerShareBase(fiTarget.Directory.Root.FullName), "DMS_LockFiles")
					If System.IO.Directory.Exists(strLockFolderPathTarget) Then
						blnUseLockFile = True
					Else
						strLockFolderPathTarget = String.Empty
					End If
				End If
			End If

			If blnUseLockFile Then
				blnSuccess = CopyFileUsingLocks(strLockFolderPathSource, strLockFolderPathTarget, fiSource, strTargetFilePath, strManagerName, Overwrite)
			Else
				CopyFileEx(fiSource.FullName, strTargetFilePath, Overwrite)
				blnSuccess = True
			End If

			Return blnSuccess

		End Function

		''' <summary>
		''' 
		''' </summary>
		''' <param name="strLockFolderPathSource">Path to the lock folder for the source file; can be an empty string</param>
		''' <param name="strLockFolderPathTarget">Path to the lock folder for the target file; can be an empty string</param>
		''' <param name="fiSource"></param>
		''' <param name="strTargetFilePath"></param>
		''' <param name="strManagerName"></param>
		''' <param name="Overwrite"></param>
		''' <returns></returns>
		''' <remarks></remarks>
		Public Function CopyFileUsingLocks(ByVal strLockFolderPathSource As String, ByVal strLockFolderPathTarget As String, ByVal fiSource As System.IO.FileInfo, ByVal strTargetFilePath As String, ByVal strManagerName As String, ByVal Overwrite As Boolean) As Boolean

			Dim intSourceFileSizeMB As Integer

			If Not Overwrite AndAlso System.IO.File.Exists(strTargetFilePath) Then
				If mDebugLevel >= 2 Then
					RaiseEvent DebugEvent("Skipping file since target exists", strTargetFilePath)
				End If
				Return True
			End If

			' Examine the size of the source file
			' If less than LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB then
			' copy the file normally
			intSourceFileSizeMB = CInt(fiSource.Length / 1024.0 / 1024.0)
			If intSourceFileSizeMB < LOCKFILE_MININUM_SOURCE_FILE_SIZE_MB OrElse (String.IsNullOrWhiteSpace(strLockFolderPathSource) And String.IsNullOrWhiteSpace(strLockFolderPathTarget)) Then
				CopyFileEx(fiSource.FullName, strTargetFilePath, Overwrite)
				Return True
			End If


			Dim strLockFilePathSource As String = String.Empty
			Dim strLockFilePathTarget As String = String.Empty

			Try
				' Create a new lock file on the source and/or target server
				' This file indicates an intent to copy a file

				Dim diLockFolderSource As System.IO.DirectoryInfo = Nothing
				Dim diLockFolderTarget As System.IO.DirectoryInfo = Nothing
				Dim intLockFileTimestamp As Int64 = GetLockFileTimeStamp()

				If Not String.IsNullOrWhiteSpace(strLockFolderPathSource) Then
					diLockFolderSource = New System.IO.DirectoryInfo(strLockFolderPathSource)
					strLockFilePathSource = CreateLockFile(diLockFolderSource, intLockFileTimestamp, fiSource, strTargetFilePath, strManagerName)
				End If

				If Not String.IsNullOrWhiteSpace(strLockFolderPathTarget) Then
					diLockFolderTarget = New System.IO.DirectoryInfo(strLockFolderPathTarget)
					strLockFilePathTarget = CreateLockFile(diLockFolderTarget, intLockFileTimestamp, fiSource, strTargetFilePath, strManagerName)
				End If


				' Find the recent LockFiles present in the source and/or target lock folders
				' These lists contain the sizes of the lock files with timestamps less than intLockFileTimestamp
				Dim lstLockFileMBSource As Generic.List(Of Integer)
				Dim lstLockFileMBTarget As Generic.List(Of Integer)

				Dim intMBBacklogSource As Integer
				Dim intMBBacklogTarget As Integer

				Dim dtWaitTimeStart As System.DateTime

				dtWaitTimeStart = System.DateTime.UtcNow

				' Wait for up to 60 minutes for the server resources to free up
				Do While System.DateTime.UtcNow.Subtract(dtWaitTimeStart).TotalMinutes < MAX_LOCKFILE_WAIT_TIME_MINUTES

					' Refresh the lock files list by finding recent lock files with a timestamp less than intLockFileTimestamp
					lstLockFileMBSource = FindLockFiles(diLockFolderSource, intLockFileTimestamp)
					lstLockFileMBTarget = FindLockFiles(diLockFolderTarget, intLockFileTimestamp)

					If lstLockFileMBSource.Count <= 1 AndAlso lstLockFileMBTarget.Count <= 1 Then
						Exit Do
					End If

					intMBBacklogSource = lstLockFileMBSource.Sum()
					intMBBacklogTarget = lstLockFileMBTarget.Sum()

					If intMBBacklogSource + intSourceFileSizeMB < LOCKFILE_TRANSFER_THRESHOLD_MB Then
						' The source server has enough resources available to allow the copy
						If intMBBacklogTarget + intSourceFileSizeMB < LOCKFILE_TRANSFER_THRESHOLD_MB Then
							' The target server has enough resources available to allow the copy
							' Copy the file
							Exit Do
						End If
					End If

					' Server resources exceed the thresholds
					' Sleep for 1 to 30 seconds, depending on intMBBacklogSource and intMBBacklogTarget
					' We compute intSleepTimeMsec using the assumption that data can be copied to/from the server at a rate of 200 MB/sec
					' This is faster than reality, but helps minimize waiting too long between checking

					Dim dblSleepTimeSec As Double
					dblSleepTimeSec = Math.Max(intMBBacklogSource, intMBBacklogTarget) / 200.0
					If dblSleepTimeSec < 1 Then dblSleepTimeSec = 1
					If dblSleepTimeSec > 30 Then dblSleepTimeSec = 30

					RaiseEvent WaitingForLockQueue(fiSource.FullName, strTargetFilePath, intMBBacklogSource, intMBBacklogTarget)

					System.Threading.Thread.Sleep(CInt(dblSleepTimeSec) * 1000)
				Loop

				If mDebugLevel >= 1 Then
					RaiseEvent DebugEvent("Copying file using Locks", fiSource.FullName & " to " & strTargetFilePath)
				End If

				' Perform the copy
				CopyFileEx(fiSource.FullName, strTargetFilePath, Overwrite)

				' Delete the lock file(s)
				DeleteFileIgnoreErrors(strLockFilePathSource)
				DeleteFileIgnoreErrors(strLockFilePathTarget)

			Catch ex As Exception
				' Error occurred
				' Delete the lock file then throw the exception
				DeleteFileIgnoreErrors(strLockFilePathSource)
				DeleteFileIgnoreErrors(strLockFilePathTarget)

				Throw ex
				Return False
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
		Protected Function CreateLockFile(ByVal diLockFolder As System.IO.DirectoryInfo, ByVal intLockFileTimestamp As Int64, ByVal fiSource As System.IO.FileInfo, ByVal strTargetFilePath As String, ByVal strManagerName As String) As String

			Dim strLockFilePath As String = String.Empty
			Dim strLockFileName As String

			If diLockFolder Is Nothing Then
				Return String.Empty
			End If

			' Define the lock file name
			strLockFileName = GenerateLockFileName(intLockFileTimestamp, fiSource, strManagerName)
			strLockFilePath = IO.Path.Combine(diLockFolder.FullName, strLockFileName)
			Do While IO.File.Exists(strLockFilePath)
				' File already exists for this manager; append a dash to the path
				strLockFileName = IO.Path.GetFileNameWithoutExtension(strLockFileName) & "-" & IO.Path.GetExtension(strLockFileName)
				strLockFilePath = IO.Path.Combine(diLockFolder.FullName, strLockFileName)
			Loop

			Try
				' Create the lock file
				Using swLockFile As System.IO.StreamWriter = New System.IO.StreamWriter(New System.IO.FileStream(strLockFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read))
					swLockFile.WriteLine("Date: " & System.DateTime.Now.ToString())
					swLockFile.WriteLine("Source: " & fiSource.FullName)
					swLockFile.WriteLine("Target: " & strTargetFilePath)
					swLockFile.WriteLine("Size_Bytes: " & fiSource.Length)
				End Using
			Catch ex As Exception
				' Error creating the lock file
				' Return an empty string
				Return String.Empty
			End Try

			Return strLockFilePath

		End Function

		Private Sub DeleteFileIgnoreErrors(ByVal strFilePath As String)
			Try
				If Not String.IsNullOrWhiteSpace(strFilePath) Then
					IO.File.Delete(strFilePath)
				End If
			Catch ex As Exception
				' Ignore errors here
			End Try
		End Sub

		''' <summary>
		''' Finds lock files with a timestamp less than
		''' </summary>
		''' <param name="diLockFolder"></param>
		''' <returns></returns>
		''' <remarks></remarks>
		Private Function FindLockFiles(ByVal diLockFolder As System.IO.DirectoryInfo, intLockFileTimestamp As Int64) As Generic.List(Of Integer)
			Static reParseLockFileName As System.Text.RegularExpressions.Regex = New System.Text.RegularExpressions.Regex("^(\d+)_(\d+)_", Text.RegularExpressions.RegexOptions.Compiled)

			Dim reMatch As System.Text.RegularExpressions.Match
			Dim intQueueTimeMSec As Int64
			Dim intFileSizeMB As Int32

			Dim lstLockFiles As Generic.List(Of Integer)
			lstLockFiles = New Generic.List(Of Integer)

			If Not diLockFolder Is Nothing Then
				diLockFolder.Refresh()

				For Each fiLockFile As IO.FileInfo In diLockFolder.GetFiles("*" & LOCKFILE_EXTENSION)
					reMatch = reParseLockFileName.Match(fiLockFile.Name)

					If reMatch.Success Then
						If Int64.TryParse(reMatch.Groups(1).Value, intQueueTimeMSec) Then
							If Int32.TryParse(reMatch.Groups(2).Value, intFileSizeMB) Then

								If intQueueTimeMSec < intLockFileTimestamp Then
									' Lock file fiLockFile was created prior to the current one
									' Make sure it's less than 1 hour old
									If Math.Abs((intLockFileTimestamp - intQueueTimeMSec) / 1000.0 / 60.0) < MAX_LOCKFILE_WAIT_TIME_MINUTES Then
										lstLockFiles.Add(intFileSizeMB)
									End If
								End If
							End If
						End If

					End If
				Next
			End If

			Return lstLockFiles

		End Function

		''' <summary>
		''' Generate the lock file name, which starts with a msec-based timestamp, then has the source file size (in MB), then has information on the machine creating the file
		''' </summary>
		''' <param name="fiSource"></param>
		''' <param name="strManagerName"></param>
		''' <returns></returns>
		''' <remarks></remarks>
		Private Function GenerateLockFileName(ByVal intLockFileTimestamp As Int64, ByVal fiSource As System.IO.FileInfo, ByVal strManagerName As String) As String
			Static reInvalidDosChars As System.Text.RegularExpressions.Regex = New System.Text.RegularExpressions.Regex("[\\/:*?""<>| ]", Text.RegularExpressions.RegexOptions.Compiled)

			Dim strLockFileName As String
			strLockFileName = intLockFileTimestamp.ToString() & "_" &
			  (fiSource.Length / 1024.0 / 1024.0).ToString("0000") & "_" &
			  System.Environment.MachineName & "_" &
			  strManagerName & LOCKFILE_EXTENSION

			' Replace any invalid characters (including spaces) with an underscore
			Return reInvalidDosChars.Replace(strLockFileName, "_")

		End Function

		Private Function GetLockFileTimeStamp() As Int64
			Return CType(Math.Round(System.DateTime.UtcNow.Subtract(New DateTime(2010, 1, 1)).TotalMilliseconds, 0), Int64)
		End Function

		''' <summary>
		''' Returns the first portion of a network share path, for example \\MyServer is returned for \\MyServer\Share\Filename.txt
		''' </summary>
		''' <param name="strServerSharePath"></param>
		''' <returns></returns>
		''' <remarks></remarks>
		Public Function GetServerShareBase(ByVal strServerSharePath As String) As String
			If strServerSharePath.StartsWith("\\") Then
				Dim intSlashIndex As Integer
				intSlashIndex = strServerSharePath.IndexOf("\", 2)
				If intSlashIndex > 0 Then
					Return strServerSharePath.Substring(0, intSlashIndex)
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
		Public Sub CopyDirectory(ByVal SourcePath As String, ByVal DestPath As String)

			'Overload with overwrite set to default=FALSE
			CopyDirectory(SourcePath, DestPath, COPY_NO_OVERWRITE)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Does not allow overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal strManagerName As String)

			'Overload with overwrite set to default=FALSE
			CopyDirectory(SourcePath, DestPath, COPY_NO_OVERWRITE, strManagerName)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Does not allow overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal FileNamesToSkip As Generic.List(Of String))

			'Overload with overwrite set to default=FALSE
			CopyDirectory(SourcePath, DestPath, COPY_NO_OVERWRITE, FileNamesToSkip)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal OverWrite As Boolean)

			'Overload with no defaults
			Dim bReadOnly As Boolean = False
			CopyDirectory(SourcePath, DestPath, OverWrite, bReadOnly)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal OverWrite As Boolean, _
  ByVal strManagerName As String)

			'Overload with no defaults
			Dim bReadOnly As Boolean = False
			CopyDirectory(SourcePath, DestPath, OverWrite, bReadOnly, New Generic.List(Of String), strManagerName)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
		''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal OverWrite As Boolean, _
  ByVal FileNamesToSkip As Generic.List(Of String))

			'Overload with no defaults
			Dim bReadOnly As Boolean = False
			CopyDirectory(SourcePath, DestPath, OverWrite, bReadOnly, FileNamesToSkip)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
		''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal OverWrite As Boolean, _
  ByVal bReadOnly As Boolean)

			'Overload with no defaults
			Dim SetAttribute As Boolean = True
			CopyDirectoryEx(SourcePath, DestPath, OverWrite, SetAttribute, bReadOnly, New Generic.List(Of String), mManagerName)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
		''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
		''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal OverWrite As Boolean, _
  ByVal bReadOnly As Boolean, _
  ByVal FileNamesToSkip As Generic.List(Of String))

			'Overload with no defaults
			Dim SetAttribute As Boolean = True
			CopyDirectoryEx(SourcePath, DestPath, OverWrite, SetAttribute, bReadOnly, FileNamesToSkip, mManagerName)

		End Sub

		''' <summary>Copies a source directory to the destination directory. Allows overwriting.</summary>
		''' <param name="SourcePath">The source directory path.</param>
		''' <param name="DestPath">The destination directory path.</param>
		''' <param name="Overwrite">true if the destination file can be overwritten; otherwise, false.</param>
		''' <param name="bReadOnly">The value to be assigned to the read-only attribute of the destination file.</param>
		''' <param name="FileNamesToSkip">List of file names to skip when copying the directory (and subdirectories); can optionally contain full path names to skip</param>
		Public Sub CopyDirectory(ByVal SourcePath As String, _
  ByVal DestPath As String, _
  ByVal OverWrite As Boolean, _
  ByVal bReadOnly As Boolean, _
  ByVal FileNamesToSkip As Generic.List(Of String), _
  ByVal strManagerName As String)

			'Overload with no defaults
			Dim SetAttribute As Boolean = True
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
		Private Sub CopyDirectoryEx(ByVal SourcePath As String, _
	ByVal DestPath As String, _
	ByVal Overwrite As Boolean, _
	ByVal SetAttribute As Boolean, _
	ByVal bReadOnly As Boolean, _
	ByRef FileNamesToSkip As Generic.List(Of String), _
	ByVal strManagerName As String)

			Dim SourceDir As System.IO.DirectoryInfo = New System.IO.DirectoryInfo(SourcePath)
			Dim DestDir As System.IO.DirectoryInfo = New System.IO.DirectoryInfo(DestPath)

			Dim dctFileNamesToSkip As Generic.Dictionary(Of String, String)

			Dim blnCopyFile As Boolean

			' the source directory must exist, otherwise throw an exception
			If SourceDir.Exists Then
				' if destination SubDir's parent SubDir does not exist throw an exception
				If Not DestDir.Parent.Exists Then
					Throw New System.IO.DirectoryNotFoundException("Destination directory does not exist: " + DestDir.Parent.FullName)
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
				Dim ChildFile As System.IO.FileInfo
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

						sTargetFilePath = System.IO.Path.Combine(DestDir.FullName, ChildFile.Name)

						If Overwrite Then
							UpdateCurrentStatus(CopyStatus.NormalCopy, ChildFile.FullName)
							CopyFileUsingLocks(ChildFile, sTargetFilePath, strManagerName, Overwrite:=True)
						Else
							' If Overwrite = false, copy the file only if it does not exist
							' this is done to avoid an IOException if a file already exists
							' this way the other files can be copied anyway...
							If Not System.IO.File.Exists(sTargetFilePath) Then
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
				For Each SubDir As System.IO.DirectoryInfo In SourceDir.GetDirectories()
					CopyDirectoryEx(SubDir.FullName, System.IO.Path.Combine(DestDir.FullName, SubDir.Name), _
					 Overwrite, SetAttribute, bReadOnly, FileNamesToSkip, strManagerName)
				Next
			Else
				Throw New System.IO.DirectoryNotFoundException("Source directory does not exist: " + SourceDir.FullName)
			End If

		End Sub

		''' <summary>
		''' Copies the file attributes from a source file to a target file, explicitly updating the read-only bit based on bReadOnly
		''' </summary>
		''' <param name="fiSourceFile">Source FileInfo</param>
		''' <param name="sTargetFilePath">Target file path</param>
		''' <param name="bReadOnly">True to force the ReadOnly bit on, False to force it off</param>
		''' <remarks></remarks>
		Protected Sub UpdateReadonlyAttribute(ByVal fiSourceFile As System.IO.FileInfo, ByVal sTargetFilePath As String, ByVal bReadOnly As Boolean)

			' Get the file attributes from the source file
			Dim fa As System.IO.FileAttributes = fiSourceFile.Attributes()
			Dim faNew As System.IO.FileAttributes

			' Change the read-only attribute to the desired value
			If bReadOnly Then
				faNew = fa Or System.IO.FileAttributes.ReadOnly
			Else
				faNew = fa And Not System.IO.FileAttributes.ReadOnly
			End If

			If fa <> faNew Then
				' Set the attributes of the destination file
				System.IO.File.SetAttributes(sTargetFilePath, fa)
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
		Public Function CopyDirectoryWithResume(ByVal SourceFolderPath As String, ByVal TargetFolderPath As String) As Boolean

			Dim Recurse As Boolean = False
			Dim eFileOverwriteMode As FileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer
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
		Public Function CopyDirectoryWithResume(ByVal SourceFolderPath As String, ByVal TargetFolderPath As String, ByVal Recurse As Boolean) As Boolean

			Dim eFileOverwriteMode As FileOverwriteMode = FileOverwriteMode.OverWriteIfDateOrLengthDiffer
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
		Public Function CopyDirectoryWithResume(ByVal SourceFolderPath As String, _
   ByVal TargetFolderPath As String, _
   ByVal Recurse As Boolean, _
   ByVal eFileOverwriteMode As FileOverwriteMode, _
   ByVal FileNamesToSkip As Generic.List(Of String)) As Boolean

			Dim FileCountSkipped As Integer = 0
			Dim FileCountResumed As Integer = 0
			Dim FileCountNewlyCopied As Integer = 0
			Dim SetAttribute As Boolean = False
			Dim bReadOnly As Boolean = False

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
		Public Function CopyDirectoryWithResume(ByVal SourceFolderPath As String, _
   ByVal TargetFolderPath As String, _
   ByVal Recurse As Boolean, _
   ByVal eFileOverwriteMode As FileOverwriteMode, _
   ByRef FileCountSkipped As Integer, _
   ByRef FileCountResumed As Integer, _
   ByRef FileCountNewlyCopied As Integer) As Boolean

			Dim SetAttribute As Boolean = False
			Dim bReadOnly As Boolean = False
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
		Public Function CopyDirectoryWithResume(ByVal SourceFolderPath As String, _
   ByVal TargetFolderPath As String, _
   ByVal Recurse As Boolean, _
   ByVal eFileOverwriteMode As FileOverwriteMode, _
   ByVal FileNamesToSkip As Generic.List(Of String), _
   ByRef FileCountSkipped As Integer, _
   ByRef FileCountResumed As Integer, _
   ByRef FileCountNewlyCopied As Integer) As Boolean

			Dim SetAttribute As Boolean = False
			Dim bReadOnly As Boolean = False

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
		Public Function CopyDirectoryWithResume(ByVal SourceFolderPath As String, _
   ByVal TargetFolderPath As String, _
   ByVal Recurse As Boolean, _
   ByVal eFileOverwriteMode As FileOverwriteMode, _
   ByVal SetAttribute As Boolean, _
   ByVal bReadOnly As Boolean, _
   ByVal FileNamesToSkip As Generic.List(Of String), _
   ByRef FileCountSkipped As Integer, _
   ByRef FileCountResumed As Integer, _
   ByRef FileCountNewlyCopied As Integer) As Boolean

			Dim diSourceFolder As System.IO.DirectoryInfo
			Dim diTargetFolder As System.IO.DirectoryInfo
			Dim dctFileNamesToSkip As Generic.Dictionary(Of String, String)

			Dim blnCopyFile As Boolean
			Dim bSuccess As Boolean = True


			diSourceFolder = New System.IO.DirectoryInfo(SourceFolderPath)
			diTargetFolder = New System.IO.DirectoryInfo(TargetFolderPath)

			' The source directory must exist, otherwise throw an exception
			If Not diSourceFolder.Exists Then
				Throw New System.IO.DirectoryNotFoundException("Source directory does not exist: " + diSourceFolder.FullName)
			End If

			' If destination SubDir's parent SubDir does not exist throw an exception
			If Not diTargetFolder.Parent.Exists Then
				Throw New System.IO.DirectoryNotFoundException("Destination directory does not exist: " + diTargetFolder.Parent.FullName)
			End If

			If diSourceFolder.FullName = diTargetFolder.FullName Then
				Throw New System.IO.IOException("Source and target directories cannot be the same: " + diTargetFolder.FullName)
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
				For Each fiSourceFile As System.IO.FileInfo In diSourceFolder.GetFiles()

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
						Dim fiExistingFile As System.IO.FileInfo
						fiExistingFile = New System.IO.FileInfo(System.IO.Path.Combine(diTargetFolder.FullName, fiSourceFile.Name))

						If fiExistingFile.Exists Then
							Select Case eFileOverwriteMode
								Case FileOverwriteMode.AlwaysOverwrite
									blnCopyFile = True

								Case FileOverwriteMode.DoNotOverwrite
									blnCopyFile = False

								Case FileOverwriteMode.OverwriteIfSourceNewer
									If fiSourceFile.LastWriteTimeUtc < fiExistingFile.LastWriteTimeUtc OrElse _
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

						Dim blnResumed As Boolean = False
						Try
							Dim strTargetFilePath As String = System.IO.Path.Combine(diTargetFolder.FullName, fiSourceFile.Name)
							bSuccess = CopyFileWithResume(fiSourceFile, strTargetFilePath, blnResumed)
						Catch ex As Exception
							Throw ex
						End Try

						If Not bSuccess Then Exit For

						If blnResumed Then
							FileCountResumed += 1
						Else
							FileCountNewlyCopied += 1
						End If

						If SetAttribute Then
							Dim sTargetFilePath As String = System.IO.Path.Combine(diTargetFolder.FullName, fiSourceFile.Name)
							UpdateReadonlyAttribute(fiSourceFile, sTargetFilePath, bReadOnly)
						End If

					End If

				Next

				If bSuccess AndAlso Recurse Then
					' Process each subdirectory
					For Each fiSourceFolder As System.IO.DirectoryInfo In diSourceFolder.GetDirectories()
						Dim strSubDirTargetFolderPath As String
						strSubDirTargetFolderPath = System.IO.Path.Combine(TargetFolderPath, fiSourceFolder.Name)
						bSuccess = CopyDirectoryWithResume(fiSourceFolder.FullName, strSubDirTargetFolderPath, _
						  Recurse, eFileOverwriteMode, SetAttribute, bReadOnly, _
						  FileNamesToSkip, FileCountSkipped, FileCountResumed, FileCountNewlyCopied)
					Next
				End If

			Catch ex As Exception
				Throw New System.IO.IOException("Exception copying directory with resume: " + ex.Message, ex)
				bSuccess = False
			End Try

			Return bSuccess

		End Function

		''' <summary>
		''' Copy a file using 
		''' </summary>
		''' <param name="SourceFilePath"></param>
		''' <param name="strTargetFilePath"></param>
		''' <param name="blnResumed"></param>
		''' <returns></returns>
		''' <remarks></remarks>
		Public Function CopyFileWithResume(ByVal SourceFilePath As String, ByVal strTargetFilePath As String, ByRef blnResumed As Boolean) As Boolean
			Dim fiSourceFile As System.IO.FileInfo

			fiSourceFile = New System.IO.FileInfo(SourceFilePath)
			Return CopyFileWithResume(fiSourceFile, strTargetFilePath, blnResumed)

		End Function

		''' <summary>
		''' Copy fiSourceFile to diTargetFolder
		''' </summary>
		''' <param name="fiSourceFile"></param>
		''' <param name="strTargetFilePath"></param>
		''' <param name="blnResumed">Output parameter; true if copying was resumed</param>
		''' <returns></returns>
		''' <remarks></remarks>
		Public Function CopyFileWithResume(ByVal fiSourceFile As System.IO.FileInfo, ByVal strTargetFilePath As String, ByRef blnResumed As Boolean) As Boolean

			Const FILE_PART_TAG As String = ".#FilePart#"
			Const FILE_PART_INFO_TAG As String = ".#FilePartInfo#"

			Dim intChunkSizeBytes As Integer
			Dim intFlushThresholdBytes As Integer

			Dim lngFileOffsetStart As Int64 = 0
			Dim blnResumeCopy As Boolean
			Dim dtSourceFileLastWriteTimeUTC As System.DateTime
			Dim strSourceFileLastWriteTime As String

			Dim swFilePart As System.IO.FileStream = Nothing

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
					Return True
				End If

				' Delete the target file if it already exists
				If System.IO.File.Exists(strTargetFilePath) Then
					System.IO.File.Delete(strTargetFilePath)
					System.Threading.Thread.Sleep(25)
				End If

				' Check for a #FilePart# file
				Dim fiFilePart As System.IO.FileInfo
				fiFilePart = New System.IO.FileInfo(strTargetFilePath & FILE_PART_TAG)

				Dim fiFilePartInfo As System.IO.FileInfo
				fiFilePartInfo = New System.IO.FileInfo(strTargetFilePath & FILE_PART_INFO_TAG)

				dtSourceFileLastWriteTimeUTC = fiSourceFile.LastWriteTimeUtc
				strSourceFileLastWriteTime = dtSourceFileLastWriteTimeUTC.ToString("yyyy-MM-dd hh:mm:ss.fff tt")

				If fiFilePart.Exists Then
					' Possibly resume copying
					' First inspect the FilePartInfo file

					If fiFilePartInfo.Exists Then
						' Open the file and read the file length and file modification time
						' If they match fiSourceFile then set blnResumeCopy to true and update lngFileOffsetStart

						Using srFilePartInfo As System.IO.StreamReader = New System.IO.StreamReader(New System.IO.FileStream(fiFilePartInfo.FullName, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite))

							Dim lstSourceLines As Generic.List(Of String) = New Generic.List(Of String)

							Do While srFilePartInfo.Peek > -1
								lstSourceLines.Add(srFilePartInfo.ReadLine())
							Loop

							If lstSourceLines.Count >= 3 Then
								' The first line contains the source file path
								' The second contains the file length, in bytes
								' The third contains the file modification time (UTC)

								If lstSourceLines(0) = fiSourceFile.FullName AndAlso _
								   lstSourceLines(1) = fiSourceFile.Length.ToString Then

									' Name and size are the same
									' See if the timestamps agree within 2 seconds (need to allow for this in case we're comparing NTFS and FAT32)

									Dim dtCachedLastWriteTimeUTC As System.DateTime
									If System.DateTime.TryParse(lstSourceLines(2), dtCachedLastWriteTimeUTC) Then
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
					swFilePart = New System.IO.FileStream(fiFilePart.FullName, IO.FileMode.Append, IO.FileAccess.Write, IO.FileShare.Read)
				Else
					UpdateCurrentStatus(CopyStatus.BufferedCopy, fiSourceFile.FullName)

					' Delete FilePart file in the target folder if it already exists
					If fiFilePart.Exists Then
						fiFilePart.Delete()
						System.Threading.Thread.Sleep(25)
					End If

					' Create the FILE_PART_INFO_TAG file
					Using swFilePartInfo As System.IO.StreamWriter = New System.IO.StreamWriter(New System.IO.FileStream(fiFilePartInfo.FullName, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read))

						' The first line contains the source file path
						' The second contains the file length, in bytes
						' The third contains the file modification time (UTC)
						swFilePartInfo.WriteLine(fiSourceFile.FullName)
						swFilePartInfo.WriteLine(fiSourceFile.Length)
						swFilePartInfo.WriteLine(strSourceFileLastWriteTime)
					End Using

					' Open the FilePart file
					swFilePart = New System.IO.FileStream(fiFilePart.FullName, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
				End If

				' Now copy the file, appending data to swFilePart

				' Open the source and seek to lngFileOffsetStart if > 0
				Dim srSourceFile As System.IO.FileStream
				srSourceFile = New System.IO.FileStream(fiSourceFile.FullName, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
				If lngFileOffsetStart > 0 Then
					srSourceFile.Seek(lngFileOffsetStart, IO.SeekOrigin.Begin)
				End If

				Dim intBytesRead As Integer
				Dim buffer() As Byte
				Dim intBytesSinceLastFlush As Int64

				Dim lngBytesWritten As Int64 = lngFileOffsetStart
				Dim sngTotalBytes As Single = srSourceFile.Length
				Dim sngProgress As Single = 0	 ' Value between 0 and 100

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
						sngProgress = lngBytesWritten / sngTotalBytes * 100
						RaiseEvent FileCopyProgress(fiSourceFile.Name, sngProgress)
					End If

					If intBytesRead < intChunkSizeBytes Then
						Exit Do
					End If
				Loop While intBytesRead > 0

				RaiseEvent FileCopyProgress(fiSourceFile.Name, 100)

				srSourceFile.Close()
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

				Throw New System.IO.IOException("Exception copying file with resume: " & ex.Message, ex)
				Return False
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
		Protected Function NearlyEqualFileTimes(ByVal dtTime1 As System.DateTime, ByVal dtTime2 As System.DateTime) As Boolean
			If Math.Abs(dtTime1.Subtract(dtTime2).TotalSeconds) <= 2 Then
				Return True
			Else
				Return False
			End If
		End Function

		Protected Sub UpdateCurrentStatusIdle()
			UpdateCurrentStatus(CopyStatus.Idle, String.Empty)
		End Sub

		Protected Sub UpdateCurrentStatus(ByVal eStatus As CopyStatus, ByVal sSourceFilePath As String)
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
		Public Function GetDirectorySize(ByVal DirPath As String) As Long

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
		Public Function GetDirectorySize(ByVal DirPath As String, ByRef FileCount As Long, ByRef SubDirCount As Long) As Long

			'Overload for returning directory size, file count and directory count for entire directory tree
			Return GetDirectorySizeEX(DirPath, FileCount, SubDirCount)

		End Function

		''' <summary>Get the directory size, file count, and directory count for the entire directory tree.</summary>
		''' <param name="DirPath">The path to the directory.</param>
		''' <param name="FileCount">The number of files in the entire directory tree.</param>
		''' <param name="SubDirCount">The number of directories in the entire directory tree.</param>
		''' <returns>The directory size.</returns>
		Private Function GetDirectorySizeEX(ByVal DirPath As String, ByRef FileCount As Long, ByRef SubDirCount As Long) As Long

			' Returns the size of the specified directory, number of files in the directory tree, and number of subdirectories
			' - Note: requires Imports System.IO
			' - Usage: Dim DirSize As Long = GetDirectorySize("D:\Projects")
			'
			' Original code obtained from vb2themax.com
			Dim DirSize As Long
			Dim Dir As System.IO.DirectoryInfo = New System.IO.DirectoryInfo(DirPath)
			'		Dim InternalFileCount As Long
			'		Dim InternalDirCount As Long

			' add the size of each file
			Dim ChildFile As System.IO.FileInfo
			For Each ChildFile In Dir.GetFiles()
				DirSize += ChildFile.Length
				FileCount += 1
			Next

			' add the size of each sub-directory, that is retrieved by recursively
			' calling this same routine
			Dim SubDir As System.IO.DirectoryInfo
			For Each SubDir In Dir.GetDirectories()
				DirSize += GetDirectorySizeEX(SubDir.FullName, FileCount, SubDirCount)
				SubDirCount += 1
			Next

			'		FileCount = InternalFileCount
			'		SubDirCount = InternalDirCount
			Return DirSize

		End Function
#End Region

#Region "MoveDirectory Function"

		Public Function MoveDirectory(ByVal SourceFolderPath As String, ByVal TargetFolderPath As String, ByVal OverwriteFiles As Boolean) As Boolean
			Return MoveDirectory(SourceFolderPath, TargetFolderPath, OverwriteFiles, mManagerName)
		End Function

		Public Function MoveDirectory(ByVal SourceFolderPath As String, ByVal TargetFolderPath As String, ByVal OverwriteFiles As Boolean, ByVal strManagerName As String) As Boolean
			Dim diSourceFolder As System.IO.DirectoryInfo
			Dim blnSuccess As Boolean

			diSourceFolder = New System.IO.DirectoryInfo(SourceFolderPath)

			' Recursively call this function for each subdirectory
			For Each fiFolder As System.IO.DirectoryInfo In diSourceFolder.GetDirectories()
				blnSuccess = MoveDirectory(fiFolder.FullName, IO.Path.Combine(TargetFolderPath, fiFolder.Name), OverwriteFiles, strManagerName)
				If Not blnSuccess Then
					Throw New Exception("Error moving directory " & fiFolder.FullName & " to " & TargetFolderPath & "; MoveDirectory returned False")
				End If
			Next

			For Each fiFile As System.IO.FileInfo In diSourceFolder.GetFiles()
				blnSuccess = CopyFileUsingLocks(fiFile.FullName, IO.Path.Combine(TargetFolderPath, fiFile.Name), strManagerName, OverwriteFiles)
				If Not blnSuccess Then
					Throw New Exception("Error copying file " & fiFile.FullName & " to " & TargetFolderPath & "; CopyFileUsingLocks returned False")
				Else
					' Delete the source file
					DeleteFileIgnoreErrors(fiFile.FullName)
				End If
			Next

			diSourceFolder.Refresh()
			If diSourceFolder.GetFileSystemInfos("*", IO.SearchOption.AllDirectories).Count = 0 Then
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

	End Class
End Namespace
