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

    public string? Key
    {
        get;
        init;
    }

    public string? Namespace
    {
        get;
        init;
    }

    public static SaveMemoryResult Failure(string message)
    {
        return new SaveMemoryResult { Success = false, ErrorMessage = message };
    }

    public static SaveMemoryResult SuccessResult(string key, string? @namespace)
    {
        return new SaveMemoryResult { Success = true, ErrorMessage = null, Key = key, Namespace = @namespace };
    }
}
