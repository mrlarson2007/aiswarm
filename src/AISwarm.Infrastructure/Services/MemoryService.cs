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

        var memoryEntry = new MemoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            Namespace = @namespace ?? string.Empty,
            Key = key,
            Value = value,
            LastUpdatedAt = timeService.UtcNow
        };

        scope.MemoryEntries.Add(memoryEntry);
        await scope.SaveChangesAsync();
    }
}
