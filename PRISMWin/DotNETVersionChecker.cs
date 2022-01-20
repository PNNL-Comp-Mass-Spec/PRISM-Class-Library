using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global

namespace PRISMWin
{
    /// <summary>
    /// This classes examines the registry to determine the newest version of .NET installed
    /// </summary>
    public class DotNETVersionChecker
    {
        // ReSharper disable once CommentTypo
        // Ignore Spelling: hklm

        #region "Constants"

        private const string EARLIER_THAN_45 = "Earlier than 4.5";
        private const string UNKNOWN_VERSION = "Unknown .NET version";

        #endregion

        #region "Events and Event Handlers"

        /// <summary>
        /// Error event
        /// </summary>
        public event ErrorEventEventHandler ErrorEvent;

        /// <summary>
        /// Error event
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public delegate void ErrorEventEventHandler(string message, Exception ex);

        #endregion

        /// <summary>
        /// Determine the human-readable version of .NET
        /// </summary>
        /// <param name="releaseKey"></param>
        private static string CheckFor45DotVersion(int releaseKey)
        {
            // Checking the version using >= will enable forward compatibility,
            // however you should always compile your code on newer versions of
            // the framework to ensure your application works the same

            // For more information see https://msdn.microsoft.com/en-us/library/hh925568(v=vs.110).aspx
            // Also see https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/versions-and-dependencies

            return releaseKey switch
            {
                > 528049 => "Later than 4.8 (build " + releaseKey + ")",
                >= 528040 => "4.8",
                >= 461808 => "4.7.2",
                >= 461308 => "4.7.1",
                >= 460798 => "4.7",
                >= 394802 => "4.6.2",
                >= 394254 => "4.6.1",
                >= 393295 => "4.6",
                >= 379893 => "4.5.2",
                >= 378675 => "4.5.1",
                >= 378389 => "4.5",
                _ => EARLIER_THAN_45
            };
        }

        private static string GetDotNetVersion45OrLater()
        {
            // Alternative to RegistryKey.OpenRemoteBaseKey is RegistryKey.OpenBaseKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")
            // However, that can give odd behavior with 32-bit code on 64-bit Windows
            // This workaround seems to work, but .OpenRemoteBaseKey() works even better
            //
            // using (var localMachineHive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            // {
            //    var softwareKey = localMachineHive.OpenSubKey("SOFTWARE");
            //    var microsoftKey = softwareKey.OpenSubKey("Microsoft");
            //    var netFrameworkKey = microsoftKey.OpenSubKey("NET Framework Setup");
            //    var ndpKey = netFrameworkKey.OpenSubKey("NDP");
            //    var v4Key = ndpKey.OpenSubKey("v4");
            //    var v4FullKey = v4Key.OpenSubKey("Full");

            //    var releaseValue = v4FullKey?.GetValue("Release");
            //    if (releaseValue != null)
            //    {
            //        var latestVersion = CheckFor45DotVersion(Convert.ToInt32(releaseValue));
            //        return latestVersion;
            //    }
            // }

            using var ndpKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "");

            var v4FullKey = ndpKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");

            var releaseValue = v4FullKey?.GetValue("Release");
            if (releaseValue != null)
            {
                var latestVersion = CheckFor45DotVersion(Convert.ToInt32(releaseValue));
                return latestVersion;
            }

            return string.Empty;
        }

        /// <summary>
        /// Get all installed versions of .NET
        /// </summary>
        public Dictionary<int, List<string>> GetInstalledDotNETVersions()
        {
            return GetInstalledDotNETVersions(false);
        }

