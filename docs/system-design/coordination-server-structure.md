# AISwarm.Server Project Structure

## Overview

The AISwarm.Server is an MCP (Model Context Protocol) server that coordinates multiple AI agents using the official `google-gemini/gemini-cli` tool. This design leverages standard MCP patterns instead of complex custom implementations.

## Architecture Integration

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   AgentLauncher │    │  gemini-cli     │    │ AISwarm.Server  │
│                 │───▶│                 │◄──▶│                 │
│ (Process Mgmt)  │    │ (MCP Client)    │    │ (MCP Server)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                ▲                       ▲
                                │                       │
                                ▼                       ▼
                         Gemini LLM              Custom MCP Tools
                        (via @aiswarm)          (@aiswarm commands)
```

**Key Integration Points:**

- **AgentLauncher**: Manages gemini-cli processes and configuration
- **gemini-cli**: Official tool with native MCP client support and advanced hooks
- **AISwarm.Server**: Standard MCP server exposing coordination tools
- **Configuration**: Via `~/.gemini/settings.json` per agent instance
- **Plugin Event Hooks**: beforeRequest, afterResponse, configChanged for advanced coordination
- **IDE Integration**: VS Code workspace-aware events and commands
- **Extended Tools API**: Built-in filesystem, shell, HTTP, Google Search capabilities

## Advanced Integration Patterns

### Plugin Event Hooks Implementation

The coordination system leverages gemini-cli's plugin event hooks for sophisticated agent coordination:

#### beforeRequest Hook Integration
```csharp
// Services/RequestInterceptorService.cs
public class RequestInterceptorService : IRequestInterceptorService
{
    private readonly IAgentContextService _agentContext;
    private readonly ICoordinationStateService _coordinationState;

    public async Task<RequestModification> InterceptRequestAsync(ApiRequest request)
    {
        // Inject agent coordination context into every request
        var agentId = _agentContext.GetCurrentAgentId();
        var coordinationState = await _coordinationState.GetAgentStateAsync(agentId);
        
        // Add coordination metadata to request
        request.Metadata["agent_id"] = agentId;
        request.Metadata["coordination_state"] = coordinationState.Status;
        request.Metadata["current_task"] = coordinationState.CurrentTaskId;
        
        // Inject active task context if available
        if (coordinationState.CurrentTaskId.HasValue)
        {
            var taskContext = await _coordinationState.GetTaskContextAsync(
                coordinationState.CurrentTaskId.Value);
            request.Context = $"{request.Context}\n\nCurrent Task: {taskContext}";
        }
        
        return new RequestModification { ModifiedRequest = request };
    }
}
```

#### afterResponse Hook Integration
```csharp
// Services/ResponseAnalyzerService.cs
public class ResponseAnalyzerService : IResponseAnalyzerService
{
    private readonly ICoordinationEventService _eventService;
    private readonly ITaskProgressService _progressService;

    public async Task AnalyzeResponseAsync(ApiResponse response)
    {
        // Extract coordination signals from Gemini responses
        var coordinationSignals = ExtractCoordinationSignals(response.Text);
        
        foreach (var signal in coordinationSignals)
        {
            switch (signal.Type)
            {
                case "task_completed":
                    await _progressService.MarkTaskCompletedAsync(signal.TaskId, signal.Result);
                    break;
                case "help_needed":
                    await _eventService.BroadcastHelpRequestAsync(signal.AgentId, signal.Details);
                    break;
                case "progress_update":
                    await _progressService.UpdateProgressAsync(signal.TaskId, signal.Progress);
                    break;
            }
        }
    }

