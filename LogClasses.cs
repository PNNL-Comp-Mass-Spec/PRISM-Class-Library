using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Security.Principal;
using System.Threading;

namespace PRISM
{

    /// <summary>
    /// The type of log message.
    /// </summary>
    public enum logMsgType
    {
        /// <summary>
        /// The message is informational.
        /// </summary>
        logNormal,
        /// <summary>
        /// The message represents an error.
        /// </summary>
        logError,
        /// <summary>
        /// The message represents a warning.
        /// </summary>
        logWarning,
        /// <summary>
        /// The message is only for debugging purposes.
        /// </summary>
        logDebug,
        /// <summary>
        /// The mesaage does not apply (to what?).
        /// </summary>
        logNA,
        /// <summary>
        /// The message is an indicator of (in)correct operation.
        /// </summary>
        logHealth
    }

    #region "Logger Interface"
    /// <summary>
    /// Defines the logging interface.
    /// </summary>
    public interface ILogger
    {

        /// <summary>
        /// Current log file path
        /// </summary>
        string CurrentLogFilePath { get; }

        /// <summary>
        /// Most recent log message
        /// </summary>
        string MostRecentLogMessage { get; }

        /// <summary>
        /// Most recent error
        /// </summary>
        string MostRecentErrorMessage { get; }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="EntryType">The ILogger error type.</param>
        /// <param name="localOnly">Post message locally only.</param>
        void PostEntry(string message, logMsgType EntryType, bool localOnly);

        /// <summary>
        /// Posts an error to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="e">The exception associated with the error.</param>
        /// <param name="localOnly">Post message locally only.</param>
        void PostError(string message, Exception e, bool localOnly);
    }
    #endregion

    #region "Logger Aware Interface"
    /// <summary>
    /// Defines the logging aware interface.
    /// </summary>
    /// <remarks>
    /// This interface is used by any class that wants to optionally support 
    /// logging to a logger that implements the ILogger interface.  The key
    /// here is the phrase optionally.  The class allows, but does not
    /// require the class user to supply an ILogger.  If the Logger is not
    /// specified, the class throws Exceptions and raises Events in the usual
    /// way.  If an ILogger is specified, the user has the option of just logging,
    /// or logging and throwing/raising Exceptions/Events in the usual way as well.
    /// </remarks>
    public interface ILoggerAware
    {
        /// <summary>
        /// Register an ILogger with a class to have it log any exception that might occur.
        /// </summary>
        /// <param name="logger">A logger object to be used when logging is desired.</param>
        void RegisterExceptionLogger(ILogger logger);

        /// <summary>
        /// Register an ILogger with a class to have it log any event that might occur.
        /// </summary>
        /// <param name="logger">A logger object to be used when logging is desired.</param>
        void RegisterEventLogger(ILogger logger);

        /// <summary>
        /// Set true and the class will raise events.  Set false and it will not.
        /// </summary>
        /// <remarks>A function like the one shown below can be placed in ILoggerAware class that will only raise the event in the
        /// event of one needing to be raised.
        /// </remarks>
        bool NotifyOnEvent { get; set; }

        /// <summary>
        /// Set true and the class will throw exceptions.  Set false and it will not
        /// </summary>
        /// <remarks>A function like this can be place in ILoggerAware class that will only throw an exception in the
        /// event of one needing to be thrown.
        /// </remarks>
        bool NotifyOnException { get; set; }

    }
    #endregion

    /// <summary>
    /// Utility functions
    /// </summary>
    public class Utilities
    {

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a compact description of the current stack
        /// </summary>
        /// <param name="objException"></param>
        /// <returns>
        /// String of the form:
        /// "Stack trace: clsCodeTest.Test-:-clsCodeTest.TestException-:-clsCodeTest.InnerTestException in clsCodeTest.vb:line 86"
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTrace(Exception objException)
        {
            return clsStackTraceFormatter.GetExceptionStackTrace(objException);
        }

