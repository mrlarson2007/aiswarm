# A2A Schema Migration Plan

## 🎯 Objective

Migrate AISwarm from custom task format to A2A (Agent-to-Agent) standard protocol schema, ensuring ecosystem compatibility while maintaining backward compatibility and existing functionality.

## 📊 Current vs A2A Schema Comparison

### Current AISwarm Task Format
```csharp
public class AISwarmTask
{
    public string Id { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### A2A Standard Task Format
```csharp
public class A2ATask
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedAgent { get; set; }
    public TaskPriority Priority { get; set; }
    public Dictionary<string, object> Input { get; set; }
    public Dictionary<string, object>? Output { get; set; }
    public TaskMetadata Metadata { get; set; }
    public string[] RequiredCapabilities { get; set; }
    public TaskConstraints? Constraints { get; set; }
}

public enum TaskStatus
{
    Pending,
    InProgress, 
    Completed,
    Failed,
    Cancelled
}

public enum TaskPriority
{
    Low,
    Normal,
    High,
    Critical
}

public class TaskMetadata
{
    public string CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public Dictionary<string, string> Tags { get; set; }
    public TimeSpan? EstimatedDuration { get; set; }
    public DateTime? Deadline { get; set; }
}

public class TaskConstraints
{
    public TimeSpan? MaxDuration { get; set; }
    public int? MaxRetries { get; set; }
    public string[] PreferredAgents { get; set; }
    public string[] ExcludedAgents { get; set; }
}
```

## 🔄 Migration Strategy

### Phase 1: Schema Extension (Week 1)

**Goal**: Extend existing schema to support A2A fields without breaking changes

#### New Models

**File: `src/AISwarm.Shared/Models/A2A/A2ATask.cs`**
```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AISwarm.Shared.Models.A2A;

