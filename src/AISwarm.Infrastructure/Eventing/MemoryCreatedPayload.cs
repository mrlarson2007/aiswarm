using AISwarm.Infrastructure.Entities;

namespace AISwarm.Infrastructure.Eventing;

public record MemoryCreatedPayload(MemoryEntryDto MemoryEntry) : IMemoryLifecyclePayload;
