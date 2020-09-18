
// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Allowed design pattern", Scope = "module")]
[assembly: SuppressMessage("Performance", "RCS1197:Optimize StringBuilder.Append/AppendLine call.", Justification = "Allowed for readability", Scope = "module")]

[assembly: SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "This RegEx is used; the compiler is confused", Scope = "member", Target = "~F:PRISM.WindowsSystemInfoInternal.mQuotedStringMatcher")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.ParseArgs(System.String[],System.String)~PRISM.CommandLineParser`1.ParserResults`0")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.ParseArgs(System.String[],System.String,System.String)~PRISM.CommandLineParser`1.ParserResults`0")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.ShowHelp(System.String,System.String,System.Int32,System.Int32)")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.WrapParagraph(System.String,System.Int32)~System.String")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.WrapParagraphAsList(System.String,System.Int32)~System.Collections.Generic.List{System.String}")]
[assembly: SuppressMessage("Design", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Acceptable design pattern", Scope = "module")]
[assembly: SuppressMessage("Redundancy", "RCS1163:Unused parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.DBTools.UpdateDatabase(System.String,System.Int32@)~System.Boolean")]
[assembly: SuppressMessage("Readability", "RCS1234:Duplicate enum value.", Justification = "Deprecated enum name", Scope = "type", Target = "~T:PRISM.FileProcessor.ProcessFilesBase.eProcessFilesErrorCodes")]
[assembly: SuppressMessage("Readability", "RCS1234:Duplicate enum value.", Justification = "Deprecated enum name", Scope = "type", Target = "~T:PRISM.FileProcessor.ProcessDirectoriesBase.ProcessDirectoriesErrorCodes")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses are not required", Scope = "member", Target = "~M:PRISM.FileProcessor.ProcessFilesOrDirectoriesBase.ComputeIncrementalProgress(System.Single,System.Single,System.Single)~System.Single")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses are not required", Scope = "member", Target = "~M:PRISM.FileProcessor.ProcessFilesOrDirectoriesBase.ComputeIncrementalProgress(System.Single,System.Single,System.Int32,System.Int32)~System.Single")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses are not required", Scope = "member", Target = "~M:PRISM.FileProcessor.ProcessFilesOrDirectoriesBase.TrimLogDataCache")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "The parentheses are correct", Scope = "member", Target = "~M:PRISM.LinuxSystemInfo.GetCPUUtilization(System.Single)~System.Single")]
[assembly: SuppressMessage("Redundancy", "RCS1163:Unused parameter.", Justification = "This parameter is used; the compiler is confused by the #if", Scope = "member", Target = "~M:PRISM.WindowsSystemInfoInternal.GetCommandLine(System.Diagnostics.Process,System.String@,System.Collections.Generic.List{System.String}@)~System.String")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Allowed name", Scope = "type", Target = "~T:PRISM.clsParseCommandLine")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Allowed name", Scope = "type", Target = "~T:PRISM.FileProcessor.ProcessFilesBase.eProcessFilesErrorCodes")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Allowed name", Scope = "type", Target = "~T:PRISM.FileProcessor.ProcessFoldersBase.eProcessFoldersErrorCodes")]
[assembly: SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Leave for debugging purposes", Scope = "member", Target = "~M:PRISM.WindowsSystemInfoInternal.GetTotalMemoryMB~System.Single")]
[assembly: SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "Leave for debugging purposes", Scope = "member", Target = "~M:PRISM.WindowsSystemInfoInternal.GetFreeMemoryMB~System.Single")]
