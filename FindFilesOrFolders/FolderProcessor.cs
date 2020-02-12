using System.IO;
using PRISM.FileProcessor;

namespace FindFilesOrFolders
{
    class FolderProcessor : ProcessDirectoriesBase
    {
        public override string GetErrorMessage()
        {
            return GetBaseClassErrorMessage();
        }

        public override bool ProcessDirectory(string inputDirectoryPath, string outputDirectoryAlternatePath, string parameterFilePath, bool resetErrorCode)
        {
            OnStatusEvent("Process directory " + inputDirectoryPath);

            if (!string.IsNullOrWhiteSpace(outputDirectoryAlternatePath))
                OnStatusEvent("  Would write results to " + outputDirectoryAlternatePath);

            if (!Directory.Exists(inputDirectoryPath))
                OnWarningEvent("Folder not found: " + inputDirectoryPath);

            System.Threading.Thread.Sleep(200);

            return true;
        }
    }
}
