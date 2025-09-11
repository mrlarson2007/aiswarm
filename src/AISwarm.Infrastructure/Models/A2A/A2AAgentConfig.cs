
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
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Validates the configuration and throws ArgumentException for invalid values.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(AgentName))
        {
            throw new ArgumentException("Agent name must be specified.", nameof(AgentName));
        }

        if (string.IsNullOrEmpty(Persona))
        {
            throw new ArgumentException("Agent persona must be specified.", nameof(Persona));
        }

        if (string.IsNullOrEmpty(PersonaDescription))
        {
            throw new ArgumentException("Agent persona description must be specified.", nameof(PersonaDescription));
        }

        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            throw new ArgumentException("Working directory must be specified.", nameof(WorkingDirectory));
        }
    }
}
