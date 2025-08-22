using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AISwarm.Server.Services;
using AISwarm.DataLayer.Services;
using AISwarm.DataLayer.Database;
using AISwarm.Shared.Contracts;
using AISwarm.DataLayer.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Server;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        
        // Configure console logging to stderr (MCP standard)
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Add database services
        ConfigureDatabaseServices(builder.Services, builder.Configuration);
        
        // Register core services
        builder.Services.AddSingleton<ITimeService, SystemTimeService>();
        builder.Services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        // Configure MCP server for agent coordination
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }

    private static void ConfigureDatabaseServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var workingDirectory = configuration["WorkingDirectory"] ?? Environment.CurrentDirectory;
        var aiswarmDirectory = Path.Combine(workingDirectory, ".aiswarm");
        var databasePath = Path.Combine(aiswarmDirectory, "coordination.db");
        
        // Ensure directory exists
        Directory.CreateDirectory(aiswarmDirectory);
        
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
    }
}