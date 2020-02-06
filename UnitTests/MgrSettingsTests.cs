using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PRISM.AppSettings;

namespace PRISMTest
{
    [TestFixture]
    class MgrSettingsTests
    {
        private readonly Dictionary<string, string> testProgSingleConfig = new Dictionary<string, string>()
        {
            { "MgrActive_Local", "False" },
            { "MgrCnfgDbConnectStr", "Data Source=mgrCtrlDbServer;Initial Catalog=manager_control;Integrated Security=SSPI" },
            { "MgrName", "Pub-xx-y" },
            { "UsingDefaults", "True" },
            { "DefaultDMSConnString", "Data Source=dmsDbServer;Initial Catalog=DMS5;Integrated Security=SSPI" },
        };

        private readonly Dictionary<string, string> testProgMultiConfig = new Dictionary<string, string>()
        {
            { "MgrActive_Local", "True" },
            { "MgrCnfgDbConnectStr", "Data Source=proteinseqs;Initial Catalog=manager_control;Integrated Security=SSPI" },
            { "MgrName", "PrismTest" },
            { "UsingDefaults", "False" },
            { "DefaultDMSConnString", "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI" },
        };

        private readonly Dictionary<string, string> testProg2MultiConfig = new Dictionary<string, string>()
        {
            { "MgrActive_Local", "True" },
            { "MgrCnfgDbConnectStr", "Data Source=proteinseqs;Initial Catalog=manager_control;Integrated Security=SSPI" },
            { "MgrName", "PrismTest" },
            { "UsingDefaults", "False" },
            { "DefaultDMSConnString", "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI" },
        };

        [Test]
        [Category("PNL_Domain")]
        public void TestLoadSingleFileConfig()
        {
            var configFile = new FileInfo(@"\\proto-2\UnitTest_Files\PRISM\MgrSettingsTests\TestProg.exe.config");
            Assert.IsTrue(configFile.Exists);
            Assert.IsNotNull(configFile.DirectoryName, "Could not determine the parent directory of the config file");

            var mgrSettings = new MgrSettings();
            var settings = mgrSettings.LoadMgrSettingsFromFile(configFile.FullName, new Dictionary<string, string>());

            foreach (var setting in testProgSingleConfig)
            {
                Assert.IsTrue(settings.ContainsKey(setting.Key));
                Assert.AreEqual(setting.Value, settings[setting.Key]);
            }
        }

        [Test]
        [Category("PNL_Domain")]
        public void TestLoadMultiFileConfig()
        {
            var configFile = new FileInfo(@"\\proto-2\UnitTest_Files\PRISM\MgrSettingsTests\TestProg.exe.config");
            Assert.IsTrue(configFile.Exists);
            Assert.IsNotNull(configFile.DirectoryName, "Could not determine the parent directory of the config file");

            var mgrSettings = new MgrSettings();
            var settings = mgrSettings.LoadMgrSettingsFromFile(configFile.FullName);

            foreach (var setting in testProgMultiConfig)
            {
                Assert.IsTrue(settings.ContainsKey(setting.Key));
                Assert.AreEqual(setting.Value, settings[setting.Key]);
            }
        }

        [Test]
        [Category("PNL_Domain")]
        public void TestLoadMultiFileConfig2()
        {
            var configFile = new FileInfo(@"\\proto-2\UnitTest_Files\PRISM\MgrSettingsTests\TestProg2.exe.config");
            Assert.IsTrue(configFile.Exists);
            Assert.IsNotNull(configFile.DirectoryName, "Could not determine the parent directory of the config file");

            var mgrSettings = new MgrSettings();
            var settings = mgrSettings.LoadMgrSettingsFromFile(configFile.FullName);

            foreach (var setting in testProg2MultiConfig)
            {
                Assert.IsTrue(settings.ContainsKey(setting.Key));
                Assert.AreEqual(setting.Value, settings[setting.Key]);
            }
        }
    }
}
