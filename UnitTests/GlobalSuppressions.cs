// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("General", "RCS1118:Mark local variable as const.", Justification = "Acceptable design pattern", Scope = "module")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Not required", Scope = "module")]
[assembly: SuppressMessage("Roslynator", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~M:PRISMTest.DirectoryTests.CreateDirectory(System.String,System.Boolean)")]
[assembly: SuppressMessage("Roslynator", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~M:PRISMTest.FileCopyTests.CreateTargetDirectoryIfMissing(System.String)")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Parameter is used if constant SHOW_TRACE_MESSAGES is true", Scope = "member", Target = "~M:PRISMTest.TestLinuxSystemInfo.ShowTraceMessage(System.String)")]
