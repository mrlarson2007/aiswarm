namespace AISwarm.Shared.Constants;

/// <summary>
/// Standard error messages for task failure reasons
/// </summary>
public static class TaskFailureReasons
{
    public const string AgentTerminated = "Agent terminated";
    public const string AgentTimeout = "Agent timeout";
    public const string ProcessFailed = "Process failed";
    public const string InvalidInput = "Invalid input";
    public const string ServiceUnavailable = "Service unavailable";
}

/// <summary>
/// Standard error messages for agent operations
/// </summary>
public static class AgentErrorMessages
{
    public const string AgentNotFound = "Agent not found";
    public const string AgentAlreadyExists = "Agent already exists";
    public const string InvalidAgentState = "Invalid agent state";
    public const string ProcessTerminationFailed = "Process termination failed";
    public const string InvalidConfiguration = "Invalid configuration";
}

/// <summary>
/// Standard error messages for task operations
/// </summary>
public static class TaskErrorMessages
{
    public const string TaskNotFound = "Task not found";
    public const string TaskAlreadyCompleted = "Task already completed";
    public const string InvalidTaskState = "Invalid task state";
    public const string NoAvailableTasks = "No available tasks";
    public const string TaskAssignmentFailed = "Task assignment failed";
}