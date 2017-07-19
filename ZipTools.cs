using System.Diagnostics;
using System.IO;

namespace PRISM
{
    /// <summary>
    /// Makes using a file archiving program easier.
    /// </summary>
    /// <remarks>There are a routines to create an archive, extract files from an existing archive,
    /// and to verify an existing archive.
    /// </remarks>
    public class ZipTools : ILoggerAware
    {

        private string m_WorkDir;
        private string m_ZipFilePath;
        private readonly int m_WaitInterval;
        private ILogger m_EventLogger;
        private bool m_CreateNoWindow;

#if !(NETSTANDARD1_x || NETSTANDARD2_0)
        private ProcessWindowStyle m_WindowStyle;
#endif

        /// <summary>
        /// Create a zip file.
        /// </summary>
        /// <param name="CmdOptions">The zip program command line arguments.</param>
        /// <param name="OutputFile">The file path of the output zip file.</param>
        /// <param name="InputSpec">The files and/or directorys to archive.</param>
        public bool MakeZipFile(string CmdOptions, string OutputFile, string InputSpec)
        {


            // Verify input file and output path have been specified
            if ((string.IsNullOrEmpty(m_ZipFilePath)) | (string.IsNullOrEmpty(m_WorkDir)))
            {
                m_EventLogger?.PostEntry("Input file path and/or working path not specified.", logMsgType.logError, true);
                return false;
            }

            // Setup the zip program
            var zipper = new clsProgRunner
            {
                Arguments = "-Add " + CmdOptions + " \"" + OutputFile + "\" \"" + InputSpec + "\"",
                Program = m_ZipFilePath,
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
            while ((zipper.State != clsProgRunner.States.NotMonitoring))
            {
                m_EventLogger?.PostEntry("Waiting for zipper program.  Going to sleep for " + m_WaitInterval + " milliseconds.", logMsgType.logHealth, true);
                clsProgRunner.SleepMilliseconds(m_WaitInterval);
            }

            // Check for valid return value after completion
            if (zipper.ExitCode != 0)
            {
                m_EventLogger?.PostEntry("Zipper program exited with code: " + zipper.ExitCode, logMsgType.logError, true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Extract files from a zip file.
        /// </summary>
        /// <param name="CmdOptions">The zip program command line arguments.</param>
        /// <param name="InputFile">The file path of the zip file from which to extract files.</param>
        /// <param name="OutPath">The path where you want to put the extracted files.</param>
        public bool UnzipFile(string CmdOptions, string InputFile, string OutPath)
        {


            // Verify input file and output path have been specified
            if ((string.IsNullOrEmpty(m_ZipFilePath)) | (string.IsNullOrEmpty(m_WorkDir)))
            {
                m_EventLogger?.PostEntry("Input file path and/or working path not specified.", logMsgType.logError, true);
                return false;
            }

            // Verify input file exists
            if (!File.Exists(InputFile))
            {
                m_EventLogger?.PostEntry("Input file " + InputFile + " not found", logMsgType.logError, true);
                return false;
            }

            // Verify output path exists
            if (!Directory.Exists(OutPath))
            {
                m_EventLogger?.PostEntry("Output directory " + OutPath + " does not exist.", logMsgType.logError, true);
                return false;
            }

            // Setup the unzip program
            var zipper = new clsProgRunner
            {
                Arguments = "-Extract " + CmdOptions + " \"" + InputFile + "\" \"" + OutPath + "\"",
                MonitoringInterval = m_WaitInterval,
                Name = "Zipper",
                Program = m_ZipFilePath,
                WorkDir = m_WorkDir,
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = m_CreateNoWindow,
#if !(NETSTANDARD1_x || NETSTANDARD2_0)
                WindowStyle = m_WindowStyle,
#endif
            };

            // Start the unzip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            while ((zipper.State != clsProgRunner.States.NotMonitoring))
            {
                m_EventLogger?.PostEntry("Waiting for zipper program.  Going to sleep for " + m_WaitInterval + " milliseconds.", logMsgType.logHealth, true);
                clsProgRunner.SleepMilliseconds(m_WaitInterval);
            }

            // Check for valid return value after completion
            if (zipper.ExitCode != 0)
            {
                m_EventLogger?.PostEntry("Zipper program exited with code: " + zipper.ExitCode, logMsgType.logError, true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Defines whether a window is displayed when calling the zipping program.
        /// </summary>
        public bool CreateNoWindow
        {
            get { return m_CreateNoWindow; }
            set { m_CreateNoWindow = value; }
        }

#if !(NETSTANDARD1_x || NETSTANDARD2_0)
        /// <summary>
        /// Window style to use when CreateNoWindow is False.
        /// </summary>
        public ProcessWindowStyle WindowStyle
        {
            get { return m_WindowStyle; }
            set { m_WindowStyle = value; }
        }
#endif

        /// <summary>
        /// The working directory for the zipping process.
        /// </summary>
        public string WorkDir
        {
            get { return m_WorkDir; }
            set { m_WorkDir = value; }
        }

        /// <summary>
        /// The path to the zipping program.
        /// </summary>
        public string ZipFilePath
        {
            get { return m_ZipFilePath; }
            set { m_ZipFilePath = value; }
        }

        /// <summary>
        /// Initializes a new instance of the ZipTools class.
        /// </summary>
        /// <param name="WorkDir">The working directory for the zipping process.</param>
        /// <param name="ZipFilePath">The path to the zipping program.</param>
        public ZipTools(string WorkDir, string ZipFilePath)
        {
            m_WorkDir = WorkDir;
            m_ZipFilePath = ZipFilePath;

            // Time in milliseconds
            m_WaitInterval = 2000;

            NotifyOnEvent = true;
            NotifyOnException = true;
        }

        /// <summary>
        /// Verifies the integrity of a zip file.
        /// </summary>
        /// <param name="FilePath">The file path of the zip file to verify.</param>
        public bool VerifyZippedFile(string FilePath)
        {


            // Verify test file exists
            if (!File.Exists(FilePath))
            {
                m_EventLogger?.PostEntry("File path file " + FilePath + " not found", logMsgType.logError, true);
                return false;
            }

            // Verify Zip file and output path have been specified
            if ((string.IsNullOrEmpty(m_ZipFilePath)) | (string.IsNullOrEmpty(m_WorkDir)))
            {
                m_EventLogger?.PostEntry("Zip file path and/or working path not specified.", logMsgType.logError, true);
                return false;
            }

            // Setup the zip program
            var zipper = new clsProgRunner
            {
                Arguments = "-test -nofix" + " " + FilePath,
                Program = m_ZipFilePath,
                WorkDir = m_WorkDir,
                MonitoringInterval = m_WaitInterval,
                Name = "Zipper",
                Repeat = false,
                RepeatHoldOffTime = 0,
                CreateNoWindow = m_CreateNoWindow,
#if !(NETSTANDARD1_x || NETSTANDARD2_0)
                WindowStyle = m_WindowStyle,
#endif
            };

            // Start the zip program
            zipper.StartAndMonitorProgram();

            // Wait for zipper program to complete
            while ((zipper.State != clsProgRunner.States.NotMonitoring))
            {
                m_EventLogger?.PostEntry("Waiting for zipper program.  Going to sleep for " + m_WaitInterval + " milliseconds.", logMsgType.logHealth, true);
                clsProgRunner.SleepMilliseconds(m_WaitInterval);
            }

            // Check for valid return value after completion
            if (zipper.ExitCode != 0)
            {
                m_EventLogger?.PostEntry("Zipper program exited with code: " + zipper.ExitCode, logMsgType.logError, true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sets the name of the event logger
        /// </summary>
        public void RegisterEventLogger(ILogger logger)
        {
            m_EventLogger = logger;
        }
        void ILoggerAware.RegisterExceptionLogger(ILogger logger)
        {
            RegisterEventLogger(logger);
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
