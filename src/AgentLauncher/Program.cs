using System.CommandLine;
using AgentLauncher.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLauncher;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Build DI container (services used after refactor of static managers)
        var services = new ServiceCollection();
        services.AddAgentLauncherServices();
        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = CommandFactory.CreateRootCommand(serviceProvider);
        return await rootCommand.InvokeAsync(args);
    }

    // ListAgentTypes and ListWorktrees moved into dedicated command handlers

    // LaunchAgent logic moved into LaunchAgentCommandHandler
}
