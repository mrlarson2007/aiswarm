using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Infrastructure.Services;

/// <summary>
/// Memory service that uses per-request transaction coordination.
/// Uses IDatabaseScopeService to automatically coordinate transactions across multiple operations.
/// </summary>
public class MemoryService : IMemoryService
{
    private const string DefaultContentType = "text";

    private readonly IDatabaseScopeService _scopedDbService;
    private readonly ITimeService _timeService;

    public MemoryService(
        IDatabaseScopeService scopedDbService,
        ITimeService timeService)
    {
        _scopedDbService = scopedDbService;
        _timeService = timeService;
    }

    public async Task SaveMemoryAsync(string key, string value, string? @namespace = null, string? type = null, string? metadata = null)
    {
        // Get scoped write scope - uses existing transaction scope if available, creates new one if needed
        var scope = _scopedDbService.GetWriteScope();
        var now = _timeService.UtcNow;
        var namespaceName = @namespace ?? "";
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);

        var entity = await scope.MemoryEntries
            .FirstOrDefaultAsync(m => m.Key == key && m.Namespace == namespaceName);

        if (entity == null)
        {
            entity = new MemoryEntry
            {
                Id = Guid.NewGuid().ToString(),
                Namespace = namespaceName,
                Key = key,
                Value = value,
                Type = type ?? DefaultContentType,
                Metadata = metadata,
                IsCompressed = false,
                Size = valueBytes.Length,
                CreatedAt = now,
                LastUpdatedAt = now,
                AccessedAt = null,
                AccessCount = 0
            };
            scope.MemoryEntries.Add(entity);
        }
        else
        {
            entity.Value = value;
            entity.Type = type ?? entity.Type;
            entity.Metadata = metadata ?? entity.Metadata;
            entity.Size = valueBytes.Length;
            entity.LastUpdatedAt = now;
        }

        await scope.SaveChangesAsync();

        // Complete transaction - this will be cached until DI scope disposal
        await _scopedDbService.CompleteAsync();
    }

    public async Task<MemoryEntryDto?> ReadMemoryAsync(string key, string? @namespace)
    {
        // Get scoped read scope - creates new scope or returns cached one
        var scope = _scopedDbService.GetReadScope();
        var namespaceName = @namespace ?? "";

        var entity = await scope.MemoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Key == key && m.Namespace == namespaceName);

        if (entity == null)
            return null;

        return new MemoryEntryDto(
            entity.Key,
            entity.Value,
            entity.Namespace,
            entity.Type,
            entity.Size,
            entity.Metadata);
    }

    public async Task UpdateMemoryAccessAsync(string key, string? @namespace)
    {
        // Get scoped write scope for access time update
        var scope = _scopedDbService.GetWriteScope();
        var namespaceName = @namespace ?? "";

        var entity = await scope.MemoryEntries
            .FirstOrDefaultAsync(m => m.Key == key && m.Namespace == namespaceName);

        if (entity == null)
            return;

        // Update access tracking
        entity.AccessedAt = _timeService.UtcNow;
        entity.AccessCount++;
        await scope.SaveChangesAsync();

        // Complete transaction
        await _scopedDbService.CompleteAsync();
    }
}