        /// <summary>
        /// Parses the StackTrace text of the given exception to return a cleaned up description of the current stack,
        /// with one line for each function in the call tree
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>
        /// Stack trace: 
        ///   clsCodeTest.Test
        ///   clsCodeTest.TestException
        ///   clsCodeTest.InnerTestException 
        ///    in clsCodeTest.vb:line 86
        /// </returns>
        /// <remarks>Useful for removing the full file paths included in the default stack trace</remarks>
        public static string GetExceptionStackTraceMultiLine(Exception ex)
        {
            return clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex);
        }
    }

    #region "File Logger Class"
    /// <summary>
    /// Provides logging to a local file.
    /// </summary>
    /// <remarks>
    /// The actual log file name changes daily and is of the form "filePath_mm-dd-yyyy.txt".
    /// </remarks>
    public class clsFileLogger : ILogger
    {
        const string DATE_TIME_FORMAT = "yyyy-MM-dd hh:mm:ss tt";

        /// <summary>
        /// Log file path
        /// </summary>
        private string m_logFileName = "";

        /// <summary>
        /// Program name
        /// </summary>
        private string m_programName;

        /// <summary>
        /// Program version
        /// </summary>
        private string m_programVersion;

        protected string m_CurrentLogFilePath = string.Empty;
        protected string m_MostRecentLogMessage = string.Empty;

        protected string m_MostRecentErrorMessage = string.Empty;

        /// <summary>
        /// Initializes a new instance of the clsFileLogger class.
        /// </summary>
        public clsFileLogger()
        {
        }

        /// <summary>
        /// Initializes a new instance of the clsFileLogger class which logs to the specified file.
        /// </summary>
        /// <param name="filePath">The name of the file to use for the log.</param>
        /// <remarks>The actual log file name changes daily and is of the form "filePath_mm-dd-yyyy.txt".</remarks>
        public clsFileLogger(string filePath)
        {
            m_logFileName = filePath;
        }

        public string CurrentLogFilePath => m_CurrentLogFilePath;

        /// <summary>
        /// Gets the product version associated with this application.
        /// </summary>
        public string ExecutableVersion
        {
            get
            {
                if ((m_programVersion == null))
                {
                    m_programVersion = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
                }
                return m_programVersion;
            }
        }

        /// <summary>
        /// Gets the name of the executable file that started the application.
        /// </summary>
        public string ExecutableName
        {
            get
            {
                if ((m_programName == null))
                {
                    m_programName = Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location);
                }
                return m_programName;
            }
        }

        /// <summary>
        /// The name of the file being used as the log.
        /// </summary>
        /// <remarks>The actual log file name changes daily and is of the form "filePath_mm-dd-yyyy.txt".</remarks>
        public string LogFilePath
        {
            get { return m_logFileName; }
            set { m_logFileName = value; }
        }

        /// <summary>
        /// Most recent log message
        /// </summary>
        public string MostRecentLogMessage => m_MostRecentLogMessage;

        /// <summary>
        /// Most recent error message
        /// </summary>
        public string MostRecentErrorMessage => m_MostRecentErrorMessage;

        /// <summary>
        /// Writes a message to the log file.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="EntryType">The ILogger error type.</param>
        private void LogToFile(string message, logMsgType EntryType)
        {
            // don't log to file if no file name given
            if (string.IsNullOrEmpty(m_logFileName))
            {
                return;
            }

            // Set up date values for file name

            // Create log file name by appending specified file name and date
            m_CurrentLogFilePath = m_logFileName + "_" + DateTime.Now.ToString("MM-dd-yyyy") + ".txt";

            try
            {
                var FormattedLogMessage =
                    DateTime.Now.ToString(DATE_TIME_FORMAT) + ", " +
                    message + ", " +
                    TypeToString(EntryType) + ", ";

                using (var swLogFile = new StreamWriter(new FileStream(m_CurrentLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
                {
                    swLogFile.WriteLine(FormattedLogMessage);
                }

                if (EntryType == logMsgType.logError)
                {
                    m_MostRecentErrorMessage = FormattedLogMessage;
                }
                else
                {
                    m_MostRecentLogMessage = FormattedLogMessage;
                }

            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Converts enumerated error type to string for logging output.
        /// </summary>
        /// <param name="MyErrType">The ILogger error type.</param>
        protected string TypeToString(logMsgType MyErrType)
        {
            string functionReturnValue;
            switch (MyErrType)
            {
                case logMsgType.logNormal:
                    functionReturnValue = "Normal";
                    break;
                case logMsgType.logError:
                    functionReturnValue = "Error";
                    break;
                case logMsgType.logWarning:
                    functionReturnValue = "Warning";
                    break;
                case logMsgType.logDebug:
                    functionReturnValue = "Debug";
                    break;
                case logMsgType.logNA:
                    functionReturnValue = "na";
                    break;
                case logMsgType.logHealth:
                    functionReturnValue = "Health";
                    break;
                default:
                    functionReturnValue = "??";
                    break;
            }
            return functionReturnValue;
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="EntryType">The ILogger error type.</param>
        /// <param name="localOnly">Post message locally only.</param>
        public virtual void PostEntry(string message, logMsgType EntryType, bool localOnly)
        {
            LogToFile(message, EntryType);
        }

        /// <summary>
        /// Posts an error to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="ex">The exception associated with the error.</param>
        /// <param name="localOnly">Post message locally only.</param>
        public virtual void PostError(string message, Exception ex, bool localOnly)
        {
            LogToFile(message + ": " + ex.Message, logMsgType.logError);
        }

    }
    #endregion

    #region "Database Logger Class"
    /// <summary>
    /// Provides logging to a database and local file.
    /// </summary>
    /// <remarks>The module name identifies the logging process; if not defined, will use MachineName:UserName</remarks>
    public class clsDBLogger : clsFileLogger
    {

        // connection string
        private string m_connection_str;

        // db error list
        private readonly StringCollection m_error_list = new StringCollection();

        /// <summary>
        /// Module name
        /// </summary>
        protected string m_moduleName;

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class.
        /// </summary>
        public clsDBLogger() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class which logs to the specified database.
        /// </summary>
        /// <param name="connectionStr">The connection string used to access the database.</param>
        public clsDBLogger(string connectionStr)
        {
            m_connection_str = connectionStr;
        }

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class which logs to the specified database and file.
        /// </summary>
        /// <param name="connectionStr">The connection string used to access the database.</param>
        /// <param name="filePath">The name of the file to use for the log.</param>
        public clsDBLogger(string connectionStr, string filePath) : base(filePath)
        {
            m_connection_str = connectionStr;
        }

        /// <summary>
        /// Initializes a new instance of the clsDBLogger class which logs to the specified database and file.
        /// </summary>
        /// <param name="modName">The string used to identify the posting process.</param>
        /// <param name="connectionStr">The connection string used to access the database.</param>
        /// <param name="filePath">The name of the file to use for the log.</param>
        /// <remarks>The module name identifies the logging process; if not defined, will use MachineName:UserName</remarks>
        public clsDBLogger(string modName, string connectionStr, string filePath) : base(filePath)
        {
            m_connection_str = connectionStr;
            m_moduleName = modName;
        }

        /// <summary>
        /// The connection string used to access the database.
        /// </summary>
        public string ConnectionString
        {
            get { return m_connection_str; }
            set { m_connection_str = value; }
        }

        /// <summary>
        /// The module name identifies the logging process.
        /// </summary>
        public string MachineName
        {
            get
            {
                var host = System.Net.Dns.GetHostName();

                if (host.Contains("."))
                {
                    host = host.Substring(0, host.IndexOf('.'));
                }

                return host;
            }
        }

        /// <summary>
        /// The module name identifies the logging process.
        /// </summary>
        public string UserName => WindowsIdentity.GetCurrent().Name;

        /// <summary>
        /// Construct the string MachineName:UserName.
        /// </summary>
        private string ConstructModuleName()
        {
            var retVal = MachineName + ":" + UserName;
            return retVal;
        }

        /// <summary>
        /// The module name identifies the logging process.
        /// </summary>
        /// <remarks>If the module name is not specified, it is filled in as
        /// MachineName:UserName.</remarks>
        public string ModuleName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(m_moduleName))
                {
                    m_moduleName = ConstructModuleName();
                }
                return m_moduleName;
            }
            set { m_moduleName = value; }
        }

        /// <summary>
        /// Writes a message to the log table.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="EntryType">The ILogger error type.</param>
        protected virtual void LogToDB(string message, logMsgType EntryType)
        {
            PostLogEntry(TypeToString(EntryType), message);
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="EntryType">The ILogger error type.</param>
        /// <param name="localOnly">Post message locally only.</param>
        public override void PostEntry(string message, logMsgType EntryType, bool localOnly)
        {
            if (LogFilePath != null)
            {
                base.PostEntry(message, EntryType, localOnly);
            }
            if (!localOnly)
            {
                LogToDB(message, EntryType);
            }
        }

        /// <summary>
        /// Posts an error to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="e">The exception associated with the error.</param>
        /// <param name="localOnly">Post message locally only.</param>
        public override void PostError(string message, Exception e, bool localOnly)
        {
            if (LogFilePath != null)
            {
                base.PostError(message, e, localOnly);
            }
            if (!localOnly)
            {
                LogToDB(message + ": " + e.Message, logMsgType.logError);
            }
        }

        /// <summary>
        /// Writes a message to the log table via the stored procedure.
        /// </summary>
        /// <param name="type">The ILogger error type.</param>
        /// <param name="message">The message to post.</param>
        private void PostLogEntry(string type, string message)
        {
            try
            {
                m_error_list.Clear();
                // create the database connection
                //
                var cnStr = m_connection_str;
                using (var dbCn = new SqlConnection(cnStr))
                {
                    dbCn.InfoMessage += OnInfoMessage;
                    dbCn.Open();

                    // create the command object
                    //
                    var sc = new SqlCommand("PostLogEntry", dbCn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    //
                    // define parameter for stored procedure's return value
                    //
                    sc.Parameters.Add("@Return", SqlDbType.Int).Direction = ParameterDirection.ReturnValue;

                    //
                    // define parameters for the stored procedure's arguments
                    //
                    sc.Parameters.Add("@type", SqlDbType.VarChar, 50).Value = type;

                    sc.Parameters.Add("@message", SqlDbType.VarChar, 500).Value = message;

                    sc.Parameters.Add("@postedBy", SqlDbType.VarChar, 50).Value = ModuleName;

                    // execute the stored procedure
                    //
                    sc.ExecuteNonQuery();

                }

                // if we made it this far, we succeeded
                //

            }
            catch (Exception ex)
            {
                PostError("Failed to post log entry in database.", ex, true);
            }
        }

        /// <summary>
        /// Event handler for InfoMessage event.
        /// </summary>
        /// <remarks>Errors and warnings sent from the SQL Server database engine are caught here.</remarks>
        private void OnInfoMessage(object sender, SqlInfoMessageEventArgs args)
        {
            foreach (SqlError err in args.Errors)
            {
                var s = "";
                s += "Message: " + err.Message;
                s += ", Source: " + err.Source;
                s += ", Class: " + err.Class;
                s += ", State: " + err.State;
                s += ", Number: " + err.Number;
                s += ", LineNumber: " + err.LineNumber;
                s += ", Procedure:" + err.Procedure;
                s += ", Server: " + err.Server;
                m_error_list.Add(s);
            }
        }

    }
    #endregion

    #region "Log Entry Class"

    /// <summary>
    /// A class to hold a log entry
    /// </summary>
    public class clsLogEntry
    {
        /// <summary>
        /// Log message
        /// </summary>
        public string Message;

        /// <summary>
        /// Log message type
        /// </summary>
        public logMsgType EntryType;

        /// <summary>
        /// When true, log to the local file but not to the database
        /// </summary>
        public bool LocalOnly;

        public clsLogEntry(string message, logMsgType entryType, bool localOnly = true)
        {
            Message = message;
            EntryType = entryType;
            LocalOnly = localOnly;
        }
    }

    #endregion

    #region "Queue Logger Class"
    /// <summary>
    /// Wraps a queuing mechanism around any object that implements ILogger interface.
    /// </summary>
    /// <remarks>The posting member functions of this class put the log entry
    /// onto the end of an internal queue and return very quickly to the caller.
    /// A separate thread within the class is used to perform the actual output of
    /// the log entries using the logging object that is specified
    /// in the constructor for this class.</remarks>
    public class clsQueLogger : ILogger
    {

        // queue to hold entries to be output

        protected ConcurrentQueue<clsLogEntry> m_queue;

        // Internal thread for outputting entries from queue
        protected Timer m_ThreadTimer;

        // logger object to use for outputting entries from queue
        protected ILogger m_logger;
        public string CurrentLogFilePath
        {
            get
            {
                if (m_logger == null)
                {
                    return string.Empty;
                }

                return m_logger.CurrentLogFilePath;
            }
        }

        public string MostRecentLogMessage
        {
            get
            {
                if (m_logger == null)
                {
                    return string.Empty;
                }

                return m_logger.MostRecentLogMessage;
            }
        }

        public string MostRecentErrorMessage
        {
            get
            {
                if (m_logger == null)
                {
                    return string.Empty;
                }

                return m_logger.MostRecentErrorMessage;
            }
        }

        /// <summary>
        /// Constructor: Initializes a new instance of the clsQueLogger class which logs to the ILogger.
        /// </summary>
        /// <param name="logger">The target logger object.</param>
        public clsQueLogger(ILogger logger)
        {
            // Remember my logging object
            m_logger = logger;

            // Create a thread safe queue for log entries
            m_queue = new ConcurrentQueue<clsLogEntry>();

            // Log every 1 second
            m_ThreadTimer = new Timer(LogFromQueue, this, 0, 1000);
        }

        /// <summary>
        /// Pull all entries from the queue and output them to the log streams.
        /// </summary>
        protected void LogFromQueue(object o)
        {
            if (m_queue.Count == 0)
                return;

            var messages = new List<clsLogEntry>();

            while (true)
            {
                if (m_queue.TryDequeue(out var le))
                {
                    messages.Add(le);
                }

                if (m_queue.Count == 0)
                    break;
            }

            m_logger.PostEntries(messages);

        }

        /// <summary>
        /// Pull all entries from the queue and output them to the log streams.
        /// </summary>
        protected void LogFromQueue()
        {
            while (true)
            {
                if (m_queue.Count == 0)
                    break;

                var le = (clsLogEntry)m_queue.Dequeue();
                m_logger.PostEntry(le.message, le.entryType, le.localOnly);
            }
            m_threadRunning = false;
        }

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="EntryType">The ILogger error type.</param>
        /// <param name="localOnly"></param>
        public void PostEntry(string message, logMsgType EntryType, bool localOnly)
        {
            var le = new clsLogEntry(message, entryType, localOnly);
            m_queue.Enqueue(le);
        }

        /// <summary>
        /// Posts a message to the log.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <param name="e">The exception associated with the error.</param>
        /// <param name="localOnly">Post message locally only.</param>
        public void PostError(string message, Exception e, bool localOnly)
        {
            var le = new clsLogEntry(message + ": " + e.Message, logMsgType.logError, localOnly);
            m_queue.Enqueue(le);
        }
    }
    #endregion


    // Deprecated
    //
    //#region "Control Logger Class"

    ///// <summary>Provides logging to a control.</summary>
    ///// <remarks>The actual log control can be a textbox, listbox and a listview.</remarks>
    //public class clsControlLogger : ILogger
    //{

    //		// log file path
    //	private ListBox m_loglsBoxCtl;
    //		// log file path
    //	private ListView m_loglsViewCtl;
    //		// log file path
    //	private TextBoxBase m_logtxtBoxCtl;
    //		// program name
    //	private string m_programName;
    //		// program version
    //	private string m_programVersion;

    //	protected string m_MostRecentLogMessage = string.Empty;

    //	protected string m_MostRecentErrorMessage = string.Empty;
    //
    //	/// <summary>Initializes a new instance of the clsFileLogger class which logs to a listbox.</summary>
    //	/// <param name="lsBox">The name of the listbox used to log message.</param>
    //	public clsControlLogger(ListBox lsBox)
    //	{
    //		m_loglsBoxCtl = lsBox;
    //	}

    //	/// <summary>Initializes a new instance of the clsFileLogger class which logs to a listview.</summary>
    //	/// <param name="lsView">The name of the listview used to log message.</param>
    //	public clsControlLogger(ListView lsView)
    //	{
    //		m_loglsViewCtl = lsView;
    //		// Set the view to show details.
    //		m_loglsViewCtl.View = View.Details;
    //		// Allow the user to edit item text.
    //		m_loglsViewCtl.LabelEdit = true;
    //		// Allow the user to rearrange columns.
    //		m_loglsViewCtl.AllowColumnReorder = true;
    //		// Display check boxes.
    //		m_loglsViewCtl.CheckBoxes = true;
    //		// Select the item and subitems when selection is made.
    //		m_loglsViewCtl.FullRowSelect = true;
    //		// Display grid lines.
    //		m_loglsViewCtl.GridLines = true;
    //		// Sort the items in the list in ascending order.
    //		m_loglsViewCtl.Sorting = Windows.Forms.SortOrder.Ascending;

    //		// Create columns for the items and subitems.
    //		m_loglsViewCtl.Columns.Add("Date", 100, HorizontalAlignment.Left);
    //		m_loglsViewCtl.Columns.Add("ProgramName", 100, HorizontalAlignment.Left);
    //		m_loglsViewCtl.Columns.Add("ProgramVersion", 100, HorizontalAlignment.Left);
    //		m_loglsViewCtl.Columns.Add("Message", 200, HorizontalAlignment.Left);
    //		m_loglsViewCtl.Columns.Add("EntryType", 100, HorizontalAlignment.Left);

    //	}

    //	/// <summary>Initializes a new instance of the clsFileLogger class which logs to a textbox.</summary>
    //	/// <param name="txtBox">The name of the textbox used to log message.</param>
    //	public clsControlLogger(TextBoxBase txtBox)
    //	{
    //		if (txtBox.Multiline == false) {
    //			throw new Exception("The textBox is not multiline!");
    //		}
    //		m_logtxtBoxCtl = txtBox;
    //	}

    //	public string CurrentLogFilePath {
    //		get { return string.Empty; }
    //	}

    //	/// <summary>Gets the product version associated with this application.</summary>
    //	public string ExecutableVersion {
    //		get {
    //			if ((m_programVersion == null)) {
    //				m_programVersion = Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
    //			}
    //			return m_programVersion;
    //		}
    //	}

    //	/// <summary>Gets the name of the executable file that started the application.</summary>
    //	public string ExecutableName {
    //		get {
    //			if ((m_programName == null)) {
    //				m_programName = Path.GetFileName(Reflection.Assembly.GetEntryAssembly().Location);
    //			}
    //			return m_programName;
    //		}
    //	}

    //	/// <summary>Gets and Sets the name of the listbox control.</summary>
    //	public ListBox LogListBox {
    //		get { return m_loglsBoxCtl; }
    //		set { m_loglsBoxCtl = value; }
    //	}

    //	/// <summary>Gets and Sets the name of the listview control.</summary>
    //	public ListView LogListView {
    //		get { return m_loglsViewCtl; }
    //		set { m_loglsViewCtl = value; }
    //	}

    //	/// <summary>Gets and Sets the name of the TextBoxBase control.</summary>
    //	public TextBoxBase LogTextBoxBase {
    //		get { return m_logtxtBoxCtl; }
    //		set { m_logtxtBoxCtl = value; }
    //	}

    //	public string MostRecentLogMessage {
    //		get { return m_MostRecentLogMessage; }
    //	}

    //	public string MostRecentErrorMessage {
    //		get { return m_MostRecentErrorMessage; }
    //	}

    //	/// <summary>Posts a message.</summary>
    //	/// <param name="message">The message to post.</param>
    //	/// <param name="EntryType">The ILogger error type.</param>
    //	private void LogToControl(string message, logMsgType EntryType)
    //	{
    //		string localMsg = null;
    //		localMsg = DateTime.Now.ToString(DATE_TIME_FORMAT) + ", " + ", " + ExecutableName + ", " + ExecutableVersion + ", " + message + ", " + TypeToString(EntryType);

    //		if (EntryType == logMsgType.logError) {
    //			m_MostRecentErrorMessage = localMsg;
    //		} else {
    //			m_MostRecentLogMessage = localMsg;
    //		}

    //		PostEntryTextBox(localMsg);
    //		PostEntryListBox(localMsg);
    //		PostEntryListview(message, EntryType);
    //	}

    //	/// <summary>Posts a message to the textbox control.</summary>
    //	/// <param name="message">The message to post.</param>
    //	private void PostEntryTextBox(string message)
    //	{
    //		if ((m_logtxtBoxCtl == null))
    //			return;
    //		m_logtxtBoxCtl.Text = message;
    //	}

    //	/// <summary>Posts a message to the listbox control.</summary>
    //	/// <param name="message">The message to post.</param>
    //	private void PostEntryListBox(string message)
    //	{
    //		if ((m_loglsBoxCtl == null))
    //			return;
    //		m_loglsBoxCtl.Items.Add(message);
    //	}

    //	/// <summary>Posts a message to the listview control.</summary>
    //	/// <param name="message">The message to post.</param>
    //	/// <param name="EntryType">The ILogger error type.</param>
    //	private void PostEntryListview(string message, logMsgType EntryType)
    //	{
    //		if ((m_loglsViewCtl == null))
    //			return;
    //		// Create three items and three sets of subitems for each item.
    //		ListViewItem lvItem = new ListViewItem(DateTime.Now.ToString(DATE_TIME_FORMAT));
    //		lvItem.SubItems.Add(ExecutableName);
    //		lvItem.SubItems.Add(ExecutableVersion);
    //		lvItem.SubItems.Add(message);
    //		lvItem.SubItems.Add(TypeToString(EntryType));
    //		// Add the items to the ListView.
    //		m_loglsViewCtl.Items.AddRange(new ListViewItem[] { lvItem });
    //	}


    //	/// <summary>Converts enumerated error type to string for logging output.</summary>
    //	/// <param name="MyErrType">The ILogger error type.</param>
    //	protected string TypeToString(logMsgType MyErrType)
    //	{
    //		string functionReturnValue = null;
    //		switch (MyErrType) {
    //			case logMsgType.logNormal:
    //				functionReturnValue = "Normal";
    //				break;
    //			case logMsgType.logError:
    //				functionReturnValue = "Error";
    //				break;
    //			case logMsgType.logWarning:
    //				functionReturnValue = "Warning";
    //				break;
    //			case logMsgType.logDebug:
    //				functionReturnValue = "Debug";
    //				break;
    //			case logMsgType.logNA:
    //				functionReturnValue = "na";
    //				break;
    //			case logMsgType.logHealth:
    //				functionReturnValue = "Health";
    //				break;
    //			default:
    //				functionReturnValue = "??";
    //				break;
    //		}
    //		return functionReturnValue;
    //	}

    //	/// <summary>Posts a message to the log.</summary>
    //	/// <param name="message">The message to post.</param>
    //	/// <param name="EntryType">The ILogger error type.</param>
    //	/// <param name="localOnly">Post message locally only.</param>
    //	public virtual void PostEntry(string message, logMsgType EntryType, bool localOnly)
    //	{
    //		LogToControl(message, EntryType);
    //	}

    //	/// <summary>Posts an error to the log.</summary>
    //	/// <param name="message">The message to post.</param>
    //	/// <param name="ex">The exception associated with the error.</param>
    //	/// <param name="localOnly">Post message locally only.</param>
    //	public virtual void PostError(string message, Exception ex, bool localOnly)
    //	{
    //		LogToControl(message + ": " + ex.Message, logMsgType.logError);
    //	}

    //}
    // #endregion

}
