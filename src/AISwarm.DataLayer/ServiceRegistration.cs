using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AISwarm.DataLayer;

public static class ServiceRegistration
{
    public static IServiceCollection AddDataLayerServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        var workingDirectory = configuration?["WorkingDirectory"] ?? Environment.CurrentDirectory;
        var aiswarmDirectory = Path.Combine(workingDirectory, ".aiswarm");

        // Ensure .aiswarm directory exists
        Directory.CreateDirectory(aiswarmDirectory);

        var databasePath = Path.Combine(aiswarmDirectory, "aiswarm.db");
        services.AddDbContextFactory<CoordinationDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Cache=Shared")
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.AmbientTransactionWarning)));

        // Register scope service as scoped for per-request transaction coordination
        services.AddScoped<IDatabaseScopeService>(sp =>
            new DatabaseScopeService(sp.GetRequiredService<IDbContextFactory<CoordinationDbContext>>()));

        // Initialize database after registration
        using var tempServiceProvider = services.BuildServiceProvider();
        using var scope = tempServiceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoordinationDbContext>>();
        using var context = factory.CreateDbContext();
        context.Database.EnsureCreated();

        // Enable WAL mode for better concurrency
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

        // Lightweight SQLite schema upgrade: add missing Tasks.PersonaId if upgrading from older DB
        try
        {
            var provider = context.Database.ProviderName ?? string.Empty;
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Tasks')";
                using var reader = cmd.ExecuteReader();
                var hasPersonaId = false;
                while (reader.Read())
                {
                    // PRAGMA table_info columns: cid(0), name(1), type(2), ...
                    var name = reader.GetString(1);
                    if (string.Equals(name, "PersonaId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPersonaId = true;
                        break;
                    }
                }

                reader.Close();
                if (!hasPersonaId)
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = "ALTER TABLE Tasks ADD COLUMN PersonaId TEXT NULL";
                    alter.ExecuteNonQuery();
                }
            }
        }
        catch (DbException)
        {
            // Best-effort upgrade; ignore if unavailable.
        }

        return services;
    }
}
