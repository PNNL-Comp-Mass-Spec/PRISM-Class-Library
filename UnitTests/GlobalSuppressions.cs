// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("General", "RCS1118:Mark local variable as const.", Justification = "Acceptable design pattern", Scope = "module")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Not required", Scope = "module")]
[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Prefer to use .First()", Scope = "member", Target = "~M:PRISMTest.TestDBTools.TestGetColumnValue(System.String,System.String,System.Int32)")]
[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Prefer to use .First()", Scope = "member", Target = "~M:PRISMTest.TestDBTools.TestQueryFailures(System.String,System.String,System.String,System.String,System.String,System.Int32,System.String)")]
[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Prefer to use .First()", Scope = "member", Target = "~M:PRISMTest.TestDBTools.TestQueryTableWork(System.String,System.String,System.String,System.String,System.Int32,System.String)")]
[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Prefer to use .First()", Scope = "member", Target = "~M:PRISMTest.TestLinuxSystemInfo.TestGetCoreUsageByProcessName(System.String,System.Int32,System.String,System.String,System.Double)")]
