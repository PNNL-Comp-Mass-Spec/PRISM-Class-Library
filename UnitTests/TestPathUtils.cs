using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class TestPathUtils
    {
        [Test]
        [TestCase(@"/proc/12343/stat", @"/proc/12343/stat")]
        [TestCase(@"/proc/subdir\filename", @"/proc/subdir/filename")]
        [TestCase(@"/proc\subdir\filename.txt", @"/proc/subdir/filename.txt")]
        public void TestAssureLinuxPath(string pathSpec, string expectedResult)
        {
            var result = clsPathUtils.AssureLinuxPath(pathSpec);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        [TestCase(@"C:\DMS_WorkDir/12343/stat", @"C:\DMS_WorkDir\12343\stat")]
        [TestCase(@"C:\DMS_WorkDir/subdir\filename", @"C:\DMS_WorkDir\subdir\filename")]
        [TestCase(@"C:\DMS_WorkDir/subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir\subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir/subdir/filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir\subdir/filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        public void TestAssureWindowsPath(string pathSpec, string expectedResult)
        {
            var result = clsPathUtils.AssureWindowsPath(pathSpec);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        [TestCase(@"/proc/12343", "stat", @"/proc/12343/stat")]
        [TestCase(@"/proc/12343/", "stat", @"/proc/12343/stat")]
        [TestCase(@"/proc/12343", "/stat", @"/stat")]
        [TestCase(@"/proc/12343/", "/stat/", @"/stat/")]
        [TestCase(@"/share/item", "dataset/results", @"/share/item/dataset/results")]
        [TestCase(@"/share/item/", "dataset/results", @"/share/item/dataset/results")]
        [TestCase(@"/share/item", "/dataset/results", @"/dataset/results")]
        [TestCase(@"/share/item/", "/dataset/results/", @"/dataset/results/")]
        public void TestCombineLinuxPaths(string path1, string path2, string expectedResult)
        {
            var result = clsPathUtils.CombineLinuxPaths(path1, path2);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        [TestCase(@"C:\DMS_WorkDir", "subdir", @"C:\DMS_WorkDir\subdir")]
        [TestCase(@"C:\DMS_WorkDir\", "subdir", @"C:\DMS_WorkDir\subdir")]
        [TestCase(@"C:\DMS_WorkDir", @"subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        [TestCase(@"C:\DMS_WorkDir\", @"subdir\filename.txt", @"C:\DMS_WorkDir\subdir\filename.txt")]
        public void TestCombineWindowsPaths(string path1, string path2, string expectedResult)
        {
            var result = clsPathUtils.CombineWindowsPaths(path1, path2);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
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
            var result = clsPathUtils.CombinePaths(path1, path2, directorySepChar);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        [TestCase("Results.txt", "*.txt", true)]
        [TestCase("Results.txt", "*.zip", false)]
        [TestCase("Results.txt", "*", true)]
        [TestCase("MSGFDB_PartTryp_MetOx_20ppmParTol.txt", "MSGF*", true)]
        [TestCase("MSGFDB_PartTryp_MetOx_20ppmParTol.txt", "XT*", false)]
        public void TestFitsMask(string fileName, string fileMask, bool expectedResult)
        {
            var result = clsPathUtils.FitsMask(fileName, fileMask);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        [TestCase(@"C:\Users\Public\Pictures", @"C:\Users\Public", "Pictures")]
        [TestCase(@"C:\Users\Public\Pictures\", @"C:\Users\Public", "Pictures")]
        [TestCase(@"C:\Windows\System32", @"C:\Windows", "System32")]
        [TestCase(@"C:\Windows", @"C:\", "Windows")]
        [TestCase(@"C:\Windows\", @"C:\", "Windows")]
        [TestCase(@"C:\", @"", @"C:\")]
        [TestCase(@"C:\DMS_WorkDir", @"C:\", "DMS_WorkDir")]
        [TestCase(@"C:\DMS_WorkDir\", @"C:\", "DMS_WorkDir")]
        [TestCase(@"C:\DMS_WorkDir\SubDir", @"C:\DMS_WorkDir", "SubDir")]
        [TestCase(@"C:\DMS_WorkDir\SubDir\", @"C:\DMS_WorkDir", "SubDir")]
        [TestCase(@"Microsoft SQL Server\Client SDK\ODBC", @"Microsoft SQL Server\Client SDK", "ODBC")]
        [TestCase(@"TortoiseGit\bin", @"TortoiseGit", "bin")]
        [TestCase(@"TortoiseGit\bin\", @"TortoiseGit", "bin")]
        [TestCase(@"TortoiseGit", @"", "TortoiseGit")]
        [TestCase(@"TortoiseGit\", @"", "TortoiseGit")]
        [TestCase(@"\\server\Share\Folder", @"\\server\Share", "Folder")]
        [TestCase(@"\\server\Share\Folder\", @"\\server\Share", "Folder")]
        [TestCase(@"\\server\Share", @"", "Share")]
        [TestCase(@"\\server\Share\", @"", "Share")]
        [TestCase(@"/etc/fonts/conf.d", @"/etc/fonts", "conf.d")]
        [TestCase(@"/etc/fonts/conf.d/", @"/etc/fonts", "conf.d")]
        [TestCase(@"/etc/fonts", @"/etc", "fonts")]
        [TestCase(@"/etc/fonts/", @"/etc", "fonts")]
        [TestCase(@"/etc", @"/", "etc")]
        [TestCase(@"/etc/", @"/", "etc")]
        [TestCase(@"/", @"", "")]
        [TestCase(@"log/xymon", @"log", "xymon")]
        [TestCase(@"log/xymon/old", @"log/xymon", "old")]
        [TestCase(@"log", @"", "log")]
        public void TestGetParentDirectoryPath(string directoryPath, string expectedParentPath, string expectedDirectoryName)
        {
            var parentPath = clsPathUtils.GetParentDirectoryPath(directoryPath, out var directoryName);

            if (string.IsNullOrWhiteSpace(parentPath))
            {
                Console.WriteLine("{0} has no parent; name is {1}", directoryPath, directoryName);
            }
            else
            {
                Console.WriteLine("{0} has parent {1} and name {2}", directoryPath, parentPath, directoryName);
            }

            Assert.AreEqual(expectedParentPath, parentPath, "Parent path mismatch");
            Assert.AreEqual(expectedDirectoryName, directoryName, "Directory name mismatch");
        }

        [Test]
        [TestCase(@"C:\DMS_WorkDir\SubDir", false)]
        [TestCase(@"C:\DMS_WorkDir\Result Directory", true)]
        [TestCase(@"C:\DMS_WorkDir\Result Directory\", true)]
        [TestCase(@"C:\DMS_WorkDir\ResultDirectory\filename.txt", false)]
        [TestCase(@"C:\DMS_WorkDir\Result Directory\filename.txt", true)]
        [TestCase(@"/proc/12343", false)]
        [TestCase(@"/proc/Result Directory", true)]
        [TestCase(@"/proc/ResultDirectory/filename.txt", false)]
        [TestCase(@"/proc/Result Directory/filename.txt", true)]
        public void TestPossiblyQuotePath(string filePath, bool expectedQuoteRequired)
        {
            var quotedPath = clsPathUtils.PossiblyQuotePath(filePath);

            var pathWasQuoted = !string.Equals(filePath, quotedPath);

            Assert.AreEqual(expectedQuoteRequired, pathWasQuoted, "Mismatch for " + filePath);
        }

        [Test]
        [TestCase(@"C:\DMS_WorkDir\filename.txt", "UpdatedFile.txt", @"C:\DMS_WorkDir\UpdatedFile.txt")]
        [TestCase(@"C:\DMS_WorkDir\Results Directory\filename.txt", "UpdatedFile.txt", @"C:\DMS_WorkDir\Results Directory\UpdatedFile.txt")]
        public void TestReplaceFilenameInPath(string existingFilePath, string newFileName, string expectedResult)
        {
            var newPath = clsPathUtils.ReplaceFilenameInPath(existingFilePath, newFileName);
            Assert.AreEqual(expectedResult, newPath);
        }
    }
}
