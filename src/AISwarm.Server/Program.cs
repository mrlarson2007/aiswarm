using AISwarm.DataLayer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AISwarm.Infrastructure;
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


        // Configure MCP server for agent coordination
        builder.Services
            .AddInfrastructureServices()
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
        Directory.CreateDirectory(aiswarmDirectory);
        services.AddDataLayerServices(configuration);
    }
}
