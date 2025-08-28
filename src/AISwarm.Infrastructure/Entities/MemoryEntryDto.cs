namespace AISwarm.Infrastructure.Entities;

public record MemoryEntryDto(
    string Key,
    string Value,
    string Namespace,
    string Type,
    int Size);

