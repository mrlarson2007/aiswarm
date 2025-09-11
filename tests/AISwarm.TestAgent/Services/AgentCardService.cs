using AISwarm.TestAgent.Models;

namespace AISwarm.TestAgent.Services;

public class AgentCardService : IAgentCardService
{
    private readonly string _agentName;
    private readonly string _description;
    private readonly string[] _skills;
    private readonly string[] _capabilities;
    private readonly string? _persona;
    private readonly string? _systemPrompt;
    private readonly DateTime _startedAt;

    public AgentCardService(
        string agentName, 
        string description, 
        string[]? skills = null,
        string[]? capabilities = null,
        string? persona = null,
        string? systemPrompt = null)
    {
        _agentName = agentName;
        _description = description;
        _skills = skills ?? new[] { "task-execution", "test-responses", "echo-service" };
        _capabilities = capabilities ?? new[] { "task-execution", "test-responses", "echo-service" };
        _persona = persona;
        _systemPrompt = systemPrompt;
        _startedAt = DateTime.UtcNow;
    }

    public AgentCard GetAgentCard(int port)
    {
        return new AgentCard
        {
            Name = "AISwarm Test Agent",
            Type = "test-agent",
            Version = "1.0.0",
            Capabilities = _capabilities, // Use configured capabilities
            Skills = _skills, // Customizable skills
            Endpoints = new AgentEndpoints
            {
                Tasks = "/tasks",
                TasksPending = "/tasks/pending",
                TaskClaim = "/tasks/{id}/claim",
                TaskComplete = "/tasks/{id}/complete",
                Health = "/health"
            },
            Metadata = new AgentMetadata
            {
                Persona = _persona ?? _agentName,  // Use persona if provided, fallback to agentName
                Description = _description,
                SystemPrompt = _systemPrompt,
                TestMode = true,
                ServerPort = port,
                StartedAt = _startedAt.ToString("o")
            }
        };
    }
}
