# PRISM Class Library

The PRISM Class Library (PRISM.dll) is a collection of routines used by
many of the software tools that support the Proteomics Research Information and Management System (PRISM)
at PNNL.  Although written for use by PRISM tools, the methods in the PRISM class library
are general utility methods, and are not dependent on any PNNL resources.

The PRISM Windows class Library (PRISMWin.dll) is a set of Windows-specific utilities.

### NuGet

PRISM.Dll is available on NuGet at:
* https://www.nuget.org/packages/PRISM-Library/

PRISMWin.dll is available on NuGet at:
* https://www.nuget.org/packages/PRISMWin-Library/

### Continuous Integration

The latest versions of the DLLs are available on the [AppVeyor CI server](https://ci.appveyor.com/project/PNNLCompMassSpec/prism-class-library/build/artifacts)

[![Build status](https://ci.appveyor.com/api/projects/status/xfpaypc30b8po1je?svg=true)](https://ci.appveyor.com/project/PNNLCompMassSpec/prism-class-library)

## Important Classes in PRISM.dll

| Class            | Description |
|------------------|-------------|
| CommandLineParser |  Flexible, powerful class for keeping parameters flags and properties for command line arguments tied together, supporting properties of primitive types (and arrays of primitive types). Supports parameter flags similar to /d -dd --dir, with case sensitivity when needed, with the separator between parameter flag and parameter as ' ' or ':' |
| GenericParserOptions | Methods that demonstrates how to decorate properties in a class so that the CommandLineParser can use them to match command line arguments |
| ConsoleMsgUtils | Methods for displaying messages at the console while monitoring a class that inherits clsEventNotifier.  Uses colors for different message types |
| clsDBTools | Methods for running an ad-hoc query against a SQL Server database and obtaining the results |
| clsEventNotifier | Abstract class that implements various status events, including status, debug, error, and warning events |
| clsExecuteDatabaseSP | Methods for executing a stored procedure in a SQL Server database and optionally obtaining the results |
| clsFileTools | Methods for copying, moving, and deleting files and folders. Supports a queueing mechanism that uses lock files to avoid overloading a remote server with too many data transfer requests |
| clsOSVersionInfo | Methods for determining the currently running operating system.  Supports both Windows and Linux |
| clsParseCommandLine | Methods for parsing command line switches like /O and /Width:5 (not as advanced as CommandLineParser) |
| clsPathUtils | Cross-platform path utilities | 
| clsProgRunner | Methods for running a single program as an external process, including monitoring it with an internal thread |
| ShareConnector | Methods for connecting a machine to an SMB/CIFS share using a password and user name |
| clsStackTraceFormatter | Methods for formatting stack traces from exceptions, either as a single line with methods separated by -:- or as multiple lines |
| StringUtilities | Methods for converting doubles to strings, either specifying the number of digits to displate after the decimal, or specifying the total digits of precision to display (considering digits left and right of the decimal point) |
| SystemInfo | Methods for accessing system processor and memory information.  Works for both Windows and Linux  |
| clsLinuxSystemInfo |  Methods to determine memory usage, CPU usage, and Linux system version |
| WindowsSystemInfo | Methods returning system information for Windows, pulled via P/Invoke |
| XmlSettingsFileAccessor | Methods for reading and writing settings in an Xml settings file |
| ZipTools  | Methods for programmatically creating and working with zip files using PKZip |

## Important Classes in PRISMWin.dll

| Class            | Description |
|------------------|-------------|
| clsDiskInfo | Provides information on free disk space, both on local drives and on remote Windows shares |
| clsDotNETVersionChecker | Reports the installed versions of the .NET framework on the local computer |
| clsProcessStats | Reports the number of CPU cores in use by a given process |


## CopyWithResume

Also included is the CopyWithResume console application.  Use this program to
copy large files between computers, with the ability to resume the copy
if the network connection is lost (or the copy process is manually terminated).

## Contacts

Written by Matthew Monroe, Dave Clark, Gary Kiebel, Nathan Trimble, and Bryson Gibbons for the Department of Energy (PNNL, Richland, WA) \
Copyright 2017, Battelle Memorial Institute.  All Rights Reserved. \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://panomics.pnl.gov/ or https://omics.pnl.gov

## License

Licensed under the Apache License, Version 2.0; you may not use this file except
in compliance with the License.  You may obtain a copy of the License at
http://www.apache.org/licenses/LICENSE-2.0
