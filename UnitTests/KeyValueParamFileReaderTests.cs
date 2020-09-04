using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PRISM.AppSettings;

namespace PRISMTest
{
    [TestFixture]
    public class KeyValueParamFileReaderTests
    {
        private const string PARAMETER_FILE_PATH = @"\\proto-2\UnitTest_Files\PRISM\ParamFileTests\ExampleParamFile.conf";
        private const string TOOL_NAME = "PRISM Unit Test";

        [TestCase(" -i VisibleColors.tsv -r true -Debug false -Smooth 7")]
        [TestCase(" -i VisibleColors.tsv -Debug false -Smooth 7", "Recurse")]
        [TestCase(" -i VisibleColors.tsv -Smooth 7", "Debug", "Recurse")]
        [TestCase(" -i VisibleColors.tsv -r true -Debug false", "Smooth")]
        [Category("PNL_Domain")]
        public void TestConvertParamsToArgs(string expectedArgumentLine, params string[] skipNames)
        {
            var paramToArgMapping = GetParamToArgMapping(skipNames, out var paramNamesToSkip);

            var paramFileReader = new KeyValueParamFileReader(TOOL_NAME, PARAMETER_FILE_PATH);
            var success = paramFileReader.ParseKeyValueParameterFile(out var paramFileEntries);
            Assert.True(success, "Error reading the parameter file");

            ValidateConvertParamsToArgs(expectedArgumentLine, paramFileReader, paramFileEntries, paramToArgMapping, paramNamesToSkip);
        }

        [TestCase(" -i VisibleColors.tsv -r true -Debug false -Smooth 7")]
        [TestCase(" -i VisibleColors.tsv -Debug false -Smooth 7", "Recurse")]
        [TestCase(" -i VisibleColors.tsv -Smooth 7", "Debug", "Recurse")]
        [TestCase(" -i VisibleColors.tsv -r true -Debug false", "Smooth")]
        public void TestConvertInMemoryParamsToArgs(string expectedArgumentLine, params string[] skipNames)
        {
            var paramToArgMapping = GetParamToArgMapping(skipNames, out var paramNamesToSkip);

            var parameterList = new List<string>
            {
                "# Input file path",
                "InputFile=VisibleColors.tsv",
                string.Empty,
                "# Search in subdirectories",
                "Recurse=true",
                string.Empty,
                "# Enable debug mode",
                "Debug=false",
                "# Number of points to smooth",
                "Smooth=7"
            };

            var paramFileReader = new KeyValueParamFileReader(string.Empty, string.Empty);
            var success = paramFileReader.ParseKeyValueParameterList(parameterList, out var paramFileEntries);
            Assert.True(success, "Error parsing the list of parameters");

            ValidateConvertParamsToArgs(expectedArgumentLine, paramFileReader, paramFileEntries, paramToArgMapping, paramNamesToSkip);
        }

        private static Dictionary<string, string> GetParamToArgMapping(IEnumerable<string> skipNames, out SortedSet<string> paramNamesToSkip)
        {
            var paramToArgMapping = new Dictionary<string, string>(25, StringComparer.OrdinalIgnoreCase)
            {
                {"InputFile", "i"},
                {"Recurse", "r"},
                {"Debug", "Debug"},
                {"Smooth", "Smooth"}
            };

            paramNamesToSkip = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in skipNames)
            {
                paramNamesToSkip.Add(item);
            }

            return paramToArgMapping;
        }

        private static void ValidateConvertParamsToArgs(
            string expectedArgumentLine,
            KeyValueParamFileReader paramFileReader,
            List<KeyValuePair<string, string>> paramFileEntries,
            Dictionary<string, string> paramToArgMapping, SortedSet<string> paramNamesToSkip)
        {
            var cmdLineArguments = paramFileReader.ConvertParamsToArgs(paramFileEntries, paramToArgMapping, paramNamesToSkip, "-");

            Console.WriteLine("Equivalent command line arguments:");
            Console.WriteLine(cmdLineArguments);

            Assert.AreEqual(expectedArgumentLine, cmdLineArguments);
        }

