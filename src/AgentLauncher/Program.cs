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
        
        // Get available agent types dynamically from ContextManager
        var availableAgents = ContextManager.GetAvailableAgentTypes().ToArray();
        agentOption.FromAmong(availableAgents);

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

        // Create the root command
        var rootCommand = new RootCommand("AI Swarm Agent Launcher - Launch Gemini CLI agents with personas and worktrees");
        
        rootCommand.AddOption(agentOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(worktreeOption);
        rootCommand.AddOption(directoryOption);
        rootCommand.AddOption(listOption);
        rootCommand.AddOption(listWorktreesOption);

        // Set the handler for the root command
        rootCommand.SetHandler(async (agentType, model, worktree, directory, list, listWorktrees) =>
        {
            if (list)
            {
                ListAgentTypes();
                return;
            }

            if (listWorktrees)
            {
                await ListWorktrees();
                return;
            }

            if (string.IsNullOrEmpty(agentType))
            {
                Console.WriteLine("Error: Agent type is required. Use --agent or -a to specify.");
                Console.WriteLine("Use --list or -l to see available agent types.");
                return;
            }

            await LaunchAgent(agentType, model, worktree, directory);
        }, agentOption, modelOption, worktreeOption, directoryOption, listOption, listWorktreesOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static void ListAgentTypes()
    {
        Console.WriteLine("Available agent types:");
        Console.WriteLine();

        var sources = ContextManager.GetAgentTypeSources();
        foreach (var kvp in sources.OrderBy(x => x.Key))
        {
            var description = kvp.Key switch
            {
                "planner" => "Plans and breaks down tasks",
                "implementer" => "Implements code and features using TDD",
                "reviewer" => "Reviews and tests code",
                _ => "Custom agent type"
            };
            Console.WriteLine($"  {kvp.Key,-12} - {description} ({kvp.Value})");
        }

        Console.WriteLine();
        Console.WriteLine("Persona file locations (in priority order):");
        Console.WriteLine($"  1. Local project: {Path.Combine(Environment.CurrentDirectory, ".aiswarm/personas")}");
        
        var envPaths = Environment.GetEnvironmentVariable("AISWARM_PERSONAS_PATH");
        if (!string.IsNullOrEmpty(envPaths))
        {
            var paths = envPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < paths.Length; i++)
            {
                Console.WriteLine($"  {i + 2}. Environment: {paths[i]}");
            }
        }
        else
        {
            Console.WriteLine($"  2. Environment variable AISWARM_PERSONAS_PATH not set");
        }
        Console.WriteLine($"  3. Embedded: Built-in personas");
        Console.WriteLine();
        Console.WriteLine("To add custom personas:");
        Console.WriteLine($"  - Create .md files with '_prompt' suffix in {Path.Combine(Environment.CurrentDirectory, ".aiswarm/personas")}");
        Console.WriteLine($"  - Or set AISWARM_PERSONAS_PATH environment variable to additional directories");
        Console.WriteLine($"  - Example: custom_agent_prompt.md becomes 'custom_agent' type");        Console.WriteLine();
        Console.WriteLine("Workspace Options:");
        Console.WriteLine("  --worktree <name>   - Create a git worktree with specified name");
        Console.WriteLine("  (default)           - Work in current branch if no worktree specified");
        Console.WriteLine();
        Console.WriteLine("Models:");
        Console.WriteLine("  Any Gemini model name can be used (e.g., gemini-1.5-flash, gemini-1.5-pro, gemini-2.0-flash-exp)");
        Console.WriteLine("  Default: Uses Gemini CLI default if --model not specified");
        Console.WriteLine("  Future: Dynamic model discovery from Gemini CLI");
    }

    private static async Task ListWorktrees()
    {
        Console.WriteLine("Git Worktrees:");
        Console.WriteLine();

        try
        {
            // Check if we're in a git repository
            if (!await GitManager.IsGitRepositoryAsync())
            {
                Console.WriteLine("  Not in a git repository.");
                Console.WriteLine("  Worktrees can only be listed from within a git repository.");
                return;
            }

            // Get existing worktrees
            var worktrees = await GitManager.GetExistingWorktreesAsync();
            
            if (worktrees.Count == 0)
            {
                Console.WriteLine("  No worktrees found.");
                Console.WriteLine("  Use --worktree <name> to create a new worktree when launching an agent.");
                return;
            }

            foreach (var kvp in worktrees.OrderBy(x => x.Key))
            {
                Console.WriteLine($"  {kvp.Key,-20} → {kvp.Value}");
            }

            Console.WriteLine();
            Console.WriteLine("To create a new worktree:");
            Console.WriteLine("  aiswarm --agent <type> --worktree <name>");
            Console.WriteLine();
            Console.WriteLine("To remove a worktree:");
            Console.WriteLine("  git worktree remove <path>");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing worktrees: {ex.Message}");
        }
    }

    private static async Task LaunchAgent(string agentType, string? model, string? worktree, string? directory)
    {
        Console.WriteLine($"Launching {agentType} agent...");
        Console.WriteLine($"Model: {model ?? "Gemini CLI default"}");
        
        try
        {
            string workingDirectory;
            
            // Handle worktree creation if specified
            if (!string.IsNullOrEmpty(worktree))
            {
                Console.WriteLine($"Creating worktree: {worktree}");
                
                // Validate worktree name
                if (!GitManager.IsValidWorktreeName(worktree))
                {
                    Console.WriteLine($"Error: Invalid worktree name '{worktree}'. Use only letters, numbers, hyphens, and underscores.");
                    return;
                }
                
                // Check if we're in a git repository
                if (!await GitManager.IsGitRepositoryAsync())
                {
                    Console.WriteLine("Error: Not in a git repository. Worktrees can only be created within git repositories.");
                    Console.WriteLine("Either run from a git repository or omit the --worktree option to work in the current directory.");
                    return;
                }
                
                // Create the worktree
                workingDirectory = await GitManager.CreateWorktreeAsync(worktree);
                Console.WriteLine($"Worktree created at: {workingDirectory}");
            }
            else
            {
                // Use specified directory or current directory
                workingDirectory = directory ?? Environment.CurrentDirectory;
                Console.WriteLine("Workspace: Current branch (no worktree)");
            }
            
            Console.WriteLine($"Working directory: {workingDirectory}");
            
            // Create context file
            Console.WriteLine("Creating context file...");
            var contextFilePath = await ContextManager.CreateContextFile(agentType, workingDirectory);
            Console.WriteLine($"Context file created: {contextFilePath}");
            
            // TODO: Phase 5 - Implement Gemini CLI integration
            Console.WriteLine("Ready to launch Gemini CLI agent (integration coming in next phase)...");
            Console.WriteLine($"Next: gemini -m {model ?? "[default]"} -i \"{contextFilePath}\"");
            
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            Console.WriteLine("Please check your git configuration and try again.");
        }
    }
}