public class A2ATask
{
    [Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string Type { get; set; } = "general";
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedAgent { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    
    public Dictionary<string, object> Input { get; set; } = new();
    public Dictionary<string, object>? Output { get; set; }
    public TaskMetadata Metadata { get; set; } = new();
    public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();
    public TaskConstraints? Constraints { get; set; }
    
    // Legacy compatibility
    [JsonIgnore]
    public string? Result 
    { 
        get => Output?.GetValueOrDefault("result")?.ToString();
        set => Output ??= new Dictionary<string, object> { ["result"] = value ?? string.Empty };
    }
}
```

**File: `src/AISwarm.Shared/Models/A2A/A2AAgent.cs`**
```csharp
namespace AISwarm.Shared.Models.A2A;

public class A2AAgent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "general";
    public string Version { get; set; } = "1.0.0";
    public AgentStatus Status { get; set; } = AgentStatus.Available;
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public string? CurrentTask { get; set; }
    public AgentHealth Health { get; set; } = new();
}

public enum AgentStatus
{
    Available,
    Busy,
    Offline,
    Error
}

public class AgentHealth
{
    public bool IsHealthy { get; set; } = true;
    public string? LastError { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}
```

#### Database Migration

**File: `src/AISwarm.DataLayer/Migrations/001_A2ATaskMigration.cs`**
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

public partial class A2ATaskMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new A2A-compliant columns to existing Tasks table
        migrationBuilder.AddColumn<string>(
            name: "AssignedAgent",
            table: "Tasks",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "Tasks",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1); // Normal priority

        migrationBuilder.AddColumn<string>(
            name: "RequiredCapabilities",
            table: "Tasks", 
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "Input",
            table: "Tasks",
            type: "TEXT",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<string>(
            name: "Output",
            table: "Tasks",
            type: "TEXT", 
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TaskMetadata",
            table: "Tasks",
            type: "TEXT",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<string>(
            name: "Constraints",
            table: "Tasks",
            type: "TEXT",
            nullable: true);

        // Create A2A Agents table
        migrationBuilder.CreateTable(
            name: "A2AAgents",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Type = table.Column<string>(type: "TEXT", nullable: false),
                Version = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                Capabilities = table.Column<string>(type: "TEXT", nullable: false),
                Metadata = table.Column<string>(type: "TEXT", nullable: false),
                LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                CurrentTask = table.Column<string>(type: "TEXT", nullable: true),
                Health = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_A2AAgents", x => x.Id);
            });

        // Migrate existing task data to A2A format
        migrationBuilder.Sql(@"
            UPDATE Tasks 
            SET 
                Input = json_object('description', Description),
                TaskMetadata = json_object(
                    'createdBy', 'system',
                    'tags', json_object()
                ),
                RequiredCapabilities = '[]'
            WHERE Input = '{}'
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "A2AAgents");
        
        migrationBuilder.DropColumn(name: "AssignedAgent", table: "Tasks");
        migrationBuilder.DropColumn(name: "Priority", table: "Tasks");
        migrationBuilder.DropColumn(name: "RequiredCapabilities", table: "Tasks");
        migrationBuilder.DropColumn(name: "Input", table: "Tasks");
        migrationBuilder.DropColumn(name: "Output", table: "Tasks");
        migrationBuilder.DropColumn(name: "TaskMetadata", table: "Tasks");
        migrationBuilder.DropColumn(name: "Constraints", table: "Tasks");
    }
}
```

### Phase 2: Service Layer Migration (Week 1-2)

#### A2A-Compatible Services

**File: `src/AISwarm.Shared/Services/IA2ATaskService.cs`**
```csharp
namespace AISwarm.Shared.Services;

public interface IA2ATaskService
{
    Task<A2ATask> CreateTaskAsync(CreateA2ATaskRequest request);
    Task<A2ATask?> GetTaskAsync(string taskId);
    Task<A2ATask[]> GetPendingTasksAsync();
    Task<A2ATask> ClaimTaskAsync(string taskId, string agentId);
    Task<A2ATask> CompleteTaskAsync(string taskId, Dictionary<string, object> output);
    Task<A2ATask> FailTaskAsync(string taskId, string error);
    Task<A2ATask[]> GetTasksByAgentAsync(string agentId);
    Task<bool> DeleteTaskAsync(string taskId);
}

public class CreateA2ATaskRequest
{
    public string Type { get; set; } = "general";
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public Dictionary<string, object> Input { get; set; } = new();
    public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();
    public TaskConstraints? Constraints { get; set; }
    public TaskMetadata? Metadata { get; set; }
}
```

**File: `src/AISwarm.Server/Services/A2ATaskService.cs`**
```csharp
using Microsoft.EntityFrameworkCore;
using AISwarm.DataLayer;
using AISwarm.Shared.Models.A2A;

namespace AISwarm.Server.Services;

public class A2ATaskService : IA2ATaskService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<A2ATaskService> _logger;

    public A2ATaskService(ApplicationDbContext context, ILogger<A2ATaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<A2ATask> CreateTaskAsync(CreateA2ATaskRequest request)
    {
        var task = new A2ATask
        {
            Id = Guid.NewGuid().ToString(),
            Type = request.Type,
            Description = request.Description,
            Priority = request.Priority,
            Input = request.Input,
            RequiredCapabilities = request.RequiredCapabilities,
            Constraints = request.Constraints,
            Metadata = request.Metadata ?? new TaskMetadata
            {
                CreatedBy = "system",
                Tags = new Dictionary<string, string>()
            }
        };

        _context.Tasks.Add(MapToEntity(task));
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created A2A task {TaskId} of type {TaskType}", task.Id, task.Type);
        return task;
    }

    public async Task<A2ATask> ClaimTaskAsync(string taskId, string agentId)
    {
        var taskEntity = await _context.Tasks.FindAsync(taskId);
        if (taskEntity == null)
            throw new ArgumentException($"Task {taskId} not found");

        if (taskEntity.Status != TaskStatus.Pending.ToString())
            throw new InvalidOperationException($"Task {taskId} is not available for claiming");

        taskEntity.Status = TaskStatus.InProgress.ToString();
        taskEntity.AssignedAgent = agentId;
        taskEntity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} claimed by agent {AgentId}", taskId, agentId);
        return MapFromEntity(taskEntity);
    }

    // Additional service methods...
    
    private TaskEntity MapToEntity(A2ATask task) => new()
    {
        Id = task.Id,
        Type = task.Type,
        Description = task.Description,
        Status = task.Status.ToString(),
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt,
        AssignedAgent = task.AssignedAgent,
        Priority = (int)task.Priority,
        Input = JsonSerializer.Serialize(task.Input),
        Output = task.Output != null ? JsonSerializer.Serialize(task.Output) : null,
        RequiredCapabilities = JsonSerializer.Serialize(task.RequiredCapabilities),
        TaskMetadata = JsonSerializer.Serialize(task.Metadata),
        Constraints = task.Constraints != null ? JsonSerializer.Serialize(task.Constraints) : null
    };

    private A2ATask MapFromEntity(TaskEntity entity) => new()
    {
        Id = entity.Id,
        Type = entity.Type,
        Description = entity.Description,
        Status = Enum.Parse<TaskStatus>(entity.Status),
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        AssignedAgent = entity.AssignedAgent,
        Priority = (TaskPriority)entity.Priority,
        Input = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Input) ?? new(),
        Output = entity.Output != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Output) : null,
        RequiredCapabilities = JsonSerializer.Deserialize<string[]>(entity.RequiredCapabilities) ?? Array.Empty<string>(),
        Metadata = JsonSerializer.Deserialize<TaskMetadata>(entity.TaskMetadata) ?? new(),
        Constraints = entity.Constraints != null ? JsonSerializer.Deserialize<TaskConstraints>(entity.Constraints) : null
    };
}
```

### Phase 3: C# A2A SDK Integration (Week 2)

#### Package Installation
```bash
cd src/AISwarm.Server
dotnet add package A2A
dotnet add package A2A.AspNetCore
```

#### A2A SDK Configuration

**File: `src/AISwarm.Server/A2A/A2AServerConfiguration.cs`**
```csharp
using A2A.AspNetCore;
using AISwarm.Shared.Models.A2A;

namespace AISwarm.Server.A2A;

public static class A2AServerConfiguration
{
    public static IServiceCollection AddA2AServer(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure A2A SDK
        services.AddA2A(options =>
        {
            options.AgentCard = new AgentCard
            {
                Name = "AISwarm Server",
                Version = "1.0.0", 
                Description = "AISwarm A2A-enabled task management server",
                Capabilities = new[] { "task-management", "agent-coordination", "code-generation" },
                Endpoints = new Dictionary<string, string>
                {
                    ["tasks"] = "/a2a/tasks",
                    ["agents"] = "/a2a/agents", 
                    ["messages"] = "/a2a/messages"
                }
            };
        });

        // Register A2A services
        services.AddScoped<IA2ATaskService, A2ATaskService>();
        services.AddScoped<IA2AAgentService, A2AAgentService>();
        
        return services;
    }

    public static WebApplication UseA2AServer(this WebApplication app)
    {
        // Add A2A middleware
        app.UseA2A();
        
        // Map A2A endpoints
        app.MapA2AEndpoints("/a2a");
        
        return app;
    }
}
```

### Phase 4: Backward Compatibility Layer (Week 2)

#### Legacy API Wrapper

**File: `src/AISwarm.Server/Services/LegacyTaskService.cs`**
```csharp
namespace AISwarm.Server.Services;

public class LegacyTaskService : ITaskService
{
    private readonly IA2ATaskService _a2aTaskService;

    public LegacyTaskService(IA2ATaskService a2aTaskService)
    {
        _a2aTaskService = a2aTaskService;
    }

    public async Task<AISwarmTask> CreateTaskAsync(string description, string type = "general")
    {
        var a2aTask = await _a2aTaskService.CreateTaskAsync(new CreateA2ATaskRequest
        {
            Description = description,
            Type = type,
            Input = new Dictionary<string, object> { ["description"] = description }
        });

        return MapToLegacyTask(a2aTask);
    }

    public async Task<AISwarmTask?> GetTaskAsync(string taskId)
    {
        var a2aTask = await _a2aTaskService.GetTaskAsync(taskId);
        return a2aTask != null ? MapToLegacyTask(a2aTask) : null;
    }

    private AISwarmTask MapToLegacyTask(A2ATask a2aTask) => new()
    {
        Id = a2aTask.Id,
        Description = a2aTask.Description,
        Type = a2aTask.Type,
        Status = a2aTask.Status.ToString(),
        CreatedAt = a2aTask.CreatedAt,
        CompletedAt = a2aTask.Status == TaskStatus.Completed ? a2aTask.UpdatedAt : null,
        Result = a2aTask.Output?.GetValueOrDefault("result")?.ToString(),
        Metadata = a2aTask.Input.Concat(a2aTask.Metadata.Tags.Select(kv => 
            new KeyValuePair<string, object>(kv.Key, kv.Value)))
            .ToDictionary(kv => kv.Key, kv => kv.Value)
    };
}
```

## 🧪 Migration Testing Strategy

### Data Migration Tests

**File: `tests/AISwarm.Tests/Migration/A2AMigrationTests.cs`**
```csharp
[TestClass]
public class A2AMigrationTests
{
    [TestMethod]
    public async Task Should_Migrate_Legacy_Tasks_To_A2A_Format()
    {
        // Arrange
        var legacyTask = new AISwarmTask
        {
            Id = "test-1",
            Description = "Test task",
            Type = "code-generation",
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            Metadata = new() { ["language"] = "python" }
        };

        // Act
        var migratedTask = await MigrationService.MigrateLegacyTaskAsync(legacyTask);

        // Assert
        Assert.AreEqual(legacyTask.Id, migratedTask.Id);
        Assert.AreEqual(legacyTask.Description, migratedTask.Description);
        Assert.AreEqual(TaskStatus.Pending, migratedTask.Status);
        Assert.IsTrue(migratedTask.Input.ContainsKey("description"));
        Assert.AreEqual("python", migratedTask.Metadata.Tags["language"]);
    }

    [TestMethod]
    public async Task Should_Maintain_Backward_Compatibility()
    {
        // Test that legacy MCP tools still work after migration
        var legacyService = new LegacyTaskService(_a2aTaskService);
        var task = await legacyService.CreateTaskAsync("Test legacy task");
        
        Assert.IsNotNull(task);
        Assert.AreEqual("Test legacy task", task.Description);
    }
}
```

### A2A Compliance Tests

**File: `tests/AISwarm.Tests/A2A/A2AComplianceTests.cs`**
```csharp
[TestClass]
public class A2AComplianceTests
{
    [TestMethod]
    public async Task Should_Provide_Agent_Card_Endpoint()
    {
        var response = await _client.GetAsync("/.well-known/agent-card.json");
        response.EnsureSuccessStatusCode();
        
        var agentCard = await response.Content.ReadFromJsonAsync<AgentCard>();
        Assert.IsNotNull(agentCard);
        Assert.AreEqual("AISwarm Server", agentCard.Name);
    }

    [TestMethod]
    public async Task Should_Support_A2A_Task_Lifecycle()
    {
        // Create task
        var createRequest = new CreateA2ATaskRequest
        {
            Description = "Test A2A task",
            Type = "test"
        };
        
        var task = await _a2aTaskService.CreateTaskAsync(createRequest);
        Assert.AreEqual(TaskStatus.Pending, task.Status);

        // Claim task
        var claimedTask = await _a2aTaskService.ClaimTaskAsync(task.Id, "test-agent");
        Assert.AreEqual(TaskStatus.InProgress, claimedTask.Status);
        Assert.AreEqual("test-agent", claimedTask.AssignedAgent);

        // Complete task
        var output = new Dictionary<string, object> { ["result"] = "Task completed" };
        var completedTask = await _a2aTaskService.CompleteTaskAsync(task.Id, output);
        Assert.AreEqual(TaskStatus.Completed, completedTask.Status);
        Assert.IsNotNull(completedTask.Output);
    }
}
```

## 📋 Migration Checklist

### Pre-Migration
- [ ] Backup existing database
- [ ] Document current API contracts
- [ ] Identify all legacy dependencies
- [ ] Create rollback plan

### Week 1: Schema Migration
- [ ] Create A2A models and enums
- [ ] Generate database migration
- [ ] Test migration on copy of production data
- [ ] Validate data integrity post-migration

### Week 2: Service Migration
- [ ] Implement A2A-compliant services
- [ ] Create backward compatibility layer
- [ ] Update dependency injection configuration
- [ ] Test existing MCP tools still function

### Week 3: Testing & Validation
- [ ] Run comprehensive test suite
- [ ] Validate A2A protocol compliance
- [ ] Performance testing with A2A format
- [ ] Integration testing with Gemini CLI fork

### Post-Migration
- [ ] Monitor system performance
- [ ] Validate A2A ecosystem integration
- [ ] Document new A2A API capabilities
- [ ] Plan deprecation timeline for legacy APIs

## 🔄 Rollback Strategy

If issues are discovered post-migration:

1. **Immediate Rollback**: Restore database from pre-migration backup
2. **Partial Rollback**: Use feature flags to disable A2A endpoints
3. **Data Recovery**: Migration includes reverse mapping functions
4. **Gradual Migration**: Phase rollout with canary deployment

This migration plan ensures AISwarm becomes A2A-compliant while maintaining all existing functionality and providing a smooth transition path for users and integrations.