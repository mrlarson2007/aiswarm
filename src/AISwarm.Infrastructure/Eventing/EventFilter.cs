using Microsoft.Extensions.Logging;

namespace AISwarm.Infrastructure.Eventing;

public record EventFilter(
    IReadOnlyList<string>? Types = null,
    string? AgentId = null,
    string? Persona = null,
    string? TaskId = null,
    LogLevel? MinSeverity = null,
    Func<EventEnvelope, bool>? Predicate = null);
