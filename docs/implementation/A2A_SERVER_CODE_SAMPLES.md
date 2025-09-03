# A2A Server - Code Implementation Samples

## 🎯 Overview

This document provides comprehensive code samples for implementing the AISwarm A2A server. These samples include the complete server implementation with all endpoints, data models, services, and infrastructure needed for A2A protocol support.

## 📁 Project Structure

```text
src/AISwarm.A2A/                    # A2A Library
├── Models/
│   ├── A2AModels.cs              # Core A2A data models
│   ├── ApiResponse.cs            # Standardized API responses
│   └── ValidationModels.cs       # Request validation models
├── Services/
│   ├── ITaskService.cs           # Task service interface
│   ├── TaskService.cs            # Task business logic
│   ├── IAgentService.cs          # Agent service interface
│   ├── AgentService.cs           # Agent management logic
│   └── IA2AServerService.cs      # A2A server service interface
├── Data/
│   ├── A2ADbContext.cs           # Entity Framework context
│   ├── Repositories/             # Data access layer
│   └── Migrations/               # Database migrations
├── Controllers/
│   ├── A2AAgentController.cs     # Agent management endpoints
│   ├── A2ATaskController.cs      # Task lifecycle management
│   └── A2AHealthController.cs    # Health checks and monitoring
├── Extensions/
│   ├── ServiceCollectionExtensions.cs  # DI registration
│   ├── ApplicationBuilderExtensions.cs # Middleware registration
│   └── A2AConfigurationExtensions.cs   # Configuration helpers
└── Configuration/
    ├── A2AOptions.cs             # A2A configuration options
    └── A2AConstants.cs           # Constants and defaults

src/AISwarm.Server/               # Updated MCP Server
├── McpTools/
│   ├── A2A/                      # A2A-specific MCP tools
│   │   ├── LaunchGeminiAgentTool.cs     # Launch Gemini A2A agents
│   │   ├── CreateA2ATaskTool.cs         # Create A2A tasks
│   │   ├── GetA2ATaskStatusTool.cs      # Query task status
│   │   └── ListA2AAgentsTool.cs         # List active agents
│   └── ... (existing MCP tools)
├── Program.cs                    # Updated with A2A registration
└── appsettings.json             # Updated configuration
```

## A2A Library Package

**Package:** `AISwarm.A2A`
**Target Framework:** .NET 8.0
**Dependencies:**

- Microsoft.AspNetCore.App (8.0.0)
- Microsoft.EntityFrameworkCore (8.0.0)
- Microsoft.Extensions.DependencyInjection (8.0.0)
- Microsoft.Extensions.Logging (8.0.0)
- Serilog.AspNetCore (8.0.0)

**NuGet Package Definition:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PackageId>AISwarm.A2A</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <Authors>AISwarm</Authors>
    <Description>A2A (Agent-to-Agent) protocol library for AISwarm</Description>
    <PackageTags>a2a;agent;protocol;aiswarm</PackageTags>
    <RepositoryUrl>https://github.com/mrlarson2007/aiswarm</RepositoryUrl>
  </PropertyGroup>
  
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Core Implementation Files

### 1. A2A Data Models

**File: `src/AISwarm.A2A/Models/A2AModels.cs`**

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AISwarm.A2A.Models;

