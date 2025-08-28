namespace AISwarm.Infrastructure;

public interface IMemoryService
{
    Task SaveMemoryAsync(string key, string value, string? @namespace, string? type = null);
}
