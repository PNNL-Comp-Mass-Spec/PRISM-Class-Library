using System;
using NUnit.Framework;
#if !(NETCOREAPP2_0)
using PRISMWin;
#endif

namespace PRISMTest
{
    [TestFixture]
    class DotNETVersionTest
    {
#if !(NETCOREAPP2_0)
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
#endif
    }
}
