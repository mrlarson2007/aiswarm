namespace AgentLauncher.Models;

/// <summary>
/// Configuration settings for an agent, including agent ID and MCP server connection details
/// </summary>
public class AgentSettings
{
    /// <summary>
    /// Unique identifier for the agent
    /// </summary>
    public required string AgentId
    {
        get; init;
    }

    /// <summary>
    /// URL for the MCP server that the agent will communicate with
    /// </summary>
    public required string McpServerUrl
    {
        get; init;
    }

    /// <summary>
    /// Additional configuration options for the agent
    /// </summary>
    public Dictionary<string, object>? AdditionalConfig
    {
        get; init;
    }
}
