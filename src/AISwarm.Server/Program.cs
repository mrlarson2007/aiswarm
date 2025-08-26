using System.Net;
using System.Net.Sockets;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AISwarm.Server;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure console logging to stderr (MCP standard)
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Add database services
        ConfigureDatabaseServices(builder.Services, builder.Configuration);

        // Configure MCP server with BOTH stdio and HTTP transports
        builder.Services
            .AddInfrastructureServices(builder.Configuration)
            .AddMcpServer()
            .WithStdioServerTransport() // For VS Code
            .WithHttpTransport() // For Gemini CLI
            .WithToolsFromAssembly();

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            // Set a longer timeout for long polling requests
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
        });

        var app = builder.Build();


        // Map MCP HTTP endpoints
        app.MapMcp();
        var task = app.RunAsync();
        var url = app.Urls.FirstOrDefault();
        Console.WriteLine("AISwarm MCP Server starting with dual transport support:");
        Console.WriteLine("- Stdio transport for VS Code MCP integration");
        Console.WriteLine($"- HTTP transport for Gemini CLI at {url}");


        await task;
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

    private static int GetAvailablePort()
    {
        // Find an available port starting from 8081 (avoiding 8080 as requested)
        for (var port = 8081; port <= 9000; port++)
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch
            {
                // Port is in use, try next one
            }

        // Fallback to a default port if no available port found
        return 8081;
    }
}
