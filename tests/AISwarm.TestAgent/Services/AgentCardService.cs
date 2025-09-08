using AISwarm.TestAgent.Models;

namespace AISwarm.TestAgent.Services;

public class AgentCardService : IAgentCardService
{
    private readonly string _agentName;
    private readonly string _description;
    private readonly DateTime _startedAt;

    public AgentCardService(string agentName, string description)
    {
        _agentName = agentName;
        _description = description;
        _startedAt = DateTime.UtcNow;
    }

    public AgentCard GetAgentCard(int port)
    {
        return new AgentCard
        {
            Name = "AISwarm Test Agent",
            Type = "test-agent",
            Version = "1.0.0",
            Capabilities = new[] { "task-execution", "test-responses", "echo-service" },
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
                Persona = _agentName,  // Use agentName instead of persona for compatibility
                Description = _description,
                TestMode = true,
                ServerPort = port,
                StartedAt = _startedAt.ToString("o")
            }
        };
    }
}
