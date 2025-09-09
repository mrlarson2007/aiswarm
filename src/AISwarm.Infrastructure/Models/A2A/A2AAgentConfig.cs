namespace AISwarm.Infrastructure.Models.A2A;

public class A2AAgentConfig
{
    public string AgentName { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
}
