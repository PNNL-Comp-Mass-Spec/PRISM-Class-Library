using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class FileCopyTests
    {
        private clsFileTools mFileTools;

        [OneTimeSetUp]
        public void Setup()
        {
            mFileTools = new clsFileTools();
        }

        [TestCase(@"\\proto-2\UnitTest_Files\PRISM", @"\\proto-2\UnitTest_Files\PRISM\FolderCopyTest")]
        public void CopyDirectory(string sourceFolderPath, string targetFolderPath)
        {
            var sourceFolder = new DirectoryInfo(sourceFolderPath);
            if (!sourceFolder.Exists)
            {
                Assert.Fail("Source directory not found: " + sourceFolderPath);
            }

            var filesToSkip = new List<string> { "H_sapiens_Uniprot_trembl_2015-10-14.fasta" };

            var targetFolder = new DirectoryInfo(targetFolderPath);
            if (targetFolder.Exists)
                targetFolder.Delete(true);

            mFileTools.CopyDirectory(sourceFolderPath, targetFolderPath, true, filesToSkip);
        }

        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\Tryp_Pig_Bov.fasta", @"\\proto-2\UnitTest_Files\PRISM\FileCopyTest\Tryp_Pig_Bov.fasta")]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\HumanContam.fasta", @"\\proto-2\UnitTest_Files\PRISM\FileCopyTest\HumanContam_Renamed.fasta")]
        public void CopyFile(string sourceFilePath, string targetFilePath)
        {
            var sourceFile = new FileInfo(sourceFilePath);
            if (!sourceFile.Exists)
            {
                Assert.Fail("Source file not found: " + sourceFile);
            }

            var targetFile = new FileInfo(targetFilePath);
            if (targetFile.Exists)
                targetFile.Delete();

            mFileTools.CopyFile(sourceFilePath, targetFilePath, false);

            System.Threading.Thread.Sleep(150);
            mFileTools.CopyFile(sourceFilePath, targetFilePath, true);

            System.Threading.Thread.Sleep(150);

            // Copy the file again, but with overwrite = false
            // This should raise an exception
            bool exceptionRaised;

            try
            {
                mFileTools.CopyFile(sourceFilePath, targetFilePath, false);
                exceptionRaised = false;
            }
            catch (Exception)
            {
                exceptionRaised = true;
            }

            Assert.IsTrue(exceptionRaised, "File copy with overwrite = false did not raise an exception; it should have");



        }

        [TestCase(@"\\gigasax\DMS_Organism_Files\Homo_sapiens\Fasta\H_sapiens_Uniprot_trembl_2015-10-14.fasta",
                  @"\\proto-2\UnitTest_Files\PRISM\FileCopyTestWithLocks\H_sapiens_Uniprot_trembl_2015-10-14.fasta")]
        [TestCase(@"\\gigasax\DMS_Organism_Files\Homo_sapiens\Fasta\H_sapiens_Uniprot_SPROT_2015-04-22.fasta",
                  @"\\proto-2\UnitTest_Files\PRISM\FileCopyTestWithLocks\H_sapiens_Uniprot_SPROT_2015-04-22.fasta")]
        [TestCase(@"C:\Windows\win.ini",
                  @"C:\temp\win.ini")]
        public void CopyFileUsingLocks(string sourceFilePath, string targetFilePath)
        {
            var sourceFile = new FileInfo(sourceFilePath);
            if (!sourceFile.Exists)
            {
                Assert.Fail("Source file not found: " + sourceFile);
            }

            mFileTools.CopyFileUsingLocks(sourceFilePath, targetFilePath, "PrismUnitTests", true);

            System.Threading.Thread.Sleep(150);
            mFileTools.CopyFileUsingLocks(sourceFilePath, targetFilePath, "PrismUnitTests", false);

            System.Threading.Thread.Sleep(150);

            // Copy the file again, but with overwrite = false
            // This should raise an exception
            bool exceptionRaised;

            try
            {
                mFileTools.CopyFile(sourceFilePath, targetFilePath, false);
                exceptionRaised = false;
            }
            catch (Exception)
            {
                exceptionRaised = true;
            }

            Assert.IsTrue(exceptionRaised, "File copy with overwrite = false did not raise an exception; it should have");

        }


        [TestCase(@"C:\Temp")]
        [TestCase(@"C:\Temp\")]
        [TestCase(@"\\proto-2\UnitTest_Files")]
        [TestCase(@"\\proto-2\UnitTest_Files\")]
        [TestCase(@"\\protoapps\UserData\Matt\")]
        public void GetDriveFreeSpaceForDirectory(string directoryPath)
        {
            long freeBytesAvailableToUser;
            long totalDriveCapacityBytes;
            long totalNumberOfFreeBytes;

            var success = PRISMWin.clsDiskInfo.GetDiskFreeSpace(directoryPath, out freeBytesAvailableToUser, out totalDriveCapacityBytes, out totalNumberOfFreeBytes);
            if (!success)
                Assert.Fail("GetDiskFreeSpace reported false");

            Console.WriteLine("Free space at {0} is {1}; space for user is {2}",
                directoryPath,
                clsFileTools.BytesToHumanReadable(totalNumberOfFreeBytes),
                clsFileTools.BytesToHumanReadable(freeBytesAvailableToUser));

        }

        [TestCase(@"C:\Temp\Testfile.txt", false)]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\TestFile.txt", false)]
        [TestCase(@"\\protoapps\UserData\Matt\TestFile.txt", false)]
        [TestCase(@"\\protoapps\UserData\Matt\TestFile.txt", true)]
        public void GetDriveFreeSpaceForFile(string targetFilePath, bool reportFreeSpaceAvailableToUser)
        {
            long freeSpaceBytes;
            string errorMessage;

            var success = PRISMWin.clsDiskInfo.GetDiskFreeSpace(targetFilePath, out freeSpaceBytes, out errorMessage, reportFreeSpaceAvailableToUser);

            var directoryPath = new FileInfo(targetFilePath).DirectoryName;

            if (!success)
            {
                Assert.Fail("GetDiskFreeSpace reported false: " + errorMessage);
            }

            Console.WriteLine("Free space at {0} is {1} (ReportFreeSpaceAvailableToUse = {2}))",
                directoryPath, clsFileTools.BytesToHumanReadable(freeSpaceBytes), reportFreeSpaceAvailableToUser);

        }

        [TestCase(@"C:\Temp\Testfile.txt", 0)]
        [TestCase(@"C:\Temp\TestHugeFile.raw", 100000)]
        [TestCase(@"C:\Temp\TestHugeFile.raw", 1000000)]
        [TestCase(@"C:\Temp\TestHugeFile.raw", 10000000)]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\TestFile.txt", 150)]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\TestHugeFile.raw", 100000)]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\TestHugeFile.raw", 1000000)]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\TestHugeFile.raw", 10000000)]
        [TestCase(@"\\protoapps\UserData\Matt\TestFile.txt", 0)]
        [TestCase(@"\\protoapps\UserData\Matt\TestFile.txt", 500)]
        public void ValidateFreeDiskSpace(string targetFilePath, long minimumFreeSpaceMB)
        {
            string errorMessage;

            long currentDiskFreeSpaceBytes;
            var success = PRISMWin.clsDiskInfo.GetDiskFreeSpace(targetFilePath, out currentDiskFreeSpaceBytes, out errorMessage);
            if (!success)
            {
                Assert.Fail("GetDiskFreeSpace reported false: " + errorMessage);
            }

            var safeToCopy = clsFileTools.ValidateFreeDiskSpace(targetFilePath, minimumFreeSpaceMB, currentDiskFreeSpaceBytes, out errorMessage);

            var sufficientOrNot = safeToCopy ? "sufficient" : "insufficient";

            Console.WriteLine("Target drive has {0} free space to copy {1} file {2}; {3} free",
                sufficientOrNot,
                clsFileTools.BytesToHumanReadable(minimumFreeSpaceMB * 1024 * 1024),
                targetFilePath,
                clsFileTools.BytesToHumanReadable(currentDiskFreeSpaceBytes));


        }

    }
}
