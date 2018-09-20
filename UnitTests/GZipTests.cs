using System;
using System.IO;
using NUnit.Framework;

namespace PRISMTest
{
    [TestFixture]
    class GZipTests
    {

        [TestCase(@"C:\Windows\win.ini", false)]
        [TestCase(@"C:\Windows\win.ini", true)]
        public void TestGZipCompressLocalFile(string filePath, bool includeMetadata)
        {
            var fileToCompress = new FileInfo(filePath);
            if (!fileToCompress.Exists)
            {
                Assert.Ignore("File not found: " + fileToCompress.FullName);
            }

            TestGZipCompressExplicitDirectoryAndName(fileToCompress.FullName, includeMetadata, 0);
            Console.WriteLine();

            TestGZipCompressExplicitDirectory(fileToCompress.FullName, includeMetadata, 0);
            Console.WriteLine();

            TestGZipCompressDefaultName(fileToCompress.FullName, includeMetadata, 0);
        }

        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", false, 23358833)]
        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", true, 23358880)]
        [Category("PNL_Domain")]
        public void TestGZipCompressExplicitDirectoryAndName(string filePath, bool includeMetadata, long expectedSizeBytes)
        {
            var fileToCompress = FileRefs.GetTestFile(filePath);

            var tempDirectoryPath = Path.GetTempPath();
            var startTime = DateTime.UtcNow;
            string compressedFileName;

            if (includeMetadata)
            {
                compressedFileName = fileToCompress.Name + "_withMetadata.gz";
                Console.WriteLine("Compressing {0} using GZipCompressWithMetadata to create {1} in {2}", fileToCompress, compressedFileName, tempDirectoryPath);
                PRISM.FileTools.GZipCompressWithMetadata(fileToCompress, tempDirectoryPath, compressedFileName);
            }
            else
            {
                compressedFileName = fileToCompress.Name + ".gz";
                Console.WriteLine("Compressing {0} using GZipCompress to create {1} in {2}", fileToCompress, compressedFileName, tempDirectoryPath);
                PRISM.FileTools.GZipCompress(fileToCompress, tempDirectoryPath, compressedFileName);
            }

            var procTimeSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds;

            var compressedFile = new FileInfo(Path.Combine(tempDirectoryPath, compressedFileName));

            if (!compressedFile.Exists)
            {
                Assert.Fail("Compressed file not found: " + compressedFile.FullName);
            }

            Console.WriteLine("Compressed {0} in {1:F1} seconds to create {2}", fileToCompress, procTimeSeconds, compressedFile.FullName);
            Console.WriteLine(".gz file size: {0:#,###} bytes", compressedFile.Length);

            // Validate the newly created .gz file, then delete it and delete the validated round-robin file
            ValidateGZipFile(fileToCompress, compressedFile, tempDirectoryPath, expectedSizeBytes, includeMetadata, true);

        }

        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", false, 23358833)]
        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", true, 23358880)]
        [Category("PNL_Domain")]
        public void TestGZipCompressExplicitDirectory(string filePath, bool includeMetadata, long expectedSizeBytes)
        {
            var fileToCompress = FileRefs.GetTestFile(filePath);

            var tempDirectoryPath = Path.GetTempPath();
            var startTime = DateTime.UtcNow;

            if (includeMetadata)
            {
                Console.WriteLine("Compressing {0} using GZipCompressWithMetadata to create a .gz file in {1}", fileToCompress, tempDirectoryPath);
                PRISM.FileTools.GZipCompressWithMetadata(fileToCompress, tempDirectoryPath);
            }
            else
            {
                Console.WriteLine("Compressing {0} using GZipCompress to create a .gz file in {1}", fileToCompress, tempDirectoryPath);
                PRISM.FileTools.GZipCompress(fileToCompress, tempDirectoryPath);
            }

            var procTimeSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds;

            var compressedFile = new FileInfo(Path.Combine(tempDirectoryPath, fileToCompress.Name + ".gz"));

            if (!compressedFile.Exists)
            {
                Assert.Fail("Compressed file not found: " + compressedFile.FullName);
            }

            Console.WriteLine("Compressed {0} in {1:F1} seconds to create {2}", fileToCompress, procTimeSeconds, compressedFile.FullName);
            Console.WriteLine(".gz file size: {0:#,###} bytes", compressedFile.Length);

            // Validate the newly created .gz file, then delete it and delete the validated round-robin file
            ValidateGZipFile(fileToCompress, compressedFile, tempDirectoryPath, expectedSizeBytes, includeMetadata, true);
        }

        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", false, 23358833)]
        [TestCase(@"GZipTest\QC_Shew_10_01_e_3Mar10_Andromeda_09-10-15.mzML", true, 23358880)]
        [Category("PNL_Domain")]
        public void TestGZipCompressDefaultName(string filePath, bool includeMetadata, long expectedSizeBytes)
        {
            var fileToCompressRemote = FileRefs.GetTestFile(filePath);

            var tempDirectoryPath = Path.GetTempPath();
            var fileToCompressLocal = new FileInfo(Path.Combine(tempDirectoryPath, fileToCompressRemote.Name));

            fileToCompressRemote.CopyTo(fileToCompressLocal.FullName, true);

            var startTime = DateTime.UtcNow;

            if (includeMetadata)
            {
                Console.WriteLine("Compressing {0} using GZipCompressWithMetadata", fileToCompressLocal);
                PRISM.FileTools.GZipCompressWithMetadata(fileToCompressLocal);
            }
            else
            {
                Console.WriteLine("Compressing {0} using GZipCompress", fileToCompressLocal);
                PRISM.FileTools.GZipCompress(fileToCompressLocal);
            }

            var procTimeSeconds = DateTime.UtcNow.Subtract(startTime).TotalSeconds;

            var compressedFile = new FileInfo(Path.Combine(tempDirectoryPath, fileToCompressLocal.Name + ".gz"));

            if (!compressedFile.Exists)
            {
                Assert.Fail("Compressed file not found: " + compressedFile.FullName);
            }

            Console.WriteLine("Compressed {0} in {1:F1} seconds to create {2}", fileToCompressLocal, procTimeSeconds, compressedFile.FullName);
            Console.WriteLine(".gz file size: {0:#,###} bytes", compressedFile.Length);

            PRISM.ProgRunner.SleepMilliseconds(250);

            // Rename the file that we just compressed
            // This is required to avoid collisions when we call ValidateGZipFile

            var movedFileToCompress = new FileInfo(fileToCompressLocal.FullName + ".original");
            if (File.Exists(movedFileToCompress.FullName))
                File.Delete(movedFileToCompress.FullName);

            File.Move(fileToCompressLocal.FullName, movedFileToCompress.FullName);

            // Refresh movedFileToCompress but do not refresh fileToCompressLocal (since we only want movedFileToCompress to end in .original)
            movedFileToCompress.Refresh();

            // Validate the newly created .gz file, then delete it and delete the validated round-robin file
            ValidateGZipFile(fileToCompressLocal, compressedFile, tempDirectoryPath, expectedSizeBytes, includeMetadata, false);

            movedFileToCompress.Delete();
        }

