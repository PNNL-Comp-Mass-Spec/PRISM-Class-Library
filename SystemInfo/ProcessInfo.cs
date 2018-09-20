
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Track basic process info metadata
    /// </summary>
    public class ProcessInfo
    {
        #region "Properties"

        /// <summary>
        /// Command line arguments as a single string
        /// </summary>
        public string Arguments { get; }

        /// <summary>
        /// Command line arguments as a list of strings
        /// </summary>
        public List<string> ArgumentList { get; }

        /// <summary>
        /// Full command line, including the ExePath and arguments
        /// </summary>
        public string CommandLine { get; }

        /// <summary>
        /// Executable name or path
        /// </summary>
        public string ExePath { get; }

        /// <summary>
        /// Executable name only
        /// </summary>
        public string ExeName { get; }

        /// <summary>
        /// Process ID
        /// </summary>
        public int ProcessID { get; }

        /// <summary>
        /// Process name
        /// </summary>
        /// <remarks>
        /// Windows processes have names; Linux processes do not
        /// On Linux will be equivalent to ExeName
        /// </remarks>
        public string ProcessName { get; }

        #endregion

        /// <summary>
        /// Constructor that takes process ID only
        /// </summary>
        /// <param name="processId"></param>
        public ProcessInfo(int processId)
        {
            Arguments = string.Empty;
            ArgumentList = new List<string>();
            ExePath = string.Empty;
            ExeName = string.Empty;
            ProcessID = processId;
        }

        /// <summary>
        /// Constructor that takes process ID and the process name
        /// </summary>
        /// <param name="processId"></param>
        /// <param name="processName">Command line</param>
        /// <remarks>
        /// Assumes that the executable path is everything before the first space and arguments are everything after the first space
        /// </remarks>
        public ProcessInfo(int processId, string processName)
        {
            Arguments = string.Empty;
            ArgumentList = new List<string>();
            CommandLine = string.Empty;
            ExePath = string.Empty;
            ExeName = string.Empty;
            ProcessID = processId;
            ProcessName = processName;
        }

        /// <summary>
        /// Constructor that takes process ID, process name, executable path, a list of arguments, and optionally the full command line
        /// </summary>
        /// <param name="processId"></param>
        /// <param name="processName"></param>
        /// <param name="exePath"></param>
        /// <param name="argumentList"></param>
        /// <param name="cmdLine"></param>
        public ProcessInfo(int processId, string processName, string exePath, List<string> argumentList, string cmdLine = "")
        {
            Arguments = argumentList == null ? string.Empty : string.Join(" ", argumentList);

            ArgumentList = argumentList;
            if (string.IsNullOrWhiteSpace(exePath))
                exePath = string.Empty;

            if (string.IsNullOrWhiteSpace(cmdLine))
            {
                if (exePath.Contains(" "))
                    CommandLine = '"' + exePath + '"' + " " + Arguments;
                else
                    CommandLine = exePath + " " + Arguments;
            }
            else
            {
                CommandLine = cmdLine;
            }

            ExePath = exePath;

            try
            {
                var serviceMatcher = new Regex("(?<ServiceName>^[^ ]+): ");
                var serviceMatch = serviceMatcher.Match(exePath);
                if (serviceMatch.Success)
                {
                    // exePath is of the form "ServiceName: arguments"
                    // Set the executable name to the text before the colon
                    ExeName = serviceMatch.Groups["ServiceName"].Value;
                }
                else
                {
                    ExeName = System.IO.Path.GetFileName(exePath);
                }

            }
            catch (Exception)
            {
                var lastSlash = exePath.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
                if (lastSlash >= 0 && lastSlash < exePath.Length)
                {
                    ExeName = exePath.Substring(lastSlash + 1);
                }
                else
                {
                    ExeName = exePath;
                }
            }

            ProcessID = processId;
            ProcessName = processName;
        }

        /// <summary>
        /// Returns process name and process ID
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0}, ID {1}", ProcessName, ProcessID);
        }

        /// <summary>
        /// Returns process name, process ID, exe path, exe name, arguments, and the full command line
        /// </summary>
        /// <returns></returns>
        public string ToStringVerbose()
        {
            var description = new StringBuilder();

            description.AppendLine("ID:      " + ProcessID);
            description.AppendLine("Name:    " + ProcessName);
            description.AppendLine("ExePath: " + ExePath);
            description.AppendLine("ExeName: " + ExeName);

            var i = 0;
            foreach (var item in ArgumentList)
            {
                i++;
                var spacer = i < 10 ? " " : "";
                description.AppendLine(string.Format("Arg{0}:   {1}{2}", i, spacer, item));
            }

            description.AppendLine("CmdLine: " + CommandLine);

            return description.ToString();
        }
    }
}
