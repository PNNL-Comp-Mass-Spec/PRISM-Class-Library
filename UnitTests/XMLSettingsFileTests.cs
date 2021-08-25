using System;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    internal class XMLSettingsFileTests
    {
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\LTQ-FT_10ppm_2014-08-06.xml", "MasicExportOptions", "WriteExtendedStats", "True")]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\LTQ-FT_10ppm_2014-08-06.xml", "SICOptions", "SICTolerance", "10.0000")]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\LTQ-FT_10ppm_2014-08-06.xml", "BinningOptions", "BinEndX", "2000")]
        [Category("PNL_Domain")]
        public void ReadSettings(string settingsFilePath, string sectionName, string settingName, string expectedValue)
        {
            var reader = new XmlSettingsFileAccessor();
            reader.LoadSettings(settingsFilePath, false);

            if (bool.TryParse(expectedValue, out var expectedBool))
            {
                var actualBool = reader.GetParam(sectionName, settingName, false, out var valueNotPresent);

                if (valueNotPresent)
                {
                    Assert.Fail("Setting not found, section {0}, setting {1}", sectionName, settingName);
                }
                Assert.AreEqual(expectedBool, actualBool, "Unexpected boolean for section {0}, setting {1}: {2}", sectionName, settingName, actualBool);

                Console.WriteLine("Value for section {0}, setting {1} is {2}", sectionName, settingName, actualBool);
                return;
            }

            var actualValue = reader.GetParam(sectionName, settingName, "", out _);
            Assert.AreEqual(expectedValue, actualValue, "Unexpected value for section {0}, setting {1}: {2}", sectionName, settingName, actualValue);

            Console.WriteLine("Value for section {0}, setting {1} is {2}", sectionName, settingName, actualValue);
        }

        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\LTQ-FT_10ppm_2014-08-06.xml", "TestOptions", "SaveDate", "Now")]
        [TestCase(@"\\proto-2\UnitTest_Files\PRISM\LTQ-FT_10ppm_2014-08-06.xml", "TestOptions", "Version", "1.0.2")]
        [Category("PNL_Domain")]
        public void SaveSettings(string settingsFilePath, string sectionName, string settingName, string value)
        {
            var reader = new XmlSettingsFileAccessor();
            reader.LoadSettings(settingsFilePath, false);

            if (value == "Now")
                value = DateTime.Now.ToString(FileTools.DATE_TIME_FORMAT);

            reader.SetParam(sectionName, settingName, value);

            reader.SaveSettings();

            Console.WriteLine("Setting {0} updated in file {1}", settingName, reader.XMLFilePath);
        }
    }
}