    private IEnumerable<CoordinationSignal> ExtractCoordinationSignals(string responseText)
    {
        // Parse response for coordination indicators
        // Look for patterns like:
        // "I need help with..." -> help_needed signal
        // "Task completed: ..." -> task_completed signal
        // "Progress: 75%" -> progress_update signal
        
        var signals = new List<CoordinationSignal>();
        
        // Pattern matching logic here
        if (responseText.Contains("task completed", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add(new CoordinationSignal 
            { 
                Type = "task_completed",
                // Extract task details
            });
        }
        
        return signals;
    }
}
```

#### configChanged Hook Integration
```csharp
// Services/ConfigurationWatcherService.cs
public class ConfigurationWatcherService : IConfigurationWatcherService
{
    private readonly IAgentReconfigurationService _reconfigService;
    private readonly ILogger<ConfigurationWatcherService> _logger;

    public async Task OnConfigurationChangedAsync(ConfigurationChangedEvent configEvent)
    {
        _logger.LogInformation("Configuration changed: {ConfigPath}", configEvent.ConfigPath);
        
        // Handle dynamic reconfiguration
        switch (configEvent.Section)
        {
            case "coordination":
                await _reconfigService.UpdateCoordinationSettingsAsync(configEvent.NewValues);
                break;
            case "agents":
                await _reconfigService.UpdateAgentSettingsAsync(configEvent.NewValues);
                break;
            case "tasks":
                await _reconfigService.UpdateTaskSettingsAsync(configEvent.NewValues);
                break;
        }
        
        // Broadcast configuration changes to other agents if needed
        await _reconfigService.NotifyOtherAgentsAsync(configEvent);
    }
}
```

### VS Code Integration Patterns

#### Workspace-Aware Agent Context
```csharp
// Services/WorkspaceContextService.cs
public class WorkspaceContextService : IWorkspaceContextService
{
    public async Task<WorkspaceContext> GetWorkspaceContextAsync()
    {
        // Leverage VS Code integration for workspace awareness
        return new WorkspaceContext
        {
            RecentFiles = await GetRecentFilesAsync(),
            CurrentSelection = await GetCurrentSelectionAsync(),
            CursorPosition = await GetCursorPositionAsync(),
            OpenTabs = await GetOpenTabsAsync(),
            GitBranch = await GetCurrentGitBranchAsync(),
            WorkspaceRoot = await GetWorkspaceRootAsync()
        };
    }

    public async Task<string> GetContextualPromptAsync(string basePrompt)
    {
        var context = await GetWorkspaceContextAsync();
        
        return $@"{basePrompt}

WORKSPACE CONTEXT:
- Current Branch: {context.GitBranch}
- Recent Files: {string.Join(", ", context.RecentFiles)}
- Current Selection: {context.CurrentSelection}
- Open Tabs: {string.Join(", ", context.OpenTabs)}

Consider this workspace context when making decisions and coordinating with other agents.";
    }
}
```

#### Native Diff-View Integration
```csharp
// Tools/DiffManagementTools.cs
[McpServerToolType]
public static class DiffManagementTools
{
    [McpServerTool]
    [Description("Present changes to user via VS Code native diff view")]
    public static async Task<string> ShowDiffPreview(
        IDiffPreviewService diffService,
        [Description("File path for the changes")] string filePath,
        [Description("Original content")] string originalContent,
        [Description("Proposed changes")] string modifiedContent,
        [Description("Description of changes")] string changeDescription)
    {
        var diffPreview = new DiffPreview
        {
            FilePath = filePath,
            OriginalContent = originalContent,
            ModifiedContent = modifiedContent,
            Description = changeDescription,
            AgentId = await diffService.GetCurrentAgentIdAsync()
        };
        
        var previewId = await diffService.ShowPreviewAsync(diffPreview);
        
        return $"Diff preview created with ID: {previewId}. User can accept/reject via VS Code commands.";
    }

