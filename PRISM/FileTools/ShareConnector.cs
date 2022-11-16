using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Connects to a file share using a password and user name
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default behavior when connecting to SMB/CIFS file shares is for
    /// the system to supply the user name and password used to log on to the local machine
    /// </para>
    /// <para>
    /// This class allows you to connect to SMB/CIFS file shares when the use of
    /// SSPI isn't available and/or when you don't wish to use the default behavior
    /// </para>
    /// <para>
    /// It's quite comparable to the "Connect using a different user name." option in the Map Network Drive
    /// utility in Windows. Much of this code came from Microsoft Knowledge Base Article - 173011
    /// </para>
    /// </remarks>
    // ReSharper disable once UnusedMember.Global
    public class ShareConnector
    {
        // Ignore Spelling: const, username

        // ReSharper disable UnusedMember.Global

        /// <summary>
        /// Resource scope
        /// </summary>
        public enum ResourceScope
        {
            /// <summary>
            /// Connected
            /// </summary>
            Connected = 1,

            /// <summary>
            /// Global network
            /// </summary>
            GlobalNetwork,

            /// <summary>
            /// Remembered
            /// </summary>
            Remembered,

            /// <summary>
            /// Recent
            /// </summary>
            Recent,

            /// <summary>
            /// Context
            /// </summary>
            Context
        }

        /// <summary>
        /// Resource type
        /// </summary>
        public enum ResourceType
        {
            /// <summary>
            /// Any
            /// </summary>
            Any = 0,

            /// <summary>
            /// Disk
            /// </summary>
            Disk = 1,

            /// <summary>
            /// Print
            /// </summary>
            Print = 2,

            /// <summary>
            /// Reserved
            /// </summary>
            Reserved = 8
        }

        /// <summary>
        /// Resource display type
        /// </summary>
        public enum ResourceDisplayType
        {
            /// <summary>
            /// Generic
            /// </summary>
            Generic = 0x0,

            /// <summary>
            /// Domain
            /// </summary>
            Domain = 0x1,

            /// <summary>
            /// Server
            /// </summary>
            Server = 0x2,

            /// <summary>
            /// Share
            /// </summary>
            Share = 0x3,

            /// <summary>
            /// File
            /// </summary>
            File = 0x4,

            /// <summary>
            /// Group
            /// </summary>
            Group = 0x5,

            /// <summary>
            /// Network
            /// </summary>
            Network = 0x6,

            /// <summary>
            /// Root
            /// </summary>
            Root = 0x7,

            /// <summary>
            /// ShareAdmin
            /// </summary>
            ShareAdmin = 0x8,

            /// <summary>
            /// Directory
            /// </summary>
            Directory = 0x9,

            /// <summary>
            /// Tree
            /// </summary>
            Tree = 0xa,

            /// <summary>
            /// NdsContainer
            /// </summary>
            NdsContainer = 0xb
        }

        /// <summary>
        /// This structure is used to group a bunch of member variables
        /// </summary>
        private struct NetResourceInfo
        {
#pragma warning disable 169,414, CS0649
            // ReSharper disable InconsistentNaming
            public ResourceScope dwScope;
            public ResourceType dwType;
            public ResourceDisplayType dwDisplayType;
            public int dwUsage;

            /// <summary>
            /// Local name
            /// </summary>
            public string lpLocalName;

            /// <summary>
            /// Remote name
            /// </summary>
            public string lpRemoteName;

            /// <summary>
            /// Comment
            /// </summary>
            public string lpComment;

            /// <summary>
            /// Provider
            /// </summary>
            public string lpProvider;
            // ReSharper restore InconsistentNaming
#pragma warning restore 169,414, CS0649
        }

        // ReSharper restore UnusedMember.Global

        private const short NO_ERROR = 0;

        // Unused constant
        // private const short CONNECT_UPDATE_PROFILE = 0x1;
        // /// <summary> Constant that may be used by NETRESOURCE->dwScope </summary>
        // private const short RESOURCE_CONNECTED As Short = &H1S
        // /// <summary> Constant that may be used by NETRESOURCE->dwScope </summary>
        // private const short RESOURCE_GLOBAL_NET As Short = &H2S

        // /// <summary> Constant that may be used by NETRESOURCE->dwType </summary>
        // private const short RESOURCE_TYPE_DISK As Short = &H1S
        // /// <summary> Constant that may be used by NETRESOURCE->dwType </summary>
        // private const short RESOURCE_TYPE_PRINT As Short = &H2S
        // /// <summary> Constant that may be used by NETRESOURCE->dwType </summary>
        // private const short RESOURCE_TYPE_ANY As Short = &H0S

        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCE_DISPLAY_TYPE_DOMAIN As Short = &H1S
        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCE_DISPLAY_TYPE_GENERIC As Short = &H0S
        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCE_DISPLAY_TYPE_SERVER As Short = &H2S
        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCE_DISPLAY_TYPE_SHARE As Short = &H3S

        /// <summary>
        /// Constant that may be used by NETRESOURCE->dwUsage
        /// </summary>
        private const short RESOURCE_USAGE_CONNECTABLE = 0x1;

        /// <summary>
        /// Constant that may be used by NETRESOURCE->dwUsage
        /// </summary>
        private const short RESOURCE_USAGE_CONTAINER = 0x2;

        [DllImport("mpr.dll", EntryPoint = "WNetAddConnection2A", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int WNetAddConnection2(ref NetResourceInfo lpNetResource, string lpPassword, string lpUserName, int dwFlags);

        [DllImport("mpr.dll", EntryPoint = "WNetCancelConnection2A", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int WNetCancelConnection2(string lpName, int dwFlags, int fForce);

        private NetResourceInfo mNetResource;
        private string mUsername;
        private string mPassword;

        private string mShareName = string.Empty;

        /// <summary>
        /// This version of the constructor requires you to specify the share name by setting the <see cref="Share">Share</see> property
        /// </summary>
        /// <remarks>For local user accounts, it is safest to use HostName\username</remarks>
        /// <param name="userName">Username</param>
        /// <param name="userPwd">Password</param>
        public ShareConnector(string userName, string userPwd)
        {
            RealNew(userName, userPwd);
        }

        /// <summary>
        /// This version of the constructor allows you to specify the share name as an argument
        /// </summary>
        /// <remarks>For local user accounts, it is safest to use HostName\username</remarks>  ///
        /// <param name="shareName">The name of the file share to which you will connect</param>
        /// <param name="userName">Username</param>
        /// <param name="userPwd">Password</param>
        public ShareConnector(string shareName, string userName, string userPwd)
        {
            DefineShareName(shareName);
            RealNew(userName, userPwd);
        }

        /// <summary>
        /// This routine is called by each of the constructors to make the actual assignments in a consistent fashion
        /// </summary>
        /// <param name="userName">Username</param>
        /// <param name="userPwd">Password</param>
        private void RealNew(string userName, string userPwd)
        {
            mUsername = userName;
            mPassword = userPwd;
            mNetResource.lpRemoteName = mShareName;
            mNetResource.dwType = ResourceType.Disk;
            mNetResource.dwScope = ResourceScope.GlobalNetwork;
            mNetResource.dwDisplayType = ResourceDisplayType.Share;
            mNetResource.dwUsage = RESOURCE_USAGE_CONNECTABLE;
        }

        /// <summary>
        /// Sets the name of the file share to which you will connect
        /// </summary>
        public string Share
        {
            get => mShareName;
            set
            {
                DefineShareName(value);
                mNetResource.lpRemoteName = mShareName;
            }
        }

        /// <summary>
        /// Connects to specified share using account/password specified through the constructor and
        /// the file share name passed as an argument
        /// </summary>
        /// <param name="shareName">The name of the file share to which you will connect</param>
        // ReSharper disable once UnusedMember.Global
        public bool Connect(string shareName)
        {
            DefineShareName(shareName);
            mNetResource.lpRemoteName = mShareName;
            return RealConnect();
        }

        /// <summary>
        /// Connects to specified share using account/password specified through the constructor
        /// <remarks>
        /// Requires you to have specified the share name by setting the <see cref="Share">Share</see> property
        /// </remarks>
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public bool Connect()
        {
            if (string.IsNullOrEmpty(mNetResource.lpRemoteName))
            {
                ErrorMessage = "Share name not specified";
                return false;
            }
            return RealConnect();
        }

        /// <summary>
        /// Updates class variable with the specified share path
        /// </summary>
        /// <remarks>If the path ends in a forward slash then the slash will be removed</remarks>
        /// <param name="shareName"></param>
        private void DefineShareName(string shareName)
        {
            if (shareName.EndsWith("\\"))
            {
                mShareName = shareName.TrimEnd('\\');
            }
            else
            {
                mShareName = shareName;
            }
        }

        /// <summary>
        /// Connects to specified share using account/password specified previously
        /// </summary>
        private bool RealConnect()
        {
            var errorNum = WNetAddConnection2(ref mNetResource, mPassword, mUsername, 0);
            if (errorNum == NO_ERROR)
            {
                Debug.WriteLine("Connected.");
                return true;
            }

            ErrorMessage = errorNum.ToString();
            Debug.WriteLine("Got error: " + errorNum);
            return false;
        }

        /// <summary>
        /// Disconnects the file share
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public bool Disconnect()
        {
            var errorNum = WNetCancelConnection2(mNetResource.lpRemoteName, 0, Convert.ToInt32(true));
            if (errorNum == NO_ERROR)
            {
                Debug.WriteLine("Disconnected.");
                return true;
            }

            ErrorMessage = errorNum.ToString();
            Debug.WriteLine("Got error: " + errorNum);
            return false;
        }

        /// <summary>
        /// Gets the error message returned by the Connect and Disconnect methods
        /// </summary>
        public string ErrorMessage { get; private set; } = string.Empty;
    }
}
