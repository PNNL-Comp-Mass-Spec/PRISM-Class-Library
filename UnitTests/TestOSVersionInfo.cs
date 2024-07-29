using System;
using System.IO;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    internal class TestOSVersionInfo
    {
        // Ignore Spelling: Xbox

        [Test]
        public void TestGetOSVersion()
        {
            var runningLinux = Path.DirectorySeparatorChar == '/';

            var osVersionInfo = new OSVersionInfo();

            var version = osVersionInfo.GetOSVersion();

            if (runningLinux)
            {
                var linuxVersion = osVersionInfo.GetLinuxVersion();
                Assert.That(linuxVersion, Is.EqualTo(version));
                return;
            }

            Console.WriteLine(version);

            var osInfo = Environment.OSVersion;
            switch (osInfo.Platform)
            {
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                    Assert.That(version.StartsWith("Windows", StringComparison.OrdinalIgnoreCase), Is.True);
                    break;
                case PlatformID.Win32S:
                    Assert.That(version.StartsWith("Win32", StringComparison.OrdinalIgnoreCase), Is.True);
                    break;
                case PlatformID.WinCE:
                    Assert.That(version.StartsWith("WinCE", StringComparison.OrdinalIgnoreCase), Is.True);
                    break;
                case PlatformID.Unix:
                    var linuxVersion = osVersionInfo.GetLinuxVersion();
                    Assert.That(linuxVersion, Is.EqualTo(version));
                    break;
                case PlatformID.Xbox:
                    Assert.That(version.StartsWith("Xbox", StringComparison.OrdinalIgnoreCase), Is.True);
                    break;
                case PlatformID.MacOSX:
                    Assert.That(version.StartsWith("MacOS", StringComparison.OrdinalIgnoreCase), Is.True);
                    break;
                default:
                    Assert.Ignore("Unknown OS Platform");
                    break;
            }
        }

        [Test]
        public void TestGetLinuxVersion()
        {
            var etcDirectory = new DirectoryInfo("/etc");

            if (!etcDirectory.Exists)
            {
                Assert.Ignore("/etc directory not found at " + etcDirectory.FullName);
            }

            var osVersionInfo = new OSVersionInfo();

            var version = osVersionInfo.GetLinuxVersion();

            Console.WriteLine(version);
        }
    }
}