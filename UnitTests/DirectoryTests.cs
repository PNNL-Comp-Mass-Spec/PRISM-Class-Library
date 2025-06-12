using System;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    internal class DirectoryTests
    {
        private FileTools mFileTools;

        [OneTimeSetUp]
        public void Setup()
        {
            mFileTools = new FileTools("PrismUnitTests", 1);
        }

        /// <summary>
        /// Test creating a directory, including creating parent directories
        /// </summary>
        /// <param name="directoryPath">Directory path</param>
        /// <param name="removeExistingBeforeCreating">When true, remove existing target directories (but only if they're empty)</param>
        [TestCase(@"C:\Temp\PRISM", false)]
        [TestCase(@"C:\Temp\TestDirectory1", true)]
        [TestCase(@"C:\Temp\TestDirectory2\Ancestor\Grandparent\Parent\Child", true)]
        public void CreateDirectory(string directoryPath, bool removeExistingBeforeCreating)
        {
            if (removeExistingBeforeCreating)
            {
                // Remove existing target directories, but only if empty
                // Does not remove C:\Temp

                var currentTarget = new DirectoryInfo(directoryPath);

                try
                {
                    while (true)
                    {
                        if (currentTarget.Exists)
                        {
                            if (currentTarget.GetFileSystemInfos().Length > 0)
                            {
                                Console.WriteLine("Directory is not empty; will not remove " + currentTarget.FullName);
                                break;
                            }

                            currentTarget.Delete();
                        }

                        if (currentTarget.Parent == null ||
                            currentTarget.Parent.FullName.Equals(@"C:\Temp", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        currentTarget = currentTarget.Parent;
                    }
                }
                catch (Exception ex)
                {
                    Assert.Fail("Error removing existing directory " + currentTarget.FullName + ": " + ex.Message);
                }
            }

            try
            {
                FileTools.CreateDirectoryIfNotExists(directoryPath);

                var targetDirectory = new DirectoryInfo(directoryPath);

                if (!targetDirectory.Exists)
                {
                    Assert.Fail("Directory was not created: " + directoryPath);
                }
            }
            catch (Exception ex)
            {
                Assert.Fail("Error creating directory " + directoryPath + ": " + ex.Message);
            }
        }
    }
}
