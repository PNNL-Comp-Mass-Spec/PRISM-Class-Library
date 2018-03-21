using System;
using System.Diagnostics;
using System.IO;
using PRISM.Logging;

namespace PRISM
{
    /// <summary>
    /// Makes using a file archiving program easier.
    /// </summary>
    /// <remarks>There are a routines to create an archive, extract files from an existing archive,
    /// and to verify an existing archive.
    /// </remarks>
    public class ZipTools
    {
        /// <summary>
        /// Working directory
        /// </summary>
        private string m_WorkDir;

        /// <summary>
        /// Program used for zipping and unzipping files
        /// </summary>
        private string m_ZipProgramPath;

        /// <summary>
        /// Interval, in milliseconds, to sleep between checking the status of a zip or unzip task
        /// </summary>
        private readonly int m_WaitInterval;

        /// <summary>
        /// Logging class
        /// </summary>
        private BaseLogger m_Logger;

#pragma warning disable 618
        [Obsolete("Use m_Logger (typically a FileLogger)")]
        private ILogger m_EventLogger;
#pragma warning restore 618

        private bool m_CreateNoWindow;

        private ProcessWindowStyle m_WindowStyle;

        /// <summary>
        /// Create a zip file.
        /// </summary>
        /// <param name="cmdOptions">The zip program command line arguments.</param>
        /// <param name="outputFile">The file path of the output zip file.</param>
        /// <param name="inputSpec">The files and/or directories to archive.</param>
        public bool MakeZipFile(string cmdOptions, string outputFile, string inputSpec)
        {


            // Verify input file and output path have been specified
            if (string.IsNullOrEmpty(m_ZipProgramPath) || string.IsNullOrEmpty(m_WorkDir))
            {
                var msg = "Zip program path and/or working path not specified";
#pragma warning disable 618
                m_EventLogger?.PostEntry(msg, logMsgType.logError, true);
#pragma warning restore 618
                m_Logger?.Error(msg);

                return false;
            }

            // Setup the zip program
            var zipper = new clsProgRunner
            {
                Arguments = "-Add " + cmdOptions + " \"" + outputFile + "\" \"" + inputSpec + "\"",
                Program = m_ZipProgramPath,
                WorkDir = m_WorkDir,
                MonitoringInterval = m_WaitInterval,
                Name = "Zipper",
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = m_CreateNoWindow
            };

            // Start the zip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            var success = WaitForZipProgram(zipper);

            return success;
        }

        /// <summary>
        /// Extract files from a zip file.
        /// </summary>
        /// <param name="cmdOptions">The zip program command line arguments.</param>
        /// <param name="zipFilePath">The file path of the zip file from which to extract files.</param>
        /// <param name="outFolderPath">The path where you want to put the extracted files.</param>
        public bool UnzipFile(string cmdOptions, string zipFilePath, string outFolderPath)
        {

            // Verify input file and output path have been specified
            if (string.IsNullOrEmpty(m_ZipProgramPath) || string.IsNullOrEmpty(m_WorkDir))
            {
                var msg = "Zip program path and/or working path not specified";
#pragma warning disable 618
                m_EventLogger?.PostEntry(msg, logMsgType.logError, true);
#pragma warning restore 618
                m_Logger?.Error(msg);

                return false;
            }

            // Verify input file exists
            if (!File.Exists(zipFilePath))
            {
                var msg = "Input file not found: " + zipFilePath;
#pragma warning disable 618
                m_EventLogger?.PostEntry(msg, logMsgType.logError, true);
#pragma warning restore 618
                m_Logger?.Error(msg);

                return false;
            }

            // Verify output path exists
            if (!Directory.Exists(outFolderPath))
            {
                var msg = "Output directory " + outFolderPath + " does not exist";
#pragma warning disable 618
                m_EventLogger?.PostEntry(msg, logMsgType.logError, true);
#pragma warning restore 618
                m_Logger?.Error(msg);

                return false;
            }

            // Setup the unzip program
            var zipper = new clsProgRunner
            {
                Arguments = "-Extract " + cmdOptions + " \"" + zipFilePath + "\" \"" + outFolderPath + "\"",
                MonitoringInterval = m_WaitInterval,
                Name = "Zipper",
                Program = m_ZipProgramPath,
                WorkDir = m_WorkDir,
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = m_CreateNoWindow,
                WindowStyle = m_WindowStyle,
            };

            // Start the unzip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            var success = WaitForZipProgram(zipper);

            return success;
        }

