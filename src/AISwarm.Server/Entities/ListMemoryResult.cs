using AISwarm.Infrastructure.Entities;

namespace AISwarm.Server.Entities;

public class ListMemoryResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<MemoryEntryDto>? Entries { get; init; }

    public static ListMemoryResult Failure(string message)
    {
        return new ListMemoryResult { Success = false, ErrorMessage = message };
    }

    public static ListMemoryResult SuccessResult(List<MemoryEntryDto> entries)
    {
        return new ListMemoryResult { Success = true, ErrorMessage = null, Entries = entries };
    }
}
