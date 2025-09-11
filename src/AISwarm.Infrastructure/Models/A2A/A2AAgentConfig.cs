
namespace AISwarm.Infrastructure.Models.A2A;

public class A2AAgentConfig
{
    public string AgentName { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string Persona { get; set; } = string.Empty;
    public string PersonaDescription { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public List<string> Capabilities { get; set; } = new();
    public string WorkingDirectory
    {
        get;
        set;
    }
}
