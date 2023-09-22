using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

// ReSharper disable UnusedMember.Global

namespace PRISMWin
{
    public static class RegistryUtils
    {
        // Ignore Spelling: Utils

        /// <summary>
        /// Determines the directory that contains R.exe and Rcmd.exe (as defined in the Windows registry)
        /// </summary>
        /// <param name="errorMessage">Output: error message if an error, otherwise an empty string</param>
        /// <param name="callingFunction">>Name of the calling method (for logging purposes)</param>
        /// <returns>Directory path, e.g. C:\Program Files\R\R-3.2.2\bin\x64</returns>
        public static string GetRPathFromWindowsRegistry(out string errorMessage, [CallerMemberName] string callingFunction = "UnknownMethod")
        {
            // ReSharper disable once IdentifierTypo
            const string RCORE_SUBKEY = @"SOFTWARE\R-core";

            try
            {
                var regRCore = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\R-core");

                string parentKey;

                if (regRCore == null)
                {
                    // Local machine SOFTWARE\R-core not found; try current user
                    regRCore = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\R-core");

                    if (regRCore == null)
                    {
                        errorMessage = string.Format("Windows Registry key '{0}' not found in HKEY_LOCAL_MACHINE nor HKEY_CURRENT_USER", RCORE_SUBKEY);
                        return string.Empty;
                    }

                    parentKey = "HKEY_CURRENT_USER";
                }
                else
                {
                    parentKey = "HKEY_LOCAL_MACHINE";
                }

                var is64Bit = Environment.Is64BitProcess;

                var rSubKey = is64Bit ? "R64" : "R";

                var registryPath = string.Format("{0}\\{1}\\{2}", parentKey, RCORE_SUBKEY, rSubKey);

                var regR = regRCore.OpenSubKey(rSubKey);

                if (regR == null)
                {
                    errorMessage = string.Format("Registry key not found: {0}", registryPath);
                    return string.Empty;
                }

                var currentVersionText = (string)regR.GetValue("Current Version");

                string bin;

                if (string.IsNullOrEmpty(currentVersionText))
                {
                    var noSubKeyMessage = string.Format("Unable to determine the R Path: {0} has no child keys", registryPath);

                    if (regR.SubKeyCount == 0)
                    {
                        errorMessage = noSubKeyMessage;
                        return string.Empty;
                    }

                    // Find the newest SubKey
                    var subKeys = regR.GetSubKeyNames().ToList();
                    subKeys.Sort();
                    subKeys.Reverse();

                    var newestSubKey = subKeys.FirstOrDefault();

                    if (newestSubKey == null)
                    {
                        errorMessage = noSubKeyMessage;
                        return string.Empty;
                    }

                    var regRNewest = regR.OpenSubKey(newestSubKey);

                    if (regRNewest == null)
                    {
                        errorMessage = noSubKeyMessage;
                        return string.Empty;
                    }

                    var installPath = (string)regRNewest.GetValue("InstallPath");

                    if (string.IsNullOrEmpty(installPath))
                    {
                        errorMessage = string.Format("Unable to determine the R Path: {0} does not have key InstallPath at {1}", newestSubKey, registryPath);
                        return string.Empty;
                    }

                    bin = Path.Combine(installPath, "bin");
                }
                else
                {
                    var installPath = (string)regR.GetValue("InstallPath");

                    if (string.IsNullOrEmpty(installPath))
                    {
                        errorMessage = string.Format("Unable to determine the R Path: {0} does not have key InstallPath", registryPath);
                        return string.Empty;
                    }

                    bin = Path.Combine(installPath, "bin");

                    // If version is of the form "3.2.3" (for Major.Minor.Build)
                    // we can directly instantiate a new Version object from the string

                    // However, in 2016 R version "3.2.4 Revised" was released, and that
                    // string cannot be directly used to instantiate a new Version object

                    // The following checks for this and removes any non-numeric characters
                    // (though it requires that the Major version be an integer)

                    var versionParts = currentVersionText.Split('.');
                    var reconstructVersion = false;

                    Version currentVersion;

                    if (currentVersionText.Length <= 1)
                    {
                        currentVersion = new Version(currentVersionText);
                    }
                    else
                    {
                        var nonNumericChars = new Regex("[^0-9]+", RegexOptions.Compiled);

                        for (var i = 1; i <= versionParts.Length - 1; i++)
                        {
                            if (!nonNumericChars.IsMatch(versionParts[i]))
                                continue;

                            versionParts[i] = nonNumericChars.Replace(versionParts[i], string.Empty);
                            reconstructVersion = true;
                        }

                        currentVersion = reconstructVersion ? new Version(string.Join(".", versionParts)) : new Version(currentVersionText);
                    }

                    // Up to 2.11.x, DLLs are installed in R_HOME\bin
                    // From 2.12.0, DLLs are installed in either i386 or x64 (or both) below the bin directory
                    // The bin directory has an R.exe file but it does not have Rcmd.exe or R.dll
                    if (currentVersion < new Version(2, 12))
                    {
                        errorMessage = string.Empty;
                        return bin;
                    }
                }

                errorMessage = string.Empty;
                return Path.Combine(bin, is64Bit ? "x64" : "i386");
            }
            catch (Exception ex)
            {
                errorMessage = string.Format("Error in GetRPathFromWindowsRegistry (called from {0}): {1}", callingFunction ?? string.Empty, ex.Message);
                return string.Empty;
            }
        }
    }
}
