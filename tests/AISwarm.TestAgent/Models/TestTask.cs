namespace AISwarm.TestAgent.Models;

public class TestTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = "test-task";
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ClaimedBy { get; set; }
    public Dictionary<string, object> Input { get; set; } = new();
    public Dictionary<string, object> Output { get; set; } = new();
}
