using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Infrastructure;

public class MemoryService(
    IDatabaseScopeService scopeService,
    ITimeService timeService) : IMemoryService
{
    public async Task SaveMemoryAsync(string key, string value, string? @namespace = null, string? type = null, string? metadata = null)
    {
        using var scope = scopeService.CreateWriteScope();

        var now = timeService.UtcNow;
        var namespaceName = @namespace ?? "";
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);

        var memoryEntry = new MemoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            Namespace = namespaceName,
            Key = key,
            Value = value,
            Type = type ?? "json",
            Metadata = metadata,
            IsCompressed = false,
            Size = valueBytes.Length,
            CreatedAt = now,
            LastUpdatedAt = now,
            AccessedAt = null,
            AccessCount = 0
        };

        scope.MemoryEntries.Add(memoryEntry);
        await scope.SaveChangesAsync();
    }

    public async Task<MemoryEntryDto?> ReadMemoryAsync(string key, string? @namespace)
    {
        using var scope = scopeService.CreateWriteScope();

        var namespaceName = @namespace ?? "";
        var memoryEntry = await scope.MemoryEntries
            .FirstOrDefaultAsync(m => m.Key == key && m.Namespace == namespaceName);

        if (memoryEntry == null)
            return null;

        // Update access tracking
        memoryEntry.AccessedAt = timeService.UtcNow;
        memoryEntry.AccessCount++;
        await scope.SaveChangesAsync();

        return new MemoryEntryDto(
            memoryEntry.Key,
            memoryEntry.Value,
            memoryEntry.Namespace,
            memoryEntry.Type,
            memoryEntry.Size);
    }
}
