namespace AISwarm.Server.Entities;

public class ReportTaskCompletionResult
{
    private ReportTaskCompletionResult(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public bool IsSuccess
    {
        get;
    }

    public string Message
    {
        get;
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
