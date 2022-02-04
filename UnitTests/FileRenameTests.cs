﻿using System;
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
            var fileToRename = new FileInfo(targetFilePath);

            if (fileToRename.Directory == null)
            {
                Assert.Fail("Unable to determine the parent directory of file " + targetFilePath);
            }

            var newFileInfo = new FileInfo(Path.Combine(fileToRename.Directory.FullName, newFileName));

            RenameFileWork(fileToRename, newFileInfo, retryCount, secondsToLockFile, expectSuccessfulRename);
        }

        /// <summary>
        /// Test renaming a file, moving it to a new directory, including waiting for a locked file to be released by another thread
        /// </summary>
        /// <param name="targetFilePath"></param>
        /// <param name="newFilePath"></param>
        /// <param name="retryCount"></param>
        /// <param name="secondsToLockFile"></param>
        /// <param name="expectSuccessfulRename"></param>
        [TestCase(@"C:\Temp\PrismLibraryTestFileA.txt", @"C:\Temp\Renamed\PrismLibraryTestFileB.txt", 3, 0)]
        [TestCase(@"C:\Temp\PrismLibraryTestFileC.txt", @"C:\Temp\Renamed\PrismLibraryTestFileD.txt", 3, 3)]
        [TestCase(@"C:\Temp\PrismLibraryTestFileE.txt", @"C:\Temp\Renamed\PrismLibraryTestFileF.txt", 3, 7)]
        [TestCase(@"C:\Temp\PrismLibraryTestFileG.txt", @"C:\Temp\Renamed\PrismLibraryTestFileH.txt", 2, 15, false)]
        public void RenameFileFullPath(string targetFilePath, string newFilePath, int retryCount, int secondsToLockFile, bool expectSuccessfulRename = true)
        {
            var fileToRename = new FileInfo(targetFilePath);

            if (fileToRename.Directory == null)
            {
                Assert.Fail("Unable to determine the parent directory of file " + targetFilePath);
            }

            var newFileInfo = new FileInfo(newFilePath);

            RenameFileWork(fileToRename, newFileInfo, retryCount, secondsToLockFile, expectSuccessfulRename);
        }

        private void RenameFileWork(FileInfo fileToRename, FileInfo newFile, int retryCount, int secondsToLockFile, bool expectSuccessfulRename = true)
        {
            if (!fileToRename.Exists)
            {
                using var writer = new StreamWriter(new FileStream(fileToRename.FullName, FileMode.Create, FileAccess.Write, FileShare.Read));

                writer.WriteLine("Test file to be renamed from {0} to {1}", fileToRename.Name, fileToRename.Name);
            }

            if (newFile.Exists)
            {
                Console.WriteLine("Deleting existing target file: " + newFile.FullName);
                newFile.Delete();
                Console.WriteLine();
                newFile.Refresh();
            }

            Console.WriteLine("Renaming {0} to {1}", fileToRename.FullName, newFile.FullName);

            var lockedReader = secondsToLockFile > 0
                ? new FileLockUtility(fileToRename.FullName, secondsToLockFile)
                : new FileLockUtility(string.Empty, secondsToLockFile);

            if (secondsToLockFile > 0)
            {
                Console.WriteLine();

                // Wait 1.5 seconds to give the lockedReader time to open the file
                ConsoleMsgUtils.SleepSeconds(1.5);
            }

            var success = mFileTools.RenameFileWithRetry(fileToRename, newFile, out var errorMessage, retryCount);

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

            Assert.AreEqual(fileToRename.FullName, newFile.FullName);

            Console.WriteLine("Successfully renamed the file to " + newFile.FullName);
        }
    }
}
