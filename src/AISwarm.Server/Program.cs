// AISwarm.Server - stdio MCP server for OpenCode delegation and shared memory.
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Server.Delegation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        // Keep stdout exclusively for the Claude Desktop MCP transport.
        builder.Services
            .AddInfrastructureServices(builder.Configuration)
            .AddOpenCodeDelegation(builder.Configuration)
#pragma warning disable MCP9005 // Desktop lifecycle diagnostics; automatic resumption is not guaranteed.
            .AddMcpServer(options => options.Capabilities = new() { Logging = new() })
#pragma warning restore MCP9005
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly();

        var app = builder.Build();

        await app.RunAsync();
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
