using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PRISM.AppSettings;
using PRISMDatabaseUtils.AppSettings;

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
        [TestCase("MgrActive_Local", "False")]
        [TestCase("MgrName", "Pub-xx-y")]
        [TestCase("MgrCnfgDbConnectStr", "Data Source=mgrCtrlDbServer;Initial Catalog=manager_control;Integrated Security=SSPI")]
        public void TestGetXmlConfigFileSetting(string settingName, string expectedValue)
        {
            var configFile = new FileInfo(@"\\proto-2\UnitTest_Files\PRISM\MgrSettingsTests\TestProg.exe.config");

            Assert.IsTrue(configFile.Exists);
            Assert.IsNotNull(configFile.DirectoryName, "Could not determine the parent directory of the config file");

            var configFilePaths = new List<string>()
            {
                configFile.FullName
            };

            var mgrSettings = new MgrSettings();
            var settingFound = mgrSettings.GetXmlConfigFileSetting(configFilePaths, settingName, out var settingValue);
            Assert.IsTrue(settingFound);

            Console.WriteLine("Value for {0} is '{1}'", settingName, settingValue);

            Assert.AreEqual(expectedValue, settingValue);
        }

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

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestLoadManagerConfigDBSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestLoadManagerConfigDB(connectionString);
        }

        [TestCase("prismweb3", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestLoadManagerConfigDBPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestLoadManagerConfigDB(connectionString);
        }

        private void TestLoadManagerConfigDB(string connectionString)
        {
            var mgrSettings = new MgrSettingsDB();
            var testSettings = new Dictionary<string, string>()
            {
                { MgrSettings.MGR_PARAM_MGR_CFG_DB_CONN_STRING, connectionString },
                { MgrSettings.MGR_PARAM_MGR_ACTIVE_LOCAL, "True" },
                { MgrSettings.MGR_PARAM_MGR_NAME, "Proto-5_InstDirScan" },
                { MgrSettings.MGR_PARAM_USING_DEFAULTS, "False" },
            };

            mgrSettings.LoadSettings(testSettings, true);

            var expectedSettings = new Dictionary<string, string>()
            {
                { "workdir", @"\\gigasax\DMS_InstSourceDirScans" },
                { "bionetuser", "ftms" },
                { "configfilename", "DMS_InstDirScanner.exe.config" },
                { "localmgrpath", @"C:\DMS_Programs" },
                { "programfoldername", "InstDirScanner" },
                { "MessageQueueTopicMgrStatus", "Manager.InstDirScan" },
            };

            foreach (var expected in expectedSettings)
            {
                if (mgrSettings.MgrParams.TryGetValue(expected.Key, out var actual))
                {
                    Assert.AreEqual(expected.Value, actual, "Parameter value is different");
                }
                else
                {
                    Assert.Fail($"Expected parameter with name {expected.Key}, but it does not exist.");
                }
            }
        }
    }
}
