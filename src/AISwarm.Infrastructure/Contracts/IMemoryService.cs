using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure.Entities;

namespace AISwarm.Infrastructure;

public interface IMemoryService
{
    Task SaveMemoryAsync(string key, string value, string? @namespace, string? type = null, string? metadata = null);
    Task<MemoryEntryDto?> ReadMemoryAsync(string key, string? @namespace);
}
