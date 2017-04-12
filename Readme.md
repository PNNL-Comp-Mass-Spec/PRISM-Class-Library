# PRISM Class Library

The PRISM Class Library (PRISM.dll) is a collection of routines used by
many of the software tools that support the Data Management System
at PNNL.

The PRISM Windows class Library (PRISMWin.dll) is a set of Windows-specific utilities.

### NuGet

PRISM.Dll is available on NuGet at:
* https://www.nuget.org/packages/PRISM-Library/1.0.1

PRISMWin.dll is available on NuGet at:
* https://www.nuget.org/packages/PRISMWin-Library/

### Continuous Integration

[![Build status](https://ci.appveyor.com/api/projects/status/jksrpug2p49jev2i?svg=true)](https://ci.appveyor.com/project/alchemistmatt/prism-class-library)

### Important Classes in PRISM.dll

clsDBTools
* For running an ad-hoc query against a SQL Server database and obtaining the results

clsEventNotifier
* Abstract class that implements various status events, including status, debug, error, and warning events

clsExecuteDatabaseSP
* For executing a stored procedure in a SQL Server database and optionally obtaining the results

clsFileTools
* For copying, moving, and deleting files and folders
* Supports a queueing mechanism that uses lock files to avoid overloading a remote server with too many data transfer requests

clsParseCommandLine
* For parsing command line switches like /O and /Width:5

clsProgRunner
* For running a single program as an external process, including monitoring it with an internal thread

clsStackTraceFormatter
* For formatting stack traces from exceptions, either as a single line with methods separated by -:- or as multiple lines

ShareConnector
* For connecting a machine to an SMB/CIFS share using a password and user name

XmlSettingsFileAccessor
* For reading and writing settings in an Xml settings file

ZipTools 
* For programmatically creating and working with zip files

### Important Classes in PRISMWin.dll

clsDiskInfo
* Provides information on free disk space, both on local drives and on remote Windows shares

clsDotNETVersionChecker
* Reports the installed versions of the .NET framework on the local computer

clsProcessStats
* Reports the number of CPU cores in use by a given process


### CopyWithResume

Also included is the CopyWithResume console application.  Use this program to
copy large files between computers, with the ability to resume the copy
if the network connection is lost (or the copy process is manually terminated).

-------------------------------------------------------------------------------
Written by Matthew Monroe, Dave Clark, Gary Kiebel, and Nathan Trimble for the Department of Energy (PNNL, Richland, WA) \
Copyright 2017, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com \
Website: http://panomics.pnl.gov/ or http://www.sysbio.org/resources/staff/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except
in compliance with the License.  You may obtain a copy of the License at
http://www.apache.org/licenses/LICENSE-2.0
