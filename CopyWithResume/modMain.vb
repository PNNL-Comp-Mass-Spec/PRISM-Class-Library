Option Strict On

Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports PRISM
' This program copies a folder from a one location to another
' By default, existing files are overwritten only if they differ in size or modification time
' Copies large files in chunks such that copying can be resumed if a network error occurs
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started in December, 2010
'
' E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
' Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
' -------------------------------------------------------------------------------
Module modMain
    Public Const PROGRAM_DATE As String = "February 17, 2017"

    Private mSourceFolderPath As String = String.Empty
    Private mTargetFolderPath As String = String.Empty

    Private mRecurse As Boolean = False
    Private mOverwriteMode As FileTools.FileOverwriteMode

    Private WithEvents mFileTools As FileTools

    Public Function Main() As Integer

        Dim intReturnCode As Integer
        Dim objParseCommandLine As New clsParseCommandLine
        Dim blnProceed As Boolean

        Dim bSuccess As Boolean

        mSourceFolderPath = String.Empty
        mTargetFolderPath = String.Empty

        mRecurse = False
        mOverwriteMode = FileTools.FileOverwriteMode.OverWriteIfDateOrLengthDiffer

        Try
            blnProceed = False
            If objParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
            End If

            If Not blnProceed OrElse
               objParseCommandLine.NeedToShowHelp Then
                ShowProgramHelp()
                intReturnCode = -1
            Else

                If String.IsNullOrEmpty(mSourceFolderPath) Then
                    ShowErrorMessage("Source folder not defined")
                    ShowProgramHelp(False)
                    Return -1
                End If

                If String.IsNullOrEmpty(mTargetFolderPath) Then
                    ShowErrorMessage("Target folder not defined")
                    ShowProgramHelp(False)
                    Return -1
                End If


                Dim FileCountSkipped = 0
                Dim FileCountResumed = 0
                Dim FileCountNewlyCopied = 0

                mFileTools = New FileTools()

                Console.WriteLine("Copying " & mSourceFolderPath & " to " & mTargetFolderPath)
                Console.WriteLine("Overwrite mode: " & mOverwriteMode.ToString())
                Console.WriteLine()

                bSuccess = mFileTools.CopyDirectoryWithResume(mSourceFolderPath, mTargetFolderPath, mRecurse, mOverwriteMode, FileCountSkipped, FileCountResumed, FileCountNewlyCopied)

                If bSuccess Then
                    Console.WriteLine()
                    Console.WriteLine("Files newly copied: ".PadRight(22) & FileCountNewlyCopied)
                    Console.WriteLine("Files resumed: ".PadRight(22) & FileCountResumed)
                    Console.WriteLine("Files skipped: ".PadRight(22) & FileCountSkipped)
                Else
                    Console.WriteLine("Copied failed for " & mSourceFolderPath & " copying to " & mTargetFolderPath)
                End If

                Console.WriteLine()
            End If

        Catch ex As Exception
            ShowErrorMessage("Error occurred in modMain->Main: " & Environment.NewLine & ex.Message)
            intReturnCode = -1
        End Try

        Return intReturnCode


    End Function

    Private Function GetAppVersion() As String
        Return Assembly.GetExecutingAssembly.GetName.Version.ToString & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim lstValidParameters = New List(Of String) From {"S", "Overwrite"}
        Dim strOverwriteValue As String = String.Empty

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(lstValidParameters) Then
                ShowErrorMessage("Invalid command line parameters",
                  (From item In objParseCommandLine.InvalidParameters(lstValidParameters) Select "/" + item).ToList())
                Return False
            Else
                With objParseCommandLine
                    ' Query objParseCommandLine to see if various parameters are present

                    mSourceFolderPath = .RetrieveNonSwitchParameter(0)
                    mTargetFolderPath = .RetrieveNonSwitchParameter(1)

                    If .RetrieveValueForParameter("S", strValue) Then mRecurse = True

                    If .RetrieveValueForParameter("O", strValue) Then
                        mOverwriteMode = FileTools.FileOverwriteMode.OverwriteIfSourceNewer
                        strOverwriteValue = String.Copy(strValue)
                    End If

                    If .RetrieveValueForParameter("Overwrite", strValue) Then
                        mOverwriteMode = FileTools.FileOverwriteMode.OverwriteIfSourceNewer
                        strOverwriteValue = String.Copy(strValue)
                    End If

                    If Not String.IsNullOrEmpty(strOverwriteValue) Then

                        If strOverwriteValue.ToUpper().StartsWith("NO") Then
                            ' None
                            mOverwriteMode = FileTools.FileOverwriteMode.DoNotOverwrite

                        ElseIf strOverwriteValue.ToUpper().StartsWith("NE") Then
                            ' Source date newer (or same date but length differs)
                            mOverwriteMode = FileTools.FileOverwriteMode.OverwriteIfSourceNewer

                        ElseIf strOverwriteValue.ToUpper().StartsWith("M") Then
                            ' Mismatched size or date
                            ' Note that newer files in target folder will get overwritten since their date doesn't match
                            mOverwriteMode = FileTools.FileOverwriteMode.OverWriteIfDateOrLengthDiffer

                        ElseIf strOverwriteValue.ToUpper().StartsWith("A") Then
                            ' All
                            mOverwriteMode = FileTools.FileOverwriteMode.AlwaysOverwrite

                        Else
                            ' Unknown overwrite mode
                            ShowErrorMessage("Unknown overwrite mode: " & strOverwriteValue)
                            Return False
                        End If

                    End If

                End With

                Return True
            End If

        Catch ex As Exception
            ShowErrorMessage("Error parsing the command line parameters: " & Environment.NewLine & ex.Message)
        End Try

        Return False

    End Function

    Private Function ShortenPath(filename As String) As String
        If filename.Length > mSourceFolderPath.Length Then
            Return filename.Substring(mSourceFolderPath.Length + 1)
        Else
            Return filename
        End If
    End Function

    Private Sub ShowErrorMessage(strMessage As String)
        Dim strSeparator = "------------------------------------------------------------------------------"

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strMessage)
        Console.WriteLine(strSeparator)
        Console.WriteLine()

        WriteToErrorStream(strMessage)
    End Sub

    Private Sub ShowErrorMessage(strTitle As String, items As List(Of String))
        Dim strSeparator = "------------------------------------------------------------------------------"
        Dim strMessage As String

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strTitle)
        strMessage = strTitle & ":"

        For Each item As String In items
            Console.WriteLine("   " + item)
            strMessage &= " " & item
        Next
        Console.WriteLine(strSeparator)
        Console.WriteLine()

        WriteToErrorStream(strMessage)
    End Sub

    Private Sub ShowProgramHelp()
        ShowProgramHelp(True)
    End Sub

    Private Sub ShowProgramHelp(blnShowProgramDescription As Boolean)

        Try
            If blnShowProgramDescription Then
                Console.WriteLine("This program copies a folder from a one location to another. " &
                  "By default, existing files are overwritten only if they differ in size or modification time. " &
                  "Copies large files in chunks such that copying can be resumed if a network error occurs.")
            End If

            Console.WriteLine()
            Console.WriteLine("Program syntax:" & ControlChars.NewLine & Path.GetFileName(Assembly.GetExecutingAssembly().Location))
            Console.WriteLine(" SourceFolderPath TargetFolderPath [/S] [/Overwrite:[None|Newer|Mismatched|All]] ")
            Console.WriteLine()
            Console.WriteLine("Will copy the files from SourceFolderPath to TargetFolderPath ")
            Console.WriteLine("Recurses subdirectories if /S is included")
            Console.WriteLine()
            Console.WriteLine("Use /Overwrite to define the overwrite mode")
            Console.WriteLine("  /Overwrite:None will skip existing files")
            Console.WriteLine("  /Overwrite:Newer will overwrite files that have a newer date in the source or the same date but different size")
            Console.WriteLine("  /Overwrite:Mismatched will overwrite files that differ in date or size")
            Console.WriteLine("  /Overwrite:All will overwrite all files")
            Console.WriteLine()
            Console.WriteLine("  Note that /Overwrite is equivalent to /Overwrite:Newer")
            Console.WriteLine("  You can shorten the overwrite switch to /Overwrite:No or /Overwrite:Ne or /Overwrite:M or /Overwrite:A")
            Console.WriteLine()
            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()
            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov")
            Console.WriteLine("Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics")
            Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            Thread.Sleep(750)

        Catch ex As Exception
            ShowErrorMessage("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub

    Private Sub WriteToErrorStream(strErrorMessage As String)
        Try
            Using swErrorStream = New StreamWriter(Console.OpenStandardError())
                swErrorStream.WriteLine(strErrorMessage)
            End Using
        Catch ex As Exception
            ' Ignore errors here
        End Try
    End Sub

    Private Sub mFileTools_CopyingFile(filename As String) Handles mFileTools.CopyingFile
        Console.WriteLine("  " & ShortenPath(filename))
    End Sub

    Private Sub mFileTools_FileCopyProgress(filename As String, percentComplete As Single) Handles mFileTools.FileCopyProgress
        Static dtLastProgressUpdate As DateTime = DateTime.Now
        Static strLastfileName As String = String.Empty

        If DateTime.Now.Subtract(dtLastProgressUpdate).TotalSeconds >= 5 OrElse percentComplete >= 100 AndAlso filename = strLastfileName Then
            dtLastProgressUpdate = DateTime.Now()
            strLastfileName = filename
            Console.WriteLine("    " & percentComplete.ToString("0.0") & "% complete")
        End If

    End Sub

    Private Sub mFileTools_ResumingFileCopy(filename As String) Handles mFileTools.ResumingFileCopy
        Console.WriteLine("  Resuming: " & ShortenPath(filename))
    End Sub
End Module

