Option Strict On

Imports System.IO
Imports System.Collections.Generic
Imports System.Text
Imports System.Text.RegularExpressions

Public Class clsStackTraceFormatter

    ' Note: to see accurate line numbers in the stack trace, 
    '       be sure to compile with option "Enable Optimizations" disabled

    Public Const STACK_TRACE_TITLE As String = "Stack trace: "
    Public Const STACK_CHAIN_SEPARATOR As String = "-:-"
    Public Const FINAL_FILE_PREFIX As String = " in "

    ''' <summary>
    ''' Parses the StackTrace text of the given exception to return a compact description of the current stack
    ''' </summary>
    ''' <param name="objException"></param>
    ''' <returns>
    ''' String of the form:
    ''' "Stack trace: clsCodeTest.Test-:-clsCodeTest.TestException-:-clsCodeTest.InnerTestException in clsCodeTest.vb:line 86"
    ''' </returns>
    ''' <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
    Public Shared Function GetExceptionStackTrace(objException As Exception) As String

        Dim stackTraceData As IEnumerable(Of String) = GetExceptionStackTraceData(objException)

        Dim sbStackTrace = New StringBuilder()
        For index = 0 To stackTraceData.Count - 1
            If index = stackTraceData.Count - 1 AndAlso stackTraceData(index).StartsWith(FINAL_FILE_PREFIX) Then
                sbStackTrace.Append(stackTraceData(index))
                Exit For
            End If

            If index = 0 Then
                sbStackTrace.Append(STACK_TRACE_TITLE & stackTraceData(index))
            Else
                sbStackTrace.Append(STACK_CHAIN_SEPARATOR & stackTraceData(index))
            End If
        Next

        Return sbStackTrace.ToString()
    End Function

    ''' <summary>
    ''' Parses the StackTrace text of the given exception to return a cleaned up description of the current stack,
    ''' with one line for each function in the call tree
    ''' </summary>
    ''' <param name="ex">Exception</param>
    ''' <returns>
    ''' Stack trace: 
    '''   clsCodeTest.Test
    '''   clsCodeTest.TestException
    '''   clsCodeTest.InnerTestException 
    '''    in clsCodeTest.vb:line 86
    ''' </returns>
    ''' <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
    Public Shared Function GetExceptionStackTraceMultiLine(ex As Exception) As String

        Dim stackTraceData As IEnumerable(Of String) = GetExceptionStackTraceData(ex)

        Dim sbStackTrace = New StringBuilder()
        sbStackTrace.AppendLine(STACK_TRACE_TITLE)

        For index = 0 To stackTraceData.Count - 1
            sbStackTrace.AppendLine("  " & stackTraceData(index))
        Next

        Return sbStackTrace.ToString()

    End Function

    Private Shared Function GetExceptionStackTraceData(objException As Exception) As IEnumerable(Of String)
        Const REGEX_FUNCTION_NAME = "at ([^(]+)\("
        Const REGEX_FILE_NAME = "in .+\\(.+)"

        Dim lstFunctions = New List(Of String)
        Dim strFinalFile As String = String.Empty

        Dim reFunctionName As New Regex(REGEX_FUNCTION_NAME, RegexOptions.Compiled Or RegexOptions.IgnoreCase)
        Dim reFileName As New Regex(REGEX_FILE_NAME, RegexOptions.Compiled Or RegexOptions.IgnoreCase)

        ' Process each line in objException.StackTrace
        ' Populate strFunctions() with the function name of each line
        Using trTextReader = New StringReader(objException.StackTrace)

            Do While trTextReader.Peek > -1
                Dim strLine = trTextReader.ReadLine()

                If Not String.IsNullOrEmpty(strLine) Then
                    Dim strCurrentFunction = String.Empty

                    Dim functionMatch = reFunctionName.Match(strLine)
                    If functionMatch.Success Then
                        strCurrentFunction = functionMatch.Groups(1).Value
                    Else
                        ' Look for the word " in "
                        Dim intIndex = strLine.ToLower().IndexOf(" in ", StringComparison.Ordinal)
                        If intIndex = 0 Then
                            ' " in" not found; look for the first space after startIndex 4
                            intIndex = strLine.IndexOf(" ", 4, StringComparison.Ordinal)
                        End If
                        If intIndex = 0 Then
                            ' Space not found; use the entire string
                            intIndex = strLine.Length - 1
                        End If

                        If intIndex > 0 Then
                            strCurrentFunction = strLine.Substring(0, intIndex)
                        End If

                    End If

                    If Not String.IsNullOrEmpty(strCurrentFunction) Then
                        lstFunctions.Add(strCurrentFunction)
                    End If

                    If strFinalFile.Length = 0 Then
                        ' Also extract the file name where the Exception occurred
                        Dim fileMatch = reFileName.Match(strLine)
                        If fileMatch.Success Then
                            strFinalFile = fileMatch.Groups(1).Value
                        End If
                    End If

                End If
            Loop

        End Using

        Dim stackTraceData = New List(Of String)
        stackTraceData.AddRange(lstFunctions)
        stackTraceData.Reverse()

        If Not String.IsNullOrWhiteSpace(strFinalFile) Then
            stackTraceData.Add(FINAL_FILE_PREFIX & strFinalFile)
        End If

        Return stackTraceData

    End Function

End Class
