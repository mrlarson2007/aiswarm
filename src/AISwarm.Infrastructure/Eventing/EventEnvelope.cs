using Microsoft.Extensions.Logging;

namespace AISwarm.Infrastructure.Eventing;

public record EventEnvelope(
    string Type,
    DateTimeOffset Timestamp,
    string? CorrelationId,
    string? Actor,
    object? Payload,
    IReadOnlyList<string>? Tags = null,
    LogLevel? Severity = null);
