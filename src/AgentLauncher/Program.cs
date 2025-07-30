using System.CommandLine;

namespace AgentLauncher;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Define the agent type option
        var agentOption = new Option<string?>(
            name: "--agent",
            description: "The type of agent to launch")
        {
            IsRequired = false
        };
        agentOption.AddAlias("-a");
        agentOption.FromAmong("planner", "implementer", "reviewer");

        // Define the model option
        var modelOption = new Option<string>(
            name: "--model",
            description: "The Gemini model to use",
            getDefaultValue: () => "gemini-1.5-flash")
        {
            IsRequired = false
        };
        modelOption.AddAlias("-m");

        // Define the worktree option
        var worktreeOption = new Option<string?>(
            name: "--worktree",
            description: "The name for the git worktree/branch (auto-generated if not specified)")
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

        // Create the root command
        var rootCommand = new RootCommand("AI Swarm Agent Launcher - Launch Gemini CLI agents with personas and worktrees");
        
        rootCommand.AddOption(agentOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(worktreeOption);
        rootCommand.AddOption(directoryOption);
        rootCommand.AddOption(listOption);

        // Set the handler for the root command
        rootCommand.SetHandler(async (agentType, model, worktree, directory, list) =>
        {
            if (list)
            {
                ListAgentTypes();
                return;
            }

            if (string.IsNullOrEmpty(agentType))
            {
                Console.WriteLine("Error: Agent type is required. Use --agent or -a to specify.");
                Console.WriteLine("Use --list or -l to see available agent types.");
                return;
            }

            await LaunchAgent(agentType, model, worktree, directory);
        }, agentOption, modelOption, worktreeOption, directoryOption, listOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static void ListAgentTypes()
    {
        Console.WriteLine("Available agent types:");
        Console.WriteLine("  planner     - Plans and breaks down tasks (stays on main/master branch)");
        Console.WriteLine("  implementer - Implements code and features (creates worktree)");
        Console.WriteLine("  reviewer    - Reviews and tests code (creates worktree)");
        Console.WriteLine();
        Console.WriteLine("Models:");
        Console.WriteLine("  Any Gemini model name can be used (e.g., gemini-1.5-flash, gemini-1.5-pro, gemini-2.0-flash-exp)");
        Console.WriteLine("  Default: gemini-1.5-flash");
        Console.WriteLine("  Future: Dynamic model discovery from Gemini CLI");
    }

    private static async Task LaunchAgent(string agentType, string model, string? worktree, string? directory)
    {
        Console.WriteLine($"Launching {agentType} agent...");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Worktree: {worktree ?? "auto-generated"}");
        Console.WriteLine($"Directory: {directory ?? Environment.CurrentDirectory}");
        
        // TODO: Phase 3 - Implement context file management
        // TODO: Phase 4 - Implement worktree creation
        // TODO: Phase 5 - Implement Gemini CLI integration
        
        Console.WriteLine("Implementation coming in next phases...");
        
        await Task.CompletedTask;
    }
}
