using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // Start event logging service
        var eventLogger = app.Services.GetRequiredService<IEventLoggerService>();
        await eventLogger.StartAsync();

        // Register shutdown handling for graceful event logger cleanup
        var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        appLifetime.ApplicationStopping.Register(() =>
        {
            // Note: We can't await here, but StopAsync should handle cleanup synchronously if needed
            _ = eventLogger.StopAsync();
        });

        // Map MCP HTTP endpoints
        app.MapMcp();
        var task = app.RunAsync();
        var url = app.Urls.FirstOrDefault();
        Console.WriteLine("AISwarm MCP Server starting with dual transport support:");
        Console.WriteLine("- Stdio transport for VS Code MCP integration");
        Console.WriteLine($"- HTTP transport for Gemini CLI at {url}");
        Console.WriteLine("- Event logging service started for audit trail");

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
}
