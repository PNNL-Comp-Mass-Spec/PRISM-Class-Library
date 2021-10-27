using System.IO;
using PRISM.FileProcessor;

namespace FindFilesOrDirectories
{
    internal class DirectoryProcessor : ProcessDirectoriesBase
    {
        public SearchOptions Options { get; private set; }

        /// <summary>
        /// Constructor that accepts an options class
        /// </summary>
        /// <param name="options"></param>
        public DirectoryProcessor(SearchOptions options)
        {
            Options = options;
        }

        public override string GetErrorMessage()
        {
            return GetBaseClassErrorMessage();
        }

        public override bool ProcessDirectory(string inputDirectoryPath, string outputDirectoryAlternatePath, string parameterFilePath, bool resetErrorCode)
        {
            Options ??= new SearchOptions
            {
                InputFileOrDirectoryPath = inputDirectoryPath,
                OutputDirectoryPath = outputDirectoryAlternatePath
            };

            OnStatusEvent("Process directory " + inputDirectoryPath);

            if (!string.IsNullOrWhiteSpace(outputDirectoryAlternatePath))
                OnStatusEvent("  Would write results to " + outputDirectoryAlternatePath);

            if (!Directory.Exists(inputDirectoryPath))
                OnWarningEvent("Directory not found: " + inputDirectoryPath);

            System.Threading.Thread.Sleep(200);

            return true;
        }
    }
}
