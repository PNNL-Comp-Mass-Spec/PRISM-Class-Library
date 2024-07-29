using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PRISM;

// ReSharper disable StringLiteralTypo

namespace PRISMTest
{
    [TestFixture]
    internal class TestPathUtils
    {
        // Ignore Spelling: proc, subdir, AppVeyor, Ubuntu, cpuinfo, meminfo, Xymon

        [TestCase("/proc/12343/stat", "/proc/12343/stat")]
        [TestCase(@"/proc/subdir\filename", "/proc/subdir/filename")]
        [TestCase(@"/proc\subdir\filename.txt", "/proc/subdir/filename.txt")]
        public void TestAssureLinuxPath(string pathSpec, string expectedResult)
        {
            var result = PathUtils.AssureLinuxPath(pathSpec);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [TestCase(@"C:\DMS_WorkDir/12343/stat", @"C:\DMS_WorkDir\12343\stat")]
        [TestCase(@"C:\DMS_WorkDir/subdir\filename", @"C:\DMS_WorkDir\subdir\filename")]
        [TestCase(@"C:\DMS_WorkDir/subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir\subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir/subdir/filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir\subdir/filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        public void TestAssureWindowsPath(string pathSpec, string expectedResult)
        {
            var result = PathUtils.AssureWindowsPath(pathSpec);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [TestCase("/proc/12343", "stat", "/proc/12343/stat")]
        [TestCase("/proc/12343/", "stat", "/proc/12343/stat")]
        [TestCase("/proc/12343", "/stat", "/stat")]
        [TestCase("/proc/12343/", "/stat/", "/stat/")]
        [TestCase("/share/item", "dataset/results", "/share/item/dataset/results")]
        [TestCase("/share/item/", "dataset/results", "/share/item/dataset/results")]
        [TestCase("/share/item", "/dataset/results", "/dataset/results")]
        [TestCase("/share/item/", "/dataset/results/", "/dataset/results/")]
        public void TestCombineLinuxPaths(string path1, string path2, string expectedResult)
        {
            var result = PathUtils.CombineLinuxPaths(path1, path2);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [TestCase(@"C:\DMS_WorkDir", "subdir", @"C:\DMS_WorkDir\subdir")]
        [TestCase(@"C:\DMS_WorkDir\", "subdir", @"C:\DMS_WorkDir\subdir")]
        [TestCase(@"C:\DMS_WorkDir", @"subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir\", @"subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        public void TestCombineWindowsPaths(string path1, string path2, string expectedResult)
        {
            var result = PathUtils.CombineWindowsPaths(path1, path2);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [TestCase(@"C:\DMS_WorkDir", "subdir", '\\', @"C:\DMS_WorkDir\subdir")]
        [TestCase(@"C:\DMS_WorkDir\", "subdir", '\\', @"C:\DMS_WorkDir\subdir")]
        [TestCase(@"C:\DMS_WorkDir", @"subdir\filename.txt", '\\', @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir\", @"subdir\filename.txt", '\\', @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase("/proc/12343", "stat", '/', "/proc/12343/stat")]
        [TestCase("/proc/12343/", "stat", '/', "/proc/12343/stat")]
        [TestCase("/proc/12343", "/stat", '/', "/stat")]
        [TestCase("/proc/12343/", "/stat/", '/', "/stat/")]
        [TestCase("/share/item", "dataset/results", '/', "/share/item/dataset/results")]
        [TestCase("/share/item/", "dataset/results", '/', "/share/item/dataset/results")]
        public void TestCombinePaths(string path1, string path2, char directorySepChar, string expectedResult)
        {
            var result = PathUtils.CombinePaths(path1, path2, directorySepChar);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [TestCase(@"\\proto-2\UnitTest_Files\PRISM", "File*", "FileCopyTest, FileCopyTestWithLocks", false, 2)]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM", "File*", "FileCopyTest, FileCopyTestWithLocks", true, 4)]
        [Category("PNL_Domain")]
        public void TestFindDirectoriesWildcardInternal(string directoryPath, string directoryMask, string expectedDirectoryNames, bool recurse, int expectedDirectoryCount)
        {
            TestFindDirectoriesWildcardWork(directoryPath, directoryMask, expectedDirectoryNames, recurse, expectedDirectoryCount);
        }

        [TestCase(@"C:\Windows", "System*", "System, System32, SystemApps, SystemResources")]
        [TestCase(@"C:\Windows\", "System*", "System, System32, SystemApps, SystemResources")]
        public void TestFindDirectoriesWildcard(string directoryPath, string directoryMask, string expectedDirectoryNames)
        {
            TestFindDirectoriesWildcardWork(directoryPath, directoryMask, expectedDirectoryNames, false);
        }

        private void TestFindDirectoriesWildcardWork(string directoryPath, string directoryMask, string expectedDirectoryNames, bool recurse, int expectedDirectoryCount = 0)
        {
            DirectoryInfo directory;

            if (directoryPath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD && !SystemInfo.IsLinux)
            {
                directory = new DirectoryInfo(NativeIOFileTools.GetWin32LongPath(directoryPath));
            }
            else
            {
                directory = new DirectoryInfo(directoryPath);
            }

            // Combine the directory path and the directory mask
            var pathSpec = Path.Combine(directory.FullName, directoryMask);

            var directories1 = PathUtils.FindDirectoriesWildcard(pathSpec, recurse);

            // Separately, send the DirectoryInfo object plus the directory mask
            var directories2 = PathUtils.FindDirectoriesWildcard(directory, directoryMask, recurse);

            Console.WriteLine("Directories via pathSpec: {0}", directories1.Count);
            Console.WriteLine("Directories via directoryMask: {0}", directories2.Count);

            Assert.That(directories2.Count, Is.EqualTo(directories1.Count), $"Directory count mismatch; {directories1.Count} vs. {directories2.Count}");

            if (string.IsNullOrWhiteSpace(expectedDirectoryNames))
                return;

            // Make sure we found directories with the expected names
            var expectedDirectoryList = expectedDirectoryNames.Split(',');

            var foundDirectoryNames = new SortedSet<string>();

            foreach (var foundDirectory in directories1)
            {
                if (foundDirectoryNames.Contains(foundDirectory.Name))
                    continue;

                foundDirectoryNames.Add(foundDirectory.Name);

                if (foundDirectoryNames.Count == 1)
                    Console.Write(foundDirectory.Name);
                else if (foundDirectoryNames.Count <= 5)
                    Console.Write(", " + foundDirectory.Name);
                else if (foundDirectoryNames.Count == 6)
                    Console.WriteLine(" ...");
            }

            Console.WriteLine();
            Console.WriteLine("Found {0} directories (recurse={1})", directories1.Count, recurse);

            foreach (var expectedDirectory in expectedDirectoryList)
            {
                if (!foundDirectoryNames.Contains(expectedDirectory.Trim()))
                    Assert.Fail($"Did not find an expected directory in {directoryPath}: {expectedDirectory}");
            }

            if (expectedDirectoryCount > 0)
                Assert.GreaterOrEqual(directories1.Count, expectedDirectoryCount, $"Found {directories1.Count} directories; expected to find {expectedDirectoryCount}");
        }

        [TestCase(@"\\proto-2\UnitTest_Files\PRISM", "*.fasta", "HumanContam.fasta, MP_06_01.fasta, Tryp_Pig_Bov.fasta", false)]
        [Category("PNL_Domain")]
        public void TestFindFilesWildcardInternal(string directoryPath, string fileMask, string expectedFileNames, bool recurse)
        {
            TestFindFilesWildcardWork(directoryPath, fileMask, expectedFileNames, recurse);
        }

        [TestCase(@"c:\windows", "*.ini", "system.ini, win.ini")]
        [TestCase(@"c:\windows\", "*.ini", "system.ini, win.ini")]
        public void TestFindFilesWildcard(string directoryPath, string fileMask, string expectedFileNames)
        {
            TestFindFilesWildcardWork(directoryPath, fileMask, expectedFileNames, false);
        }

        /// <summary>
        /// Find files recursively below C:\Windows
        /// Only run this inside PNNL because it is slow on AppVeyor
        /// </summary>
        /// <param name="directoryPath">Directory path</param>
        /// <param name="fileMask">File mask</param>
        /// <param name="expectedFileNames">Expected file names</param>
        [TestCase(@"c:\windows", "*.ini", "system.ini, win.ini")]
        [TestCase(@"c:\windows\", "*.dll", "perfos.dll, perfnet.dll")]
        [Category("PNL_Domain")]
        public void TestFindFilesRecurse(string directoryPath, string fileMask, string expectedFileNames)
        {
            TestFindFilesWildcardWork(directoryPath, fileMask, expectedFileNames, true);
        }

        [TestCase(@"UnitTests\Data\LinuxTestFiles\Ubuntu\proc\cpuinfo", "*info", "cpuinfo, meminfo", 6)]
        public void TestFindFilesWildcardRelativeDirectory(string filePath, string fileMask, string expectedFileNames, int expectedFileCount)
        {
            // Get the full path to the LinuxTestFiles directory, 3 levels up from the cpuinfo test file
            var cpuInfoFile = FileRefs.GetTestFile(filePath);

            var currentDirectory = cpuInfoFile.Directory;

            if (currentDirectory == null)
                Assert.Fail("Cannot determine the parent directory of " + cpuInfoFile.FullName);

            for (var parentCount = 1; parentCount < 3; parentCount++)
            {
                var parentCandidate = currentDirectory.Parent;

                if (parentCandidate == null)
                    Assert.Fail("Cannot determine the parent directory of " + currentDirectory.FullName);

                currentDirectory = parentCandidate;
            }

            TestFindFilesWildcardWork(currentDirectory.FullName, fileMask, expectedFileNames, true, expectedFileCount);
        }

        private void TestFindFilesWildcardWork(string directoryPath, string fileMask, string expectedFileNames, bool recurse, int expectedFileCount = 0)
        {
            DirectoryInfo directory;

            if (directoryPath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD && !SystemInfo.IsLinux)
            {
                directory = new DirectoryInfo(NativeIOFileTools.GetWin32LongPath(directoryPath));
            }
            else
            {
                directory = new DirectoryInfo(directoryPath);
            }

            // Combine the directory path and the file mask
            var pathSpec = Path.Combine(directory.FullName, fileMask);

            var files1 = PathUtils.FindFilesWildcard(pathSpec, recurse);

            // Separately, send the DirectoryInfo object plus the file mask
            var files2 = PathUtils.FindFilesWildcard(directory, fileMask, recurse);

            int allowedVariance;

            // The results should be the same, though the number of .ini files in the Windows directory can vary so we allow some variance
            if (directoryPath.IndexOf(@"\windows", StringComparison.OrdinalIgnoreCase) >= 0 || files1.Count > 1000)
                allowedVariance = (int)Math.Floor(files1.Count * 0.05);
            else
                allowedVariance = 0;

            Console.WriteLine("Files via pathSpec: {0}", files1.Count);
            Console.WriteLine("Files via fileMask: {0}", files2.Count);

            var fileCountDifference = Math.Abs(files1.Count - files2.Count);

            Assert.LessOrEqual(fileCountDifference, allowedVariance, $"File count mismatch; {allowedVariance} > {fileCountDifference}");

            if (string.IsNullOrWhiteSpace(expectedFileNames))
                return;

            // Make sure we found files with the expected names
            var expectedFileList = expectedFileNames.Split(',');

            var foundFileNames = new SortedSet<string>();

            foreach (var foundFile in files1)
            {
                if (foundFileNames.Contains(foundFile.Name))
                    continue;

                foundFileNames.Add(foundFile.Name);

                if (foundFileNames.Count == 1)
                    Console.Write(foundFile.Name);
                else if (foundFileNames.Count <= 5)
                    Console.Write(", " + foundFile.Name);
                else if (foundFileNames.Count == 6)
                    Console.WriteLine(" ...");
            }

            Console.WriteLine();
            Console.WriteLine("Found {0} files (recurse={1})", files1.Count, recurse);

            foreach (var expectedFile in expectedFileList)
            {
                if (!foundFileNames.Contains(expectedFile.Trim()))
                    Assert.Fail($"Did not find an expected file in {directoryPath}: {expectedFile}");
            }

            if (expectedFileCount > 0)
                Assert.GreaterOrEqual(files1.Count, expectedFileCount, $"Found {files1.Count} files; expected to find {expectedFileCount}");
        }

        [TestCase("Results.txt", "*.txt", true)]
        [TestCase("Results.txt", "*.zip", false)]
        [TestCase("Results.txt", "*", true)]
        [TestCase("MSGFDB_PartTryp_MetOx_20ppmParTol.txt", "MSGF*", true)]
        [TestCase("MSGFDB_PartTryp_MetOx_20ppmParTol.txt", "XT*", false)]
        public void TestFitsMask(string fileName, string fileMask, bool expectedResult)
        {
            var result = PathUtils.FitsMask(fileName, fileMask);

            Assert.That(result, Is.EqualTo(expectedResult));

            if (result)
                Console.WriteLine("{0} matches\n{1}", fileName, fileMask);
            else
                Console.WriteLine("{0} does not match\n{1}", fileName, fileMask);
        }

        [TestCase(@"C:\Users\Public\Pictures", @"C:\Users\Public", "Pictures")]
        [TestCase(@"C:\Users\Public\Pictures\", @"C:\Users\Public", "Pictures")]
        [TestCase(@"C:\Windows\System32", @"C:\Windows", "System32")]
        [TestCase(@"C:\Windows", @"C:\", "Windows")]
        [TestCase(@"C:\Windows\", @"C:\", "Windows")]
        [TestCase(@"C:\", "", @"C:\")]
        [TestCase(@"C:\DMS_WorkDir", @"C:\", "DMS_WorkDir")]
        [TestCase(@"C:\DMS_WorkDir\", @"C:\", "DMS_WorkDir")]
        [TestCase(@"C:\DMS_WorkDir\SubDir", @"C:\DMS_WorkDir", "SubDir")]
        [TestCase(@"C:\DMS_WorkDir\SubDir\", @"C:\DMS_WorkDir", "SubDir")]
        [TestCase(@"Microsoft SQL Server\Client SDK\ODBC", @"Microsoft SQL Server\Client SDK", "ODBC")]
        [TestCase(@"TortoiseGit\bin", "TortoiseGit", "bin")]
        [TestCase(@"TortoiseGit\bin\", "TortoiseGit", "bin")]
        [TestCase("TortoiseGit", "", "TortoiseGit")]
        [TestCase(@"TortoiseGit\", "", "TortoiseGit")]
        [TestCase(@"\\server\Share\Directory", @"\\server\Share", "Directory")]
        [TestCase(@"\\server\Share\Directory\", @"\\server\Share", "Directory")]
        [TestCase(@"\\server\Share", "", "Share")]
        [TestCase(@"\\server\Share\", "", "Share")]
        [TestCase("/etc/fonts/conf.d", "/etc/fonts", "conf.d")]
        [TestCase("/etc/fonts/conf.d/", "/etc/fonts", "conf.d")]
        [TestCase("/etc/fonts", "/etc", "fonts")]
        [TestCase("/etc/fonts/", "/etc", "fonts")]
        [TestCase("/etc", "/", "etc")]
        [TestCase("/etc/", "/", "etc")]
        [TestCase("/", "", "")]
        [TestCase("log/xymon", "log", "xymon")]
        [TestCase("log/xymon/old", "log/xymon", "old")]
        [TestCase("log", "", "log")]
        public void TestGetParentDirectoryPath(string directoryPath, string expectedParentPath, string expectedDirectoryName)
        {
            var parentPath = PathUtils.GetParentDirectoryPath(directoryPath, out var directoryName);

            if (string.IsNullOrWhiteSpace(parentPath))
            {
                Console.WriteLine("{0} has no parent; name is {1}", directoryPath, directoryName);
            }
            else
            {
                Console.WriteLine("{0} has parent {1} and name {2}", directoryPath, parentPath, directoryName);
            }

            Assert.That(parentPath, Is.EqualTo(expectedParentPath), "Parent path mismatch");
            Assert.That(directoryName, Is.EqualTo(expectedDirectoryName), "Directory name mismatch");
        }

        [TestCase(@"C:\DMS_WorkDir\SubDir", false)]
        [TestCase(@"C:\DMS_WorkDir\Result Directory", true)]
        [TestCase(@"C:\DMS_WorkDir\Result Directory\", true)]
        [TestCase(@"C:\DMS_WorkDir\ResultDirectory\filename.txt", false)]
        [TestCase(@"C:\DMS_WorkDir\Result Directory\filename.txt", true)]
        [TestCase("/proc/12343", false)]
        [TestCase("/proc/Result Directory", true)]
        [TestCase("/proc/ResultDirectory/filename.txt", false)]
        [TestCase("/proc/Result Directory/filename.txt", true)]
        public void TestPossiblyQuotePath(string filePath, bool expectedQuoteRequired)
        {
            var quotedPath = PathUtils.PossiblyQuotePath(filePath);

            var pathWasQuoted = !string.Equals(filePath, quotedPath);

            Assert.That(pathWasQuoted, Is.EqualTo(expectedQuoteRequired), "Mismatch for " + filePath);
        }

        [TestCase(@"C:\DMS_WorkDir\filename.txt", "UpdatedFile.txt", @"C:\DMS_WorkDir\UpdatedFile.txt")]
        [TestCase(@"C:\DMS_WorkDir\Results Directory\filename.txt", "UpdatedFile.txt", @"C:\DMS_WorkDir\Results Directory\UpdatedFile.txt")]
        public void TestReplaceFilenameInPath(string existingFilePath, string newFileName, string expectedResult)
        {
            var newPath = PathUtils.ReplaceFilenameInPath(existingFilePath, newFileName);
            Assert.That(newPath, Is.EqualTo(expectedResult));
        }
    }
}
