namespace AISwarm.Server.McpTools;

public class ReportTaskCompletionResult
{
    public bool IsSuccess { get; }
    public string Message { get; }

    private ReportTaskCompletionResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static ReportTaskCompletionResult Success(string taskId)
    {
        return new ReportTaskCompletionResult(true, $"Task completed successfully: {taskId}");
    }

    public static ReportTaskCompletionResult Failure(string message)
    {
        return new ReportTaskCompletionResult(false, message);
    }
}