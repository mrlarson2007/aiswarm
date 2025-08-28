namespace AISwarm.Server.Entities;

public class SaveMemoryResult
{
    public bool Success
    {
        get;
        init;
    }

    public string? ErrorMessage
    {
        get;
        init;
    }

    public static SaveMemoryResult Failure(string message)
    {
        return new SaveMemoryResult { Success = false, ErrorMessage = message };
    }
}
