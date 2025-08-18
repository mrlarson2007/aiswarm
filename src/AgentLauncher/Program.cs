using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using AgentLauncher.Services;

namespace AgentLauncher;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Build DI container (services used after refactor of static managers)
        var services = new ServiceCollection();
        services.AddAgentLauncherServices();
        var serviceProvider = services.BuildServiceProvider();

        // Define the agent type option
        var agentOption = new Option<string?>(
            name: "--agent",
            description: "The type of agent to launch")
        {
            IsRequired = false
        };
        agentOption.AddAlias("-a");

        // Get available agent types dynamically via context service
        var contextService = serviceProvider.GetRequiredService<IContextService>();
        var availableAgents = contextService.GetAvailableAgentTypes().ToArray();
        if (availableAgents.Length > 0)
        {
            agentOption.FromAmong(availableAgents);
        }

        // Define the model option
        var modelOption = new Option<string?>(
            name: "--model",
            description: "The Gemini model to use (uses Gemini CLI default if not specified)")
        {
            IsRequired = false
        };
        modelOption.AddAlias("-m");

        // Define the worktree option
        var worktreeOption = new Option<string?>(
            name: "--worktree",
            description: "Create a git worktree with the specified name")
        {
            IsRequired = false
        };
        worktreeOption.AddAlias("-w");

        // Define the directory option
        var directoryOption = new Option<string?>(
            name: "--directory",
            description: "The working directory for the agent (defaults to current directory)")
        {
            IsRequired = false
        };
        directoryOption.AddAlias("-d");

        // Define the list agents option
        var listOption = new Option<bool>(
            name: "--list",
            description: "List available agent types")
        {
            IsRequired = false
        };
        listOption.AddAlias("-l");

        // Define the list worktrees option
        var listWorktreesOption = new Option<bool>(
            name: "--list-worktrees",
            description: "List existing git worktrees")
        {
            IsRequired = false
        };

        // Define the dry run option
        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Create context file but don't launch Gemini CLI")
        {
            IsRequired = false
        };

        // Create the root command
        var rootCommand = new RootCommand("AI Swarm Agent Launcher - Launch Gemini CLI agents with personas and worktrees");

        rootCommand.AddOption(agentOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(worktreeOption);
        rootCommand.AddOption(directoryOption);
        rootCommand.AddOption(listOption);
        rootCommand.AddOption(listWorktreesOption);
        rootCommand.AddOption(dryRunOption);

        // Set the handler for the root command
        rootCommand.SetHandler(async (agentType, model, worktree, directory, list, listWorktrees, dryRun) =>
        {
            if (list)
            {
                var listAgents = serviceProvider.GetRequiredService<AgentLauncher.Commands.ListAgentsCommandHandler>();
                listAgents.Run();
                return;
            }

            if (listWorktrees)
            {
                var listWorktreesHandler = serviceProvider.GetRequiredService<AgentLauncher.Commands.ListWorktreesCommandHandler>();
                await listWorktreesHandler.RunAsync();
                return;
            }

            if (string.IsNullOrEmpty(agentType))
            {
                Console.WriteLine("Error: Agent type is required. Use --agent or -a to specify.");
                Console.WriteLine("Use --list or -l to see available agent types.");
                return;
            }

            var launcher = serviceProvider.GetRequiredService<AgentLauncher.Commands.LaunchAgentCommandHandler>();
            var ok = await launcher.RunAsync(agentType, model, worktree, directory, dryRun);
            if (!ok)
            {
                Console.WriteLine("Launch failed.");
            }
        }, agentOption, modelOption, worktreeOption, directoryOption, listOption, listWorktreesOption, dryRunOption);

        return await rootCommand.InvokeAsync(args);
    }

    // ListAgentTypes and ListWorktrees moved into dedicated command handlers

    // LaunchAgent logic moved into LaunchAgentCommandHandler
}
