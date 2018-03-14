using System;
using System.IO;
using NUnit.Framework;

namespace PRISMTest
{
    [TestFixture]
    class GZipTests
    {

        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", false, 3434)]
        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", true, 3434)]
        public void TestGZipCompressExplicitName(string filePath, bool includeMetadata, int expectedSizeBytes)
        {
            // Get the full path to the LinuxTestFiles folder, 3 levels up from the cpuinfo test file
            var fileToCompress = FileRefs.GetTestFile(filePath);

            var tempDirectoryPath = Path.GetTempPath();
            var startTime = DateTime.UtcNow;
            string compressedFileName;

            if (includeMetadata)
            {
                compressedFileName = fileToCompress.Name + "_withMetadata.gz";
                PRISM.clsFileTools.GZipCompressWithMetadata(fileToCompress, tempDirectoryPath, compressedFileName);
            }
            else
            {
                compressedFileName = fileToCompress.Name + ".gz";
                PRISM.clsFileTools.GZipCompress(fileToCompress, tempDirectoryPath, compressedFileName);
            }

            var procTimeSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds;

            var compressedFile = new FileInfo(Path.Combine(tempDirectoryPath, compressedFileName));

            if (!compressedFile.Exists)
            {
                Assert.Fail("Compressed file not found: " + compressedFile.FullName);
            }

            Console.WriteLine("Compressed {0} in {1} seconds to create {2}", fileToCompress, procTimeSeconds, compressedFile.FullName);
            Console.WriteLine(".gz file size: {0} bytes", compressedFile.Length);

            // Validate the newly created .gz file, then delete it and delete the validated round-robin file
            ValidateGZipFile(fileToCompress, compressedFile, tempDirectoryPath);

        }

        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", false, 3434)]
        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", true, 3434)]
        public void TestGZipCompressDefaultName(string filePath, bool includeMetadata, int expectedSizeBytes)
        {
            // Get the full path to the LinuxTestFiles folder, 3 levels up from the cpuinfo test file
            var fileToCompress = FileRefs.GetTestFile(filePath);

            var tempDirectoryPath = Path.GetTempPath();
            var startTime = DateTime.UtcNow;

            if (includeMetadata)
            {
                PRISM.clsFileTools.GZipCompressWithMetadata(fileToCompress, tempDirectoryPath);
            }
            else
            {
                PRISM.clsFileTools.GZipCompress(fileToCompress, tempDirectoryPath);
            }

            var procTimeSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds;

            var compressedFile = new FileInfo(Path.Combine(tempDirectoryPath, fileToCompress.Name + ".gz"));

            if (!compressedFile.Exists)
            {
                Assert.Fail("Compressed file not found: " + compressedFile.FullName);
            }

            Console.WriteLine("Compressed {0} in {1} seconds to create {2}", fileToCompress, procTimeSeconds, compressedFile.FullName);
            Console.WriteLine(".gz file size: {0} bytes", compressedFile.Length);

            // Validate the newly created .gz file, then delete it and delete the validated round-robin file
            ValidateGZipFile(fileToCompress, compressedFile, tempDirectoryPath);
        }

        private void ValidateGZipFile(FileInfo fileToCompress, FileInfo compressedFile, string tempDirectoryPath)
        {

            PRISM.clsProgRunner.SleepMilliseconds(250);

            // Decompress the newly created .gz file
            // Use both .GZipDecompressWithMetadata and .GZipDecompress

            var roundRobinFilenameWithMeta = fileToCompress.Name;
            var roundRobinFilenameNoMeta = Path.GetFileNameWithoutExtension(fileToCompress.Name) + "_RoundRobinNoMeta" + Path.GetExtension(fileToCompress.Name);

            PRISM.clsFileTools.GZipDecompressWithMetadata(compressedFile, tempDirectoryPath);
            PRISM.clsFileTools.GZipDecompress(compressedFile, tempDirectoryPath, roundRobinFilenameNoMeta);

            var roundRobinFileWithMeta = new FileInfo(Path.Combine(tempDirectoryPath, roundRobinFilenameWithMeta));
            var roundRobinFileNoMeta = new FileInfo(Path.Combine(tempDirectoryPath, roundRobinFilenameNoMeta));

            if (!roundRobinFileWithMeta.Exists)
            {
                Assert.Fail("Round robin file with metadata not found: " + roundRobinFileWithMeta.FullName);
            }

            if (!roundRobinFileNoMeta.Exists)
            {
                Assert.Fail("Round robin file not found: " + roundRobinFileNoMeta.FullName);
            }

            Assert.AreEqual(fileToCompress.Length, roundRobinFileWithMeta.Length, "Round robin file size does not match the original file (.gz file with metadata)");

            Assert.AreEqual(fileToCompress.Length, roundRobinFileNoMeta.Length, "Round robin file size does not match the original file (.gz file without metadata");

            // Delete the files in the temp directory
            compressedFile.Delete();
            roundRobinFileWithMeta.Delete();
            roundRobinFileNoMeta.Delete();
        }
    }
}
