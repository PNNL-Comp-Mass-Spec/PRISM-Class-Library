using System.IO;

namespace PRISMTest
{
    class clsTestFileCopyInfo
    {
        /// <summary>
        /// Source file
        /// </summary>
        public FileInfo SourceFile { get; }

        /// <summary>
        /// Target file
        /// </summary>
        public FileInfo TargetFile { get; }

        /// <summary>
        /// Flag for tracking if the SourceFile has been copied to the TargetFile
        /// </summary>
        private bool Copied { get; set; }

        public clsTestFileCopyInfo(FileInfo sourceFile, FileInfo targetFile)
        {
            SourceFile = sourceFile;
            TargetFile = targetFile;
            Copied = false;
        }

        /// <summary>
        /// Copy the source file to the target file
        /// </summary>
        public void CopyToTargetNow()
        {
            if (Copied)
                return;

            SourceFile.CopyTo(TargetFile.FullName, true);
            // Console.WriteLine("{0:HH:mm:ss.fff}: Copied file from {1} to {2}", DateTime.Now, SourceFile.FullName, TargetFile.FullName);

            Copied = true;
        }
    }
}