    [McpServerTool]
    [Description("Check status of diff previews")]
    public static async Task<string> CheckDiffStatus(
        IDiffPreviewService diffService,
        [Description("Preview ID to check")] string previewId)
    {
        var status = await diffService.GetPreviewStatusAsync(previewId);
        
        return JsonSerializer.Serialize(new
        {
            PreviewId = previewId,
            Status = status.Status, // Pending, Accepted, Rejected
            UserFeedback = status.UserFeedback,
            Timestamp = status.LastUpdated
        });
    }
}
```

### Extended Tools API Integration

#### Built-in Tool Orchestration
```csharp
// Services/ExtendedToolsOrchestrator.cs
public class ExtendedToolsOrchestrator : IExtendedToolsOrchestrator
{
    public async Task<string> ExecuteResearchTaskAsync(string query)
    {
        // Orchestrate multiple built-in tools for comprehensive research
        var results = new List<string>();
        
        // 1. Use Google Search tool
        var searchResults = await ExecuteGeminiToolAsync("google_search", new { query });
        results.Add($"Search Results: {searchResults}");
        
        // 2. Use filesystem tool to check local docs
        var localDocs = await ExecuteGeminiToolAsync("filesystem_search", new 
        { 
            path = "./docs", 
            pattern = query 
        });
        results.Add($"Local Documentation: {localDocs}");
        
        // 3. Use memory tool to check previous research
        var previousResearch = await ExecuteGeminiToolAsync("memory_recall", new 
        { 
            topic = query,
            max_results = 5 
        });
        results.Add($"Previous Research: {previousResearch}");
        
        // 4. Combine and summarize
        var combinedResults = string.Join("\n\n", results);
        return combinedResults;
    }
    
    private async Task<string> ExecuteGeminiToolAsync(string toolName, object parameters)
    {
        // Integration with gemini-cli built-in tools
        // This would call the appropriate tool via the Tools API
        // Implementation depends on how gemini-cli exposes tool execution
        
        return await _geminiToolsClient.ExecuteToolAsync(toolName, parameters);
    }
}
```

## Clean Architecture Layout

Following clean architecture principles and MCP server patterns:

```
src/AISwarm.Server/
├── Program.cs                              # MCP server host entry point
├── AISwarm.Server.csproj                   # Project file with MCP dependencies
├── Tools/                                  # MCP tool implementations
│   ├── TaskManagementTools.cs             # Task CRUD operations
│   ├── WorktreeManagementTools.cs         # Worktree lifecycle tools
│   ├── AgentManagementTools.cs            # Agent spawning/termination
│   └── HealthCheckTools.cs                # System health monitoring
├── Services/                               # Business logic services
│   ├── ITaskCoordinationService.cs        # Core coordination interface
│   ├── TaskCoordinationService.cs         # Task management implementation
│   ├── IWorktreeManager.cs                # Worktree management interface
│   ├── WorktreeManager.cs                 # Git worktree operations
│   ├── IWorktreeSetupService.cs           # Worktree setup and configuration
│   ├── WorktreeSetupService.cs            # Agent instruction placement
│   ├── IAgentProcessManager.cs            # Agent process interface
│   ├── AgentProcessManager.cs             # Process spawning/termination
│   └── DatabaseInitializationService.cs   # Schema creation/migration
├── Data/                                   # Data access layer
│   ├── CoordinationDbContext.cs           # SQLite database context
│   ├── Repositories/                      # Repository pattern
│   │   ├── ITaskRepository.cs             # Task data interface
│   │   ├── TaskRepository.cs              # Task data implementation
│   │   ├── IAgentRepository.cs            # Agent data interface
│   │   ├── AgentRepository.cs             # Agent data implementation
│   │   ├── IWorktreeRepository.cs         # Worktree data interface
│   │   └── WorktreeRepository.cs          # Worktree data implementation
│   └── Migrations/                        # Database schema evolution
│       ├── 001_InitialSchema.sql         # Complete initial database schema
│       ├── MigrationRunner.cs            # Migration execution engine
│       └── IMigrationService.cs          # Migration service interface
└── Configuration/                          # Configuration and DI
    ├── ServiceCollectionExtensions.cs     # DI container setup
    └── CoordinationServerSettings.cs      # Configuration model
```

## Key Dependencies

```xml
<PackageReferences>
  <!-- MCP Server SDK -->
  <PackageReference Include="ModelContextProtocol" />
  <PackageReference Include="Microsoft.Extensions.Hosting" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
  
