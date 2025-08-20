using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.DataLayer.Database;

/// <summary>
/// Database context for agent coordination and task management
/// </summary>
public class CoordinationDbContext : DbContext
{
    public CoordinationDbContext(DbContextOptions<CoordinationDbContext> options) : base(options)
    {
    }

    public DbSet<Agent> Agents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Agent entity
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.PersonaId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AgentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.WorkingDirectory).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.WorktreeName).HasMaxLength(100);
            entity.Property(e => e.AssignedWorktree).HasMaxLength(500);
            entity.Property(e => e.ProcessId).HasMaxLength(20);
        });
    }
}