        /// <summary>
        /// Defines whether a window is displayed when calling the zipping program.
        /// </summary>
        public bool CreateNoWindow
        {
            get => m_CreateNoWindow;
            set => m_CreateNoWindow = value;
        }

        /// <summary>
        /// Window style to use when CreateNoWindow is False.
        /// </summary>
        public ProcessWindowStyle WindowStyle
        {
            get => m_WindowStyle;
            set => m_WindowStyle = value;
        }

        /// <summary>
        /// The working directory for the zipping process.
        /// </summary>
        public string WorkDir
        {
            get => m_WorkDir;
            set => m_WorkDir = value;
        }

        /// <summary>
        /// The path to the zipping program.
        /// </summary>
        public string ZipFilePath
        {
            get => m_ZipProgramPath;
            set => m_ZipProgramPath = value;
        }

        /// <summary>
        /// Initializes a new instance of the ZipTools class.
        /// </summary>
        /// <param name="workDir">The working directory for the zipping process.</param>
        /// <param name="zipFilePath">The path to the zipping program.</param>
        public ZipTools(string workDir, string zipFilePath)
        {
            m_WorkDir = workDir;
            m_ZipProgramPath = zipFilePath;

            // Time in milliseconds
            m_WaitInterval = 2000;

            NotifyOnEvent = true;
            NotifyOnException = true;
        }

        /// <summary>
        /// Verifies the integrity of a zip file.
        /// </summary>
        /// <param name="zipFilePath">The file path of the zip file to verify.</param>
        public bool VerifyZippedFile(string zipFilePath)
        {

            // Verify test file exists
            if (!File.Exists(zipFilePath))
            {
                var msg = "Zip file not found; cannot verify: " + zipFilePath;
#pragma warning disable 618
                m_EventLogger?.PostEntry(msg, logMsgType.logError, true);
#pragma warning restore 618
                m_Logger?.Error(msg);

                return false;
            }

            // Verify Zip file and output path have been specified
            if (string.IsNullOrEmpty(m_ZipProgramPath) || string.IsNullOrEmpty(m_WorkDir))
            {
                var msg = "Zip program path and/or working path not specified";
#pragma warning disable 618
                m_EventLogger?.PostEntry(msg, logMsgType.logError, true);
#pragma warning restore 618
                m_Logger?.Error(msg);

                return false;
            }

            // Setup the zip program
            var zipper = new clsProgRunner
            {
                Arguments = "-test -nofix" + " " + zipFilePath,
                Program = m_ZipProgramPath,
                WorkDir = m_WorkDir,
                MonitoringInterval = m_WaitInterval,
                Name = "Zipper",
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = m_CreateNoWindow,
                WindowStyle = m_WindowStyle,
            };

            // Start the zip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            var success = WaitForZipProgram(zipper);

            return success;
        }

        private bool WaitForZipProgram(clsProgRunner zipper)
        {
            while (zipper.State != clsProgRunner.States.NotMonitoring)
            {
                var msg = "Waiting for zipper program; sleeping for " + m_WaitInterval + " milliseconds";
#pragma warning disable 618
                m_EventLogger?.PostEntry(msg, logMsgType.logHealth, true);
#pragma warning restore 618
                m_Logger?.Debug(msg);

                clsProgRunner.SleepMilliseconds(m_WaitInterval);
            }

            // Check for valid return value after completion
            if (zipper.ExitCode == 0)
                return true;

            var errorMsg = "Zipper program exited with code: " + zipper.ExitCode;
#pragma warning disable 618
            m_EventLogger?.PostEntry(errorMsg, logMsgType.logError, true);
#pragma warning restore 618
            m_Logger?.Error(errorMsg);

            return false;
        }

        /// <summary>
        /// Associate a logger with this class
        /// </summary>
        public void RegisterEventLogger(BaseLogger logger)
        {
            m_Logger = logger;
        }

        /// <summary>
        /// Associate an event logger with this class
        /// </summary>
        [Obsolete("Use RegisterEventLogger that takes a BaseLogger (typically a FileLogger)")]
        public void RegisterEventLogger(ILogger logger)
        {
            m_EventLogger = logger;
        }

        /// <summary>
        /// Gets or Sets notify on event.
        /// </summary>
        public bool NotifyOnEvent { get; set; }

        /// <summary>
        /// Gets or Sets notify on exception.
        /// </summary>
        public bool NotifyOnException { get; set; }

    }
}
