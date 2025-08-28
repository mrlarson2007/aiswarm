namespace AISwarm.Server.Entities;

public record ReadMemoryResult(bool Success, string? ErrorMessage, string? Value, string? Key, string? Namespace)
{
    public static ReadMemoryResult Failure(string errorMessage) =>
        new(false, errorMessage, null, null, null);

    public static ReadMemoryResult SuccessResult(string key, string value, string @namespace) =>
        new(true, null, value, key, @namespace);
}