namespace PRISM.Logging;

public class LogProcedureInfo
{
    /// <summary>
    /// Procedure used to store log messages
    /// </summary>
    public string ProcedureName { get; private set; }

    /// <summary>
    /// LogType parameter name
    /// </summary>
    public string LogTypeParamName { get; private set; }

    /// <summary>
    /// LogType parameter size
    /// </summary>
    public int LogTypeParamSize { get; private set; }

    /// <summary>
    /// Message parameter name
    /// </summary>
    public string MessageParamName { get; private set; }

    /// <summary>
    /// Message parameter size
    /// </summary>
    public int MessageParamSize { get; private set; }

    /// <summary>
    /// Log source parameter name
    /// </summary>
    public string LogSourceParamName { get; private set; }

    /// <summary>
    /// Log source parameter size
    /// </summary>
    public int LogSourceParamSize { get; private set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public LogProcedureInfo()
    {
        UpdateProcedureInfo();
    }

    /// <summary>
    /// Update the logging procedure info
    /// </summary>
    /// <param name="procedureName"></param>
    /// <param name="logTypeParamName"></param>
    /// <param name="messageParamName"></param>
    /// <param name="postedByParamName"></param>
    /// <param name="logTypeParamSize"></param>
    /// <param name="messageParamSize"></param>
    /// <param name="postedByParamSize"></param>
    public void UpdateProcedureInfo(
        string procedureName = "",
        string logTypeParamName = "",
        string messageParamName = "",
        string postedByParamName = "",
        int logTypeParamSize = 128,
        int messageParamSize = 4096,
        int postedByParamSize = 128)
    {
        ProcedureName = procedureName;

        LogTypeParamName = logTypeParamName;
        MessageParamName = messageParamName;
        LogSourceParamName = postedByParamName;

        LogTypeParamSize = logTypeParamSize;
        MessageParamSize = messageParamSize;
        LogSourceParamSize = postedByParamSize;
    }
}