  <!-- Database -->
  <PackageReference Include="Microsoft.Data.Sqlite" />
  <PackageReference Include="Dapper" />
  
  <!-- Logging -->
  <PackageReference Include="Microsoft.Extensions.Logging" />
  <PackageReference Include="Serilog.Extensions.Hosting" />
  
  <!-- Process Management -->
  <PackageReference Include="System.Diagnostics.Process" />
</PackageReferences>
```

## Program.cs Structure

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using AISwarm.Server.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr for MCP compliance
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register coordination services
builder.Services.AddCoordinationServerServices();

// Configure MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

## Service Registration Pattern

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoordinationServerServices(
        this IServiceCollection services)
    {
        // Configuration
        services.Configure<CoordinationServerSettings>(
            builder.Configuration.GetSection("CoordinationServer"));

        // Database
        services.AddSingleton<CoordinationDbContext>();
        services.AddSingleton<DatabaseInitializationService>();
        services.AddSingleton<IMigrationService, MigrationService>();

        // Repositories
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IWorktreeRepository, WorktreeRepository>();

        // Services
        services.AddScoped<ITaskCoordinationService, TaskCoordinationService>();
        services.AddScoped<IWorktreeManager, WorktreeManager>();
        services.AddScoped<IWorktreeSetupService, WorktreeSetupService>();
        services.AddScoped<IAgentProcessManager, AgentProcessManager>();

        return services;
    }
}
```

## MCP Tool Example Structure

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;
using AISwarm.Server.Services;

[McpServerToolType]
public static class TaskManagementTools
{
    [McpServerTool]
    [Description("Agent heartbeat and get available tasks")]
    public static async Task<string> HeartbeatAndGetTasks(
        ITaskCoordinationService coordinationService,
        [Description("Agent unique identifier")] string agentId,
        [Description("Agent persona type")] string personaType)
    {
        var tasks = await coordinationService.HeartbeatAndGetTasksAsync(agentId, personaType);
        return JsonSerializer.Serialize(tasks, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    [McpServerTool]
    [Description("Claim ownership of a specific task")]
    public static async Task<string> ClaimTask(
        ITaskCoordinationService coordinationService,
        [Description("Task ID to claim")] int taskId,
        [Description("Agent ID claiming the task")] string agentId)
    {
        var success = await coordinationService.ClaimTaskAsync(taskId, agentId);
        return success ? $"Task {taskId} claimed successfully" : $"Failed to claim task {taskId}";
    }

    [McpServerTool]
    [Description("Report progress on current task")]
    public static async Task<string> ReportTaskProgress(
        ITaskCoordinationService coordinationService,
        [Description("Task ID")] int taskId,
        [Description("Agent ID")] string agentId,
        [Description("Progress description")] string progressNote,
        [Description("Percentage complete (0-100)")] int? percentComplete = null)
    {
        await coordinationService.ReportProgressAsync(taskId, agentId, progressNote, percentComplete);
        return $"Progress reported for task {taskId}";
    }
}
```

## Repository Pattern Example

```csharp
public interface ITaskRepository
{
    Task<int> CreateAsync(CreateTaskRequest request);
    Task<TaskInfo?> GetByIdAsync(int taskId);
    Task<IEnumerable<TaskInfo>> GetAvailableAsync(string personaType);
    Task<bool> ClaimAsync(int taskId, string agentId);
    Task CompleteAsync(int taskId, TaskResult result);
    Task FailAsync(int taskId, string errorMessage);
}

public class TaskRepository : ITaskRepository
{
    private readonly CoordinationDbContext _context;

    public TaskRepository(CoordinationDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateAsync(CreateTaskRequest request)
    {
        const string sql = @"
            INSERT INTO tasks (title, description, persona_id, target_worktree, metadata)
            VALUES (@Title, @Description, @PersonaId, @TargetWorktree, @Metadata)
            RETURNING id";

        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<int>(sql, request);
    }

    // ... other implementations
}
```

## Configuration Model

```csharp
public class CoordinationServerSettings
{
    public string DatabasePath { get; set; } = ".aiswarm/coordination.db";
    public string AgentLauncherPath { get; set; } = "dotnet";
    public string AgentLauncherArgs { get; set; } = "run --project";
    public TimeSpan AgentHeartbeatTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan TaskLeaseTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public bool EnableAutoMigration { get; set; } = true;
}
```

## Testing Strategy

```
tests/AISwarm.Server.Tests/
├── Tools/                              # MCP tool tests
│   ├── TaskManagementToolsTests.cs
│   └── WorktreeManagementToolsTests.cs
├── Services/                           # Service layer tests
│   ├── TaskCoordinationServiceTests.cs
│   └── WorktreeManagerTests.cs
├── Data/                               # Repository tests
│   └── TaskRepositoryTests.cs
└── TestDoubles/                        # Test infrastructure
    ├── InMemoryCoordinationDbContext.cs
    └── FakeAgentProcessManager.cs
```

This structure follows:

- **Clean Architecture**: Clear separation of concerns
- **MCP Patterns**: Tool-based API surface
- **Dependency Injection**: All services are injectable for testing
- **Repository Pattern**: Abstracts database operations
- **SOLID Principles**: Single responsibility, dependency inversion
- **Testability**: All layers can be unit tested independently

## Migration System Design

The coordination server uses a hash-based migration tracking system to ensure reliable schema evolution:

### Migration Tracking Table

```sql
-- 000_MigrationTracking.sql
CREATE TABLE IF NOT EXISTS migration_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_name TEXT NOT NULL UNIQUE,
    script_hash TEXT NOT NULL,
    applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    execution_time_ms INTEGER NOT NULL,
    success BOOLEAN NOT NULL DEFAULT 1
);

CREATE INDEX idx_migration_name ON migration_history(migration_name);
```

### Migration Service Interface

```csharp
public interface IMigrationService
{
    Task<bool> RequiresMigrationAsync();
    Task RunMigrationsAsync();
    Task<IEnumerable<MigrationInfo>> GetMigrationHistoryAsync();
    Task<string> CalculateScriptHashAsync(string scriptPath);
}

public class MigrationInfo
{
    public string MigrationName { get; set; }
    public string ScriptHash { get; set; }
    public DateTime AppliedAt { get; set; }
    public int ExecutionTimeMs { get; set; }
    public bool Success { get; set; }
}
```

### Migration Runner Implementation

```csharp
public class MigrationService : IMigrationService
{
    private readonly CoordinationDbContext _context;
    private readonly ILogger<MigrationService> _logger;

    public async Task<bool> RequiresMigrationAsync()
    {
        var pendingMigrations = await GetPendingMigrationsAsync();
        return pendingMigrations.Any();
    }

    public async Task RunMigrationsAsync()
    {
        var pendingMigrations = await GetPendingMigrationsAsync();
        
        foreach (var migration in pendingMigrations.OrderBy(m => m.Name))
        {
            await ExecuteMigrationAsync(migration);
        }
    }

    private async Task<IEnumerable<PendingMigration>> GetPendingMigrationsAsync()
    {
        var migrationFiles = Directory.GetFiles("Data/Migrations", "*.sql")
            .Where(f => !Path.GetFileName(f).StartsWith("000_")) // Skip tracking table
            .OrderBy(f => f);

        var appliedMigrations = await GetAppliedMigrationsAsync();
        var pendingMigrations = new List<PendingMigration>();

        foreach (var file in migrationFiles)
        {
            var migrationName = Path.GetFileNameWithoutExtension(file);
            var currentHash = await CalculateScriptHashAsync(file);
            var applied = appliedMigrations.FirstOrDefault(m => m.MigrationName == migrationName);

            if (applied == null)
            {
                // New migration
                pendingMigrations.Add(new PendingMigration
                {
                    Name = migrationName,
                    FilePath = file,
                    Hash = currentHash,
                    Reason = "New migration"
                });
            }
            else if (applied.ScriptHash != currentHash)
            {
                // Modified migration - requires re-run
                _logger.LogWarning("Migration {Migration} has been modified (hash changed)", migrationName);
                pendingMigrations.Add(new PendingMigration
                {
                    Name = migrationName,
                    FilePath = file,
                    Hash = currentHash,
                    Reason = "Script modified"
                });
            }
        }

        return pendingMigrations;
    }

    public async Task<string> CalculateScriptHashAsync(string scriptPath)
    {
        var scriptContent = await File.ReadAllTextAsync(scriptPath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(scriptContent));
        return Convert.ToHexString(hashBytes);
    }

    private async Task ExecuteMigrationAsync(PendingMigration migration)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var scriptContent = await File.ReadAllTextAsync(migration.FilePath);
            
            using var connection = _context.CreateConnection();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // Execute migration script
                await connection.ExecuteAsync(scriptContent, transaction: transaction);
                
                // Record successful migration
                await RecordMigrationAsync(connection, migration, stopwatch.ElapsedMilliseconds, true, transaction);
                
                transaction.Commit();
                
                _logger.LogInformation("Migration {Migration} completed successfully in {ElapsedMs}ms", 
                    migration.Name, stopwatch.ElapsedMilliseconds);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            // Record failed migration
            using var connection = _context.CreateConnection();
            await RecordMigrationAsync(connection, migration, stopwatch.ElapsedMilliseconds, false);
            
            _logger.LogError(ex, "Migration {Migration} failed after {ElapsedMs}ms", 
                migration.Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Migration Benefits

- **Hash Verification**: Detects if migration scripts have been modified
- **Idempotent Execution**: Safe to run multiple times
- **Transaction Safety**: Each migration runs in a transaction
- **Performance Tracking**: Records execution time for monitoring
- **Failure Recovery**: Failed migrations are recorded for troubleshooting
- **Audit Trail**: Complete history of all migration attempts

## Agent Instructions Integration

The coordination system automatically places comprehensive instructions in each agent worktree to guide their coordination behavior.

### Instruction File Placement

```csharp
// Services/IWorktreeSetupService.cs
public interface IWorktreeSetupService
{
    Task SetupWorktreeForAgentAsync(string worktreePath, AgentPersona persona, string coordinationServerEndpoint);
    Task PlaceAgentInstructionsAsync(string worktreePath);
    Task CreateGeminiConfigurationAsync(string worktreePath, string coordinationServerEndpoint);
}

// Services/WorktreeSetupService.cs
public class WorktreeSetupService : IWorktreeSetupService
{
    public async Task PlaceAgentInstructionsAsync(string worktreePath)
    {
        var instructionsPath = Path.Combine(worktreePath, "AGENT_INSTRUCTIONS.md");
        var instructionsContent = await GetEmbeddedResourceAsync("agent-coordination-prompt.md");
        await File.WriteAllTextAsync(instructionsPath, instructionsContent);
    }
}
```

### Instruction Content Structure

The `AGENT_INSTRUCTIONS.md` file provides agents with:

- **Coordination Workflow**: Step-by-step guide for task management
- **MCP Tool Usage**: Examples of coordination API calls
- **Behavioral Guidelines**: Proactive, collaborative, and efficient patterns
- **Success Indicators**: Clear metrics for effective coordination
- **Common Patterns**: Workflow examples for typical scenarios

### Integration Points

1. **Worktree Creation**: Instructions placed automatically during setup
2. **Agent Initialization**: Agents read instructions on startup
3. **Ongoing Reference**: Available throughout agent lifecycle
4. **Consistent Guidance**: Same coordination expectations across all agents
