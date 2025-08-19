using Microsoft.EntityFrameworkCore;
using AISwarm.Server.Data.Entities;

namespace AISwarm.Server.Data;

/// <summary>
/// Database context for AISwarm coordination system
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

        // Agent configuration
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonaId).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasDefaultValue("active");
            entity.HasIndex(e => e.PersonaId);
            entity.HasIndex(e => e.AssignedWorktree);
            entity.HasIndex(e => e.LastHeartbeat);
        });
    }
}