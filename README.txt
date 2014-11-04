== Overview ==

The PRISM Class Library is a collection of routines used by
many of the software tools that support the Data Management System
at PNNL.  

== Important Classes in PRISM.dll ==

clsFileTools
	- For copying, moving, and deleting files and folders
	- Supports a queueing mechanism that uses lock files to avoid 
	  overloading a remote server with too many data transfer requests

clsProgRunner
	- For running a single program as an external process,
      including monitoring it with an internal thread

ShareConnector
	- For connecting a machine to an SMB/CIFS share using a password and user name

XmlSettingsFileAccessor
	- For reading and writing settings in an Xml settings file

ZipTools 
	- For programmatically creating and working with zip files


== CopyWithResume ==

Also included is the CopyWithResume console application.  Use this program to
copy large files between computers, with the ability to resume the copy
if the network connection is lost (or the copy process is manually terminated).

-------------------------------------------------------------------------------
Written by Matthew Monroe, Dave Clark, Gary Kiebel, and Nathan Trimble 
for the Department of Energy (PNNL, Richland, WA)
Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
Website: http://panomics.pnnl.gov/ or http://www.sysbio.org/resources/staff/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
