using System;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    internal class FileRenameTests
    {
        private FileTools mFileTools;

        [OneTimeSetUp]
        public void Setup()
        {
            mFileTools = new FileTools("PrismUnitTests", 1);
        }

        /// <summary>
        /// Test renaming a file, including waiting for a locked file to be released by another thread
        /// </summary>
        /// <param name="targetFilePath"></param>
        /// <param name="newFileName"></param>
        /// <param name="retryCount"></param>
        /// <param name="secondsToLockFile"></param>
        /// <param name="expectSuccessfulRename"></param>
        [TestCase(@"C:\Temp\PrismLibraryTestFileA.txt", "PrismLibraryTestFileB.txt", 3, 0)]
        [TestCase(@"C:\Temp\PrismLibraryTestFileC.txt", "PrismLibraryTestFileD.txt", 3, 3)]
        [TestCase(@"C:\Temp\PrismLibraryTestFileE.txt", "PrismLibraryTestFileF.txt", 3, 7)]
        [TestCase(@"C:\Temp\PrismLibraryTestFileG.txt", "PrismLibraryTestFileH.txt", 2, 15, false)]
        public void RenameFile(string targetFilePath, string newFileName, int retryCount, int secondsToLockFile, bool expectSuccessfulRename = true)
        {
            var targetFile = new FileInfo(targetFilePath);

            if (targetFile.Directory == null)
            {
                Assert.Fail("Unable to determine the parent directory of file " + targetFilePath);
            }

            if (!targetFile.Directory.Exists)
                mFileTools.CreateDirectoryIfNotExists(targetFile.Directory.FullName);

            if (!targetFile.Exists)
            {
                using var writer = new StreamWriter(new FileStream(targetFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read));

                writer.WriteLine("Test file to be renamed from {0} to {1}", targetFile.Name, newFileName);
            }

            var newFile = new FileInfo(Path.Combine(targetFile.Directory.FullName, newFileName));

            if (newFile.Exists)
            {
                Console.WriteLine("Deleting existing target file: " + newFile.FullName);
                newFile.Delete();
                Console.WriteLine();
            }

            Console.WriteLine("Renaming {0} to {1}", targetFile.FullName, newFile.Name);

            var lockedReader = secondsToLockFile > 0
                ? new FileLockUtility(targetFile.FullName, secondsToLockFile)
                : new FileLockUtility(string.Empty, secondsToLockFile);

            if (secondsToLockFile > 0)
            {
                Console.WriteLine();

                // Wait 1.5 seconds to give the lockedReader time to open the file
                ConsoleMsgUtils.SleepSeconds(1.5);
            }

            var success = mFileTools.RenameFileWithRetry(targetFile, newFileName, retryCount, out var errorMessage);

            Console.WriteLine();

            if (!success)
            {
                if (expectSuccessfulRename)
                {
                    Assert.Fail(errorMessage);
                }
                else
                {
                    Console.WriteLine("As expected, was unable to rename the file since locked:");
                    Console.WriteLine(errorMessage);

                    lockedReader.CloseFileNow();

                    // Wait 1.5 seconds to give the lockedReader time to close the file and display a status message
                    ConsoleMsgUtils.SleepSeconds(1.5);
                    return;
                }
            }

            newFile.Refresh();

            if (!newFile.Exists)
            {
                Assert.Fail("Renamed file not found: " + newFile.FullName);
            }

            Console.WriteLine("Successfully renamed the file to " + newFile.FullName);
        }
    }
}
