using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PRISM
{
    /// <summary>
    /// Connects to a file share using a password and user name.
    /// </summary>
    /// <remarks>
    /// The default behaviour when connecting to SMB/CIFS file shares is for
    /// the system to supply the user name and password used to logon to the local machine.
    /// This class allows you to connect to SMB/CIFS file shares when the use of
    /// SSPI isn't availabe and/or when you don't wish to use the default behaviour.
    /// It's quite comparable to the "Connect using a different user name." option in the Map Network Drive
    /// utility in Windows.  Much of this code came from Microsoft Knowledge Base Article - 173011.  It was
    /// then modified to fit our needs.
    /// </remarks>
    public class ShareConnector
    {

        private string mErrorMessage = "";

#pragma warning disable 1591
        public enum ResourceScope
        {
            Connected = 1,
            GlobalNetwork,
            Remembered,
            Recent,
            Context
        }

        public enum ResourceType
        {
            Any = 0,
            Disk = 1,
            Print = 2,
            Reserved = 8
        }

        public enum ResourceDisplaytype
        {
            Generic = 0x0,
            Domain = 0x1,
            Server = 0x2,
            Share = 0x3,
            File = 0x4,
            Group = 0x5,
            Network = 0x6,
            Root = 0x7,
            Shareadmin = 0x8,
            Directory = 0x9,
            Tree = 0xa,
            Ndscontainer = 0xb
        }
#pragma warning restore 1591

        /// <summary>
        /// This structure is used to group a bunch of member variables.
        /// </summary>
        private struct udtNetResource
        {
            public ResourceScope dwScope;
            public ResourceType dwType;
            public ResourceDisplaytype dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        private const short NO_ERROR = 0;

        // Unused constant
        // private const short CONNECT_UPDATE_PROFILE = 0x1;
        // /// <summary> Constant that may be used by NETRESOURCE->dwScope </summary>
        // private const short RESOURCE_CONNECTED As Short = &H1S
        // /// <summary> Constant that may be used by NETRESOURCE->dwScope </summary>
        // private const short RESOURCE_GLOBALNET As Short = &H2S

        // /// <summary> Constant that may be used by NETRESOURCE->dwType </summary>
        // private const short RESOURCETYPE_DISK As Short = &H1S
        // /// <summary> Constant that may be used by NETRESOURCE->dwType </summary>
        // private const short RESOURCETYPE_PRINT As Short = &H2S
        // /// <summary> Constant that may be used by NETRESOURCE->dwType </summary>
        // private const short RESOURCETYPE_ANY As Short = &H0S

        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCEDISPLAYTYPE_DOMAIN As Short = &H1S
        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCEDISPLAYTYPE_GENERIC As Short = &H0S
        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCEDISPLAYTYPE_SERVER As Short = &H2S
        // /// <summary> Constant that may be used by NETRESOURCE->dwDisplayType </summary>
        // private const short RESOURCEDISPLAYTYPE_SHARE As Short = &H3S

        /// <summary>
        ///  Constant that may be used by NETRESOURCE->dwUsage
        /// </summary>
        private const short RESOURCEUSAGE_CONNECTABLE = 0x1;

        /// <summary>
        ///  Constant that may be used by NETRESOURCE->dwUsage
        /// </summary>
        private const short RESOURCEUSAGE_CONTAINER = 0x2;

        [DllImport("mpr.dll", EntryPoint = "WNetAddConnection2A", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int WNetAddConnection2(ref udtNetResource lpNetResource, string lpPassword, string lpUserName, int dwFlags);

        [DllImport("mpr.dll", EntryPoint = "WNetCancelConnection2A", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int WNetCancelConnection2(string lpName, int dwFlags, int fForce);

        private udtNetResource mNetResource;
        private string mUsername;
        private string mPassword;

        private string mShareName = string.Empty;

        /// <summary>
        /// This version of the constructor requires you to specify the sharename by setting the <see cref="Share">Share</see> property.
        /// </summary>
        /// <param name="userName">Username</param>
        /// <param name="userPwd">Password</param>
        /// <remarks>For local user accounts, it is safest to use HostName\username</remarks>
        public ShareConnector(string userName, string userPwd)
        {
            RealNew(userName, userPwd);
        }

        /// <summary>
        /// This version of the constructor allows you to specify the sharename as an argument.
        /// </summary>
        /// <param name="shareName">The name of the file share to which you will connect.</param>
        /// <param name="userName">Username</param>
        /// <param name="userPwd">Password</param>
        /// <remarks>For local user accounts, it is safest to use HostName\username</remarks>  ///
        public ShareConnector(string shareName, string userName, string userPwd)
        {
            DefineShareName(shareName);
            RealNew(userName, userPwd);
        }

        /// <summary>
        /// This routine is called by each of the constructors to make the actual assignments in a consistent fashion.
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
            mNetResource.dwDisplayType = ResourceDisplaytype.Share;
            mNetResource.dwUsage = RESOURCEUSAGE_CONNECTABLE;
        }

        /// <summary>
        /// Sets the name of the file share to which you will connect.
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
        /// the file share name passed as an argument.
        /// </summary>
        /// <param name="shareName">The name of the file share to which you will connect.</param>
        public bool Connect(string shareName)
        {

            DefineShareName(shareName);
            mNetResource.lpRemoteName = mShareName;
            return RealConnect();

        }

        /// <summary>
        /// Connects to specified share using account/password specified through the constructor.
        /// Requires you to have specifyed the sharename by setting the <see cref="Share">Share</see> property.
        /// </summary>
        public bool Connect()
        {

            if (string.IsNullOrEmpty(mNetResource.lpRemoteName))
            {
                mErrorMessage = "Share name not specified";
                return false;
            }
            return RealConnect();

        }

        /// <summary>
        /// Updates class variable with the specified share path
        /// </summary>
        /// <param name="shareName"></param>
        /// <remarks>If the path ends in a forward slash then the slash will be removed</remarks>
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
        /// Connects to specified share using account/password specified previously.
        /// This is the function that actually does the connection based on the setup
        /// from the Connect function.
        /// </summary>
        private bool RealConnect()
        {
            var errorNum = WNetAddConnection2(ref mNetResource, mPassword, mUsername, 0);
            if (errorNum == NO_ERROR)
            {
                Debug.WriteLine("Connected.");
                return true;
            }

            mErrorMessage = errorNum.ToString();
            Debug.WriteLine("Got error: " + errorNum);
            return false;
        }

        /// <summary>
        /// Disconnects the files share.
        /// </summary>
        public bool Disconnect()
        {
            var errorNum = WNetCancelConnection2(mNetResource.lpRemoteName, 0, Convert.ToInt32(true));
            if (errorNum == NO_ERROR)
            {
                Debug.WriteLine("Disconnected.");
                return true;
            }

            mErrorMessage = errorNum.ToString();
            Debug.WriteLine("Got error: " + errorNum);
            return false;
        }

        /// <summary>
        /// Gets the error message returned by the Connect and Disconnect functions.
        /// </summary>
        public string ErrorMessage => mErrorMessage;
    }
}
