using System;
using NUnit.Framework;
using PRISM;

namespace PRISMTest
{
    [TestFixture]
    class DotNETVersionTest
    {
        [Test]
        public void TestGetLatestDotNETVersion()
        {
            var versionChecker = new clsDotNETVersionChecker();

            var latestVersion = versionChecker.GetLatestDotNETVersion();

            Console.WriteLine(latestVersion);
        }

        [Test]
        public void TestGetInstalledDotNETVersions()
        {
            var versionChecker = new clsDotNETVersionChecker();

            var installedVersions = versionChecker.GetInstalledDotNETVersions();

            foreach (var majorVersion in installedVersions)
            {
                foreach (var installedVersion in majorVersion.Value)
                    Console.WriteLine(installedVersion);
            }
            
        }
    }
}
