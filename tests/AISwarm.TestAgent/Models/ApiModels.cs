namespace AISwarm.TestAgent.Models;

public class CreateTaskRequest
{
    public required string Description { get; set; }
    public required Dictionary<string, object> Input { get; set; }
}

public class ClaimTaskRequest
{
    public required string AgentId { get; set; }
}

public class CompleteTaskRequest
{
    public required Dictionary<string, object> Output { get; set; }
}