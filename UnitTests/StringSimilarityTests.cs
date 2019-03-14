using System;
using NUnit.Framework;
using PRISM.DataUtils;

namespace PRISMTest
{
    [TestFixture]
    class StringSimilarityTests
    {

        [Test]
        [TestCase("Breadbox", "Redbox", 0.6666666, 0.6666666)]
        [TestCase("CSharp", "Csharp", 1, 1)]
        [TestCase("C# Code", "C# Code", 1, 1)]
        [TestCase("C# 6 Code", "C# 7 Code", 0.8, 0.6)]
        [TestCase("C# Code", "C#Code", 0.88888, 1)]
        [TestCase("C# Code", "C#_Code", 0.8, 1)]
        [TestCase(".NET 4.6.2", ".NET 4.6.1", 0.8571, 0.8)]
        [TestCase("Abc", "XYZ", 0, 0)]
        [TestCase("William", "George", 0, 0)]
        [TestCase("William", "Bill", 0.4444, 0.4444)]
        [TestCase("QC_Shew_18_02_Run-01_13Mar19_Valco_19-01-03", "QC_Shew_18_02_Run-02_13Mar19_Valco_19-01-03", 0.9524, 0.9375)]
        [TestCase("QC_Shew_18_02_Run-01_13Mar19_Valco_19-01-03", "QC_Shew_18_02_Frac-08-of-11_12Mar19_Tiger_19-01-05", 0.6154, 0.5217)]
        [TestCase("QC_Shew_16_01_Run-02_05Mar19_Valco_19-01-03", "QC_Shew_18_02-Run2_4Mar19_Arwen_18-11-02", 0.5185, 0.4516)]
        [TestCase("QC_Shew_16_01_Run-02_05Mar19_Valco_19-01-03", "50	QC_Shew_18_02-Run2_4Mar19_Arwen_18-11-02", 0.5122, 0.4375)]
        [TestCase("QC_Shew_16_01_Frac-10-of-11_26Feb19_Tiger_19-01-05", "QC_Shew_18_02-run2_01Mar19_Arwen_18-11-02", 0.3820, 0.3235)]
        [TestCase("QC_Shew_18_02-run8_02Mar19_Arwen_18-11-02", "QC_Shew_18_02-run4_01Mar19_Arwen_18-11-02", 0.9, 0.8710)]
        [TestCase("QC_Shew2_18-02_01Mar19_Merry_18-09-02", "QC_Shew1_18-02_01Mar19_Merry_18-09-02", 0.9444, 0.9286)]
        public void CompareStringsWithNumbers(string text1, string text2, double expectedSimilarityScore, double expectedSimilarityScoreNoSymbolsOrWhitespace)
        {
            // Score with numbers, whitespace, and symbols
            var similarityScore = StringSimilarityTool.CompareStrings(text1, text2, false, false);

            // Score with numbers, but no whitespace or symbols
            var similarityScoreNoSymbolsOrWhitespace = StringSimilarityTool.CompareStrings(text1, text2, false, true);

            DisplayAndCompareScores(text1, text2,
                                    similarityScore, similarityScoreNoSymbolsOrWhitespace,
                                    expectedSimilarityScore, expectedSimilarityScoreNoSymbolsOrWhitespace);
        }

        [Test]
        [TestCase("Breadbox", "Redbox", 0.6666666, 0.6666666)]
        [TestCase("CSharp", "Csharp", 1, 1)]
        [TestCase("C# Code", "C# Code", 1, 1)]
        [TestCase("C# 6 Code", "C# 7 Code", 1, 1)]
        [TestCase("C# Code", "C#Code", 0.75, 1)]
        [TestCase("C# Code", "C#_Code", 0.75, 1)]
        [TestCase(".NET 4.6.2", ".NET 4.6.1", 1, 1)]
        [TestCase("Abc", "XYZ", 0, 0)]
        [TestCase("William", "George", 0, 0)]
        [TestCase("William", "Bill", 0.4444, 0.4444)]
        [TestCase("QC_Shew_18_02_Run-01_13Mar19_Valco_19-01-03", "QC_Shew_18_02_Run-02_13Mar19_Valco_19-01-03", 1, 1)]
        [TestCase("QC_Shew_18_02_Run-01_13Mar19_Valco_19-01-03", "QC_Shew_18_02_Frac-08-of-11_12Mar19_Tiger_19-01-05", 0.4571, 0.4571)]
        [TestCase("QC_Shew_16_01_Run-02_05Mar19_Valco_19-01-03", "QC_Shew_18_02-Run2_4Mar19_Arwen_18-11-02", 0.6875, 0.6875)]
        [TestCase("QC_Shew_16_01_Run-02_05Mar19_Valco_19-01-03", "50	QC_Shew_18_02-Run2_4Mar19_Arwen_18-11-02", 0.6875, 0.6875)]
        [TestCase("QC_Shew_16_01_Frac-10-of-11_26Feb19_Tiger_19-01-05", "QC_Shew_18_02-run2_01Mar19_Arwen_18-11-02", 0.3235, -1)]
        [TestCase("QC_Shew_18_02-run8_02Mar19_Arwen_18-11-02", "QC_Shew_18_02-run4_01Mar19_Arwen_18-11-02",1, 1)]
        [TestCase("QC_Shew2_18-02_01Mar19_Merry_18-09-02", "QC_Shew1_18-02_01Mar19_Merry_18-09-02", 1, 1)]
        public void CompareStringsNoNumbers(string text1, string text2, double expectedSimilarityScore, double expectedSimilarityScoreNoSymbolsOrWhitespace)
        {
            // Score without numbers but with whitespace, and symbols
            var similarityScore = StringSimilarityTool.CompareStrings(text1, text2, true, false);

            // Score without numbers, whitespace, or symbols
            var similarityScoreNoSymbolsOrWhitespace = StringSimilarityTool.CompareStrings(text1, text2, true, true);

            DisplayAndCompareScores(text1, text2,
                                    similarityScore, similarityScoreNoSymbolsOrWhitespace,
                                    expectedSimilarityScore, expectedSimilarityScoreNoSymbolsOrWhitespace);
        }

        private void DisplayAndCompareScores(string text1, string text2, double similarityScore, double similarityScoreNoSymbolsOrWhitespace, double expectedSimilarityScore, double expectedSimilarityScoreNoSymbolsOrWhitespace)
        {

            Console.WriteLine("With whitespace, similarity score is \n{0:F4} for \n{1} vs.\n{2}",
                              similarityScore, text1, text2);
            Console.WriteLine();

            Console.WriteLine("Ignoring whitespace, similarity score is \n{0:F4} for \n{1} vs.\n{2}",
                              similarityScoreNoSymbolsOrWhitespace, text1, text2);
            Console.WriteLine();

            if (expectedSimilarityScore < 0 || expectedSimilarityScoreNoSymbolsOrWhitespace < 0)
            {
                Console.WriteLine("Warning: Comparison skipped");
                return;
            }

            Assert.AreEqual(expectedSimilarityScore, similarityScore, 0.0001,
                            "Actual score of {0} does not match the expected score, {1}",
                            similarityScore, expectedSimilarityScore);

            Assert.AreEqual(expectedSimilarityScoreNoSymbolsOrWhitespace, similarityScoreNoSymbolsOrWhitespace, 0.0001,
                            "Actual score of {0} does not match the expected score, {1} (ignore whitespace)",
                            similarityScoreNoSymbolsOrWhitespace, expectedSimilarityScoreNoSymbolsOrWhitespace);

        }
    }

}