using System;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    public class PathTests
    {
        [Test]
        [TestCase(@"C:\temp", 10, @"C:\temp")]
        [TestCase(@"C:\temp", 20, @"C:\temp")]
        [TestCase(@"C:\temp\DataFile.txt", 10, @"C:\t..\Dat..")]
        [TestCase(@"C:\temp\DataFile.txt", 20, @"C:\temp\DataFile.txt")]
        [TestCase(@"C:\temp\DataFile.txt", 30, @"C:\temp\DataFile.txt")]
        [TestCase(@"C:\Program Files\Beyond Compare 4\BCompare.exe", 40, @"C:\Progr..\Beyond Compare 4\BCompare.exe")]
        [TestCase(@"C:\Program Files\Beyond Compare 4\BCompare.exe", 50, @"C:\Program Files\Beyond Compare 4\BCompare.exe")]
        [TestCase(@"C:\Program Files\Beyond Compare 4\BCompare.exe", 60, @"C:\Program Files\Beyond Compare 4\BCompare.exe")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs", 40, @"\\proto-6\dm..\AnalysisToolManager1\Logs")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\", 40, @"\\proto-6\d..\AnalysisToolManager1\Logs\")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs", 50, @"\\proto-6\dms_programs\AnalysisToolManager1\Logs")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs", 60, @"\\proto-6\dms_programs\AnalysisToolManager1\Logs")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\", 60, @"\\proto-6\dms_programs\AnalysisToolManager1\Logs\")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 40, @"\\p..\...\L..\AnalysisMgr_01-15-2017.txt")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 60, @"\\proto-6\...\AnalysisTool..\Logs\AnalysisMgr_01-15-2017.txt")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 80, @"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 100, @"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt")]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 120, @"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt")]
        public void CompactPath(string pathToCompact, int maxLength, string expectedResult)
        {
            var shortPath = clsFileTools.CompactPathString(pathToCompact, maxLength);

            Console.WriteLine(shortPath);

            Assert.AreEqual(expectedResult, shortPath, "Unexpected short path for {0}: {1}", pathToCompact, shortPath);
        }
    }
}
