using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AISwarm.Server.Services;
using AISwarm.DataLayer.Services;
using AISwarm.DataLayer.Database;
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
        builder.Services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: "AISwarmCoordination"));
        
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
}