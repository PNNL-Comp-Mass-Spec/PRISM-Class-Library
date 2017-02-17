using System;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    public class PathTests
    {
        [Test]
        [TestCase(@"C:\temp", 10)]
        [TestCase(@"C:\temp", 20)]
        [TestCase(@"C:\temp\DataFile.txt", 10)]
        [TestCase(@"C:\temp\DataFile.txt", 20)]
        [TestCase(@"C:\temp\DataFile.txt", 30)]
        [TestCase(@"C:\Program Files\Beyond Compare 4\BCompare.exe", 40)]
        [TestCase(@"C:\Program Files\Beyond Compare 4\BCompare.exe", 50)]
        [TestCase(@"C:\Program Files\Beyond Compare 4\BCompare.exe", 60)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs", 40)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\", 40)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs", 50)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs", 60)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\", 60)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 40)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 60)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 80)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 100)]
        [TestCase(@"\\proto-6\dms_programs\AnalysisToolManager1\Logs\AnalysisMgr_01-15-2017.txt", 120)]
        public void CompactPath(string pathToCompact, int maxLength)
        {
            var shortPath = clsFileTools.CompactPathString(pathToCompact, maxLength);

            Console.WriteLine(shortPath);
        }
    }
}
