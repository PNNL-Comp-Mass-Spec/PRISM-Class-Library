using System;
using System.Diagnostics;
using System.IO;
using PRISM.Logging;

// ReSharper disable UnusedMember.Global

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Routines for calling an external zipping program, like 7-zip.exe
    /// </summary>
    /// <remarks>
    /// There are routines to create an archive, extract files from an existing archive, and verify an existing archive
    /// </remarks>
    // ReSharper disable once UnusedMember.Global
    [Obsolete("It is preferable to use the zipping methods in class ZipFileTools since it uses System.IO.Compression.ZipFile")]
    public class ZipTools
    {
        // ReSharper disable once CommentTypo
        // Ignore Spelling: nofix

        /// <summary>
        /// Interval, in milliseconds, to sleep between checking the status of a zip or unzip task
        /// </summary>
        private readonly int mWaitInterval;

        /// <summary>
        /// Logging class
        /// </summary>
        private BaseLogger mLogger;

        /// <summary>
        /// Create a zip file
        /// </summary>
        /// <param name="cmdOptions">The zip program command line arguments</param>
        /// <param name="outputFile">The file path of the output zip file</param>
        /// <param name="inputSpec">The files and/or directories to archive</param>
        public bool MakeZipFile(string cmdOptions, string outputFile, string inputSpec)
        {
            // Verify input file and output path have been specified
            if (string.IsNullOrEmpty(ZipFilePath) || string.IsNullOrEmpty(WorkDir))
            {
                const string msg = "Zip program path and/or working path not specified";

                mLogger?.Error(msg);

                return false;
            }

            // Set up the zip program
            var zipper = new ProgRunner
            {
                Arguments = "-Add " + cmdOptions + " \"" + outputFile + "\" \"" + inputSpec + "\"",
                Program = ZipFilePath,
                WorkDir = WorkDir,
                MonitoringInterval = mWaitInterval,
                Name = "Zipper",
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = CreateNoWindow
            };

            // Start the zip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            var success = WaitForZipProgram(zipper);

            return success;
        }

        /// <summary>
        /// Extract files from a zip file
        /// </summary>
        /// <param name="cmdOptions">The zip program command line arguments</param>
        /// <param name="zipFilePath">The file path of the zip file from which to extract files</param>
        /// <param name="outputDirectoryPath">The path where you want to put the extracted files</param>
        public bool UnzipFile(string cmdOptions, string zipFilePath, string outputDirectoryPath)
        {
            // Verify input file and output path have been specified
            if (string.IsNullOrEmpty(ZipFilePath) || string.IsNullOrEmpty(WorkDir))
            {
                const string msg = "Zip program path and/or working path not specified";

                mLogger?.Error(msg);

                return false;
            }

            // Verify input file exists
            if (!File.Exists(zipFilePath))
            {
                var msg = "Input file not found: " + zipFilePath;

                mLogger?.Error(msg);

                return false;
            }

            // Verify output path exists
            if (!Directory.Exists(outputDirectoryPath))
            {
                var msg = "Output directory " + outputDirectoryPath + " does not exist";

                mLogger?.Error(msg);

                return false;
            }

            // Set up the unzip program
            var zipper = new ProgRunner
            {
                Arguments = "-Extract " + cmdOptions + " \"" + zipFilePath + "\" \"" + outputDirectoryPath + "\"",
                MonitoringInterval = mWaitInterval,
                Name = "Zipper",
                Program = ZipFilePath,
                WorkDir = WorkDir,
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = CreateNoWindow,
                WindowStyle = WindowStyle,
            };

            // Start the unzip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            var success = WaitForZipProgram(zipper);

            return success;
        }

        /// <summary>
        /// Defines whether a window is displayed when calling the zipping program
        /// </summary>
        public bool CreateNoWindow { get; set; }

        /// <summary>
        /// Window style to use when CreateNoWindow is False
        /// </summary>
        public ProcessWindowStyle WindowStyle { get; set; }

        /// <summary>
        /// The working directory for the zipping process
        /// </summary>
        public string WorkDir { get; set; }

        /// <summary>
        /// The path to the zipping program
        /// </summary>
        public string ZipFilePath { get; set; }

        /// <summary>
        /// Initializes a new instance of the ZipTools class
        /// </summary>
        /// <param name="workDir">The working directory for the zipping process</param>
        /// <param name="zipFilePath">The path to the zipping program</param>
        public ZipTools(string workDir, string zipFilePath)
        {
            WorkDir = workDir;
            ZipFilePath = zipFilePath;

            // Time in milliseconds
            mWaitInterval = 2000;

            NotifyOnEvent = true;
            NotifyOnException = true;
        }

        /// <summary>
        /// Verifies the integrity of a zip file
        /// </summary>
        /// <param name="zipFilePath">The file path of the zip file to verify</param>
        public bool VerifyZippedFile(string zipFilePath)
        {
            // Verify test file exists
            if (!File.Exists(zipFilePath))
            {
                var msg = "Zip file not found; cannot verify: " + zipFilePath;

                mLogger?.Error(msg);

                return false;
            }

            // Verify Zip file and output path have been specified
            if (string.IsNullOrEmpty(ZipFilePath) || string.IsNullOrEmpty(WorkDir))
            {
                const string msg = "Zip program path and/or working path not specified";

                mLogger?.Error(msg);

                return false;
            }

            // Set up the zip program
            var zipper = new ProgRunner
            {
                // ReSharper disable once StringLiteralTypo
                Arguments = "-test -nofix " + zipFilePath,
                Program = ZipFilePath,
                WorkDir = WorkDir,
                MonitoringInterval = mWaitInterval,
                Name = "Zipper",
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = CreateNoWindow,
                WindowStyle = WindowStyle,
            };

            // Start the zip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            var success = WaitForZipProgram(zipper);

            return success;
        }

        private bool WaitForZipProgram(ProgRunner zipper)
        {
            while (zipper.State != ProgRunner.States.NotMonitoring)
            {
                var msg = "Waiting for zipper program; sleeping for " + mWaitInterval + " milliseconds";

                mLogger?.Debug(msg);

                AppUtils.SleepMilliseconds(mWaitInterval);
            }

            // Check for valid return value after completion
            if (zipper.ExitCode == 0)
                return true;

            var errorMsg = "Zipper program exited with code: " + zipper.ExitCode;

            mLogger?.Error(errorMsg);

            return false;
        }

        /// <summary>
        /// Associate a logger with this class
        /// </summary>
        public void RegisterEventLogger(BaseLogger logger)
        {
            mLogger = logger;
        }

        /// <summary>
        /// Gets or Sets notify on event
        /// </summary>
        public bool NotifyOnEvent { get; set; }

        /// <summary>
        /// Gets or Sets notify on exception
        /// </summary>
        public bool NotifyOnException { get; set; }
    }
}
