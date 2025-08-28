namespace AISwarm.Infrastructure;

public interface IMemoryService
{
    Task SaveMemoryAsync(string key, string value, string? @namespace, string? type = null, string? metadata = null);
    Task<(bool Found, string? Value)> ReadMemoryAsync(string key, string? @namespace);
}
