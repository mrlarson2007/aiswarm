namespace AISwarm.TestAgent.Models;

public class AgentCard
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Version { get; set; }
    public required string[] Capabilities { get; set; }
    public required AgentEndpoints Endpoints { get; set; }
    public required AgentMetadata Metadata { get; set; }
}

public class AgentEndpoints
{
    public required string Tasks { get; set; }
    public required string TasksPending { get; set; }
    public required string TaskClaim { get; set; }
    public required string TaskComplete { get; set; }
    public required string Health { get; set; }
}

public class AgentMetadata
{
    public required string Persona { get; set; }
    public required string Description { get; set; }
    public bool TestMode { get; set; }
    public int ServerPort { get; set; }
    public required string StartedAt { get; set; }
}