using AISwarm.Infrastructure.Entities;

namespace AISwarm.Infrastructure.Eventing;

public interface IMemoryLifecyclePayload : IEventPayload
{
    MemoryEntryDto MemoryEntry { get; }
}