// Core Task Model
public class A2ATask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string Type { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public string? AssignedAgent { get; set; }
    public string CreatedBy { get; set; } = "system";
    public string? CompletedBy { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    
    public Dictionary<string, object> Input { get; set; } = new();
    public Dictionary<string, object>? Output { get; set; }
    
    public TaskMetadata Metadata { get; set; } = new();
    public List<string> RequiredCapabilities { get; set; } = new();
    public TaskConstraints? Constraints { get; set; }
    
    // Tracking fields
    public int RetryCount { get; set; } = 0;
    public DateTime? LastRetryAt { get; set; }
    public string? LastError { get; set; }
    
    // Performance metrics
    public TimeSpan? ProcessingTime { get; set; }
    public DateTime? ClaimedAt { get; set; }
}

public enum TaskStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    Expired
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
    public string CreatedBy { get; set; } = "system";
    public string? UpdatedBy { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public TimeSpan? EstimatedDuration { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Source { get; set; }
    public string? ParentTaskId { get; set; }
    public List<string> Dependencies { get; set; } = new();
}

public class TaskConstraints
{
    public TimeSpan? MaxDuration { get; set; }
    public int? MaxRetries { get; set; }
    public List<string> PreferredAgents { get; set; } = new();
    public List<string> ExcludedAgents { get; set; } = new();
    public Dictionary<string, object> ResourceRequirements { get; set; } = new();
}

// Core Agent Model
public class A2AAgent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Type { get; set; } = string.Empty;
    
    public string Version { get; set; } = "1.0.0";
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentStatus Status { get; set; } = AgentStatus.Available;
    
    public List<string> Capabilities { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    public string? CurrentTask { get; set; }
    public AgentHealth Health { get; set; } = new();
    
    // Performance tracking
    public int TasksCompleted { get; set; } = 0;
    public int TasksFailed { get; set; } = 0;
    public TimeSpan TotalProcessingTime { get; set; } = TimeSpan.Zero;
    public double AverageTaskTime => TasksCompleted > 0 ? TotalProcessingTime.TotalMinutes / TasksCompleted : 0;
}

public enum AgentStatus
{
    Available,
    Busy,
    Offline,
    Error,
    Maintenance
}

public class AgentHealth
{
    public bool IsHealthy { get; set; } = true;
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Request/Response Models
public class CreateTaskRequest
{
    [Required]
    public string Type { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public Dictionary<string, object> Input { get; set; } = new();
    public TaskMetadata? Metadata { get; set; }
    public List<string> RequiredCapabilities { get; set; } = new();
    public TaskConstraints? Constraints { get; set; }
}

public class TaskClaimRequest
{
    [Required]
    public string AgentId { get; set; } = string.Empty;
    
    public Dictionary<string, object> AgentMetadata { get; set; } = new();
}

public class TaskCompleteRequest
{
    [Required]
    public string CompletedBy { get; set; } = string.Empty;
    
    public Dictionary<string, object> Output { get; set; } = new();
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class TaskFailRequest
{
    [Required]
    public string FailedBy { get; set; } = string.Empty;
    
    [Required]
    public string Error { get; set; } = string.Empty;
    
    public DateTime? FailedAt { get; set; }
    public bool ShouldRetry { get; set; } = true;
    public Dictionary<string, object> ErrorMetadata { get; set; } = new();
}

public class RegisterAgentRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Type { get; set; } = string.Empty;
    
    public string Version { get; set; } = "1.0.0";
    public List<string> Capabilities { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public AgentHealth? Health { get; set; }
}

public class UpdateAgentStatusRequest
{
    [Required]
    public AgentStatus Status { get; set; }
    
    public string? CurrentTask { get; set; }
    public DateTime? LastSeen { get; set; }
    public AgentHealth? Health { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// Agent Card (A2A Protocol)
public class AgentCard
{
    public string Name { get; set; } = "AISwarm A2A Server";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "AISwarm Agent-to-Agent Communication Server";
    public List<string> Capabilities { get; set; } = new();
    public AgentCardEndpoints Endpoints { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class AgentCardEndpoints
{
    public string Tasks { get; set; } = "/tasks";
    public string TasksPending { get; set; } = "/tasks/pending";
    public string TaskClaim { get; set; } = "/tasks/{id}/claim";
    public string TaskComplete { get; set; } = "/tasks/{id}/complete";
    public string TaskFail { get; set; } = "/tasks/{id}/fail";
    public string AgentRegister { get; set; } = "/agents/register";
    public string AgentStatus { get; set; } = "/agents/{id}/status";
    public string Health { get; set; } = "/health";
}
```

### 2. API Response Models

**File: `src/AISwarm.A2A/Models/ApiResponse.cs`**

```csharp
using System.Text.Json.Serialization;

namespace AISwarm.A2A.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? RequestId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    public static ApiResponse<T> Ok(T data, Dictionary<string, object>? metadata = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Metadata = metadata
        };
    }

    public static ApiResponse<T> Fail(string error, Dictionary<string, object>? metadata = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = error,
            Metadata = metadata
        };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(Dictionary<string, object>? metadata = null)
    {
        return new ApiResponse
        {
            Success = true,
            Metadata = metadata
        };
    }

    public new static ApiResponse Fail(string error, Dictionary<string, object>? metadata = null)
    {
        return new ApiResponse
        {
            Success = false,
            Error = error,
            Metadata = metadata
        };
    }
}

public class PaginatedResponse<T> : ApiResponse<IEnumerable<T>>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PaginatedResponse<T> Ok(
        IEnumerable<T> data, 
        int page, 
        int pageSize, 
        int totalCount,
        Dictionary<string, object>? metadata = null)
    {
        return new PaginatedResponse<T>
        {
            Success = true,
            Data = data,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Metadata = metadata
        };
    }
}
```

### 3. Task Service Implementation

**File: `src/AISwarm.A2A/Services/TaskService.cs`**

```csharp
using AISwarm.A2A.Models;
using AISwarm.A2A.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AISwarm.A2A.Services;

public interface ITaskService
{
    Task<A2ATask> CreateTaskAsync(CreateTaskRequest request);
    Task<A2ATask?> GetTaskAsync(string id);
    Task<IEnumerable<A2ATask>> GetTasksAsync(TaskQueryParameters parameters);
    Task<IEnumerable<A2ATask>> GetPendingTasksAsync(string? agentName = null);
    Task<A2ATask?> ClaimTaskAsync(string taskId, TaskClaimRequest request);
    Task<A2ATask?> CompleteTaskAsync(string taskId, TaskCompleteRequest request);
    Task<A2ATask?> FailTaskAsync(string taskId, TaskFailRequest request);
    Task<A2ATask?> CancelTaskAsync(string taskId, string cancelledBy);
    Task<bool> DeleteTaskAsync(string taskId);
    Task<TaskStatistics> GetTaskStatisticsAsync();
    Task CleanupExpiredTasksAsync();
}

public class TaskService : ITaskService
{
    private readonly A2ADbContext _context;
    private readonly ILogger<TaskService> _logger;
    private readonly IAgentService _agentService;

    public TaskService(A2ADbContext context, ILogger<TaskService> logger, IAgentService agentService)
    {
        _context = context;
        _logger = logger;
        _agentService = agentService;
    }

    public async Task<A2ATask> CreateTaskAsync(CreateTaskRequest request)
    {
        var task = new A2ATask
        {
            Type = request.Type,
            Description = request.Description,
            Priority = request.Priority,
            Input = request.Input,
            RequiredCapabilities = request.RequiredCapabilities,
            Constraints = request.Constraints,
            Metadata = request.Metadata ?? new TaskMetadata()
        };

        // Set deadline if specified in constraints
        if (task.Constraints?.MaxDuration.HasValue == true)
        {
            task.Metadata.Deadline = DateTime.UtcNow.Add(task.Constraints.MaxDuration.Value);
        }

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created task {TaskId} of type {TaskType}", task.Id, task.Type);
        
        return task;
    }

    public async Task<A2ATask?> GetTaskAsync(string id)
    {
        return await _context.Tasks.FindAsync(id);
    }

    public async Task<IEnumerable<A2ATask>> GetTasksAsync(TaskQueryParameters parameters)
    {
        var query = _context.Tasks.AsQueryable();

        // Apply filters
        if (parameters.Status.HasValue)
            query = query.Where(t => t.Status == parameters.Status.Value);

        if (parameters.Type != null)
            query = query.Where(t => t.Type == parameters.Type);

        if (parameters.AssignedAgent != null)
            query = query.Where(t => t.AssignedAgent == parameters.AssignedAgent);

        if (parameters.CreatedAfter.HasValue)
            query = query.Where(t => t.CreatedAt >= parameters.CreatedAfter.Value);

        if (parameters.CreatedBefore.HasValue)
            query = query.Where(t => t.CreatedAt <= parameters.CreatedBefore.Value);

        // Apply sorting
        query = parameters.SortBy?.ToLowerInvariant() switch
        {
            "priority" => parameters.SortDescending 
                ? query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt),
            "status" => parameters.SortDescending 
                ? query.OrderByDescending(t => t.Status)
                : query.OrderBy(t => t.Status),
            "updatedat" => parameters.SortDescending 
                ? query.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                : query.OrderBy(t => t.UpdatedAt ?? t.CreatedAt),
            _ => parameters.SortDescending 
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt)
        };

        // Apply pagination
        if (parameters.Skip.HasValue)
            query = query.Skip(parameters.Skip.Value);

        if (parameters.Take.HasValue)
            query = query.Take(parameters.Take.Value);

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<A2ATask>> GetPendingTasksAsync(string? agentName = null)
    {
        var query = _context.Tasks
            .Where(t => t.Status == TaskStatus.Pending)
            .Where(t => !t.Metadata.Deadline.HasValue || t.Metadata.Deadline > DateTime.UtcNow);

        // Filter by agent capabilities if specified
        if (!string.IsNullOrEmpty(agentName))
        {
            var agent = await _agentService.GetAgentAsync(agentName);
            if (agent != null)
            {
                query = query.Where(t => 
                    t.RequiredCapabilities.Count == 0 ||
                    t.RequiredCapabilities.All(cap => agent.Capabilities.Contains(cap)));
            }
        }

        return await query
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<A2ATask?> ClaimTaskAsync(string taskId, TaskClaimRequest request)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for claim by {AgentId}", taskId, request.AgentId);
            return null;
        }

        if (task.Status != TaskStatus.Pending)
        {
            _logger.LogWarning("Task {TaskId} is not pending (status: {Status}), cannot be claimed by {AgentId}", 
                taskId, task.Status, request.AgentId);
            return null;
        }

        // Check if task is expired
        if (task.Metadata.Deadline.HasValue && task.Metadata.Deadline <= DateTime.UtcNow)
        {
            task.Status = TaskStatus.Expired;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            _logger.LogWarning("Task {TaskId} expired, cannot be claimed by {AgentId}", taskId, request.AgentId);
            return null;
        }

        // Verify agent capabilities
        var agent = await _agentService.GetAgentAsync(request.AgentId);
        if (agent != null && task.RequiredCapabilities.Any())
        {
            var missingCapabilities = task.RequiredCapabilities
                .Where(cap => !agent.Capabilities.Contains(cap))
                .ToList();
            
            if (missingCapabilities.Any())
            {
                _logger.LogWarning("Agent {AgentId} missing required capabilities for task {TaskId}: {Capabilities}", 
                    request.AgentId, taskId, string.Join(", ", missingCapabilities));
                return null;
            }
        }

        // Claim the task
        task.Status = TaskStatus.InProgress;
        task.AssignedAgent = request.AgentId;
        task.ClaimedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        // Update agent metadata if provided
        if (request.AgentMetadata.Any())
        {
            foreach (var kvp in request.AgentMetadata)
            {
                task.Metadata.Tags[$"agent_{kvp.Key}"] = kvp.Value.ToString() ?? "";
            }
        }

        await _context.SaveChangesAsync();

        // Update agent status
        if (agent != null)
        {
            await _agentService.UpdateAgentStatusAsync(request.AgentId, new UpdateAgentStatusRequest
            {
                Status = AgentStatus.Busy,
                CurrentTask = taskId,
                LastSeen = DateTime.UtcNow
            });
        }

        _logger.LogInformation("Task {TaskId} claimed by agent {AgentId}", taskId, request.AgentId);
        
        return task;
    }

    public async Task<A2ATask?> CompleteTaskAsync(string taskId, TaskCompleteRequest request)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for completion by {AgentId}", taskId, request.CompletedBy);
            return null;
        }

        if (task.Status != TaskStatus.InProgress)
        {
            _logger.LogWarning("Task {TaskId} is not in progress (status: {Status}), cannot be completed by {AgentId}", 
                taskId, task.Status, request.CompletedBy);
            return null;
        }

        if (task.AssignedAgent != request.CompletedBy)
        {
            _logger.LogWarning("Task {TaskId} assigned to {AssignedAgent}, cannot be completed by {AgentId}", 
                taskId, task.AssignedAgent, request.CompletedBy);
            return null;
        }

        // Complete the task
        task.Status = TaskStatus.Completed;
        task.CompletedBy = request.CompletedBy;
        task.CompletedAt = request.CompletedAt ?? DateTime.UtcNow;
        task.Output = request.Output;
        task.UpdatedAt = DateTime.UtcNow;

        // Calculate processing time
        if (task.ClaimedAt.HasValue)
        {
            task.ProcessingTime = task.CompletedAt.Value - task.ClaimedAt.Value;
        }

        // Update metadata
        if (request.Metadata.Any())
        {
            foreach (var kvp in request.Metadata)
            {
                task.Metadata.Tags[$"completion_{kvp.Key}"] = kvp.Value.ToString() ?? "";
            }
        }

        await _context.SaveChangesAsync();

        // Update agent metrics
        var agent = await _agentService.GetAgentAsync(request.CompletedBy);
        if (agent != null)
        {
            agent.TasksCompleted++;
            if (task.ProcessingTime.HasValue)
            {
                agent.TotalProcessingTime = agent.TotalProcessingTime.Add(task.ProcessingTime.Value);
            }

            await _agentService.UpdateAgentStatusAsync(request.CompletedBy, new UpdateAgentStatusRequest
            {
                Status = AgentStatus.Available,
                CurrentTask = null,
                LastSeen = DateTime.UtcNow
            });
        }

        _logger.LogInformation("Task {TaskId} completed by agent {AgentId} in {ProcessingTime}", 
            taskId, request.CompletedBy, task.ProcessingTime);
        
        return task;
    }

    public async Task<A2ATask?> FailTaskAsync(string taskId, TaskFailRequest request)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for failure by {AgentId}", taskId, request.FailedBy);
            return null;
        }

        if (task.Status != TaskStatus.InProgress)
        {
            _logger.LogWarning("Task {TaskId} is not in progress (status: {Status}), cannot be failed by {AgentId}", 
                taskId, task.Status, request.FailedBy);
            return null;
        }

        // Fail the task
        task.LastError = request.Error;
        task.RetryCount++;
        task.LastRetryAt = request.FailedAt ?? DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        // Determine if task should be retried
        var maxRetries = task.Constraints?.MaxRetries ?? 3;
        if (request.ShouldRetry && task.RetryCount < maxRetries)
        {
            // Reset for retry
            task.Status = TaskStatus.Pending;
            task.AssignedAgent = null;
            task.ClaimedAt = null;
            
            _logger.LogInformation("Task {TaskId} failed by {AgentId}, queued for retry (attempt {RetryCount}/{MaxRetries})", 
                taskId, request.FailedBy, task.RetryCount, maxRetries);
        }
        else
        {
            // Mark as permanently failed
            task.Status = TaskStatus.Failed;
            task.CompletedAt = request.FailedAt ?? DateTime.UtcNow;
            
            _logger.LogError("Task {TaskId} permanently failed by {AgentId} after {RetryCount} attempts: {Error}", 
                taskId, request.FailedBy, task.RetryCount, request.Error);
        }

        // Add error metadata
        if (request.ErrorMetadata.Any())
        {
            foreach (var kvp in request.ErrorMetadata)
            {
                task.Metadata.Tags[$"error_{kvp.Key}"] = kvp.Value.ToString() ?? "";
            }
        }

        await _context.SaveChangesAsync();

        // Update agent metrics
        var agent = await _agentService.GetAgentAsync(request.FailedBy);
        if (agent != null)
        {
            agent.TasksFailed++;

            await _agentService.UpdateAgentStatusAsync(request.FailedBy, new UpdateAgentStatusRequest
            {
                Status = AgentStatus.Available,
                CurrentTask = null,
                LastSeen = DateTime.UtcNow,
                Health = new AgentHealth
                {
                    IsHealthy = true,
                    LastError = request.Error,
                    LastErrorAt = DateTime.UtcNow,
                    Metrics = agent.Health.Metrics
                }
            });
        }

        return task;
    }

