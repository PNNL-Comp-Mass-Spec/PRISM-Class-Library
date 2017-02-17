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

            var filesToSkip = new List<string> {"H_sapiens_Uniprot_trembl_2015-10-14.fasta"};

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
    }
}
