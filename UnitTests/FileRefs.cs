using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace PRISMTest
{
    internal static class FileRefs
    {
        private const string DEFAULT_SHARE_PATH = @"\\proto-2\unitTest_Files\PRISM";

        /// <summary>
        /// This keeps track of the last parent path where we successfully found a data file
        /// </summary>
        /// <remarks>This path is check first on subsequent calls to GetTestFile in the same test session</remarks>
        private static string mLastMatchedParentPath = "";

        /// <summary>
        /// When true, use Assert.Fail() if the file is not found
        /// When false, return null
        /// </summary>
        public static bool AssertFailIfNotFound { get; set; }

        /// <summary>
        /// Share folder path
        /// </summary>
        public static string SharePath { get; set; } = DEFAULT_SHARE_PATH;

        /// <summary>
        /// Look for the file given by the relative path
        /// </summary>
        /// <param name="relativeFilePath">File path, e.g. Test_Data\Test.txt</param>
        /// <returns>FileInfo object if found, otherwise either uses Assert.Fail or returns null (based on AssertFailIfNotFound)</returns>
        public static FileInfo GetTestFile(string relativeFilePath)
        {
            var dataFile = new FileInfo(relativeFilePath);
            if (dataFile.Exists)
            {
                return dataFile;
            }

#if DEBUG
            Console.WriteLine("Could not find " + relativeFilePath);
            Console.WriteLine("Checking alternative locations");
#endif

            var relativePathsToCheck = new List<string>
            {
                dataFile.Name
            };

            if (!Path.IsPathRooted(relativeFilePath) &&
                (relativeFilePath.Contains(Path.DirectorySeparatorChar) || relativeFilePath.Contains(Path.AltDirectorySeparatorChar)))
            {
                relativePathsToCheck.Add(relativeFilePath);
            }

            if (!string.IsNullOrWhiteSpace(mLastMatchedParentPath))
            {
                // Check the last parent path where we successfully found a data file
                // The new data file might be in the same folder
                foreach (var relativePath in relativePathsToCheck)
                {
                    var alternateFile = new FileInfo(Path.Combine(mLastMatchedParentPath, relativePath));
                    if (alternateFile.Exists)
                        return alternateFile;
                }
            }

            var parentToCheck = dataFile.Directory.Parent;
            while (parentToCheck != null)
            {
                foreach (var relativePath in relativePathsToCheck)
                {
                    var alternateFile = new FileInfo(Path.Combine(parentToCheck.FullName, relativePath));
                    if (alternateFile.Exists)
                    {
#if DEBUG
                        Console.WriteLine("... found at " + alternateFile.FullName);
                        Console.WriteLine();
#endif
                        mLastMatchedParentPath = parentToCheck.FullName;
                        return alternateFile;
                    }
                }

                parentToCheck = parentToCheck.Parent;
            }

            foreach (var relativePath in relativePathsToCheck)
            {
                var serverPathFile = new FileInfo(Path.Combine(SharePath, relativePath));
                if (serverPathFile.Exists)
                {
#if DEBUG
                    Console.WriteLine("... found at " + serverPathFile);
                    Console.WriteLine();
#endif
                    mLastMatchedParentPath = SharePath;
                    return serverPathFile;
                }
            }

            var currentDirectory = new DirectoryInfo(".");

            if (AssertFailIfNotFound)
            {
                Assert.Fail("Could not find " + relativeFilePath + "; current working directory: " + currentDirectory.FullName);
            }

            return null;
        }

    }
}
