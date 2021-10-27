using System.IO;
using PRISM.FileProcessor;

namespace FindFilesOrDirectories
{
    internal class FileProcessor : ProcessFilesBase
    {
        public SearchOptions Options { get; private set; }

        /// <summary>
        /// Constructor that accepts an options class
        /// </summary>
        /// <param name="options"></param>
        public FileProcessor(SearchOptions options)
        {
            Options = options;
        }

        public override string GetErrorMessage()
        {
            return GetBaseClassErrorMessage();
        }

        public override bool ProcessFile(string inputFilePath, string outputDirectoryPath, string parameterFilePath, bool resetErrorCode)
        {
            Options ??= new SearchOptions
            {
                InputFileOrDirectoryPath = inputFilePath,
                OutputDirectoryPath = outputDirectoryPath
            };

            OnStatusEvent("Process file " + PRISM.FileTools.CompactPathString(inputFilePath, 60));

            if (!string.IsNullOrWhiteSpace(outputDirectoryPath))
                OnStatusEvent("  Would write results to " + outputDirectoryPath);

            if (!File.Exists(inputFilePath))
                OnWarningEvent("File not found: " + inputFilePath);

            System.Threading.Thread.Sleep(200);

            return true;
        }
    }
}
