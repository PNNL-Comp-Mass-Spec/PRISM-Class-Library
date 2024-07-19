using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    [Category("PNL_Domain")]
    public class NativeIOTests
    {
        [TestCase(1, 50)]
        [TestCase(1, 250)]
        [TestCase(2, 50)]
        [TestCase(3, 75)]
        [TestCase(4, 75)]
        [TestCase(5, 75)]
        [TestCase(5, 150)]
        public void CreateAndDeleteDirectories(int directoryDepth, int directoryNameLength)
        {
            var success1 = CreateDirectories(directoryDepth, directoryNameLength, out var directoriesCreatedOrFound);

            var success2 = DeleteDirectories(directoriesCreatedOrFound);

            Assert.IsTrue(success1, "Error creating directories");
            Assert.IsTrue(success2, "Error deleting directories");
        }

        [TestCase(3, 25, 10)]
        [TestCase(3, 75, 10)]
        public void CreateAndDeleteFiles(int directoryDepth, int directoryNameLength, int countIncrement)
        {
            var success1 = CreateDirectories(directoryDepth, directoryNameLength, out var directoriesCreatedOrFound);

            // Create 5 files of increasing size in the innermost directory created
            var currentDirectory = directoriesCreatedOrFound.Peek();

            var currentFile = string.Empty;
            var errorOccurred = false;
            var fileSizeMismatch = false;

            var filesCreated = new List<string>();

            var expectedLengthsByName = new SortedDictionary<string, long>();

            var actualLengthsByName = new SortedDictionary<string, long>();

            try
            {
                var valuesToWrite = 10;
                for (var i = 0; i < 5; i++)
                {
                    var finalOutputFilePath = BuildPath(currentDirectory, string.Format("TestFile{0}", i + 1));

                    if (finalOutputFilePath.Length < NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD)
                    {
                        currentFile = finalOutputFilePath;
                    }
                    else
                    {
                        currentFile = Path.GetTempFileName();
                    }

                    Console.WriteLine("Creating " + currentFile);

                    using (var writer = new StreamWriter(new FileStream(currentFile, FileMode.Create, FileAccess.ReadWrite)))
                    {
                        for (var j = 0; j < valuesToWrite; j++)
                        {
                            writer.Write("{0:4D}", j);
                        }
                    }

                    if (!finalOutputFilePath.Equals(currentFile))
                    {
                        Console.WriteLine("Copying file to final destination: " + finalOutputFilePath);
                        NativeIOFileTools.Copy(currentFile, finalOutputFilePath, true);
                        File.Delete(currentFile);
                    }

                    filesCreated.Add(finalOutputFilePath);

                    var filename = Path.GetFileName(finalOutputFilePath);
                    expectedLengthsByName.Add(filename, valuesToWrite * 2);

                    valuesToWrite += countIncrement;
                }

                Console.WriteLine();

                // Delete each file after first determining its size
                foreach (var filePath in filesCreated)
                {
                    try
                    {
                        currentFile = filePath;
                        var filename = Path.GetFileName(currentFile);

                        var fileSizeBytes = NativeIOFileTools.GetFileLength(currentFile);

                        Console.WriteLine("{0,-15} {1} bytes", filename, fileSizeBytes);
                        actualLengthsByName.Add(filename, fileSizeBytes);

                        NativeIOFileTools.Delete(currentFile);
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine("Exception deleting file: {0}\n{1}", ex2.Message, currentFile);
                        errorOccurred = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception creating file: {0}\n{1}", ex.Message, currentFile);
                errorOccurred = true;
            }

            Console.WriteLine();

            foreach (var item in actualLengthsByName)
            {
                var expectedSize = expectedLengthsByName[item.Key];

                if (expectedSize == item.Value)
                {
                    Console.WriteLine("{0} has expected size of {1} bytes", item.Key, expectedSize);
                }
                else
                {
                    Console.WriteLine("File {0} is {1} bytes instead of {2} bytes", item.Key, item.Value, expectedSize);
                    fileSizeMismatch = true;
                }
            }

            Console.WriteLine();

            var success2 = DeleteDirectories(directoriesCreatedOrFound, true);

            Console.WriteLine();

            Assert.IsFalse(errorOccurred, "Error creating or deleting files");

            Assert.IsFalse(fileSizeMismatch, "File size mismatch");

            Assert.IsTrue(success1, "Error creating directories");

            if (!success2)
            {
                Console.WriteLine("Warning: Error deleting directories; a handle is likely still open to one of them");
            }
        }

        private bool CreateDirectories(int directoryDepth, int directoryNameLength, out Stack<string> directoriesCreatedOrFound)
        {
            var currentDirectory = string.Empty;
            directoriesCreatedOrFound = new Stack<string>();

            var errorOccurred = false;

            try
            {
                var startingDirectory = Path.GetTempPath();
                var baseDirectoryName = "PRISMTest_NativeIOTests_".PadRight(directoryNameLength - 2, '_');

                currentDirectory = BuildPath(startingDirectory, string.Format("{0}{1:D2}", baseDirectoryName, 0));
                CreateDirectory(currentDirectory, directoriesCreatedOrFound);

                for (var i = 1; i < directoryDepth; i++)
                {
                    currentDirectory = BuildPath(currentDirectory, string.Format("{0}{1:D2}", baseDirectoryName, i));
                    CreateDirectory(currentDirectory, directoriesCreatedOrFound);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception creating directory: {0}\n{1}", ex.Message, currentDirectory);
                errorOccurred = true;
            }

            return !errorOccurred;
        }

        /// <summary>
        /// Delete directories in the stack
        /// </summary>
        /// <param name="directoriesCreatedOrFound">Stack of directory paths</param>
        /// <param name="recursive">When true, delete any files or subdirectories inside each target directory</param>
        /// <returns>True if success, false if an error</returns>
        private bool DeleteDirectories(Stack<string> directoriesCreatedOrFound, bool recursive = false)
        {
            var errorOccurred = false;

            while (directoriesCreatedOrFound.Count > 0)
            {
                var currentDirectory = directoriesCreatedOrFound.Pop();

                try
                {
                    if (!NativeIODirectoryTools.Exists(currentDirectory))
                    {
                        Console.WriteLine("Did not find expected directory:\n{0}", currentDirectory);
                        errorOccurred = true;
                        continue;
                    }

                    NativeIODirectoryTools.Delete(currentDirectory, recursive);
                    Console.WriteLine("Deleted: " + currentDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception deleting directory: {0}\n{1}", ex.Message, currentDirectory);
                    errorOccurred = true;
                }
            }

            return !errorOccurred;
        }

        private string BuildPath(string basePath, string pathToAppend)
        {
            return basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + pathToAppend.TrimStart(Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Create the given directory
        /// </summary>
        private void CreateDirectory(string directoryPath, Stack<string> directoriesCreatedOrFound)
        {
            directoriesCreatedOrFound.Push(directoryPath);

            if (NativeIODirectoryTools.Exists(directoryPath))
            {
                Console.WriteLine("Not re-creating existing directory: " + directoryPath);
            }

            NativeIODirectoryTools.CreateDirectory(directoryPath);
            Console.WriteLine("Created: " + directoryPath);
        }
    }
}
