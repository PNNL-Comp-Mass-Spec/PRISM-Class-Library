using System;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class TestOSVersionInfo
    {

        [Test]
        public void TestGetOSVersion()
        {
            var runningLinux = Path.DirectorySeparatorChar == '/';

            var osVersionInfo = new clsOSVersionInfo();

            var version = osVersionInfo.GetOSVersion();

            if (runningLinux)
            {
                var linuxVersion = osVersionInfo.GetLinuxVersion();
                Assert.AreEqual(version, linuxVersion);
                return;
            }

            Console.WriteLine(version);

            var osInfo = Environment.OSVersion;
            switch (osInfo.Platform)
            {
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                    Assert.True(version.StartsWith("Windows", StringComparison.OrdinalIgnoreCase));
                    break;
                case PlatformID.Win32S:
                    Assert.True(version.StartsWith("Win32", StringComparison.OrdinalIgnoreCase));
                    break;
                case PlatformID.WinCE:
                    Assert.True(version.StartsWith("WinCE", StringComparison.OrdinalIgnoreCase));
                    break;
                case PlatformID.Unix:
                    var linuxVersion = osVersionInfo.GetLinuxVersion();
                    Assert.AreEqual(version, linuxVersion);
                    break;
                case PlatformID.Xbox:
                    Assert.True(version.StartsWith("Xbox", StringComparison.OrdinalIgnoreCase));
                    break;
                case PlatformID.MacOSX:
                    Assert.True(version.StartsWith("MacOS", StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    Assert.Ignore("Unknown OS Platform");
                    break;
            }


        }

        [Test]
        public void TestGetLinuxVersion()
        {
            var etcFolder = new DirectoryInfo("/etc");

            if (!etcFolder.Exists)
            {
                Assert.Ignore("/etc folder not found at " + etcFolder.FullName);
            }

            var osVersionInfo = new clsOSVersionInfo();

            var version = osVersionInfo.GetLinuxVersion();

            Console.WriteLine(version);
        }
    }
}