    public async Task<A2ATask?> CancelTaskAsync(string taskId, string cancelledBy)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null) return null;

        if (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Failed)
        {
            _logger.LogWarning("Cannot cancel task {TaskId} with status {Status}", taskId, task.Status);
            return null;
        }

        task.Status = TaskStatus.Cancelled;
        task.UpdatedAt = DateTime.UtcNow;
        task.CompletedAt = DateTime.UtcNow;
        task.Metadata.Tags["cancelled_by"] = cancelledBy;

        // Free up assigned agent
        if (!string.IsNullOrEmpty(task.AssignedAgent))
        {
            await _agentService.UpdateAgentStatusAsync(task.AssignedAgent, new UpdateAgentStatusRequest
            {
                Status = AgentStatus.Available,
                CurrentTask = null,
                LastSeen = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} cancelled by {CancelledBy}", taskId, cancelledBy);
        
        return task;
    }

    public async Task<bool> DeleteTaskAsync(string taskId)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null) return false;

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} deleted", taskId);
        return true;
    }

    public async Task<TaskStatistics> GetTaskStatisticsAsync()
    {
        var tasks = await _context.Tasks.ToListAsync();
        
        return new TaskStatistics
        {
            TotalTasks = tasks.Count,
            PendingTasks = tasks.Count(t => t.Status == TaskStatus.Pending),
            InProgressTasks = tasks.Count(t => t.Status == TaskStatus.InProgress),
            CompletedTasks = tasks.Count(t => t.Status == TaskStatus.Completed),
            FailedTasks = tasks.Count(t => t.Status == TaskStatus.Failed),
            CancelledTasks = tasks.Count(t => t.Status == TaskStatus.Cancelled),
            ExpiredTasks = tasks.Count(t => t.Status == TaskStatus.Expired),
            AverageProcessingTime = tasks
                .Where(t => t.ProcessingTime.HasValue)
                .Select(t => t.ProcessingTime!.Value.TotalMinutes)
                .DefaultIfEmpty(0)
                .Average(),
            TasksByType = tasks.GroupBy(t => t.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            TasksByPriority = tasks.GroupBy(t => t.Priority)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };
    }

    public async Task CleanupExpiredTasksAsync()
    {
        var expiredTasks = await _context.Tasks
            .Where(t => t.Status == TaskStatus.Pending || t.Status == TaskStatus.InProgress)
            .Where(t => t.Metadata.Deadline.HasValue && t.Metadata.Deadline <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var task in expiredTasks)
        {
            task.Status = TaskStatus.Expired;
            task.UpdatedAt = DateTime.UtcNow;
            task.CompletedAt = DateTime.UtcNow;

            // Free up assigned agent
            if (!string.IsNullOrEmpty(task.AssignedAgent))
            {
                await _agentService.UpdateAgentStatusAsync(task.AssignedAgent, new UpdateAgentStatusRequest
                {
                    Status = AgentStatus.Available,
                    CurrentTask = null,
                    LastSeen = DateTime.UtcNow
                });
            }
        }

        if (expiredTasks.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} tasks", expiredTasks.Count);
        }
    }
}

