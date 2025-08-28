using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;

namespace AISwarm.Infrastructure;

public class MemoryService(
    IDatabaseScopeService scopeService,
    ITimeService timeService) : IMemoryService
{
    public async Task SaveMemoryAsync(string key, string value, string? @namespace = null)
    {
        using var scope = scopeService.CreateWriteScope();

        var now = timeService.UtcNow;
        var namespaceName = @namespace ?? "default";
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
        
        var memoryEntry = new MemoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            Namespace = namespaceName,
            Key = key,
            Value = value,
            Type = "json",
            Metadata = null,
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
}
