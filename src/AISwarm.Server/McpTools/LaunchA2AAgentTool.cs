using System.ComponentModel;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Models.A2A;
using AISwarm.Shared.Models;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class LaunchA2AAgentTool
{
    private readonly IA2AService _a2AService;
    private readonly IContextService _contextService;
    private readonly IAppLogger _logger;

    public LaunchA2AAgentTool(
        IA2AService a2AService,
        IContextService contextService,
        IAppLogger logger)
    {
        _a2AService = a2AService;
        _contextService = contextService;
        _logger = logger;
    }

    [McpServerTool(Name = "launch-a2a-agent")]
    [Description("Launch an A2A test agent with configurable parameters")]
    public async Task<LaunchA2AAgentResult> LaunchA2AAgentAsync(
        [Description("Name of the agent to launch (required)")]
        string? agentName,
        [Description("Optional description for the agent")]
        string? description,
        [Description("Optional port for the agent (auto-assign if not provided)")]
        int? port,
        [Description("Optional model for the agent (default 'gemini-2.5-flash')")]
        string? model,
        [Description("Optional persona type (default 'implementer' for A2A testing)")]
        string? persona)
    {
        try
        {
            // Resolve and validate persona
            var resolvedPersona = persona ?? "implementer"; // Default to 'implementer' for A2A testing
            if (!_contextService.IsValidAgentType(resolvedPersona))
            {
                var availablePersonas = string.Join(", ", _contextService.GetAvailableAgentTypes());
                return LaunchA2AAgentResult.Failure(
                    $"Invalid persona '{resolvedPersona}'. Available personas: {availablePersonas}");
            }

            // Generate persona description based on type
            var personaDescription = _contextService.GetPersonaPrompt(resolvedPersona);

            // Create A2A agent configuration
            var config = new A2AAgentConfig
            {
                AgentName = agentName ?? string.Empty,
                Description = description,
                Port = port,
                Model = model ?? "gemini-2.5-flash",
                Persona = resolvedPersona,
                PersonaDescription = personaDescription,
                WorkingDirectory = Environment.CurrentDirectory
            };

            // Launch the agent using A2AService
            var agentInstance = await _a2AService.LaunchAgentAsync(config);

            // Extract port from AgentUrl (e.g., "http://localhost:3001" -> 3001)
            var uri = new Uri(agentInstance.AgentUrl);
            var agentPort = uri.Port;

            return LaunchA2AAgentResult.CreateSuccessResult(
                agentUrl: agentInstance.AgentUrl,
                port: agentPort,
                agentName: agentInstance.AgentName,
                processId: agentInstance.ProcessId,
                status: agentInstance.Status.ToString());
        }
        catch (ArgumentException ex)
        {
            _logger.Error($"Invalid configuration for A2A agent: {ex.Message}");
            return LaunchA2AAgentResult.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception launching A2A agent: {ex.Message}");
            return LaunchA2AAgentResult.Failure($"Exception: {ex.Message}");
        }
    }
}

public class LaunchA2AAgentResult : Result<LaunchA2AAgentResult>
{
    public string? AgentUrl { get; init; }
    public int? Port { get; init; }
    public string? AgentName { get; init; }
    public int? ProcessId { get; init; }
    public string? Status { get; init; }

    public static LaunchA2AAgentResult CreateSuccessResult(
        string agentUrl,
        int port,
        string agentName,
        int processId,
        string status)
    {
        var result = CreateSuccess();
        return new LaunchA2AAgentResult
        {
            Success = result.Success,
            AgentUrl = agentUrl,
            Port = port,
            AgentName = agentName,
            ProcessId = processId,
            Status = status
        };
    }
}
