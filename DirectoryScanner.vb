Option Explicit On 

Imports System
Imports System.IO
Imports System.Collections
Imports System.Diagnostics

Namespace Files
    ''' <summary>Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.</summary>
    Public Class DirectoryScanner
        ''' <summary>Event is raised whenever a matching file is found.</summary>
        ''' <remarks>This event is most useful for implementing a progress indicator.</remarks>
        ''' <param name="fileName">The found file's full path.</param>
        Public Event FoundFile(ByVal fileName As String)
        Private searchDirs As String()
        Private al As ArrayList

        ''' <summary>Initializes a new instance of the DirectoryScanner class.</summary>
        ''' <param name="dirs">An array of directory paths to scan.</param>
        Public Sub New(ByVal dirs As String())
            searchDirs = dirs
            al = New ArrayList
        End Sub

        ''' <summary>Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.</summary>
        ''' <param name="results">An array of file paths found.</param>
        ''' <param name="dirs">An array of regular expressions to use in the search.</param>
        ''' <returns>Always return true.</returns>
        Public Function PerformScan(ByRef results As ArrayList, ByVal ParamArray searchPatterns As String()) As Boolean
            Dim dir As String
            Dim pattern As String
            Dim i As Int32

            If al.Count > 0 Then
                al.Clear()
            End If
            For Each dir In searchDirs
                For Each pattern In searchPatterns
                    If (Not RecursiveFileSearch(dir, pattern)) Then
                        Return False
                    End If
                Next pattern
            Next dir
            results = al
            Return True
        End Function

        Private Function RecursiveFileSearch(ByVal searchDir As String, ByVal filePattern As String) As Boolean
            For Each f As String In Directory.GetFiles(searchDir, filePattern)
                al.Add(f)
                RaiseEvent FoundFile(f)
            Next f
            For Each d As String In Directory.GetDirectories(searchDir)
                RecursiveFileSearch(d, filePattern)
            Next d
            Return True
        End Function
    End Class
End Namespace
