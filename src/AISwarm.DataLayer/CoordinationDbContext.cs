using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.DataLayer;

/// <summary>
///     Database context for agent coordination and task management
/// </summary>
public class CoordinationDbContext(
    DbContextOptions<CoordinationDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents
    {
        get;
        set;
    } = null!;

    public DbSet<WorkItem> Tasks
    {
        get;
        set;
    } = null!;

    public DbSet<MemoryEntry> MemoryEntries
    {
        get;
        set;
    } = null!;

    public DbSet<EventLog> EventLogs
    {
        get;
        set;
    } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Agent entity
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.PersonaId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.WorkingDirectory).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.WorktreeName).HasMaxLength(100);
            entity.Property(e => e.AssignedWorktree).HasMaxLength(500);
            entity.Property(e => e.ProcessId).HasMaxLength(20);
        });

        // Configure WorkItem entity
        modelBuilder.Entity<WorkItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.AgentId).HasMaxLength(50).IsRequired(false);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.PersonaId).HasMaxLength(50).IsRequired(false);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Priority).HasConversion<string>().HasDefaultValue(TaskPriority.Normal);
            entity.Property(e => e.Result);
        });

        // Configure MemoryEntry entity
        modelBuilder.Entity<MemoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Namespace).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Key).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50).HasDefaultValue("json");
            entity.Property(e => e.Metadata);
            entity.HasIndex(e => new { e.Namespace, e.Key }).IsUnique();
        });

        // Configure EventLog entity
        modelBuilder.Entity<EventLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Actor).HasMaxLength(200);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.Payload);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.Severity).HasMaxLength(20).HasDefaultValue("Information");
            entity.Property(e => e.Tags).HasMaxLength(500);

            // Indexes for efficient querying
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EntityId);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.Actor);
        });
    }
}
