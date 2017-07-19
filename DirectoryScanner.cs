using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PRISM
{

    /// <summary>
    /// Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.
    /// </summary>
    public class DirectoryScanner
    {
        /// <summary>
        /// A file was found when scanning the directory
        /// </summary>
        public event FoundFileEventHandler FoundFile;

        /// <summary>
        /// Event is raised whenever a matching file is found.
        /// </summary>
        /// <remarks>This event is most useful for implementing a progress indicator.</remarks>
        /// <param name="fileName">The found file's full path.</param>
        public delegate void FoundFileEventHandler(string fileName);
        private readonly List<string> mSearchDirs;

        private readonly List<string> mFileList;

        /// <summary>
        /// Constructor: Initializes a new instance of the DirectoryScanner class.
        /// </summary>
        /// <param name="dirs">An array of directory paths to scan.</param>
        public DirectoryScanner(IEnumerable<string> dirs) : this(dirs.ToList())
        {
        }

        /// <summary>
        /// Constructor: Initializes a new instance of the DirectoryScanner class.
        /// </summary>
        /// <param name="dirs">A list of directory paths to scan</param>
        /// <remarks></remarks>
        public DirectoryScanner(List<string> dirs)
        {
            mSearchDirs = dirs;
            mFileList = new List<string>();
        }

#if !(NETSTANDARD1_x || NETSTANDARD2_0)
        /// <summary>
        /// Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.
        /// </summary>
        /// <param name="results">An array of file paths found; unchanged if no matches</param>
        /// <param name="searchPatterns">An array of regular expressions to use in the search.</param>
        /// <returns>Always returns true</returns>
        [Obsolete("Use PerformScan that returns a List of strings")]
        public bool PerformScan(ref ArrayList results, params string[] searchPatterns)
        {
            var files = PerformScan(searchPatterns);

            if (files.Count > 0)
            {
                if (results == null)
                {
                    results = new ArrayList();
                }
                else
                {
                    results.Clear();
                }

                foreach (var item in files)
                {
                    results.Add(item);
                }
            }

            return true;

        }
#endif

        /// <summary>
        /// Performs a recursive search of a directory tree looking for file names that match a set of regular expressions.
        /// </summary>
        /// <param name="searchPatterns">An array of regular expressions to use in the search.</param>
        /// <returns>A list of the file paths found; empty list if no matches</returns>
        public List<string> PerformScan(params string[] searchPatterns)
        {
            mFileList.Clear();

            foreach (var dir in mSearchDirs)
            {
                foreach (var pattern in searchPatterns)
                {
                    RecursiveFileSearch(dir, pattern);
                }
            }

            return mFileList;

        }

        private void RecursiveFileSearch(string searchDir, string filePattern)
        {
            foreach (var f in Directory.GetFiles(searchDir, filePattern))
            {
                mFileList.Add(f);
                FoundFile?.Invoke(f);
            }

            foreach (var d in Directory.GetDirectories(searchDir))
            {
                RecursiveFileSearch(d, filePattern);
            }
        }

    }
}
