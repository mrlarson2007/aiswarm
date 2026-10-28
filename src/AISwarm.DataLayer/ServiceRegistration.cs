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

        return services;
    }
}
