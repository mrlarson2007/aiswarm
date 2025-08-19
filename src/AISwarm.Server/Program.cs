using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AISwarm.Shared.Contracts;
using AISwarm.Server.Services;

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

        // Register time service for deterministic time control
        builder.Services.AddSingleton<ITimeService, SystemTimeService>();

        // Configure MCP server for agent coordination
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport();

        await builder.Build().RunAsync();
    }
}