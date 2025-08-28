namespace AISwarm.Server.Entities;

public class SaveMemoryResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static SaveMemoryResult Failure(string message) => new() { Success = false, ErrorMessage = message };
    public static SaveMemoryResult SuccessResult() => new() { Success = true, ErrorMessage = null };
}
