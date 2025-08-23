using Microsoft.EntityFrameworkCore;
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
        var databasePath = Path.Combine(aiswarmDirectory, "aiswarm.db");
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}")
                .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.AmbientTransactionWarning)));

        return services;
    }
}
