using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PRISM.AppSettings;
using PRISMDatabaseUtils.AppSettings;

// ReSharper disable StringLiteralTypo

namespace PRISMTest
{
    [TestFixture]
    internal class MgrSettingsTests
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: Ctrl, dms, dmsreader, ftms, postgresql, proteinseqs, Seqs

        // ReSharper restore CommentTypo

        private static readonly SortedSet<string> mValidatedConnectionStrings = new();

        private readonly Dictionary<string, string> testProgSingleConfig = new()
        {
            { "MgrActive_Local", "False" },
            { "MgrCnfgDbConnectStr", "Data Source=mgrCtrlDbServer;Initial Catalog=manager_control;Integrated Security=SSPI" },
            { "MgrName", "Pub-xx-y" },
            { "UsingDefaults", "True" },
            { "DefaultDMSConnString", "Data Source=dmsDbServer;Initial Catalog=DMS5;Integrated Security=SSPI" }
        };

        private readonly Dictionary<string, string> testProgMultiConfig = new()
        {
            { "MgrActive_Local", "True" },
            { "MgrCnfgDbConnectStr", "Data Source=proteinseqs;Initial Catalog=manager_control;Integrated Security=SSPI" },
            { "MgrName", "PrismTest" },
            { "UsingDefaults", "False" },
            { "DefaultDMSConnString", "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI" }
        };

        private readonly Dictionary<string, string> testProg2MultiConfig = new()
        {
            { "MgrActive_Local", "True" },
            { "MgrCnfgDbConnectStr", "Data Source=proteinseqs;Initial Catalog=manager_control;Integrated Security=SSPI" },
            { "MgrName", "PrismTest" },
            { "UsingDefaults", "False" },
            { "DefaultDMSConnString", "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI" }
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

            var configFilePaths = new List<string>
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

            ValidateSettings(testProgSingleConfig, settings);
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

            ValidateSettings(testProgMultiConfig, settings);
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

            ValidateSettings(testProg2MultiConfig, settings);
        }

        private void ValidateSettings(Dictionary<string, string> expectedSettings, IReadOnlyDictionary<string, string> settings)
        {
            foreach (var setting in expectedSettings)
            {
                Assert.IsTrue(settings.ContainsKey(setting.Key));

                Console.WriteLine("Value for {0,-30} {1}", setting.Key + ":", settings[setting.Key]);
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

        [TestCase("prismdb1", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestLoadManagerConfigDBPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestLoadManagerConfigDB(connectionString);
        }

        [TestCase("prismdb1", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestLoadManagerConfigDBPostgresPgPass(string server, string database)
        {
            // The password for the dmsreader user will read from file c:\users\CurrentUser\AppData\Roaming\postgresql\pgpass.conf
            // Authentication will fail if that file does not exist

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);
            TestLoadManagerConfigDB(connectionString);
        }

        private void TestLoadManagerConfigDB(string connectionString)
        {
            var mgrSettings = new MgrSettingsDB();
            var testSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { MgrSettings.MGR_PARAM_MGR_CFG_DB_CONN_STRING, connectionString },
                { MgrSettings.MGR_PARAM_MGR_ACTIVE_LOCAL, "True" },
                { MgrSettings.MGR_PARAM_MGR_NAME, "Proto-4_InstDirScan" },
                { MgrSettings.MGR_PARAM_USING_DEFAULTS, "False" },
                { "SchemaPrefix.DMS", "DMS5.dbo" },
                { "SchemaPrefix.DMSPipeline", "[DMS_Pipeline].dbo" },
                { "SchemaPrefix.DMSCapture", "DMS_Capture" },
            };

            Console.WriteLine("Connecting to database using " + connectionString);

            mgrSettings.LoadSettings(testSettings, true);

            if (!mValidatedConnectionStrings.Contains(connectionString))
            {
                Console.WriteLine();
                mgrSettings.ValidatePgPass();
                Console.WriteLine();

                mValidatedConnectionStrings.Add(connectionString);
            }

            var expectedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "WorkDir", @"\\gigasax\DMS_InstSourceDirScans" },
                { "BionetUser", "ftms" },
                { "ConfigFileName", "DMS_InstDirScanner.exe.config" },
                { "LocalMgrPath", @"C:\DMS_Programs" },
                { "ProgramFolderName", "InstDirScanner" },
                { "MessageQueueTopicMgrStatus", "Manager.InstDirScan" },
            };

            foreach (var expected in expectedSettings)
            {
                if (mgrSettings.MgrParams.TryGetValue(expected.Key, out var actual))
                {
                    Console.WriteLine("Value for {0,-30} {1}", expected.Key + ":", actual);
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
