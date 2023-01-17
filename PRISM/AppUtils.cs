using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PRISM
{
    /// <summary>
    /// Static class with commonly-used application tools and some thread
    /// </summary>
    public static class AppUtils
    {
        /// <summary>
        /// Force the garbage collector to run, waiting up to 1 second for it to finish
        /// </summary>
        public static void GarbageCollectNow()
        {
            const int maxWaitTimeMSec = 1000;
            GarbageCollectNow(maxWaitTimeMSec);
        }

        /// <summary>
        /// Force the garbage collector to run
        /// </summary>
        /// <param name="maxWaitTimeMSec"></param>
        public static void GarbageCollectNow(int maxWaitTimeMSec)
        {
            const int THREAD_SLEEP_TIME_MSEC = 100;

            if (maxWaitTimeMSec < 100)
                maxWaitTimeMSec = 100;
            if (maxWaitTimeMSec > 5000)
                maxWaitTimeMSec = 5000;

            Thread.Sleep(100);

            try
            {
                var gcThread = new Thread(GarbageCollectWaitForGC);
                gcThread.Start();

                var totalThreadWaitTimeMsec = 0;
                while (gcThread.IsAlive && totalThreadWaitTimeMsec < maxWaitTimeMSec)
                {
                    Thread.Sleep(THREAD_SLEEP_TIME_MSEC);
                    totalThreadWaitTimeMsec += THREAD_SLEEP_TIME_MSEC;
                }

#if NETFRAMEWORK
                // Thread.Abort() Throws a "PlatformNotSupportedException" on all .NET Standard/.NET Core platforms; warning as of .NET 5.0
                if (gcThread.IsAlive)
                    gcThread.Abort();
#endif
            }
            catch
            {
                // Ignore errors here
            }
        }

        private static void GarbageCollectWaitForGC()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Returns the full path to the directory into which this application should read/write settings file information
        /// </summary>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName</remarks>
        /// <param name="appName"></param>
        public static string GetAppDataDirectoryPath(string appName)
        {
            string appDataDirectory;

            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = string.Empty;
            }

            try
            {
                appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
                if (!Directory.Exists(appDataDirectory))
                {
                    Directory.CreateDirectory(appDataDirectory);
                }
            }
            catch (Exception)
            {
                // Error creating the directory, revert to using the system Temp directory
                appDataDirectory = Path.GetTempPath();
            }

            return appDataDirectory;
        }

        /// <summary>
        /// Returns the full path to the directory that contains the currently executing .Exe or .Dll
        /// </summary>
        public static string GetAppDirectoryPath()
        {
            // Could use Application.StartupPath, but .GetExecutingAssembly is better
            return Path.GetDirectoryName(GetAppPath());
        }

        /// <summary>
        /// Returns the full path to the executing .Exe or .Dll
        /// </summary>
        /// <returns>File path</returns>
        public static string GetAppPath()
        {
            return GetEntryOrExecutingAssembly().Location;
        }

        /// <summary>
        /// Returns the .NET assembly version followed by the program date
        /// </summary>
        /// <param name="programDate"></param>
        public static string GetAppVersion(string programDate)
        {
            return GetEntryOrExecutingAssembly().GetName().Version + " (" + programDate + ")";
        }

        /// <summary>
        /// Returns the entry assembly, if it is unavailable, returns the executing assembly
        /// </summary>
        public static Assembly GetEntryOrExecutingAssembly()
        {
            var entry = Assembly.GetEntryAssembly();
            var executing = Assembly.GetExecutingAssembly();
            return entry ?? executing;
        }

        /// <summary>
        /// Pause program execution for the specific number of milliseconds (maximum 10 seconds)
        /// </summary>
        /// <param name="sleepTimeMsec">Value between 10 and 10000 (i.e. between 10 msec and 10 seconds)</param>
        public static void SleepMilliseconds(int sleepTimeMsec)
        {
            if (sleepTimeMsec < 10)
                sleepTimeMsec = 10;
            else if (sleepTimeMsec > 10000)
                sleepTimeMsec = 10000;

            Task.Delay(sleepTimeMsec).Wait();

            // Option 2:
            // using (EventWaitHandle tempEvent = new ManualResetEvent(false))
            // {
            //     tempEvent.WaitOne(TimeSpan.FromMilliseconds(sleepTimeMsec));
            // }

            // Option 3, though this will be deprecated in .NET Standard
            // System.Threading.Thread.Sleep(sleepTimeMsec);
        }

        /// <summary>
        /// Pause program execution for the specific number of milliseconds (maximum 10 seconds)
        /// </summary>
        /// <param name="sleepTimeMsec">Value between 10 and 10000 (i.e. between 10 msec and 10 seconds)</param>
        public static async Task SleepMillisecondsAsync(int sleepTimeMsec)
        {
            if (sleepTimeMsec < 10)
                sleepTimeMsec = 10;
            else if (sleepTimeMsec > 10000)
                sleepTimeMsec = 10000;

            await Task.Delay(TimeSpan.FromMilliseconds(sleepTimeMsec)).ConfigureAwait(false);
        }
    }
}
