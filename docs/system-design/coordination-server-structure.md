# AISwarm.Server Project Structure

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

