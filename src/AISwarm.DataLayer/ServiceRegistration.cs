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
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();
        var workingDirectory = configuration?["WorkingDirectory"] ?? Environment.CurrentDirectory;
        var aiswarmDirectory = Path.Combine(workingDirectory, ".aiswarm");

        // Ensure .aiswarm directory exists
        Directory.CreateDirectory(aiswarmDirectory);

        var databasePath = Path.Combine(aiswarmDirectory, "aiswarm.db");
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}")
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.AmbientTransactionWarning)));

        // Initialize database after registration
        using var tempServiceProvider = services.BuildServiceProvider();
        using var scope = tempServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoordinationDbContext>();
        context.Database.EnsureCreated();

        return services;
    }
}