        [TestCase("Recurse", true)]
        [TestCase("Debug",false)]
        [Category("PNL_Domain")]
        public void TestParamIsEnabled(string parameterName, bool expectedIsEnabled)
        {
            var paramFileReader = new KeyValueParamFileReader(TOOL_NAME, PARAMETER_FILE_PATH);
            var success = paramFileReader.ParseKeyValueParameterFile(out var paramFileEntries);
            Assert.True(success, "Error reading the parameter file");

            var enabled = paramFileReader.ParamIsEnabled(paramFileEntries, parameterName);

            Console.WriteLine("{0}={1}", parameterName, enabled);
            Assert.AreEqual(expectedIsEnabled, enabled, "Parameter {0} was {1}, but expected to be {2}", parameterName, enabled, expectedIsEnabled);
        }

        [TestCase("InputFile,Recurse,Debug,Smooth", "VisibleColors.tsv", "true", "false", "7")]
        [Category("PNL_Domain")]
        public void TestReadParameters(string paramNames, params string[] expectedParamValues)
        {
            var paramNameList = paramNames.Split(',').ToList();
            var paramFileEntries = GetParamFileEntries(out var success);

            Assert.True(success, "Error reading the parameter file");

            for (var i = 0; i < paramNameList.Count; i++)
            {
                var paramValue = KeyValueParamFileReader.GetParameterValue(paramFileEntries, paramNameList[i]);
                Console.WriteLine("{0}={1}", paramNameList[i], paramValue);
                Assert.AreEqual(expectedParamValues[i], paramValue);
            }
        }

        [TestCase("Recurse,Debug", true, false)]
        [Category("PNL_Domain")]
        public void TestReadParameters(string paramNames, params bool[] expectedParamValues)
        {
            var paramNameList = paramNames.Split(',').ToList();
            var paramFileEntries = GetParamFileEntries(out var success);

            Assert.True(success, "Error reading the parameter file");

            for (var i = 0; i < paramNameList.Count; i++)
            {
                var paramValue = KeyValueParamFileReader.GetParameterValue(paramFileEntries, paramNameList[i], false);
                Console.WriteLine("{0}={1}", paramNameList[i], paramValue);
                Assert.AreEqual(expectedParamValues[i], paramValue);
            }
        }

        [TestCase("Smooth", 7)]
        [Category("PNL_Domain")]
        public void TestReadParameters(string paramNames, params int[] expectedParamValues)
        {
            var paramNameList = paramNames.Split(',').ToList();
            var paramFileEntries = GetParamFileEntries(out var success);

            Assert.True(success, "Error reading the parameter file");

            for (var i = 0; i < paramNameList.Count; i++)
            {
                var paramValue = KeyValueParamFileReader.GetParameterValue(paramFileEntries, paramNameList[i], 0);
                Console.WriteLine("{0}={1}", paramNameList[i], paramValue);
                Assert.AreEqual(expectedParamValues[i], paramValue);
            }
        }

        private List<KeyValuePair<string, string>> GetParamFileEntries(out bool success)
        {
            var paramFileReader = new KeyValueParamFileReader(TOOL_NAME, PARAMETER_FILE_PATH);
            success = paramFileReader.ParseKeyValueParameterFile(out var paramFileEntries);
            return paramFileEntries;
        }

        [TestCase(@"C:\NonExistingParameterFile.conf")]
        [TestCase("")]
        public void TestReadMissingParameterFile(string parameterFilePath)
        {
            var paramFileReader = new KeyValueParamFileReader(TOOL_NAME, parameterFilePath);
            var success = paramFileReader.ParseKeyValueParameterFile(out _);

            Assert.False(success, "ParseKeyValueParameterFile returned true unexpectedly");
            Assert.True(paramFileReader.ParamFileNotFound, "Parameter file exists, but it was expected to be missing: " + parameterFilePath);

            Console.WriteLine("Validated behavior trying to load a non-existent parameter file");
        }

        [TestCase("", @"C:\NonExistingParameterFile.conf")]
        [TestCase(@"C:\WorkDir", "NonExistingParameterFile.conf")]
        [TestCase("", "")]
        public void TestReadMissingParameterFile(string workingDirectoryPath, string parameterFileName)
        {
            var paramFileReader = new KeyValueParamFileReader(TOOL_NAME, workingDirectoryPath, parameterFileName);
            var success = paramFileReader.ParseKeyValueParameterFile(out _);

            Assert.False(success, "ParseKeyValueParameterFile returned true unexpectedly");
            Assert.True(paramFileReader.ParamFileNotFound, "Parameter file exists, but it was expected to be missing: " + paramFileReader.ParamFilePath);

            Console.WriteLine("Validated behavior trying to load a non-existent parameter file");
        }
    }
}
