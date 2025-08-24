# Event-Driven Notifications for Agent Coordination

Author: AI Swarm Team  
Date: 2025-08-24

## Overview

Replace periodic DB polling with an in-memory, event-driven notification subsystem to wake waiting operations (e.g., `get_next_task`) immediately when relevant state changes occur. Keep SQLite as the source of truth; events are non-durable hints and telemetry.

Goals:
- Lower latency and database load by eliminating fixed-interval polling
- Support multiple event types (task lifecycle, agent lifecycle, logs)
- Allow multiple independent subscribers (agents, planners, log sinks)
- Preserve correctness via DB-verified state transitions

## High-Level Architecture

```mermaid
flowchart LR
  subgraph Server[Coordination Server]
    EB[(EventBus)]
    R[Router / Filters]
    DB[(SQLite)]

    CT[CreateTaskMcpTool]
    NT[GetNextTaskMcpTool]
    RC[ReportCompletionMcpTool]
    AM[AgentManagementMcpTool]

    CT -->|insert Pending| DB
    RC -->|update Completed/Failed| DB
    AM -->|launch/kill/heartbeat| DB

    CT -->|publish TaskCreated| EB
    RC -->|publish TaskCompleted/Failed| EB
    AM -->|publish AgentLaunched/Killed/Heartbeat| EB

    EB --> R

    R -->|TaskCreated (agentId/persona)| NT
    R -->|TaskCompleted/Failed (taskId)| Planner
    R -->|LogEvent| FileSink
    R -->|LogEvent| DbSink
  end

  subgraph Agents
    AG[Agents (Implementer/Reviewer/...)]
  end

  AG <-->|MCP get_next_task| NT
  AG -->|MCP report_task_completion/failure| RC
  Planner[[Planner Tool/Process]] -. subscribes .-> R
  FileSink[(File Logger)] -. sink .-> R
  DbSink[(DB Logger)] -. sink .-> R
```

## Event Model

- Envelope: `EventEnvelope { Id, Type, Timestamp, CorrelationId, Actor, Payload, Tags[], Severity? }`
- Types: `TaskCreated`, `TaskUpdated`, `TaskCompleted`, `TaskFailed`, `AgentLaunched`, `AgentHeartbeat`, `AgentKilled`, `LogEvent`, `Custom`
- Payloads: Minimal identifiers (e.g., `taskId`, `agentId`, `persona`)—consumers re-query DB

## Interfaces

```csharp
public record EventEnvelope(
    string Type,
    DateTimeOffset Timestamp,
    string? CorrelationId,
    string? Actor,
    object? Payload,
    IReadOnlyList<string>? Tags = null,
    LogLevel? Severity = null);

public record EventFilter(
    IReadOnlyList<string>? Types = null,
    Guid? AgentId = null,
    string? Persona = null,
    Guid? TaskId = null,
    LogLevel? MinSeverity = null,
    Func<EventEnvelope, bool>? Predicate = null);

public interface IEventBus
{
    IAsyncEnumerable<EventEnvelope> Subscribe(EventFilter filter, CancellationToken ct = default);
    ValueTask PublishAsync(EventEnvelope evt, CancellationToken ct = default);
    bool TryPublish(EventEnvelope evt); // non-blocking best-effort
}

public interface IEventSink
{
    Task OnEventAsync(EventEnvelope evt, CancellationToken ct);
}

public interface IWorkItemNotificationService
{
    IAsyncEnumerable<EventEnvelope> SubscribeForAgent(Guid agentId, CancellationToken ct = default);
    IAsyncEnumerable<EventEnvelope> SubscribeForPersona(string persona, CancellationToken ct = default);
    ValueTask PublishTaskCreated(Guid taskId, Guid? agentId, string? persona);
    ValueTask PublishTaskCompleted(Guid taskId);
    ValueTask PublishTaskFailed(Guid taskId, string reason);
}
```

## Routing & Backpressure

- Backing: `System.Threading.Channels` per subscriber, bounded capacity (e.g., 64)
- Router performs filter matching and fan-out
- Overflow strategy: drop-oldest or coalesce frequent types (e.g., `TaskCreated`, `AgentHeartbeat`)
- Metrics: counters for dropped events per subscriber/type

## Tool/Service Integration

- `CreateTaskMcpTool`: insert Pending → `PublishTaskCreated`
- `GetNextTaskMcpTool`: fast-path DB check; if none, `SubscribeForAgent/Persona` and await; on wake, re-query and atomic claim
- `ReportTaskCompletionMcpTool`: update status → `PublishTaskCompleted/Failed`
- `AgentManagementMcpTool`: launch/kill/heartbeat → publish corresponding events
- Optional: `IAppLogger` adapter emits `LogEvent` to bus for sinks

## Concurrency & Correctness

- DB remains the authority; events only wake consumers
- Atomic state transitions enforced via conditional updates (e.g., `Pending -> InProgress` with `WHERE Status=Pending`)
- Multiple agents can be woken by persona events; only one wins DB claim; others continue waiting

## Resilience

- Server restart clears channels; agents re-subscribe on next call
- Timeouts respected using existing `GetNextTaskConfiguration` for long-poll boundaries
- Cancellation tokens cleanly remove subscriptions

## Testing Strategy

- Unit: filter matching, backpressure, coalescing, sink invocation
- Integration: sleeping agent woken by `TaskCreated` and successfully claims; two agents one task → single winner; kill agent interrupts waiters and marks dangling tasks failed
- Perf: notify-to-claim latency vs polling baseline; memory under bursty publication

## Migration / Roll-in

- Small project: no feature flags; event bus enabled by default
- Keep polling code paths as safety net in `GetNextTaskMcpTool` (fast-path first, then await)

## Open Questions

- Do we want a dedicated `EventLog` table for durable audit? (separate from transient bus)
- Should we expose a public MCP tool for subscribing to certain event classes (e.g., planner waiting on completion), or keep it server-internal and let planners poll `get_task_status`? 
- Coalescing thresholds per event type? Configurable capacities?
