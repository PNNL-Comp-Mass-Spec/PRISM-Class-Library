using System.IO;
using PRISM.FileProcessor;

namespace FindFilesOrFolders
{
    class FileProcessor : ProcessFilesBase
    {
        public override string GetErrorMessage()
        {
            return GetBaseClassErrorMessage();
        }

        public override bool ProcessFile(string inputFilePath, string outputFolderPath, string parameterFilePath, bool resetErrorCode)
        {

            OnStatusEvent("Process file " + PRISM.clsFileTools.CompactPathString(inputFilePath, 60));

            if (!string.IsNullOrWhiteSpace(outputFolderPath))
                OnStatusEvent("  Would write results to " + outputFolderPath);

            if (!File.Exists(inputFilePath))
                OnWarningEvent("File not found: " + inputFilePath);

            System.Threading.Thread.Sleep(200);

            return true;
        }
    }
}
