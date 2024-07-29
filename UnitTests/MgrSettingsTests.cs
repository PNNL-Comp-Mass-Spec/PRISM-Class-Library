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

        // Ignore Spelling: Ctrl, dms, dmsreader, ftms, postgres, postgresql, proteinseqs, Seqs, sql

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

            Assert.That(settingValue, Is.EqualTo(expectedValue));
        }

        [Test]
        [Category("PNL_Domain")]
        public void TestLoadSingleFileConfig()
        {
            var configFile = new FileInfo(@"\\proto-2\UnitTest_Files\PRISM\MgrSettingsTests\TestProg.exe.config");
            Assert.IsTrue(configFile.Exists);
            Assert.IsNotNull(configFile.DirectoryName, "Could not determine the parent directory of the config file");

            var mgrSettings = new MgrSettings();

            // Send an empty dictionary to LoadMgrSettingsFromFile() to use the method that only reads settings from a single .config file
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

            // Read the .exe.config file, plus other, related files (TestProg.exe.db.config and TestProg.exe.local.config)
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

            // Read the .exe.config file, plus other, related files (TestProg2.exe.db.config and TestProg2.exe.local.config)
            var settings = mgrSettings.LoadMgrSettingsFromFile(configFile.FullName);

            ValidateSettings(testProg2MultiConfig, settings);
        }

        private void ValidateSettings(Dictionary<string, string> expectedSettings, IReadOnlyDictionary<string, string> settings)
        {
            foreach (var setting in expectedSettings)
            {
                Assert.IsTrue(settings.ContainsKey(setting.Key));

                Console.WriteLine("Value for {0,-30} {1}", setting.Key + ":", settings[setting.Key]);
                Assert.That(settings[setting.Key], Is.EqualTo(setting.Value));
            }
        }

        [TestCase("ProteinSeqs", "Manager_Control")]
        [Category("DatabaseIntegrated")]
        public void TestLoadManagerConfigDBSqlServer(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringSqlServer(server, database, "Integrated", string.Empty);
            TestLoadManagerConfigDB(connectionString, false);
        }

        [TestCase("prismdb2", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestLoadManagerConfigDBPostgres(string server, string database)
        {
            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER, TestDBTools.DMS_READER_PASSWORD);
            TestLoadManagerConfigDB(connectionString, true);
        }

        [TestCase("prismdb2", "dms")]
        [Category("DatabaseNamedUser")]
        public void TestLoadManagerConfigDBPostgresPgPass(string server, string database)
        {
            // The password for the dmsreader user will read from file C:\users\username\AppData\Roaming\postgresql\pgpass.conf
            // Authentication will fail if that file does not exist

            // On Proto-2, the Jenkins service runs under the NETWORK SERVICE account
            // The required location for the pgpass file is: C:\Windows\ServiceProfiles\NetworkService\AppData\Roaming\postgresql\pgpass.conf

            var connectionString = TestDBTools.GetConnectionStringPostgres(server, database, TestDBTools.DMS_READER);
            TestLoadManagerConfigDB(connectionString, true);
        }

        private void TestLoadManagerConfigDB(string connectionString, bool isPostgres)
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
                { "SchemaPrefix.DMSCapture", "DMS_Capture.dbo" },
                { "SchemaPrefix.ManagerControl", "Manager_Control.dbo" },
            };

            if (isPostgres)
            {
                testSettings["SchemaPrefix.DMS"] = "public";
                testSettings["SchemaPrefix.DMSPipeline"] = "sw";
                testSettings["SchemaPrefix.DMSCapture"] = "cap";
                testSettings["SchemaPrefix.ManagerControl"] = "mc";
            }

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
                    Assert.That(actual, Is.EqualTo(expected.Value), "Parameter value is different");
                }
                else
                {
                    Assert.Fail($"Expected parameter with name {expected.Key}, but it does not exist.");
                }
            }
        }
    }
}
