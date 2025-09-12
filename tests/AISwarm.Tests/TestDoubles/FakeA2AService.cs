using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Models.A2A;

namespace AISwarm.Tests.TestDoubles;

/// <summary>
/// Test double for IA2AService that simulates agent launching behavior for testing.
/// </summary>
public class FakeA2AService : IA2AService
{
    public List<A2AAgentConfig> LaunchedConfigs { get; } = new();
    public List<A2AAgentInstance> LaunchedAgents { get; } = new();
    
    public bool ShouldThrowException { get; set; }
    public string? ExceptionMessage { get; set; }
    
    public A2AAgentInstance? NextAgentInstance { get; set; }

    public Task<A2AAgentInstance> LaunchAgentAsync(A2AAgentConfig config)
    {
        // Simulate the same validation as real A2AService (before recording config)
        if (string.IsNullOrEmpty(config.AgentName))
        {
            throw new ArgumentException("Agent name is required.", nameof(config.AgentName));
        }
        
        LaunchedConfigs.Add(config);
        
        if (ShouldThrowException)
        {
            throw new InvalidOperationException(ExceptionMessage ?? "Simulated exception");
        }

        var agentInstance = NextAgentInstance ?? new A2AAgentInstance
        {
            AgentId = Guid.NewGuid().ToString(),
            AgentName = config.AgentName,
            AgentUrl = $"http://localhost:{config.Port ?? 3001}",
            ProcessId = 12345,
            Status = A2AAgentStatus.Starting,
            Capabilities = config.Capabilities.ToArray(),
            Description = config.Description,
            LaunchedAt = DateTime.UtcNow
        };
        
        LaunchedAgents.Add(agentInstance);
        return Task.FromResult(agentInstance);
    }
}