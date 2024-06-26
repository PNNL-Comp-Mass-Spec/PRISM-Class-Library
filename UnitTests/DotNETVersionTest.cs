﻿using System;
using NUnit.Framework;
#if !NETCOREAPP2_0
using PRISMWin;
#endif

namespace PRISMTest
{
    [TestFixture]
    internal class DotNETVersionTest
    {
#if !NETCOREAPP2_0
        [Test]
        public void TestGetLatestDotNETVersion()
        {
            var versionChecker = new DotNETVersionChecker();

            var latestVersion = versionChecker.GetLatestDotNETVersion();

            Console.WriteLine(latestVersion);
        }

        [Test]
        public void TestGetInstalledDotNETVersions()
        {
            var versionChecker = new DotNETVersionChecker();

            foreach (var majorVersion in versionChecker.GetInstalledDotNETVersions())
            {
                foreach (var installedVersion in majorVersion.Value)
                {
                    Console.WriteLine(installedVersion);
                }
            }
        }
#endif
    }
}
