using AISwarm.Infrastructure.Entities;

namespace AISwarm.Server.Entities;

public record ReadMemoryResult(
    bool Success,
    string? ErrorMessage,
    string? Value,
    string? Key,
    string? Namespace,
    string? Type,
    int? Size,
    string? Metadata = null)
{
    public static ReadMemoryResult Failure(string errorMessage)
    {
        return new ReadMemoryResult(false, errorMessage, null, null, null, null, null);
    }

    public static ReadMemoryResult SuccessResult(MemoryEntryDto entry)
    {
        return new ReadMemoryResult(true,
            null,
            Key: entry.Key,
            Value: entry.Value,
            Namespace: entry.Namespace,
            Type: entry.Type,
            Size: entry.Size,
            Metadata: entry.Metadata);
    }
}
