using AISwarm.Infrastructure.Entities;

namespace AISwarm.Server.Entities;

public record ReadMemoryResult(
    bool Success,
    string? ErrorMessage,
    string? Value,
    string? Key,
    string? Namespace,
    string? Type,
    int? Size)
{

    public static ReadMemoryResult Failure(string errorMessage) =>
        new(false, errorMessage, null, null, null, null, null);

    public static ReadMemoryResult SuccessResult(MemoryEntryDto entry) =>
        new(Success: true,
            ErrorMessage: null,
            Key: entry.Key,
            Value: entry.Value,
            Namespace: entry.Namespace,
            Type: entry.Type,
            Size: entry.Size);

}
