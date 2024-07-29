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
            Assert.That(success, Is.True, "Error reading the parameter file");

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
            Assert.That(success, Is.True, "Error parsing the list of parameters");

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

            Assert.That(cmdLineArguments, Is.EqualTo(expectedArgumentLine));
        }

        [TestCase(5, "Color=green", "Color", "green")]
        [TestCase(5, "Color = green", "Color", "green")]
        [TestCase(5, "Color=Cerulean blue", "Color", "Cerulean blue")]
        [TestCase(5, "Color = Cerulean blue", "Color", "Cerulean blue")]
        [TestCase(5, "City=New Orleans", "City", "New Orleans")]
        [TestCase(5, "Color=green    # Grass", "Color", "green", "# Grass")]
        [TestCase(5, "Color=green    # Grass color", "Color", "green", "# Grass color")]
        [TestCase(5, "Color = green    #", "Color", "green", "#")]
        [TestCase(5, "Color=Cerulean blue   # Ocean hue", "Color", "Cerulean blue", "# Ocean hue")]
        [TestCase(5, "Color=Cerulean blue# Ocean hue", "Color", "Cerulean blue", "# Ocean hue")]
        [TestCase(5, "Color=Cerulean blue #Ocean hue", "Color", "Cerulean blue", "#Ocean hue")]
        [TestCase(5, "First Name=Frank", "First Name", "Frank")]
        [TestCase(5, "First Name = Sharon White ", "First Name", "Sharon White")]
        [TestCase(5, "First Name=Bob # Staff", "First Name", "Bob", "# Staff")]
        [TestCase(5, "First Name=Sharon White  # Manager", "First Name", "Sharon White", "# Manager")]
        [TestCase(5, "City=New Orleans    #   Louisiana  ", "City", "New Orleans", "#   Louisiana")]
        [TestCase(5, "City=New Orleans #  Louisiana  ", "City", "New Orleans", "#  Louisiana")]
        [TestCase(5, "City=#  Louisiana  ", "City", "", "#  Louisiana")]
        [TestCase(5, "City=#", "City", "", "#")]
        [TestCase(5, "City= #", "City", "", "#")]
        [TestCase(5, "City=", "City", "", "")]
        public void TestGetKeyValueParamFileLine(
            int lineNumber, string lineText, string expectedParamName = "", string expectedValue = "", string expectedComment = "")
        {
            var paramFileLine1 = new KeyValueParamFileLine(lineNumber, lineText, true);

            var paramFileLine2 = new KeyValueParamFileLine(paramFileLine1);

            Console.WriteLine("ParamName=[{0}], Value=[{1}], Comment=[{2}]", paramFileLine1.ParamName, paramFileLine1.ParamValue, paramFileLine1.Comment);

            Assert.That(paramFileLine2.ParamName, Is.EqualTo(paramFileLine1.ParamName), "Parameter names don't match from the two calls to KeyValueParamFileLine");
            Assert.That(paramFileLine2.ParamValue, Is.EqualTo(paramFileLine1.ParamValue), "Values don't match from the two calls to KeyValueParamFileLine");

            if (string.IsNullOrWhiteSpace(expectedParamName) && string.IsNullOrWhiteSpace(expectedValue))
                return;

            Assert.That(paramFileLine1.LineNumber, Is.EqualTo(lineNumber), "Line number mismatch");

            Assert.That(paramFileLine1.Text, Is.EqualTo(lineText), "Full line text mismatch");

            Assert.That(paramFileLine1.ParamName, Is.EqualTo(expectedParamName), "Actual key does not match the expected key");
            Assert.That(paramFileLine1.ParamValue, Is.EqualTo(expectedValue), "Actual value does not match the expected value");

            if (!expectedComment.Equals(string.Empty))
                Assert.That(paramFileLine1.Comment, Is.EqualTo(expectedComment), "Actual comment does not match the expected comment");
        }

        [TestCase("Color=green", false, "Color", "green")]
        [TestCase("Color=green", true, "Color", "green")]
        [TestCase("Color = green", true, "Color", "green")]
        [TestCase("Color=Cerulean blue", true, "Color", "Cerulean blue")]
        [TestCase("Color = Cerulean blue", false, "Color", "Cerulean blue")]
        [TestCase("City=New Orleans", false, "City", "New Orleans")]
        [TestCase("City=New Orleans", true, "City", "New Orleans")]
        [TestCase("Color=green    # Grass", false, "Color", "green    # Grass", "# Grass")]
        [TestCase("Color=green    # Grass color", true, "Color", "green", "# Grass color")]
        [TestCase("Color = green    #", true, "Color", "green", "#")]
        [TestCase("Color=Cerulean blue   # Ocean hue", false, "Color", "Cerulean blue   # Ocean hue", "# Ocean hue")]
        [TestCase("Color=Cerulean blue# Ocean hue", false, "Color", "Cerulean blue# Ocean hue", "# Ocean hue")]
        [TestCase("Color=Cerulean blue #Ocean hue", false, "Color", "Cerulean blue #Ocean hue", "#Ocean hue")]
        [TestCase("Color=Cerulean blue   # Ocean hue", true, "Color", "Cerulean blue", "# Ocean hue")]
        [TestCase("Color=Cerulean blue# Ocean hue", true, "Color", "Cerulean blue", "# Ocean hue")]
        [TestCase("Color=Cerulean blue #Ocean hue", true, "Color", "Cerulean blue", "#Ocean hue")]
        [TestCase("First Name=Frank", false, "First Name", "Frank")]
        [TestCase("First Name=Frank", true, "First Name", "Frank")]
        [TestCase("First Name = Sharon White ", true, "First Name", "Sharon White")]
        [TestCase("First Name=Bob # Staff", false, "First Name", "Bob # Staff", "# Staff")]
        [TestCase("First Name=Bob # Staff", true, "First Name", "Bob", "# Staff")]
        [TestCase("First Name=Sharon White  # Manager", false, "First Name", "Sharon White  # Manager", "# Manager")]
        [TestCase("First Name=Sharon White  # Manager", true, "First Name", "Sharon White", "# Manager")]
        [TestCase("City=New Orleans    #   Louisiana  ", false, "City", "New Orleans    #   Louisiana", "#   Louisiana")]
        [TestCase("City=New Orleans #  Louisiana  ", true, "City", "New Orleans", "#  Louisiana")]
        [TestCase("City=#  Louisiana  ", false, "City", "#  Louisiana", "#  Louisiana")]
        [TestCase("City=#  Louisiana  ", true, "City", "", "#  Louisiana")]
        [TestCase("City=#", false, "City", "#", "#")]
        [TestCase("City=#", true, "City", "", "#")]
        [TestCase("City= #", false, "City", "#", "#")]
        [TestCase("City= #", true, "City", "", "#")]
        [TestCase("City=", false, "City", "", "")]
        public void TestGetKeyValueSetting(string settingText, bool removeComment, string expectedKey = "", string expectedValue = "", string expectedComment = "")
        {
            var kvSetting1 = KeyValueParamFileReader.GetKeyValueSetting(settingText, removeComment);

            var kvSetting2 = KeyValueParamFileReader.GetKeyValueSetting(settingText, out var comment, removeComment);

            Console.WriteLine("Key=[{0}], Value=[{1}], Comment=[{2}]", kvSetting2.Key, kvSetting2.Value, comment);

            Assert.That(kvSetting1.Key, Is.EqualTo(kvSetting2.Key), "Keys don't match from the two calls to GetKeyValueSetting");
            Assert.That(kvSetting1.Value, Is.EqualTo(kvSetting2.Value), "Values don't match from the two calls to GetKeyValueSetting");

            if (string.IsNullOrWhiteSpace(expectedKey) && string.IsNullOrWhiteSpace(expectedValue))
                return;

            Assert.That(kvSetting2.Key, Is.EqualTo(expectedKey), "Actual key does not match the expected key");
            Assert.That(kvSetting2.Value, Is.EqualTo(expectedValue), "Actual value does not match the expected value");

            if (!expectedComment.Equals(string.Empty))
                Assert.That(comment, Is.EqualTo(expectedComment), "Actual comment does not match the expected comment");
        }

        [TestCase("Recurse", true)]
        [TestCase("Debug", false)]
        [Category("PNL_Domain")]
        public void TestParamIsEnabled(string parameterName, bool expectedIsEnabled)
        {
            var paramFileReader = new KeyValueParamFileReader(TOOL_NAME, PARAMETER_FILE_PATH);
            var success = paramFileReader.ParseKeyValueParameterFile(out var paramFileEntries);
            Assert.That(success, Is.True, "Error reading the parameter file");

            var enabled = paramFileReader.ParamIsEnabled(paramFileEntries, parameterName);

            Console.WriteLine("{0}={1}", parameterName, enabled);
            Assert.That(enabled, Is.EqualTo(expectedIsEnabled), $"Parameter {parameterName} was {enabled}, but expected to be {expectedIsEnabled}");
        }

        [TestCase("InputFile,Recurse,Debug,Smooth", "VisibleColors.tsv", "true", "false", "7")]
        [Category("PNL_Domain")]
        public void TestReadParameters(string paramNames, params string[] expectedParamValues)
        {
            var paramNameList = paramNames.Split(',').ToList();
            var paramFileEntries = GetParamFileEntries(out var success);

            Assert.That(success, Is.True, "Error reading the parameter file");

            for (var i = 0; i < paramNameList.Count; i++)
            {
                var paramValue = KeyValueParamFileReader.GetParameterValue(paramFileEntries, paramNameList[i]);
                Console.WriteLine("{0}={1}", paramNameList[i], paramValue);
                Assert.That(paramValue, Is.EqualTo(expectedParamValues[i]));
            }
        }

        [TestCase("Recurse,Debug", true, false)]
        [Category("PNL_Domain")]
        public void TestReadParameters(string paramNames, params bool[] expectedParamValues)
        {
            var paramNameList = paramNames.Split(',').ToList();
            var paramFileEntries = GetParamFileEntries(out var success);

            Assert.That(success, Is.True, "Error reading the parameter file");

            for (var i = 0; i < paramNameList.Count; i++)
            {
                var paramValue = KeyValueParamFileReader.GetParameterValue(paramFileEntries, paramNameList[i], false);
                Console.WriteLine("{0}={1}", paramNameList[i], paramValue);
                Assert.That(paramValue, Is.EqualTo(expectedParamValues[i]));
            }
        }

        [TestCase("Smooth", 7)]
        [Category("PNL_Domain")]
        public void TestReadParameters(string paramNames, params int[] expectedParamValues)
        {
            var paramNameList = paramNames.Split(',').ToList();
            var paramFileEntries = GetParamFileEntries(out var success);

            Assert.That(success, Is.True, "Error reading the parameter file");

            for (var i = 0; i < paramNameList.Count; i++)
            {
                var paramValue = KeyValueParamFileReader.GetParameterValue(paramFileEntries, paramNameList[i], 0);
                Console.WriteLine("{0}={1}", paramNameList[i], paramValue);
                Assert.That(paramValue, Is.EqualTo(expectedParamValues[i]));
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

            Assert.That(success, Is.False, "ParseKeyValueParameterFile returned true unexpectedly");
            Assert.That(paramFileReader.ParamFileNotFound, Is.True, "Parameter file exists, but it was expected to be missing: " + parameterFilePath);

            Console.WriteLine("Validated behavior trying to load a non-existent parameter file");
        }

        [TestCase("", @"C:\NonExistingParameterFile.conf")]
        [TestCase(@"C:\WorkDir", "NonExistingParameterFile.conf")]
        [TestCase("", "")]
        public void TestReadMissingParameterFile(string workingDirectoryPath, string parameterFileName)
        {
            var paramFileReader = new KeyValueParamFileReader(TOOL_NAME, workingDirectoryPath, parameterFileName);
            var success = paramFileReader.ParseKeyValueParameterFile(out _);

            Assert.That(success, Is.False, "ParseKeyValueParameterFile returned true unexpectedly");
            Assert.That(paramFileReader.ParamFileNotFound, Is.True, "Parameter file exists, but it was expected to be missing: " + paramFileReader.ParamFilePath);

            Console.WriteLine("Validated behavior trying to load a non-existent parameter file");
        }

        [TestCase(5, "Color=green", "", "red", false, "Color", "red")]
        [TestCase(5, "Color=green", "", "red", true, "Color", "red")]
        [TestCase(5, "Color = green", "PlotColor", "red", false, "PlotColor", "red")]
        [TestCase(5, "Color = green", "PlotColor", "red", true, "PlotColor", "red")]
        [TestCase(5, "Color=Cerulean blue", "", "", false, "Color", "")]
        [TestCase(5, "Color=Cerulean blue", "", "", true, "Color", "")]
        [TestCase(5, "Color = Cerulean blue", "", "blue", false, "Color", "blue")]
        [TestCase(5, "Color = Cerulean blue", "", "blue", true, "Color", "blue")]
        [TestCase(5, "Color=green    # Grass", "", "brown", false, "Color", "brown", "# Grass")]
        [TestCase(5, "Color=green    # Grass", "", "brown", true, "Color", "brown", "# Grass")]
        [TestCase(5, "Color=green    # Grass color", "Turf", "tan", false, "Turf", "tan", "# Grass color")]
        [TestCase(5, "Color=green    # Grass color", "Turf", "tan", true, "Turf", "tan", "# Grass color")]
        [TestCase(5, "City=#  Louisiana  ", "Town", "Baton Rouge", false, "Town", "Baton Rouge", "#  Louisiana")]
        [TestCase(5, "City=#  Louisiana  ", "Town", "Baton Rouge", true, "Town", "Baton Rouge", "#  Louisiana")]
        [TestCase(5, "City=#", "City", "Town", false, "City", "Town", "")]
        [TestCase(5, "City=#", "City", "Town", true, "City", "Town", "")]
        [TestCase(5, "City= #", "Town", "", false, "Town", "", "#")]
        [TestCase(5, "City= #", "Town", "", true, "Town", "", "#")]
        [TestCase(5, "City=", "", "Baton Rouge", false, "City", "Baton Rouge", "")]
        [TestCase(5, "City=", "", "Baton Rouge", true, "City", "Baton Rouge", "")]
        public void TestUpdateParamFileLine(
            int lineNumber, string lineText,
            string newParamName, string newParamValue, bool updateTextProperty,
            string expectedParamName = "", string expectedValue = "", string expectedComment = "")
        {
            var paramFileLine = new KeyValueParamFileLine(lineNumber, lineText, true);

            if (newParamName.Equals(string.Empty))
                newParamName = paramFileLine.ParamName;

            paramFileLine.StoreParameter(newParamName, newParamValue, paramFileLine.Comment, updateTextProperty);

            Console.WriteLine("ParamName=[{0}], Value=[{1}], Comment=[{2}]", paramFileLine.ParamName, paramFileLine.ParamValue, paramFileLine.Comment);
            Console.WriteLine("TextLine=[{0}]", paramFileLine.Text);

            if (string.IsNullOrWhiteSpace(expectedParamName) && string.IsNullOrWhiteSpace(expectedValue))
                return;

            Assert.That(paramFileLine.LineNumber, Is.EqualTo(lineNumber), "Line number mismatch");

            if (updateTextProperty)
                Assert.That(paramFileLine.Text, Is.Not.EqualTo(lineText), "Full line text matched, but should not have");
            else
                Assert.That(paramFileLine.Text, Is.EqualTo(lineText), "Full line text mismatch, but should have matched");

            Assert.That(paramFileLine.ParamName, Is.EqualTo(expectedParamName), "Actual key does not match the expected key");
            Assert.That(paramFileLine.ParamValue, Is.EqualTo(expectedValue), "Actual value does not match the expected value");

            if (!expectedComment.Equals(string.Empty))
                Assert.That(paramFileLine.Comment, Is.EqualTo(expectedComment), "Actual comment does not match the expected comment");
        }
    }
}
