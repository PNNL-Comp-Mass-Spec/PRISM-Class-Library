using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PRISM
{
    /// <summary>
    /// Class for determining the currently running operating system
    /// Based on https://code.msdn.microsoft.com/windowsapps/How-to-determine-the-263b1850
    /// </summary>
    /// <remarks>For Windows and Linux, reports details about the OS version</remarks>
    public class clsOSVersionInfo
    {

        /// <summary>
        /// Determine the operating system version
        /// </summary>
        /// <returns>Human-readable description of the OS version</returns>
        /// <remarks>For Windows and Linux, reports details about the OS version</remarks>
        public string GetOSVersion()
        {
#if !(NETSTANDARD1_x)
            var osInfo = Environment.OSVersion;

            switch (osInfo.Platform)
            {
                // For old Windows kernel
                case PlatformID.Win32Windows:
                    return GetWin32Version(osInfo);
                // For NT kernel
                case PlatformID.Win32NT:
                    return GetWinNTVersion(osInfo);
                case PlatformID.Win32S:
                    return "Win32S";
                case PlatformID.WinCE:
                    return "WinCE";
                case PlatformID.Unix:
                    return GetLinuxVersion();
                case PlatformID.Xbox:
                    return "Xbox";
                case PlatformID.MacOSX:
                    return "MacOSX";
                default:
                    return "Unknown";
            }
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsVersion(RuntimeInformation.OSDescription);
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxVersion();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "MacOSX";
            }
            return "Unknown";
#endif
        }

        /// <summary>
        /// Determine the version of Linux that we're running
        /// </summary>
        /// <returns>String describing the OS version</returns>
        /// <remarks>If run on Windows, will look for files in the \etc folder on the current drive</remarks>
        public string GetLinuxVersion()
        {
            var versionFiles = new Dictionary<string, string>
            {
                {"Redhat", "/etc/redhat-release"},
                {"Centos", "/etc/centos-release"},
                {"Ubuntu", "/etc/lsb-release"},
                {"Debian", "/etc/debian_version"},
                {"Fedora", "/etc/fedora-release"},
                {"Gentoo", "gentoo-release"},
                {"SuSE through v13.0", "/etc/SuSE-release"},
                {"Mandriva", "/etc/mandriva-release"},
                {"Slackware", "/etc/slackware-version"},
                {"Generic", "/etc/os-release"},     // Used by SuSE 13.1, plus others
                {"Solaris", "/etc/release"}
            };

            foreach (var versionFileInfo in versionFiles)
            {
                var versionFile = new FileInfo(versionFileInfo.Value);
                if (!versionFile.Exists)
                    continue;

                List<string> versionInfo;

                using (var reader = new StreamReader(new FileStream(versionFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    switch (versionFileInfo.Key)
                    {
                        case "Ubuntu":
                            versionInfo = GetUbuntuVersion(reader);
                            break;
                        case "Generic":
                            versionInfo = GetOSReleaseVersion(reader);
                            break;
                        default:
                            var versionText = GetFirstLineVersion(reader, versionFileInfo.Key);
                            versionInfo = new List<string>
                            {
                                versionText
                            };
                            break;
                    }
                }

                if (versionInfo.Count == 0)
                    continue;

                if (versionInfo.Count == 1)
                {
                    return versionInfo.First();
                }

                if (versionInfo.Count > 1)
                {
                    return string.Join("; ", versionInfo);
                }

            }

            // Expected Linux version files were not found
            // Find the first "release" file in the etc folder

            var etcFolder = new DirectoryInfo("/etc");

            if (!etcFolder.Exists)
            {
                return "Unknown";
            }

            foreach (var releaseFile in etcFolder.GetFiles("*release"))
            {
                var dataDisplayed = new SortedSet<string>();

                using (var reader = new StreamReader(new FileStream(releaseFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = StripQuotes(reader.ReadLine());

                        if (string.IsNullOrWhiteSpace(dataLine))
                        {
                            continue;
                        }

                        if (dataDisplayed.Contains(dataLine))
                            continue;

                        dataDisplayed.Add(dataLine);
                    }
                }

                if (dataDisplayed.Count > 0)
                    return string.Join("; ", dataDisplayed);
            }

            return "Unknown";
        }

        /// <summary>
        /// Return the first line of an operating system version file
        /// </summary>
        /// <param name="versionFilePath"></param>
        /// <param name="osName">Operating system name (empty by default); if non-blank, the version returned is guaranteed to contain this text</param>
        /// <returns></returns>
        public string GetFirstLineVersion(string versionFilePath, string osName = "")
        {
            var versionFile = new FileInfo(versionFilePath);
            if (!versionFile.Exists)
            {
                return string.IsNullOrWhiteSpace(osName) ? string.Empty : osName;
            }

            using (var reader = new StreamReader(new FileStream(versionFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                return GetFirstLineVersion(reader, osName);
            }

        }

        private string GetFirstLineVersion(StreamReader reader, string osName)
        {

            // The first line should have the version info
            while (!reader.EndOfStream)
            {
                var dataLine = StripQuotes(reader.ReadLine());
                if (string.IsNullOrWhiteSpace(dataLine))
                    continue;

                if (!string.IsNullOrWhiteSpace(osName) && !dataLine.ToLower().Contains(osName))
                    return osName + " " + dataLine.Trim();

                return dataLine.Trim();
            }

            return string.IsNullOrWhiteSpace(osName) ? string.Empty : osName;
        }

        private IEnumerable<string> GetFirstNValues(Dictionary<string, string> contents, int valuesToReturn)
        {
            var uniqueValues = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in contents)
            {
                if (uniqueValues.Contains(item.Value))
                    continue;

                uniqueValues.Add(item.Value);
                yield return item.Value;

                if (uniqueValues.Count == valuesToReturn)
                    break;
            }

        }

        /// <summary>
        /// Parse version information from an os-release file
        /// </summary>
        /// <param name="osReleaseFilePath"></param>
        /// <returns></returns>
        public string GetOSReleaseVersion(string osReleaseFilePath)
        {
            var versionFile = new FileInfo(osReleaseFilePath);
            if (!versionFile.Exists)
                return string.Empty;

            List<string> versionInfo;

            using (var reader = new StreamReader(new FileStream(versionFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                versionInfo = GetOSReleaseVersion(reader);
            }

            if (versionInfo.Count == 0)
                return string.Empty;

            if (versionInfo.Count == 1)
            {
                return versionInfo.First();
            }

            return string.Join("; ", versionInfo);
        }

        private List<string> GetOSReleaseVersion(StreamReader reader)
        {
            var versionInfo = new List<string>();

            // Expected format of the os-release file
            // NAME="Ubuntu"
            // VERSION="17.04 (Zesty Zeus)"
            // ID=ubuntu
            // ID_LIKE=debian
            // PRETTY_VERSION= "Ubuntu 17.04"
            var contents = ReadReleaseFile(reader);

            if (contents.TryGetValue("PRETTY_VERSION", out var description))
            {
                versionInfo.Add(description);
                return versionInfo;
            }

            if (contents.TryGetValue("NAME", out var osName))
            {
                if (contents.TryGetValue("VERSION", out var osVersion))
                {
                    versionInfo.Add(osName + " " + osVersion);
                    return versionInfo;
                }
            }

            // Not in the expected format; add the first 4 non-duplicate values
            versionInfo.AddRange(GetFirstNValues(contents, 4));
            return versionInfo;
        }

        /// <summary>
        /// Parse version information from an Ubuntu lsb-release file
        /// </summary>
        /// <param name="lsbReleaseFilePath"></param>
        /// <returns></returns>
        public string GetUbuntuVersion(string lsbReleaseFilePath)
        {
            var versionFile = new FileInfo(lsbReleaseFilePath);
            if (!versionFile.Exists)
                return string.Empty;

            List<string> versionInfo;

            using (var reader = new StreamReader(new FileStream(versionFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                versionInfo = GetUbuntuVersion(reader);
            }

            if (versionInfo.Count == 0)
                return string.Empty;

            if (versionInfo.Count == 1)
            {
                return versionInfo.First();
            }

            return string.Join("; ", versionInfo);
        }

        private List<string> GetUbuntuVersion(StreamReader reader)
        {
            var versionInfo = new List<string>();

            // Expected format of the lsb-release file
            // DISTRIB_ID=Ubuntu
            // DISTRIB_RELEASE=17.04
            // DISTRIB_CODENAME=Zesty
            // DISTRIB_DESCRIPTION="Ubuntu 17.04"
            var contents = ReadReleaseFile(reader);

            if (contents.TryGetValue("DISTRIB_DESCRIPTION", out var description))
            {
                versionInfo.Add(description);
                return versionInfo;
            }

            if (contents.TryGetValue("DISTRIB_ID", out var distribId))
            {
                if (contents.TryGetValue("DISTRIB_RELEASE", out var distribRelease))
                {
                    versionInfo.Add(distribId + " " + distribRelease);
                    return versionInfo;
                }
            }

            // Not in the expected format; add the first 4 non-duplicate values
            versionInfo.AddRange(GetFirstNValues(contents, 4));
            return versionInfo;
        }

#if !(NETSTANDARD1_x)
        /// <summary>
        /// For old windows kernel
        /// </summary>
        /// <param name="osInfo"></param>
        /// <returns></returns>
        private string GetWin32Version(OperatingSystem osInfo)
        {

            //Code to determine specific version of Windows 95,
            //Windows 98, Windows 98 Second Edition, or Windows Me.
            switch (osInfo.Version.Minor)
            {
                case 0:
                    return "Windows 95";
                case 10:
                    switch (osInfo.Version.Revision.ToString())
                    {
                        case "2222A":
                            return "Windows 98 Second Edition";
                        default:
                            return "Windows 98";
                    }
                case 90:
                    return "Windows Me";
                default:
                    return "Unknown";
            }

        }

        /// <summary>
        /// For NT kernel
        /// </summary>
        /// <param name="osInfo"></param>
        /// <returns></returns>
        private string GetWinNTVersion(OperatingSystem osInfo)
        {

            // Code to determine specific version of Windows NT 3.51,
            // Windows NT 4.0, Windows 2000, or Windows XP.
            switch (osInfo.Version.Major)
            {
                case 3:
                    return "Windows NT 3.51";
                case 4:
                    return "Windows NT 4.0";
                case 5:
                    switch (osInfo.Version.Minor)
                    {
                        case 0:
                            return "Windows 2000";
                        case 1:
                            return "Windows XP";
                        case 2:
                            return "Windows 2003";
                    }
                    break;
                case 6:
                    switch (osInfo.Version.Minor)
                    {
                        case 0:
                            return "Windows Vista";
                        case 1:
                            return "Windows 7";
                        case 2:
                            return "Windows 8";
                        case 3:
                            return "Windows 8.1";
                    }
                    break;
                case 10:
                    return "Windows 10";
                default:
                    if (osInfo.Version.Major > 10)
                        return "Windows " + osInfo.Version.Major;

                    break;
            }

            return "Unknown";
        }
#endif

        private string GetWindowsVersion(string osDescription)
        {
            var versionMatch = new Regex(@" (?<Major>\d+)\.(?<Minor>\d+)\.(?<Build>\d+)");
            var version = versionMatch.Match(osDescription);
            var versionMajor = int.Parse(version.Groups["Major"].Value);
            var versionMinor = int.Parse(version.Groups["Minor"].Value);
            //var versionBuild = int.Parse(version.Groups["Build"].Value);

            // Code to determine specific version of Windows NT 3.51,
            // Windows NT 4.0, Windows 2000, or Windows XP.
            switch (versionMajor)
            {
                case 3:
                    return "Windows NT 3.51";
                case 4:
                    return "Windows NT 4.0";
                case 5:
                    switch (versionMinor)
                    {
                        case 0:
                            return "Windows 2000";
                        case 1:
                            return "Windows XP";
                        case 2:
                            return "Windows 2003";
                    }
                    break;
                case 6:
                    switch (versionMinor)
                    {
                        case 0:
                            return "Windows Vista";
                        case 1:
                            return "Windows 7";
                        case 2:
                            return "Windows 8";
                        case 3:
                            return "Windows 8.1";
                    }
                    break;
                case 10:
                    return "Windows 10";
                default:
                    if (versionMajor > 10)
                        return "Windows " + versionMajor;

                    break;
            }

            return "Unknown";
        }

        /// <summary>
        /// Read a Linux os-release file or similar release file
        /// where the contents are expected to be in the form KEY=Value
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private Dictionary<string, string> ReadReleaseFile(StreamReader reader)
        {
            var contents = new List<string>();
            var osInfo = new Dictionary<string, string>();

            var sepChars = new[] { '=' };

            while (!reader.EndOfStream)
            {
                var dataLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(dataLine))
                    continue;

                contents.Add(dataLine);

                var lineParts = dataLine.Split(sepChars, 2);
                if (lineParts.Length > 1)
                {
                    osInfo.Add(lineParts[0], StripQuotes(lineParts[1]));
                }
            }

            if (osInfo.Count == 0 && contents.Count > 0)
            {
                for (var i = 1; i <= contents.Count; i++)
                {
                    osInfo.Add("Item" + i, contents[i]);
                }
                return osInfo;
            }

            return osInfo;

        }

        /// <summary>
        /// Remove leading and trailing double quotes
        /// </summary>
        /// <param name="dataLine"></param>
        /// <returns></returns>
        private string StripQuotes(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
                return string.Empty;

            return dataLine.TrimStart('"').TrimEnd('"');
        }

    }
}
