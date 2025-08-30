using AISwarm.Infrastructure.Entities;

namespace AISwarm.Infrastructure.Eventing;

public record MemoryUpdatedPayload(MemoryEntryDto MemoryEntry) : IMemoryLifecyclePayload;