// Supporting classes
public class TaskQueryParameters
{
    public TaskStatus? Status { get; set; }
    public string? Type { get; set; }
    public string? AssignedAgent { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;
    public int? Skip { get; set; }
    public int? Take { get; set; }
}

public class TaskStatistics
{
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int CancelledTasks { get; set; }
    public int ExpiredTasks { get; set; }
    public double AverageProcessingTime { get; set; }
    public Dictionary<string, int> TasksByType { get; set; } = new();
    public Dictionary<string, int> TasksByPriority { get; set; } = new();
}
```

### 4. Task Controller Implementation

**File: `src/AISwarm.A2A/Controllers/TaskController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using AISwarm.A2A.Models;
using AISwarm.A2A.Services;
using System.ComponentModel.DataAnnotations;

namespace AISwarm.A2A.Controllers;

[ApiController]
[Route("a2a/tasks")]
[Produces("application/json")]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TaskController> _logger;

    public TaskController(ITaskService taskService, ILogger<TaskController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new task
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<A2ATask>), 201)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<ActionResult<ApiResponse<A2ATask>>> CreateTask([FromBody] CreateTaskRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid request data"));
            }

            var task = await _taskService.CreateTaskAsync(request);
            
            _logger.LogInformation("Created task {TaskId} for type {TaskType}", task.Id, task.Type);
            
            return CreatedAtAction(
                nameof(GetTask), 
                new { id = task.Id }, 
                ApiResponse<A2ATask>.Ok(task, new Dictionary<string, object>
                {
                    ["location"] = Url.Action(nameof(GetTask), new { id = task.Id }) ?? ""
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create task");
            return BadRequest(ApiResponse.Fail($"Failed to create task: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get a specific task by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<A2ATask>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<ActionResult<ApiResponse<A2ATask>>> GetTask([FromRoute] string id)
    {
        var task = await _taskService.GetTaskAsync(id);
        
        if (task == null)
        {
            return NotFound(ApiResponse.Fail($"Task {id} not found"));
        }

        return Ok(ApiResponse<A2ATask>.Ok(task));
    }

    /// <summary>
    /// Get all tasks with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<A2ATask>>), 200)]
    public async Task<ActionResult<ApiResponse<IEnumerable<A2ATask>>>> GetTasks(
        [FromQuery] TaskStatus? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? assignedAgent = null,
        [FromQuery] DateTime? createdAfter = null,
        [FromQuery] DateTime? createdBefore = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            var parameters = new TaskQueryParameters
            {
                Status = status,
                Type = type,
                AssignedAgent = assignedAgent,
                CreatedAfter = createdAfter,
                CreatedBefore = createdBefore,
                SortBy = sortBy,
                SortDescending = sortDescending,
                Skip = skip,
                Take = Math.Min(take, 100) // Cap at 100 items
            };

            var tasks = await _taskService.GetTasksAsync(parameters);

            return Ok(ApiResponse<IEnumerable<A2ATask>>.Ok(tasks, new Dictionary<string, object>
            {
                ["query_parameters"] = parameters,
                ["result_count"] = tasks.Count()
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tasks");
            return BadRequest(ApiResponse.Fail($"Failed to get tasks: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get pending tasks available for processing
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<A2ATask>>), 200)]
    public async Task<ActionResult<ApiResponse<IEnumerable<A2ATask>>>> GetPendingTasks(
        [FromQuery] string? agentName = null)
    {
        try
        {
            var tasks = await _taskService.GetPendingTasksAsync(agentName);

            return Ok(ApiResponse<IEnumerable<A2ATask>>.Ok(tasks, new Dictionary<string, object>
            {
                ["agent_filter"] = agentName ?? "none",
                ["available_tasks"] = tasks.Count()
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending tasks");
            return BadRequest(ApiResponse.Fail($"Failed to get pending tasks: {ex.Message}"));
        }
    }

    /// <summary>
    /// Claim a pending task for processing
    /// </summary>
    [HttpPost("{id}/claim")]
    [ProducesResponseType(typeof(ApiResponse<A2ATask>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<ActionResult<ApiResponse<A2ATask>>> ClaimTask(
        [FromRoute] string id, 
        [FromBody] TaskClaimRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid claim request"));
            }

            var task = await _taskService.ClaimTaskAsync(id, request);
            
            if (task == null)
            {
                return BadRequest(ApiResponse.Fail($"Cannot claim task {id}. Task may not exist, not be pending, or agent may lack required capabilities."));
            }

            _logger.LogInformation("Task {TaskId} claimed by agent {AgentId}", id, request.AgentId);

            return Ok(ApiResponse<A2ATask>.Ok(task, new Dictionary<string, object>
            {
                ["claimed_by"] = request.AgentId,
                ["claimed_at"] = task.ClaimedAt ?? DateTime.UtcNow
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to claim task {TaskId}", id);
            return BadRequest(ApiResponse.Fail($"Failed to claim task: {ex.Message}"));
        }
    }

    /// <summary>
    /// Mark a task as completed
    /// </summary>
    [HttpPost("{id}/complete")]
    [ProducesResponseType(typeof(ApiResponse<A2ATask>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<ActionResult<ApiResponse<A2ATask>>> CompleteTask(
        [FromRoute] string id, 
        [FromBody] TaskCompleteRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid completion request"));
            }

            var task = await _taskService.CompleteTaskAsync(id, request);
            
            if (task == null)
            {
                return BadRequest(ApiResponse.Fail($"Cannot complete task {id}. Task may not exist, not be in progress, or not assigned to the specified agent."));
            }

            _logger.LogInformation("Task {TaskId} completed by agent {AgentId}", id, request.CompletedBy);

            return Ok(ApiResponse<A2ATask>.Ok(task, new Dictionary<string, object>
            {
                ["completed_by"] = request.CompletedBy,
                ["completed_at"] = task.CompletedAt ?? DateTime.UtcNow,
                ["processing_time"] = task.ProcessingTime?.TotalSeconds ?? 0
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete task {TaskId}", id);
            return BadRequest(ApiResponse.Fail($"Failed to complete task: {ex.Message}"));
        }
    }

    /// <summary>
    /// Mark a task as failed
    /// </summary>
    [HttpPost("{id}/fail")]
    [ProducesResponseType(typeof(ApiResponse<A2ATask>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<ActionResult<ApiResponse<A2ATask>>> FailTask(
        [FromRoute] string id, 
        [FromBody] TaskFailRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid failure request"));
            }

            var task = await _taskService.FailTaskAsync(id, request);
            
            if (task == null)
            {
                return BadRequest(ApiResponse.Fail($"Cannot fail task {id}. Task may not exist, not be in progress, or not assigned to the specified agent."));
            }

            _logger.LogWarning("Task {TaskId} failed by agent {AgentId}: {Error}", id, request.FailedBy, request.Error);

            return Ok(ApiResponse<A2ATask>.Ok(task, new Dictionary<string, object>
            {
                ["failed_by"] = request.FailedBy,
                ["failed_at"] = DateTime.UtcNow,
                ["retry_count"] = task.RetryCount,
                ["will_retry"] = task.Status == TaskStatus.Pending
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process failure for task {TaskId}", id);
            return BadRequest(ApiResponse.Fail($"Failed to process task failure: {ex.Message}"));
        }
    }

    /// <summary>
    /// Cancel a task
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<A2ATask>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<ActionResult<ApiResponse<A2ATask>>> CancelTask(
        [FromRoute] string id,
        [FromBody] CancelTaskRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Invalid cancellation request"));
            }

            var task = await _taskService.CancelTaskAsync(id, request.CancelledBy);
            
            if (task == null)
            {
                return NotFound(ApiResponse.Fail($"Task {id} not found or cannot be cancelled"));
            }

            _logger.LogInformation("Task {TaskId} cancelled by {CancelledBy}", id, request.CancelledBy);

            return Ok(ApiResponse<A2ATask>.Ok(task, new Dictionary<string, object>
            {
                ["cancelled_by"] = request.CancelledBy,
                ["cancelled_at"] = DateTime.UtcNow
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel task {TaskId}", id);
            return BadRequest(ApiResponse.Fail($"Failed to cancel task: {ex.Message}"));
        }
    }

    /// <summary>
    /// Delete a task
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<ActionResult<ApiResponse>> DeleteTask([FromRoute] string id)
    {
        try
        {
            var deleted = await _taskService.DeleteTaskAsync(id);
            
            if (!deleted)
            {
                return NotFound(ApiResponse.Fail($"Task {id} not found"));
            }

            _logger.LogInformation("Task {TaskId} deleted", id);

            return Ok(ApiResponse.Ok(new Dictionary<string, object>
            {
                ["deleted_task"] = id,
                ["deleted_at"] = DateTime.UtcNow
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete task {TaskId}", id);
            return BadRequest(ApiResponse.Fail($"Failed to delete task: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get task statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<TaskStatistics>), 200)]
    public async Task<ActionResult<ApiResponse<TaskStatistics>>> GetTaskStatistics()
    {
        try
        {
            var statistics = await _taskService.GetTaskStatisticsAsync();

            return Ok(ApiResponse<TaskStatistics>.Ok(statistics, new Dictionary<string, object>
            {
                ["generated_at"] = DateTime.UtcNow
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task statistics");
            return BadRequest(ApiResponse.Fail($"Failed to get task statistics: {ex.Message}"));
        }
    }
}

// Supporting request models
public class CancelTaskRequest
{
    [Required]
    public string CancelledBy { get; set; } = string.Empty;
    
    public string? Reason { get; set; }
}
```

### 2. A2A Configuration Options

**File: `src/AISwarm.A2A/Configuration/A2AOptions.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace AISwarm.A2A.Configuration;

public class A2AOptions
{
    public const string SectionName = "A2A";
    
    /// <summary>
    /// Base path for A2A endpoints (default: "/a2a")
    /// </summary>
    public string BasePath { get; set; } = "/a2a";
    
    /// <summary>
    /// Enable A2A server functionality
    /// </summary>
    public bool EnableServer { get; set; } = true;
    
    /// <summary>
    /// Task cleanup interval in minutes (default: 60)
    /// </summary>
    public int TaskCleanupIntervalMinutes { get; set; } = 60;
    
    /// <summary>
    /// Default task timeout in minutes (default: 30)
    /// </summary>
    public int DefaultTaskTimeoutMinutes { get; set; } = 30;
    
    /// <summary>
    /// Maximum number of retry attempts for failed tasks (default: 3)
    /// </summary>
    public int MaxTaskRetries { get; set; } = 3;
    
    /// <summary>
    /// Agent heartbeat timeout in minutes (default: 5)
    /// </summary>
    public int AgentHeartbeatTimeoutMinutes { get; set; } = 5;
    
    /// <summary>
    /// Database connection string for A2A data
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = "Data Source=a2a.db";
    
    /// <summary>
    /// Server information for agent card
    /// </summary>
    public A2AServerInfo ServerInfo { get; set; } = new();
    
    /// <summary>
    /// Security settings
    /// </summary>
    public A2ASecurityOptions Security { get; set; } = new();
    
    /// <summary>
    /// Performance settings
    /// </summary>
    public A2APerformanceOptions Performance { get; set; } = new();
}

public class A2AServerInfo
{
    public string Name { get; set; } = "AISwarm A2A Server";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "AISwarm Agent-to-Agent Communication Server";
    public List<string> DefaultCapabilities { get; set; } = new() { "task-management", "agent-coordination" };
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class A2ASecurityOptions
{
    /// <summary>
    /// Enable API key authentication
    /// </summary>
    public bool EnableApiKeys { get; set; } = false;
    
    /// <summary>
    /// Require HTTPS for A2A endpoints
    /// </summary>
    public bool RequireHttps { get; set; } = false;
    
    /// <summary>
    /// Enable CORS for A2A endpoints
    /// </summary>
    public bool EnableCors { get; set; } = true;
    
    /// <summary>
    /// Allowed origins for CORS
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new() { "*" };
    
    /// <summary>
    /// Rate limiting settings
    /// </summary>
    public A2ARateLimitOptions RateLimit { get; set; } = new();
}

public class A2ARateLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int RequestsPerMinute { get; set; } = 100;
    public int RequestsPerHour { get; set; } = 1000;
}

public class A2APerformanceOptions
{
    /// <summary>
    /// Maximum number of concurrent tasks per agent
    /// </summary>
    public int MaxConcurrentTasksPerAgent { get; set; } = 1;
    
    /// <summary>
    /// Task query page size limit
    /// </summary>
    public int MaxQueryPageSize { get; set; } = 100;
    
    /// <summary>
    /// Enable query result caching
    /// </summary>
    public bool EnableQueryCaching { get; set; } = true;
    
    /// <summary>
    /// Cache expiration in minutes
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 5;
}

public static class A2AConstants
{
    public const string AgentCardPath = "/.well-known/agent-card.json";
    public const string HealthCheckPath = "/health";
    public const string TasksPath = "/tasks";
    public const string AgentsPath = "/agents";
    
    public static class Headers
    {
        public const string AgentName = "X-Agent-Name";
        public const string AgentVersion = "X-Agent-Version";
        public const string RequestId = "X-Request-Id";
        public const string ApiKey = "X-API-Key";
    }
    
    public static class ClaimTypes
    {
        public const string AgentId = "agent_id";
        public const string AgentName = "agent_name";
        public const string Capabilities = "capabilities";
    }
}
```

This comprehensive set of code samples provides everything needed to implement a production-ready A2A server with:

- Complete data models following A2A protocol standards
- Full CRUD operations for tasks and agents
- Proper error handling and logging
- Task claiming and assignment logic
- Agent capability matching
- Task retry and failure handling
- Performance metrics and statistics
- RESTful API design with OpenAPI documentation
- Comprehensive validation and security

The implementation is ready for integration with the existing AISwarm MCP server and can be extended with additional features like vector embeddings, advanced querying, and real-time notifications.

## 🔧 A2A Library Integration with MCP Server

### Service Registration Extensions

**File: `src/AISwarm.A2A/Extensions/ServiceCollectionExtensions.cs`**

```csharp
using AISwarm.A2A.Configuration;
using AISwarm.A2A.Data;
using AISwarm.A2A.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AISwarm.A2A.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds A2A services to the service collection
    /// </summary>
    public static IServiceCollection AddA2AServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure A2A options
        services.Configure<A2AOptions>(configuration.GetSection(A2AOptions.SectionName));
        
        // Add database context
        services.AddDbContext<A2ADbContext>((serviceProvider, options) =>
        {
            var a2aOptions = serviceProvider.GetRequiredService<IOptions<A2AOptions>>().Value;
            options.UseSqlite(a2aOptions.ConnectionString);
        });
        
        // Add core services
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAgentService, AgentService>();
        
        // Add background services
        services.AddHostedService<TaskCleanupService>();
        services.AddHostedService<AgentHeartbeatService>();
        
        // Add memory cache for performance
        services.AddMemoryCache();
        
        return services;
    }
    
    /// <summary>
    /// Adds A2A controllers and API endpoints
    /// </summary>
    public static IServiceCollection AddA2AControllers(this IServiceCollection services)
    {
        services.AddControllers();
        return services;
    }
    
    /// <summary>
    /// Adds A2A with all required dependencies
    /// </summary>
    public static IServiceCollection AddA2A(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddA2AServices(configuration)
            .AddA2AControllers();
    }
}
```

### MCP Tools for A2A Integration

**File: `src/AISwarm.Server/McpTools/A2A/CreateA2ATaskTool.cs`**

```csharp
using AISwarm.A2A.Models;
using AISwarm.A2A.Services;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools.A2A;

[McpServerToolType]
public class CreateA2ATaskTool
{
    private readonly ITaskService _taskService;
    private readonly ILogger<CreateA2ATaskTool> _logger;

    public CreateA2ATaskTool(ITaskService taskService, ILogger<CreateA2ATaskTool> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [McpServerTool("create_a2a_task", "Create a new A2A task for agent processing")]
    public async Task<string> CreateTaskAsync(
        [McpServerToolParam("type", "Type of task (e.g., 'code-generation', 'analysis', 'review')")]
        string type,
        [McpServerToolParam("description", "Detailed description of what the task should accomplish")]
        string description,
        [McpServerToolParam("priority", "Task priority: Low, Normal, High, or Critical")]
        string priority = "Normal",
        [McpServerToolParam("capabilities", "Required agent capabilities (comma-separated)")]
        string? capabilities = null,
        [McpServerToolParam("input", "Task input data as JSON string")]
        string? input = null)
    {
        try
        {
            var taskRequest = new CreateTaskRequest
            {
                Type = type,
                Description = description,
                Priority = Enum.TryParse<TaskPriority>(priority, true, out var parsedPriority) 
                    ? parsedPriority 
                    : TaskPriority.Normal,
                RequiredCapabilities = capabilities?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim()).ToList() ?? new List<string>(),
                Input = string.IsNullOrEmpty(input) 
                    ? new Dictionary<string, object>()
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(input) ?? new()
            };

            var task = await _taskService.CreateTaskAsync(taskRequest);
            
            _logger.LogInformation("Created A2A task {TaskId} via MCP tool", task.Id);
            
            return $"Successfully created A2A task:\n" +
                   $"ID: {task.Id}\n" +
                   $"Type: {task.Type}\n" +
                   $"Description: {task.Description}\n" +
                   $"Priority: {task.Priority}\n" +
                   $"Status: {task.Status}\n" +
                   $"Created: {task.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create A2A task via MCP tool");
            return $"Failed to create A2A task: {ex.Message}";
        }
    }
}
```

**File: `src/AISwarm.Server/McpTools/A2A/LaunchGeminiAgentTool.cs`**

```csharp
using ModelContextProtocol.Server;
using System.Diagnostics;

namespace AISwarm.Server.McpTools.A2A;

[McpServerToolType]
public class LaunchGeminiAgentTool
{
    private readonly ILogger<LaunchGeminiAgentTool> _logger;
    private static readonly Dictionary<string, Process> _runningAgents = new();

    public LaunchGeminiAgentTool(ILogger<LaunchGeminiAgentTool> logger)
    {
        _logger = logger;
    }

    [McpServerTool("launch_gemini_agent", "Launch a Gemini CLI agent in A2A mode")]
    public async Task<string> LaunchAgentAsync(
        [McpServerToolParam("agent_name", "Unique name for the agent instance")]
        string agentName,
        [McpServerToolParam("capabilities", "Agent capabilities (comma-separated)")]
        string capabilities = "code-generation,analysis",
        [McpServerToolParam("server_url", "A2A server URL")]
        string? serverUrl = "http://localhost:5000",
        [McpServerToolParam("auto_confirm", "Enable --yolo mode for automatic file creation")]
        bool autoConfirm = false)
    {
        try
        {
            // Check if agent is already running
            if (_runningAgents.ContainsKey(agentName))
            {
                var existingProcess = _runningAgents[agentName];
                if (!existingProcess.HasExited)
                {
                    return $"Agent '{agentName}' is already running (PID: {existingProcess.Id})";
                }
                else
                {
                    _runningAgents.Remove(agentName);
                }
            }

            // Build command arguments
            var args = new List<string>
            {
                "external/gemini-cli/packages/cli/dist/index.js",
                "a2a",
                "--server", serverUrl,
                "--agent-name", agentName,
                "--capabilities", capabilities
            };

            if (autoConfirm)
            {
                args.Add("--yolo");
            }

            // Start the agent process
            var processInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(processInfo);
            if (process == null)
            {
                return "Failed to start Gemini agent process";
            }

            _runningAgents[agentName] = process;

            _logger.LogInformation("Launched Gemini agent {AgentName} with PID {ProcessId}", agentName, process.Id);

            return $"Successfully launched Gemini agent:\n" +
                   $"Name: {agentName}\n" +
                   $"PID: {process.Id}\n" +
                   $"Capabilities: {capabilities}\n" +
                   $"Server: {serverUrl}\n" +
                   $"Auto-confirm: {autoConfirm}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch Gemini agent {AgentName}", agentName);
            return $"Failed to launch Gemini agent: {ex.Message}";
        }
    }

    [McpServerTool("list_gemini_agents", "List all running Gemini CLI agents")]
    public async Task<string> ListAgentsAsync()
    {
        try
        {
            var activeAgents = _runningAgents
                .Where(kvp => !kvp.Value.HasExited)
                .ToList();

            if (!activeAgents.Any())
            {
                return "No running Gemini agents found";
            }

            var result = "Running Gemini Agents:\n";
            foreach (var (agentName, process) in activeAgents)
            {
                result += $"- {agentName} (PID: {process.Id})\n";
            }

            return result.TrimEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Gemini agents");
            return $"Failed to list agents: {ex.Message}";
        }
    }
}
```

### Updated MCP Server Configuration

**File: `src/AISwarm.Server/Program.cs` (Updated)**

```csharp
using AISwarm.A2A.Extensions;
using AISwarm.Infrastructure;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add MCP services
builder.Services.AddMcpServer();
builder.Services.AddAISwarmInfrastructure(builder.Configuration);

// Add A2A services as library
builder.Services.AddA2A(builder.Configuration);

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure MCP server
app.UseMcpServer();

// Map A2A endpoints (integrated into MCP server)
app.MapControllers();

// Agent card endpoint
app.MapGet("/.well-known/agent-card.json", async context =>
{
    var agentCard = new
    {
        name = "AISwarm Unified Server",
        version = "1.0.0",
        description = "AISwarm MCP Server with A2A Protocol Support",
        capabilities = new[] { "mcp-tools", "a2a-protocol", "task-management", "agent-coordination" },
        endpoints = new
        {
            mcp = "/mcp",
            a2a_tasks = "/a2a/tasks",
            a2a_agents = "/a2a/agents",
            health = "/health"
        }
    };
    await context.Response.WriteAsJsonAsync(agentCard);
});

// Configure health checks
app.MapHealthChecks("/health");

app.Run();
```

### Configuration Update

**File: `src/AISwarm.Server/appsettings.json` (A2A section added)**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Mcp": {
    "Transport": "stdio",
    "HttpTransport": {
      "Port": 5000,
      "Host": "localhost"
    }
  },
  "A2A": {
    "SectionName": "A2A",
    "BasePath": "/a2a",
    "EnableServer": true,
    "TaskCleanupIntervalMinutes": 60,
    "DefaultTaskTimeoutMinutes": 30,
    "MaxTaskRetries": 3,
    "AgentHeartbeatTimeoutMinutes": 5,
    "ConnectionString": "Data Source=a2a.db"
  }
}
```

## 🚀 Benefits of A2A Library Approach

### **1. Unified Architecture**

- ✅ Single server binary with both MCP and A2A capabilities
- ✅ Shared database, logging, and configuration
- ✅ Reduced deployment complexity
- ✅ Consistent monitoring and health checks

### **2. MCP Tool Integration**

- ✅ Native MCP tools for A2A operations
- ✅ `create_a2a_task` tool for task creation
- ✅ `launch_gemini_agent` tool for agent management
- ✅ `list_gemini_agents` tool for monitoring

### **3. Developer Experience**

- ✅ Familiar dependency injection patterns
- ✅ Standard ASP.NET Core controllers
- ✅ Consistent error handling and validation
- ✅ Rich configuration options

### **4. Scalability**

- ✅ Shared connection pooling
- ✅ Efficient resource utilization
- ✅ Background task processing
- ✅ Built-in caching support

This library-based approach provides seamless integration between MCP and A2A protocols while maintaining clean architectural boundaries and enabling powerful agent coordination capabilities.
