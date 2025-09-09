using System.ComponentModel;
using AISwarm.Infrastructure;
using AISwarm.Shared.Models;
using ModelContextProtocol.Server;
using System.IO;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class LaunchA2AAgentTool
{
    private readonly IProcessLauncher _processLauncher;
    private readonly IAppLogger _logger;
    private readonly IFileSystemService _fileSystemService;

    public LaunchA2AAgentTool(
        IProcessLauncher processLauncher,
        IAppLogger logger,
        IFileSystemService fileSystemService)
    {
        _processLauncher = processLauncher;
        _logger = logger;
        _fileSystemService = fileSystemService;
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
        [Description("Optional model for the agent (default 'gemini-1.5-flash')")]
        string? model)
    {
        // Validate agent name
        if (string.IsNullOrEmpty(agentName))
        {
            return LaunchA2AAgentResult.Failure("Agent name is required.");
        }

        // Validate executable path
        var executablePath = Path.Combine("tools-packages", "AISwarm.TestAgent.exe");
        if (!_fileSystemService.FileExists(executablePath))
        {
            _logger.Error($"Executable not found: {executablePath}");
            return LaunchA2AAgentResult.Failure($"AISwarm.TestAgent.exe not found at {executablePath}");
        }

        // Validate port number if provided
        if (port.HasValue && (port.Value < 0 || port.Value > 65535))
        {
            return LaunchA2AAgentResult.Failure($"Invalid port number: {port.Value}. Port must be between 0 and 65535.");
        }

        return LaunchA2AAgentResult.Failure("Not implemented.");
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