        /// <summary>
        /// Get all installed versions of .NET
        /// </summary>
        private static Dictionary<int, List<string>> GetInstalledDotNETVersions(bool findingLegacyVersions)
        {
            // Keys in dotNETVersions are major versions (2, 3, or 4)
            // Values are a list of minor versions
            var dotNETVersions = new Dictionary<int, List<string>>();

            if (!findingLegacyVersions)
            {
                var latestVersion = GetDotNetVersion45OrLater();

                if (!string.Equals(latestVersion, EARLIER_THAN_45))
                {
                    var majorVersion = GetMajorVersion(latestVersion);
                    if (majorVersion > 0)
                    {
                        StoreVersion(dotNETVersions, majorVersion, latestVersion);
                    }
                }
            }

            // Opens the registry key for the .NET Framework entry
            using var ndpKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\");

            if (ndpKey == null)
                return dotNETVersions;

            foreach (var versionKeyName in ndpKey.GetSubKeyNames())
            {
                if (!versionKeyName.StartsWith("v"))
                    continue;

                var versionKey = ndpKey.OpenSubKey(versionKeyName);
                if (versionKey == null)
                    continue;

                var majorVersion = GetMajorVersion(versionKeyName);

                var versionName = (string)versionKey.GetValue("Version", "");
                var versionSP = versionKey.GetValue("SP", "").ToString();
                var versionInstall = versionKey.GetValue("Install", "").ToString();

                if (versionInstall.Length == 0)
                {
                    // No service pack install; store this version
                    StoreVersion(dotNETVersions, majorVersion, versionName);
                }
                else
                {
                    if (versionSP != "" && versionInstall == "1")
                    {
                        StoreVersion(dotNETVersions, majorVersion, versionName + "  SP" + versionSP);
                    }
                }

                if (versionName != "")
                {
                    continue;
                }

                foreach (var subKeyName in versionKey.GetSubKeyNames())
                {
                    var subKey = versionKey.OpenSubKey(subKeyName);
                    if (subKey == null)
                        continue;

                    var subKeyVersionName = (string)subKey.GetValue("Version", "");
                    var subKeySP = "";

                    if (subKeyVersionName != "")
                        subKeySP = subKey.GetValue("SP", "").ToString();

                    var subKeyInstall = subKey.GetValue("Install", "").ToString();
                    if (subKeyInstall.Length == 0)
                    {
                        // No service pack install; store this version
                        StoreVersion(dotNETVersions, majorVersion, subKeyVersionName);
                    }
                    else
                    {
                        if (subKeySP != "" && subKeyInstall == "1")
                        {
                            StoreVersion(dotNETVersions, majorVersion, subKeyName + "  " + subKeyVersionName + "  SP" + subKeySP);
                        }
                        else if (subKeyInstall == "1")
                        {
                            StoreVersion(dotNETVersions, majorVersion, subKeyName + "  " + subKeyVersionName);
                        }
                    }
                }
            }

            return dotNETVersions;
        }

        /// <summary>
        /// Lookup the newest version of .NET in the registry
        /// </summary>
        /// <returns>.NET version</returns>
        public string GetLatestDotNETVersion()
        {
            try
            {
                var latestVersion = GetDotNetVersion45OrLater();
                if (!string.Equals(latestVersion, EARLIER_THAN_45))
                {
                    return latestVersion;
                }

                var dotNETVersions = GetInstalledDotNETVersions(true);

                // Find the newest version in dotNETVersions
                var newestMajorVersion = (from item in dotNETVersions orderby item.Key select item.Value).Last();
                if (newestMajorVersion.Count == 0)
                {
                    return UNKNOWN_VERSION;
                }

                var reVersionMatch = new Regex(@"(?<Major>\d+)\.(?<Minor>\d+)\.(?<Build>\d+)");

                // Find the highest version in newestMajorVersion
                var newestVersion = "";
                var newestMajor = 0;
                var newestMinor = 0;
                var newestBuild = 0;

                foreach (var installedVersion in newestMajorVersion)
                {
                    var match = reVersionMatch.Match(installedVersion);
                    if (!match.Success)
                        continue;

                    var major = int.Parse(match.Groups["Major"].Value);
                    var minor = int.Parse(match.Groups["Minor"].Value);
                    var build = int.Parse(match.Groups["Build"].Value);

                    var updateNewest = false;
                    if (string.IsNullOrWhiteSpace(newestVersion))
                    {
                        updateNewest = true;
                    }
                    else
                    {
                        if (major > newestMajor)
                            updateNewest = true;
                        else if (major == newestMajor && minor > newestMinor)
                            updateNewest = true;
                        else if (major == newestMajor && minor == newestMinor && build > newestBuild)
                            updateNewest = true;
                    }

                    if (updateNewest)
                    {
                        newestVersion = installedVersion;
                        newestMajor = major;
                        newestMinor = minor;
                        newestBuild = build;
                    }
                }

                if (string.IsNullOrWhiteSpace(newestVersion))
                    return UNKNOWN_VERSION;

                return newestVersion;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error determining the .NET version: " + ex.Message, ex);
                return UNKNOWN_VERSION;
            }
        }

        /// <summary>
        /// Look for the version integer in versionKeyName
        /// </summary>
        /// <param name="versionKeyName"></param>
        private static int GetMajorVersion(string versionKeyName)
        {
            // This RegEx is used to find the first integer in a string
            var reVersionMatch = new Regex(@"(?<Version>\d+)");

            var versionMatch = reVersionMatch.Match(versionKeyName);
            if (versionMatch.Success)
            {
                return int.Parse(versionMatch.Groups["Version"].Value);
            }

            return 0;
        }

        /// <summary>
        /// Report an error
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex">Exception (allowed to be nothing)</param>
        protected void OnErrorEvent(string message, Exception ex)
        {
            ErrorEvent?.Invoke(message, ex);
        }

        private static void StoreVersion(IDictionary<int, List<string>> dotNETVersions, int majorVersion, string specificVersion)
        {
            if (string.IsNullOrWhiteSpace(specificVersion))
                return;

            if (dotNETVersions.TryGetValue(majorVersion, out var installedVariants))
            {
                installedVariants.Add(specificVersion);
            }
            else
            {
                installedVariants = new List<string>()
                {
                    specificVersion
                };

                dotNETVersions.Add(majorVersion, installedVariants);
            }
        }
    }
}
