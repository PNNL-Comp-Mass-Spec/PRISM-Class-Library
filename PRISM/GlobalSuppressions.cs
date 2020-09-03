
// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Allowed design pattern", Scope = "module")]
[assembly: SuppressMessage("Performance", "RCS1197:Optimize StringBuilder.Append/AppendLine call.", Justification = "Allowed for readability", Scope = "module")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "type", Target = "~T:PRISM.clsParseCommandLine")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "type", Target = "~T:PRISM.FileProcessor.ProcessFilesBase.eProcessFilesErrorCodes")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "type", Target = "~T:PRISM.FileProcessor.ProcessFoldersBase.eProcessFoldersErrorCodes")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.ParseArgs(System.String[],System.String)~PRISM.CommandLineParser`1.ParserResults`0")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.ParseArgs(System.String[],System.String,System.String)~PRISM.CommandLineParser`1.ParserResults`0")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.ShowHelp(System.String,System.String,System.Int32,System.Int32)")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.WrapParagraph(System.String,System.Int32)~System.String")]
[assembly: SuppressMessage("Design", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.CommandLineParser`1.WrapParagraphAsList(System.String,System.Int32)~System.Collections.Generic.List{System.String}")]
[assembly: SuppressMessage("Redundancy", "RCS1163:Unused parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:PRISM.DBTools.UpdateDatabase(System.String,System.Int32@)~System.Boolean")]
