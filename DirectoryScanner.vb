Option Explicit On 
Option Strict On

Imports System
Imports System.IO
Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics

Namespace Files
    ''' <summary>Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.</summary>
    Public Class DirectoryScanner
        ''' <summary>Event is raised whenever a matching file is found.</summary>
        ''' <remarks>This event is most useful for implementing a progress indicator.</remarks>
        ''' <param name="fileName">The found file's full path.</param>
        Public Event FoundFile(ByVal fileName As String)
		Private mSearchDirs As List(Of String)
		Private mFileList As List(Of String)

		''' <summary>Initializes a new instance of the DirectoryScanner class.</summary>
		''' <param name="dirs">An array of directory paths to scan.</param>
		Public Sub New(ByVal dirs As String())
			Me.New(dirs.ToList())
		End Sub

		''' <summary>
		''' Initializes a new instance of the DirectoryScanner class.
		''' </summary>
		''' <param name="dirs">A list of directory paths to scan</param>
		''' <remarks></remarks>
		Public Sub New(ByVal dirs As List(Of String))
			mSearchDirs = dirs
			mFileList = New List(Of String)
		End Sub

		''' <summary>Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.</summary>
		''' <param name="results">An array of file paths found; unchanged if no matches</param>
		''' <param name="searchPatterns">An array of regular expressions to use in the search.</param>
		''' <returns>Always returns true</returns>
		Public Function PerformScan(ByRef results As ArrayList, ByVal ParamArray searchPatterns As String()) As Boolean
			Dim files As Generic.List(Of String)
			files = PerformScan(searchPatterns)

			If files.Count > 0 Then
				If results Is Nothing Then
					results = New ArrayList()
				Else
					results.Clear()
				End If

				For Each item As String In files
					results.Add(item)
				Next
			End If

			Return True

		End Function

		''' <summary>Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.</summary>
		''' <param name="searchPatterns">An array of regular expressions to use in the search.</param>
		''' <returns>A list of the file paths found; empty list if no matches</returns>
		Public Function PerformScan(ByVal ParamArray searchPatterns As String()) As Generic.List(Of String)
			mFileList.Clear()

			For Each dir As String In mSearchDirs
				For Each pattern As String In searchPatterns
					RecursiveFileSearch(dir, pattern)
				Next pattern
			Next dir

			Return mFileList

		End Function

		Private Sub RecursiveFileSearch(ByVal searchDir As String, ByVal filePattern As String)
			For Each f As String In Directory.GetFiles(searchDir, filePattern)
				mFileList.Add(f)
				RaiseEvent FoundFile(f)
			Next f
			For Each d As String In Directory.GetDirectories(searchDir)
				RecursiveFileSearch(d, filePattern)
			Next d
		End Sub

    End Class
End Namespace
