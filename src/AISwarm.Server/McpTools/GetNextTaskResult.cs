namespace AISwarm.Server.McpTools;

/// <summary>
/// Result of getting the next task via MCP tool
/// </summary>
public class GetNextTaskResult
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// ID of the retrieved task (null if no tasks available)
    /// </summary>
    public string? TaskId { get; set; }
    
    /// <summary>
    /// Persona content for the task (null if no tasks available)
    /// </summary>
    public string? Persona { get; set; }
    
    /// <summary>
    /// Description of the task (null if no tasks available)
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Message with reinforcing prompt to call the tool again
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Creates a failure result with error message
    /// </summary>
    public static GetNextTaskResult Failure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}