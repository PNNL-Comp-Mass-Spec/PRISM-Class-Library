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

The latest versions of the DLLs are available for six months on the [AppVeyor CI server](https://ci.appveyor.com/project/PNNLCompMassSpec/prism-class-library/build/artifacts)

[![Build status](https://ci.appveyor.com/api/projects/status/xfpaypc30b8po1je?svg=true)](https://ci.appveyor.com/project/PNNLCompMassSpec/prism-class-library)

## Important Classes in PRISM.dll

| Category         | Class            | Description |
|------------------|------------------|-------------|
| Application Settings Management     | CommandLineParser       | Flexible, powerful class for keeping parameters flags and properties for command line arguments tied together, supporting properties of primitive types (and arrays of primitive types). Supports parameter flags similar to /d -dd --dir, with case sensitivity when needed, with the separator between parameter flag and parameter as ' ' or ':'. |
| Application Settings Management     | GenericParserOptions    | Methods that demonstrates how to decorate properties in a class so that the CommandLineParser can use them to match command line arguments. |
| Application Settings Management     | clsParseCommandLine     | Methods for parsing command line switches like /O and /Width:5 (not as advanced as CommandLineParser). |
| Application Settings Management     | MgrSettings             | Loads settings from .exe.config files, and other similarly formatted files that reside in the same directory as a program executable file. |
| Application Settings Management     | XmlSettingsFileAccessor | Methods for reading and writing settings in an Xml settings file. |
| Database Utilities                  | DBTools                 | Obsolete SQL Server database access methods; use PRISMDatabaseUtils.dll instead (see below) |
| Database Utilities                  | ExecuteDatabaseSP       | Obsolete SQL Server stored procedure methods; use PRISMDatabaseUtils.dll instead (see below)  |
| File/Directory Processor Base Class | ProcessFilesBase        | Base class for classes that process a file or files, creating new output files in an output directory. |
| File/Directory Processor Base Class | ProcessDirectoriesBase  | Base class for classes that process a directory or directories. |
| File Utilities                      | FileTools               | Methods for copying, moving, and deleting files and directories. Supports a queueing mechanism that uses lock files to avoid overloading a remote server with too many data transfer requests.  Also includes methods for reading/writing Gzipped files that include filename and modification date metadata in the .gz file header. |
| File Utilities                      | PathUtils               | Cross-platform path utilities. | 
| File Utilities                      | ShareConnector          | Methods for connecting a machine to an SMB/CIFS share using a password and user name. |
| File Utilities                      | ZipTools                | Methods for programmatically creating and working with zip files using PKZip. |
| Logging                             | LogTools                | Class for handling logging via the FileLogger and DatabaseLogger. |
| Logging                             | FileLogger              | Logs messages to a file. |
| Logging                             | ODBCDatabaseLogger      | Logs messages to an database by calling a stored procedure using ODBC. |
| Logging                             | SQLServerDatabaseLogger | Logs messages to a SQL Server database by calling a stored procedure. |
| Logging                             | EventNotifier           | Abstract class that implements various status events, including status, debug, error, and warning events. |
| Output Utilities                    | ConsoleMsgUtils         | Methods for displaying messages at the console while monitoring a class that inherits EventNotifier.  Uses colors for different message types. Also includes WrapParagraph methods for wrapping a paragraph to a given number of characters. |
| Output Utilities                    | StackTraceFormatter     | Methods for formatting stack traces from exceptions, either as a single line with methods separated by -:- or as multiple lines. |
| Output Utilities                    | StringUtilities         | Methods for converting doubles to strings, either specifying the number of digits to displate after the decimal, or specifying the total digits of precision to display (considering digits left and right of the decimal point). |
| Program Execution                   | ProgRunner              | Methods for running a single program as an external process, including monitoring it with an internal thread. |
| System Info                         | LinuxSystemInfo         | Methods to determine memory usage, CPU usage, and Linux system version. |
| System Info                         | OSVersionInfo           | Methods for determining the currently running operating system.  Supports both Windows and Linux. |
| System Info                         | SystemInfo              | Methods for accessing system processor and memory information.  Works for both Windows and Linux. |
| System Info                         | WindowsSystemInfo       | Methods returning system information for Windows, pulled via P/Invoke. |


## Important Classes in PRISMDatabaseUtils.dll

| Category                        | Class            | Description |
|---------------------------------|------------------|-------------|
| Database Utilities              | DbToolsFactory   | Methods for obtaining an instance of SQLServerDBTools or PostgresDBTools, which can be used to query databases or call stored procedures in databases. |
| Database Utilities              | DataTableUtils   | Methods for appending columns to a data table. Also includes methods for retrieving data from a row of values, using a columnMap dictionary. |
| Database Utilities              | PostgresDBTools  | Methods for interacting with a PostgreSQL database to run ad-hoc queries and obtain the results. Also supports calling stored procedures (which may return results via INOUT parameters). |
| Database Utilities              | SQLServerDBTools | Methods for interacting with a SQL Server database to run ad-hoc queries and obtain the results. Also supports calling stored procedures (which may or may not return results). |
| Application Settings Management | MgrSettingsDB    | Inherits from MgrSettings. Loads settings from the Manager_Control database, and merges those settings with those loaded from the .exe.config files by the base class. |

## Important Classes in PRISMWin.dll

| Class                | Description |
|----------------------|-------------|
| DiskInfo             | Provides information on free disk space, both on local drives and on remote Windows shares |
| DotNETVersionChecker | Reports the installed versions of the .NET framework on the local computer |
| ProcessStats         | Reports the number of CPU cores in use by a given process |

## CopyWithResume

Also included is the CopyWithResume console application.  Use this program to
copy large files between computers, with the ability to resume the copy
if the network connection is lost (or the copy process is manually terminated).

## Contacts

Written by Matthew Monroe, Dave Clark, Gary Kiebel, Nathan Trimble, and Bryson Gibbons for the Department of Energy (PNNL, Richland, WA) \
Copyright 2020, Battelle Memorial Institute.  All Rights Reserved. \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics

## License

Licensed under the Apache License, Version 2.0; you may not use this program except
in compliance with the License.  You may obtain a copy of the License at
http://www.apache.org/licenses/LICENSE-2.0
