using System.CommandLine;
using AgentLauncher.Commands;
using AISwarm.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLauncher.Services;

public static class CommandFactory
{
    public static RootCommand CreateRootCommand(ServiceProvider serviceProvider)
    {
        var agentOption = new Option<string?>(
            "--agent",
            "The type of agent to launch") { IsRequired = false };
        agentOption.AddAlias("-a");

        var contextService = serviceProvider.GetRequiredService<IContextService>();
        var availableAgents = contextService.GetAvailableAgentTypes().ToArray();
        if (availableAgents.Length > 0)
            agentOption.FromAmong(availableAgents);

        var modelOption = new Option<string?>(
            "--model",
            "The Gemini model to use (uses Gemini CLI default if not specified)") { IsRequired = false };
        modelOption.AddAlias("-m");

        var worktreeOption = new Option<string?>(
            "--worktree",
            "Create a git worktree with the specified name") { IsRequired = false };
        worktreeOption.AddAlias("-w");

        var directoryOption = new Option<string?>(
            "--directory",
            "The working directory for the agent (defaults to current directory)") { IsRequired = false };
        directoryOption.AddAlias("-d");

        var listOption = new Option<bool>(
            "--list",
            "List available agent types") { IsRequired = false };
        listOption.AddAlias("-l");

        var listWorktreesOption = new Option<bool>(
            "--list-worktrees",
            "List existing git worktrees") { IsRequired = false };

        var dryRunOption = new Option<bool>(
            "--dry-run",
            "Create context file but don't launch Gemini CLI") { IsRequired = false };

        var initOption = new Option<bool>(
            "--init",
            "Initialize .aiswarm directory with template persona files") { IsRequired = false };

        var monitorOption = new Option<bool>(
            "--monitor",
            "Register agent in database and monitor its lifecycle") { IsRequired = false };

        var rootCommand =
            new RootCommand("AI Swarm Agent Launcher - Launch Gemini CLI agents with personas and worktrees");

        rootCommand.AddOption(agentOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(worktreeOption);
        rootCommand.AddOption(directoryOption);
        rootCommand.AddOption(listOption);
        rootCommand.AddOption(listWorktreesOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(initOption);
        rootCommand.AddOption(monitorOption);

        rootCommand.SetHandler(async context =>
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
                var listAgents = serviceProvider.GetRequiredService<ListAgentsCommandHandler>();
                listAgents.Run();
                return;
            }

            if (listWorktrees)
            {
                var listWorktreesHandler = serviceProvider.GetRequiredService<ListWorktreesCommandHandler>();
                await listWorktreesHandler.RunAsync();
                return;
            }

            if (init)
            {
                var initHandler = serviceProvider.GetRequiredService<InitCommandHandler>();
                var initOk = await initHandler.RunAsync();
                if (!initOk)
                    Console.WriteLine("Initialization failed.");
                return;
            }

            if (string.IsNullOrEmpty(agentType))
            {
                Console.WriteLine("Error: Agent type is required. Use --agent or -a to specify.");
                Console.WriteLine("Use --list or -l to see available agent types.");
                return;
            }

            var launcher = serviceProvider.GetRequiredService<LaunchAgentCommandHandler>();
            var ok = await launcher.RunAsync(agentType, model, worktree, directory, dryRun, monitor);
            if (!ok)
                Console.WriteLine("Launch failed.");
        });

        return rootCommand;
    }
}
