using System;
using System.IO;
using System.Threading.Tasks;
using PRISM;

namespace PRISMTest
{
    /// <summary>
    /// This class will spawn a separate thread that opens a file with a stream reader and
    /// keeps the file open for the specified number of seconds
    /// </summary>
    internal class FileLockUtility
    {
        private bool mCloseFile;

        public int LockTimeSeconds { get; }

        public string TargetFilePath { get; }

        private readonly DateTime StartTime;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="targetFilePath"></param>
        /// <param name="lockTimeSeconds"></param>
        public FileLockUtility(string targetFilePath, int lockTimeSeconds)
        {
            TargetFilePath = targetFilePath;
            LockTimeSeconds = lockTimeSeconds;
            StartTime = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(targetFilePath))
                return;

            // Start OpenFileAndWait in a new thread
            Task.Factory.StartNew(() => OpenFileAndWait(targetFilePath));
        }

        public void CloseFileNow()
        {
            mCloseFile = true;
        }

        private void OpenFileAndWait(string targetFilePath)
        {
            mCloseFile = false;

            using var reader = new StreamReader(new FileStream(targetFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

            if (!reader.EndOfStream)
            {
                reader.ReadLine();
            }

            while (true)
            {
                ConsoleMsgUtils.SleepSeconds(1);
                var elapsedSeconds = DateTime.UtcNow.Subtract(StartTime).TotalSeconds;

                if (elapsedSeconds >= LockTimeSeconds || mCloseFile)
                    break;

                var secondsRemaining = (int)Math.Round(LockTimeSeconds - elapsedSeconds);

                Console.WriteLine("Holding file handle open; {0} {1} remaining",
                    secondsRemaining,
                    secondsRemaining == 1 ? "second" : "seconds");
            }

            Console.WriteLine();

            Console.WriteLine("File held open for {0:F1} seconds{1}",
                DateTime.UtcNow.Subtract(StartTime).TotalSeconds,
                mCloseFile ? " (closed early)" : string.Empty);
        }
    }
}