        private void MoveFile(FileInfo fileToMove, string newFilePath)
        {
            var targetFile = new FileInfo(newFilePath);
            if (string.Equals(fileToMove.FullName, targetFile.FullName, StringComparison.OrdinalIgnoreCase))
            {
                // Names match; nothing to do
                return;
            }

            if (targetFile.Exists)
                targetFile.Delete();

            fileToMove.MoveTo(newFilePath);
        }

        private void ValidateGZipFile(
            FileInfo fileToCompress, FileInfo compressedFile,
            string tempDirectoryPath, long expectedSizeBytes,
            bool includedMetadata, bool usedExplicitNames)
        {

            PRISM.ProgRunner.SleepMilliseconds(250);

            // Decompress the newly created .gz file
            // Use both .GZipDecompressWithMetadata and .GZipDecompress

            FileInfo roundRobinFileWithMeta;
            FileInfo roundRobinFileNoMeta;

            if (usedExplicitNames)
            {
                PRISM.FileTools.GZipDecompressWithMetadata(compressedFile, tempDirectoryPath);
                roundRobinFileWithMeta = new FileInfo(Path.Combine(tempDirectoryPath, fileToCompress.Name));
                MoveFile(roundRobinFileWithMeta, roundRobinFileWithMeta.FullName + ".withmetadata");

                var roundRobinFilenameNoMeta = Path.GetFileNameWithoutExtension(fileToCompress.Name) + "_RoundRobinNoMeta" + Path.GetExtension(fileToCompress.Name);
                PRISM.FileTools.GZipDecompress(compressedFile, tempDirectoryPath, roundRobinFilenameNoMeta);
                roundRobinFileNoMeta = new FileInfo(Path.Combine(tempDirectoryPath, roundRobinFilenameNoMeta));
                MoveFile(roundRobinFileNoMeta, roundRobinFileNoMeta.FullName + ".nometadata");
            }
            else
            {
                PRISM.FileTools.GZipDecompressWithMetadata(compressedFile);
                roundRobinFileWithMeta = new FileInfo(Path.Combine(tempDirectoryPath, fileToCompress.Name));
                MoveFile(roundRobinFileWithMeta, roundRobinFileWithMeta.FullName + ".withmetadata");

                PRISM.FileTools.GZipDecompress(compressedFile, tempDirectoryPath);
                roundRobinFileNoMeta = new FileInfo(Path.Combine(tempDirectoryPath, fileToCompress.Name));
                MoveFile(roundRobinFileNoMeta, roundRobinFileNoMeta.FullName + ".nometadata");
            }

            if (!roundRobinFileWithMeta.Exists)
            {
                Assert.Fail("Round robin file with metadata not found: " + roundRobinFileWithMeta.FullName);
            }

            if (!roundRobinFileNoMeta.Exists)
            {
                Assert.Fail("Round robin file not found: " + roundRobinFileNoMeta.FullName);
            }

            // Compare file sizes
            Assert.AreEqual(fileToCompress.Length, roundRobinFileWithMeta.Length, "Round robin file size does not match the original file (.gz file with metadata)");
            Console.WriteLine("File to compress ({0}) and round robin file with metadata ({1}) are both {2:#,###} bytes", fileToCompress.Name, roundRobinFileWithMeta.Name, fileToCompress.Length);

            Assert.AreEqual(fileToCompress.Length, roundRobinFileNoMeta.Length, "Round robin file size does not match the original file (.gz file without metadata");
            Console.WriteLine("File to compress ({0}) and round robin file without metadata ({1}) are both {2:#,###} bytes", fileToCompress.Name, roundRobinFileNoMeta.Name, fileToCompress.Length);

            // Compare file modification times
            var timeDiffMsecWithMeta = Math.Abs(fileToCompress.LastWriteTimeUtc.Subtract(roundRobinFileWithMeta.LastWriteTimeUtc).TotalSeconds);
            Assert.AreEqual(timeDiffMsecWithMeta, 0, 5, "Round robin file size does not match the original file (.gz file with metadata)");
            Console.WriteLine("File to compress, modified {0}, matches round robin file with metadata, modified {1}", fileToCompress.LastWriteTime, roundRobinFileWithMeta.LastWriteTime);

            if (!includedMetadata)
            {
                var timeDiffMsecNoMeta = Math.Abs(fileToCompress.LastWriteTimeUtc.Subtract(roundRobinFileNoMeta.LastWriteTimeUtc).TotalSeconds);
                Assert.AreEqual(timeDiffMsecNoMeta, 0, 5, "Round robin file size does not match the original file (.gz file without metadata");
                Console.WriteLine("File to compress, modified {0}, matches round robin file without metadata, modified {1}", fileToCompress.LastWriteTime, roundRobinFileNoMeta.LastWriteTime);
            }

            if (expectedSizeBytes > 0)
            {
                // Compare actual .gz size to expected size
                Assert.AreEqual(expectedSizeBytes, compressedFile.Length, "Compressed .gz file size does not match expected size");
                Console.WriteLine("File to compress has the expected size, {0:#,###} bytes", expectedSizeBytes);
            }

            if (includedMetadata)
            {
                // Assure that the .gz file's date matches the current time
                var timeDiffMsecGz = Math.Abs(DateTime.UtcNow.Subtract(compressedFile.LastWriteTimeUtc).TotalSeconds);
                Assert.AreEqual(timeDiffMsecGz, 0, 60, "Compressed .gz file time does not match the current time; they should be close");
                Console.WriteLine("The modification time of the .gz file matches the current date/time; this is expected when including metadata: {0}", compressedFile.LastWriteTime);
            }
            else
            {
                // Assure that the .gz file's date matches the file to compress
                var timeDiffMsecGz = Math.Abs(fileToCompress.LastWriteTimeUtc.Subtract(compressedFile.LastWriteTimeUtc).TotalSeconds);
                Assert.AreEqual(timeDiffMsecGz, 0, 2.05, "Compressed .gz file time does not match the original file to compress; they should be close");
                Console.WriteLine("The modification time of the .gz file matches the compressed file's date/time; this is expected when not including metadata: {0}", compressedFile.LastWriteTime);
            }

            // Delete the files in the temp directory
            compressedFile.Delete();
            roundRobinFileWithMeta.Delete();
            roundRobinFileNoMeta.Delete();
        }
    }
}
