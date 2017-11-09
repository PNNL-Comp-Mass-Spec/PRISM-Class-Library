using System.IO;
using PRISM.FileProcessor;

namespace FindFilesOrFolders
{
    class FolderProcessor : ProcessFoldersBase
    {
        public override string GetErrorMessage()
        {
            return GetBaseClassErrorMessage();
        }

        public override bool ProcessFolder(string inputFolderPath, string outputFolderAlternatePath, string parameterFilePath, bool resetErrorCode)
        {
            OnStatusEvent("Process folder " + inputFolderPath);

            if (!string.IsNullOrWhiteSpace(outputFolderAlternatePath))
                OnStatusEvent("  Would write results to " + outputFolderAlternatePath);

            if (!Directory.Exists(inputFolderPath))
                OnWarningEvent("Folder not found: " + inputFolderPath);

            System.Threading.Thread.Sleep(200);

            return true;
        }
    }
}
