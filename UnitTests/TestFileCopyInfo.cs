﻿using System;
using System.IO;

namespace PRISMTest
{
    internal class TestFileCopyInfo
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

        public TestFileCopyInfo(FileInfo sourceFile, FileInfo targetFile)
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

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (TestLinuxSystemInfo.SHOW_TRACE_MESSAGES)
#pragma warning disable CS0162
                // ReSharper disable once HeuristicUnreachableCode
                Console.WriteLine("{0:HH:mm:ss.fff}: Copied file from {1} to {2}", DateTime.Now, SourceFile.FullName, TargetFile.FullName);
#pragma warning restore CS0162

            Copied = true;
        }
    }
}
