using AISwarm.Shared.Models;
using AISwarm.Infrastructure.Entities; // For MemoryEntryDto

namespace AISwarm.Server.Entities;

/// <summary>
///     Result of waiting for a memory key via MCP tool
/// </summary>
public class WaitForMemoryKeyResult : Result<WaitForMemoryKeyResult>
{
    /// <summary>
    ///     The memory entry that was waited for (only populated on success)
    /// </summary>
    public MemoryEntryDto? MemoryEntry { get; init; }

    // Parameterless constructor required by the 'new()' constraint in Result<T>
    public WaitForMemoryKeyResult() { }

    /// <summary>
    ///     Creates a successful result with the found memory entry
    /// </summary>
    public static WaitForMemoryKeyResult SuccessWithMemoryEntry(MemoryEntryDto memoryEntry) // Renamed method
    {
        return new WaitForMemoryKeyResult
        {
            Success = true,
            MemoryEntry = memoryEntry
        };
    }
}
