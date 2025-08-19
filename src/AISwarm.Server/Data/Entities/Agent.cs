using System.ComponentModel.DataAnnotations;

namespace AISwarm.Server.Data.Entities;

/// <summary>
/// Agent entity for database persistence
/// </summary>
public class Agent
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string PersonaId { get; set; } = string.Empty;
    
    public string? AssignedWorktree { get; set; }
    
    [Required]
    public string Status { get; set; } = "active";
    
    public DateTime RegisteredAt { get; set; }
    
    public DateTime LastHeartbeat { get; set; }
    
    public string? Metadata { get; set; }
}