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

        // Add MCP Server - implementation will come via TDD
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport();

        await builder.Build().RunAsync();
    }
}