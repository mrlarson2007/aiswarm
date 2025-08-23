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

        // Define the init option
        var initOption = new Option<bool>(
            name: "--init",
            description: "Initialize .aiswarm directory with template persona files")
        {
            IsRequired = false
        };

        // Define the monitor option
        var monitorOption = new Option<bool>(
            name: "--monitor",
            description: "Register agent in database and monitor its lifecycle")
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
        rootCommand.AddOption(initOption);
        rootCommand.AddOption(monitorOption);

        // Set the handler for the root command
        rootCommand.SetHandler(async (context) =>
        {
            var agentType = context.ParseResult.GetValueForOption(agentOption);
            var model = context.ParseResult.GetValueForOption(modelOption);
            var worktree = context.ParseResult.GetValueForOption(worktreeOption);
            var directory = context.ParseResult.GetValueForOption(directoryOption);
            var list = context.ParseResult.GetValueForOption(listOption);
            var listWorktrees = context.ParseResult.GetValueForOption(listWorktreesOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var init = context.ParseResult.GetValueForOption(initOption);
            var monitor = context.ParseResult.GetValueForOption(monitorOption);

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

            if (init)
            {
                var initHandler = serviceProvider.GetRequiredService<AgentLauncher.Commands.InitCommandHandler>();
                var initOk = await initHandler.RunAsync();
                if (!initOk)
                {
                    Console.WriteLine("Initialization failed.");
                }
                return;
            }

            if (string.IsNullOrEmpty(agentType))
            {
                Console.WriteLine("Error: Agent type is required. Use --agent or -a to specify.");
                Console.WriteLine("Use --list or -l to see available agent types.");
                return;
            }

            var launcher = serviceProvider.GetRequiredService<AgentLauncher.Commands.LaunchAgentCommandHandler>();
            var ok = await launcher.RunAsync(agentType, model, worktree, directory, dryRun, monitor);
            if (!ok)
            {
                Console.WriteLine("Launch failed.");
            }
        });

        return await rootCommand.InvokeAsync(args);
    }

    // ListAgentTypes and ListWorktrees moved into dedicated command handlers

    // LaunchAgent logic moved into LaunchAgentCommandHandler
}
