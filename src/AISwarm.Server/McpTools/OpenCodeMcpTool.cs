using System.ComponentModel;
using System.Text.Json;
using AISwarm.Infrastructure;
using AISwarm.Server.Delegation;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class OpenCodeMcpTool(
    IAgentService agent,
    DelegationService executor)
{
    [McpServerTool(Name = "list_models", ReadOnly = true)]
    [Description("Discover configured provider/model IDs. Listing does not prove a model is local or reachable.")]
    public async Task<AgentModels> ListModelsAsync(
        [Description("Existing absolute working directory used to resolve agent configuration.")]
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
            throw new ArgumentException("Working directory must be an existing absolute path.", nameof(workingDirectory));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await agent.ListModelsAsync(workingDirectory, timeout.Token);
    }

    [McpServerTool(Name = "delegate_task")]
    [Description("Execute instructions as an MCP SDK task. Requires the io.modelcontextprotocol/tasks extension and July 2026 or later protocol. Use tasks/get for results and tasks/cancel for cancellation. Host support, including Claude Desktop, must be verified; automatic resumption is not guaranteed.")]
    public async Task<CallToolResult> DelegateTaskAsync(
        [Description("Instructions for this task; no named personas.")]
        string instructions,
        [Description("Exact provider/model ID from list_models. No default or fallback model is selected.")]
        string model,
        [Description("Existing absolute working directory. Use a separate worktree for concurrent edits.")]
        string workingDirectory,
        [Description("Execution timeout in seconds, from 1 to 86400.")]
        int timeoutSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        var result = await executor.ExecuteAsync(
            instructions, model, workingDirectory, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        return new CallToolResult
        {
            IsError = result.IsError || result.ExitCode != 0,
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, JsonSerializerOptions.Web) }],
            StructuredContent = JsonSerializer.SerializeToElement(result, JsonSerializerOptions.Web)
        };
    }
}
