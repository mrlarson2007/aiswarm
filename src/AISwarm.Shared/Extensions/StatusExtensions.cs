using AISwarm.DataLayer.Entities;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Shared.Extensions;

/// <summary>
/// Extension methods for AgentStatus to improve business logic readability
/// </summary>
public static class AgentStatusExtensions
{
    /// <summary>
    /// Determines if the agent is in an active state
    /// </summary>
    public static bool IsActive(this AgentStatus status) => status is 
        AgentStatus.Running or 
        AgentStatus.Starting;

    /// <summary>
    /// Determines if the agent is in a terminal state (cannot transition further)
    /// </summary>
    public static bool IsTerminal(this AgentStatus status) => status is 
        AgentStatus.Stopped or 
        AgentStatus.Failed or 
        AgentStatus.Killed;

    /// <summary>
    /// Determines if the agent can be killed (is in a killable state)
    /// </summary>
    public static bool CanBeKilled(this AgentStatus status) => 
        status.IsActive() || status == AgentStatus.Unhealthy;

    /// <summary>
    /// Determines if the agent is in a transitional state
    /// </summary>
    public static bool IsTransitional(this AgentStatus status) => status is 
        AgentStatus.Starting or 
        AgentStatus.Stopping;

    /// <summary>
    /// Determines if the agent is healthy and operational
    /// </summary>
    public static bool IsHealthy(this AgentStatus status) => status is 
        AgentStatus.Running;
}

/// <summary>
/// Extension methods for TaskStatus to improve business logic readability
/// </summary>
public static class TaskStatusExtensions
{
    /// <summary>
    /// Determines if the task has completed (successfully or with failure)
    /// </summary>
    public static bool IsComplete(this TaskStatus status) => status is 
        TaskStatus.Completed or 
        TaskStatus.Failed;

    /// <summary>
    /// Determines if the task is currently being worked on
    /// </summary>
    public static bool IsActive(this TaskStatus status) => 
        status == TaskStatus.InProgress;

    /// <summary>
    /// Determines if the task is available for assignment
    /// </summary>
    public static bool IsAvailable(this TaskStatus status) => 
        status == TaskStatus.Pending;

    /// <summary>
    /// Determines if the task can be assigned to an agent
    /// </summary>
    public static bool CanBeAssigned(this TaskStatus status) => 
        status == TaskStatus.Pending;

    /// <summary>
    /// Determines if the task can be cancelled
    /// </summary>
    public static bool CanBeCancelled(this TaskStatus status) => 
        status is TaskStatus.Pending or TaskStatus.InProgress;

    /// <summary>
    /// Determines if the task requires no further action
    /// </summary>
    public static bool IsFinal(this TaskStatus status) => 
        status.IsComplete();
}