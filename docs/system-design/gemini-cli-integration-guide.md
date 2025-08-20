# Gemini CLI Integration Guide

## Overview

This guide provides comprehensive implementation patterns for integrating AISwarm with the official `google-gemini/gemini-cli` tool using its advanced capabilities including plugin event hooks, IDE integration, and extended Tools API.

## Quick Start Configuration

### Basic Settings.json Setup

```json
{
  "servers": {
    "aiswarm": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/AISwarm.Server"]
    }
  },
  "extensions": {
    "aiswarm-hooks": {
      "beforeRequest": true,
      "afterResponse": true,
      "configChanged": true
    }
  },
  "ide": {
    "vscode": {
      "workspaceAware": true,
      "diffPreview": true,
      "customCommands": true
    }
  }
}
```

### AgentLauncher Configuration Template

```csharp
// Services/GeminiConfigurationService.cs
public class GeminiConfigurationService
{
    public async Task<string> GenerateAgentConfigAsync(AgentPersona persona, string workspacePath)
    {
        var config = new
        {
            servers = new
            {
                aiswarm = new
                {
                    command = "dotnet",
                    args = new[] { "run", "--project", "path/to/AISwarm.Server" }
                }
            },
            extensions = new
            {
                aiswarm_hooks = new
                {
                    beforeRequest = true,
                    afterResponse = true,
                    configChanged = true
                }
            },
            ide = new
            {
                vscode = new
                {
                    workspaceAware = true,
                    workspacePath = workspacePath,
                    diffPreview = true,
                    customCommands = true
                }
            },
            agent = new
            {
                id = Guid.NewGuid().ToString(),
                persona = persona.Type,
                instructions = persona.Instructions,
                coordination = new
                {
                    heartbeatInterval = TimeSpan.FromMinutes(1),
                    taskClaimTimeout = TimeSpan.FromMinutes(30),
                    maxConcurrentTasks = 1
                }
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }
}
```

## Plugin Event Hooks Implementation

### 1. beforeRequest Hook Patterns

#### Context Injection Pattern
```csharp
public class CoordinationContextInjector : IBeforeRequestHook
{
    private readonly IAgentContextService _contextService;
    
    public async Task<RequestModification> ProcessAsync(BeforeRequestEvent requestEvent)
    {
        var agentContext = await _contextService.GetCurrentContextAsync();
        
        // Inject coordination state into every request
        var enhancedPrompt = $@"{requestEvent.Request.Prompt}

AGENT COORDINATION CONTEXT:
- Agent ID: {agentContext.AgentId}
- Current Task: {agentContext.CurrentTask?.Title ?? "None"}
- Team Status: {await GetTeamStatusSummaryAsync()}
- Available Tools: @aiswarm claim_task, @aiswarm report_progress, @aiswarm help_request

Remember to use coordination tools when appropriate and keep team members informed of your progress.";

        return new RequestModification
        {
            ModifiedPrompt = enhancedPrompt,
            AdditionalMetadata = new Dictionary<string, object>
            {
                ["agent_id"] = agentContext.AgentId,
                ["task_id"] = agentContext.CurrentTask?.Id,
                ["coordination_session"] = agentContext.SessionId
            }
        };
    }
}
```

#### Task Context Enrichment Pattern
```csharp
public class TaskContextEnrichmentHook : IBeforeRequestHook
{
    public async Task<RequestModification> ProcessAsync(BeforeRequestEvent requestEvent)
    {
        var currentTask = await _taskService.GetCurrentTaskAsync();
        if (currentTask == null) return RequestModification.NoChange;
        
        // Add task-specific context and constraints
        var taskEnrichedPrompt = $@"{requestEvent.Request.Prompt}

CURRENT TASK CONTEXT:
- Task: {currentTask.Title}
- Description: {currentTask.Description}
- Status: {currentTask.Status}
- Progress: {currentTask.ProgressPercentage}%
- Deadline: {currentTask.Deadline:yyyy-MM-dd HH:mm}
- Dependencies: {string.Join(", ", currentTask.Dependencies)}

Focus your response on advancing this specific task. Use @aiswarm report_progress when you make meaningful progress.";

        return new RequestModification { ModifiedPrompt = taskEnrichedPrompt };
    }
}
```

### 2. afterResponse Hook Patterns

#### Coordination Signal Extraction Pattern
```csharp
public class CoordinationSignalExtractor : IAfterResponseHook
{
    private readonly ICoordinationEventService _eventService;
    
    public async Task ProcessAsync(AfterResponseEvent responseEvent)
    {
        var signals = ExtractCoordinationSignals(responseEvent.Response.Text);
        
        foreach (var signal in signals)
        {
            await ProcessCoordinationSignal(signal);
        }
    }
    
    private List<CoordinationSignal> ExtractCoordinationSignals(string responseText)
    {
        var signals = new List<CoordinationSignal>();
        
        // Pattern: "I need help with X"
        var helpPattern = @"I need help with (.+?)(?:\.|$)";
        var helpMatches = Regex.Matches(responseText, helpPattern, RegexOptions.IgnoreCase);
        foreach (Match match in helpMatches)
        {
            signals.Add(new CoordinationSignal
            {
                Type = SignalType.HelpRequest,
                Content = match.Groups[1].Value.Trim(),
                Urgency = DetermineUrgency(match.Value)
            });
        }
        
        // Pattern: "Task completed: X"
        var completionPattern = @"Task completed:?\s*(.+?)(?:\.|$)";
        var completionMatches = Regex.Matches(responseText, completionPattern, RegexOptions.IgnoreCase);
        foreach (Match match in completionMatches)
        {
            signals.Add(new CoordinationSignal
            {
                Type = SignalType.TaskCompletion,
                Content = match.Groups[1].Value.Trim()
            });
        }
        
        // Pattern: Progress indicators "Progress: 75%" or "75% complete"
        var progressPattern = @"(?:Progress:?\s*|(\d+)%\s*complete)";
        var progressMatches = Regex.Matches(responseText, progressPattern, RegexOptions.IgnoreCase);
        foreach (Match match in progressMatches)
        {
            if (int.TryParse(match.Groups[1].Value, out int percentage))
            {
                signals.Add(new CoordinationSignal
                {
                    Type = SignalType.ProgressUpdate,
                    Content = percentage.ToString()
                });
            }
        }
        
        return signals;
    }
}
```

#### Automatic Tool Suggestion Pattern
```csharp
public class AutomaticToolSuggestionHook : IAfterResponseHook
{
    public async Task ProcessAsync(AfterResponseEvent responseEvent)
    {
        var response = responseEvent.Response.Text;
        var suggestions = new List<string>();
        
        // Suggest coordination tools based on response content
        if (ContainsTaskCompletion(response))
        {
            suggestions.Add("Consider using @aiswarm report_completion to notify team members");
        }
        
        if (ContainsQuestionOrUncertainty(response))
        {
            suggestions.Add("Consider using @aiswarm help_request if you need team assistance");
        }
        
        if (ContainsProgressIndicators(response))
        {
            suggestions.Add("Consider using @aiswarm report_progress to update task status");
        }
        
        if (suggestions.Any())
        {
            var enhancedResponse = $@"{response}

COORDINATION SUGGESTIONS:
{string.Join("\n", suggestions.Select(s => $"• {s}"))}";
            
            // Update the response with suggestions
            responseEvent.Response.Text = enhancedResponse;
        }
    }
}
```

### 3. configChanged Hook Patterns

#### Dynamic Team Reconfiguration Pattern
```csharp
public class DynamicTeamReconfigurationHook : IConfigChangedHook
{
    public async Task ProcessAsync(ConfigChangedEvent configEvent)
    {
        if (configEvent.ChangedSection == "team")
        {
            await HandleTeamConfigurationChange(configEvent);
        }
        else if (configEvent.ChangedSection == "tasks")
        {
            await HandleTaskConfigurationChange(configEvent);
        }
        else if (configEvent.ChangedSection == "coordination")
        {
            await HandleCoordinationConfigurationChange(configEvent);
        }
    }
    
    private async Task HandleTeamConfigurationChange(ConfigChangedEvent configEvent)
    {
        // Handle changes to team structure, roles, permissions
        var newTeamConfig = configEvent.NewConfiguration.GetSection("team");
        
        // Update agent roles and permissions
        foreach (var agentConfig in newTeamConfig.GetChildren())
        {
            var agentId = agentConfig.Key;
            var newRole = agentConfig["role"];
            var newPermissions = agentConfig.GetSection("permissions").Get<string[]>();
            
            await _teamService.UpdateAgentRoleAsync(agentId, newRole, newPermissions);
        }
        
        // Broadcast team structure changes to all agents
        await _coordinationService.BroadcastTeamUpdateAsync(newTeamConfig);
    }
}
```

## VS Code Integration Patterns

### 1. Workspace-Aware Context Loading

#### Smart Context Aggregation Pattern
```csharp
public class SmartWorkspaceContextProvider : IWorkspaceContextProvider
{
    public async Task<WorkspaceContext> GetEnhancedContextAsync()
    {
        var context = new WorkspaceContext();
        
        // Get current VS Code state
        context.CurrentFile = await _vscodeService.GetActiveFileAsync();
        context.Selection = await _vscodeService.GetCurrentSelectionAsync();
        context.CursorPosition = await _vscodeService.GetCursorPositionAsync();
        
        // Get recent activity
        context.RecentFiles = await _vscodeService.GetRecentFilesAsync(limit: 10);
        context.RecentEdits = await _vscodeService.GetRecentEditsAsync(TimeSpan.FromHours(1));
        
        // Get project context
        context.GitStatus = await _gitService.GetStatusAsync();
        context.BranchName = await _gitService.GetCurrentBranchAsync();
        context.RecentCommits = await _gitService.GetRecentCommitsAsync(limit: 5);
        
        // Get team context
        context.TeamMembers = await _teamService.GetActiveTeamMembersAsync();
        context.TeamActivity = await _teamService.GetRecentActivityAsync(TimeSpan.FromMinutes(30));
        
        return context;
    }
    
    public async Task<string> GenerateContextualPromptAsync(string basePrompt)
    {
        var context = await GetEnhancedContextAsync();
        
        return $@"{basePrompt}

ENHANCED WORKSPACE CONTEXT:

Current State:
- File: {context.CurrentFile?.Path ?? "None"}
- Selection: {context.Selection ?? "None"}
- Cursor: {context.CursorPosition}

Recent Activity:
- Files: {string.Join(", ", context.RecentFiles.Take(3).Select(f => Path.GetFileName(f)))}
- Last Edit: {context.RecentEdits.FirstOrDefault()?.Timestamp.ToString("HH:mm") ?? "None"}

Project Status:
- Branch: {context.BranchName}
- Git Status: {context.GitStatus}
- Last Commit: {context.RecentCommits.FirstOrDefault()?.Message ?? "None"}

Team Context:
- Active Members: {context.TeamMembers.Count}
- Recent Activity: {context.TeamActivity.Count} events in last 30 minutes

Consider this rich context when making decisions and coordinating with team members.";
    }
}
```

### 2. Native Diff-View Integration

#### Collaborative Change Management Pattern
```csharp
[McpServerToolType]
public static class CollaborativeChangeTools
{
    [McpServerTool]
    [Description("Propose changes with team review via VS Code diff view")]
    public static async Task<string> ProposeChanges(
        IDiffCollaborationService diffService,
        [Description("File path")] string filePath,
        [Description("Proposed changes")] string newContent,
        [Description("Change description")] string description,
        [Description("Reviewers (comma-separated agent IDs)")] string reviewers = null)
    {
        var proposal = new ChangeProposal
        {
            FilePath = filePath,
            NewContent = newContent,
            Description = description,
            ProposedBy = await diffService.GetCurrentAgentIdAsync(),
            Reviewers = reviewers?.Split(',').Select(r => r.Trim()).ToList() ?? new List<string>(),
            CreatedAt = DateTime.UtcNow
        };
        
        var proposalId = await diffService.CreateProposalAsync(proposal);
        
        // Show diff in VS Code
        await diffService.ShowDiffPreviewAsync(proposalId);
        
        // Notify reviewers
        if (proposal.Reviewers.Any())
        {
            await diffService.NotifyReviewersAsync(proposalId, proposal.Reviewers);
        }
        
        return $"Change proposal {proposalId} created. Reviewers notified: {string.Join(", ", proposal.Reviewers)}";
    }
    
    [McpServerTool]
    [Description("Review and respond to change proposals")]
    public static async Task<string> ReviewChanges(
        IDiffCollaborationService diffService,
        [Description("Proposal ID to review")] string proposalId,
        [Description("Review decision: approve, reject, request_changes")] string decision,
        [Description("Review comments")] string comments = null)
    {
        var review = new ChangeReview
        {
            ProposalId = proposalId,
            ReviewerId = await diffService.GetCurrentAgentIdAsync(),
            Decision = Enum.Parse<ReviewDecision>(decision, true),
            Comments = comments,
            ReviewedAt = DateTime.UtcNow
        };
        
        await diffService.SubmitReviewAsync(review);
        
        // If approved by all reviewers, auto-apply changes
        var proposal = await diffService.GetProposalAsync(proposalId);
        if (await diffService.IsFullyApprovedAsync(proposalId))
        {
            await diffService.ApplyChangesAsync(proposalId);
            return $"Changes approved and applied to {proposal.FilePath}";
        }
        
        return $"Review submitted for proposal {proposalId}. Status: {decision}";
    }
}
```

## Extended Tools API Integration

### 1. Research and Information Gathering

#### Multi-Source Research Pattern
```csharp
public class MultiSourceResearchOrchestrator
{
    public async Task<ResearchResult> ConductResearchAsync(string query, ResearchScope scope)
    {
        var result = new ResearchResult { Query = query, StartedAt = DateTime.UtcNow };
        
        // 1. Google Search for external information
        if (scope.HasFlag(ResearchScope.External))
        {
            var searchResults = await _geminiTools.GoogleSearchAsync(new GoogleSearchRequest
            {
                Query = query,
                MaxResults = 5,
                IncludeSnippets = true
            });
            result.ExternalSources = searchResults;
        }
        
        // 2. Local filesystem search for project documentation
        if (scope.HasFlag(ResearchScope.Local))
        {
            var localResults = await _geminiTools.FilesystemSearchAsync(new FilesystemSearchRequest
            {
                Paths = new[] { "./docs", "./src", "./tests" },
                Query = query,
                FileTypes = new[] { "*.md", "*.cs", "*.txt" },
                MaxResults = 10
            });
            result.LocalSources = localResults;
        }
        
        // 3. Memory system for previous research and decisions
        if (scope.HasFlag(ResearchScope.Memory))
        {
            var memoryResults = await _geminiTools.MemoryRecallAsync(new MemoryRecallRequest
            {
                Topic = query,
                MaxResults = 5,
                IncludeContext = true
            });
            result.MemorySources = memoryResults;
        }
        
        // 4. Team knowledge via coordination system
        if (scope.HasFlag(ResearchScope.Team))
        {
            var teamKnowledge = await _coordinationService.QueryTeamKnowledgeAsync(query);
            result.TeamSources = teamKnowledge;
        }
        
        // 5. Synthesize and summarize findings
        result.Summary = await SynthesizeFindings(result);
        result.CompletedAt = DateTime.UtcNow;
        
        // 6. Store in memory for future reference
        await _geminiTools.MemoryStoreAsync(new MemoryStoreRequest
        {
            Topic = query,
            Content = result.Summary,
            Metadata = new { sources = result.GetSourceCount(), scope = scope.ToString() }
        });
        
        return result;
    }
}
```

### 2. Intelligent File Management

#### Smart File Operations Pattern
```csharp
public class SmartFileOperations
{
    public async Task<FileOperationResult> CreateFileWithContextAsync(CreateFileRequest request)
    {
        // 1. Analyze directory structure for placement
        var directoryAnalysis = await _geminiTools.FilesystemAnalyzeAsync(new DirectoryAnalysisRequest
        {
            Path = Path.GetDirectoryName(request.FilePath),
            AnalysisType = "structure_and_patterns"
        });
        
        // 2. Check for similar files and patterns
        var similarFiles = await _geminiTools.FilesystemSearchAsync(new FilesystemSearchRequest
        {
            Paths = new[] { "./src" },
            Query = $"similar to {Path.GetFileName(request.FilePath)}",
            FileTypes = new[] { $"*{Path.GetExtension(request.FilePath)}" }
        });
        
        // 3. Generate content based on patterns and context
        var templateAnalysis = AnalyzeFileTemplates(similarFiles);
        var enhancedContent = await EnhanceContentWithPatterns(request.Content, templateAnalysis);
        
        // 4. Create file with enhanced content
        var fileResult = await _geminiTools.FilesystemWriteAsync(new FileWriteRequest
        {
            Path = request.FilePath,
            Content = enhancedContent,
            CreateDirectories = true,
            BackupExisting = true
        });
        
        // 5. Update project index and documentation
        await UpdateProjectDocumentation(request.FilePath, enhancedContent);
        
        // 6. Notify team members
        await _coordinationService.NotifyFileCreatedAsync(request.FilePath, request.Description);
        
        return new FileOperationResult
        {
            Success = fileResult.Success,
            FilePath = request.FilePath,
            EnhancementsApplied = templateAnalysis.PatternsFound,
            TeamNotified = true
        };
    }
}
```

## Agent Coordination Workflows

### 1. Proactive Task Management

#### Smart Task Distribution Pattern
```csharp
public class SmartTaskDistribution
{
    public async Task<TaskAssignmentResult> DistributeTasksIntelligentlyAsync()
    {
        // 1. Analyze agent capabilities and current workload
        var agents = await _agentService.GetActiveAgentsAsync();
        var agentCapabilities = new Dictionary<string, AgentCapability>();
        
        foreach (var agent in agents)
        {
            var capability = await AnalyzeAgentCapability(agent);
            agentCapabilities[agent.Id] = capability;
        }
        
        // 2. Get pending tasks and analyze requirements
        var pendingTasks = await _taskService.GetPendingTasksAsync();
        var taskRequirements = new Dictionary<int, TaskRequirement>();
        
        foreach (var task in pendingTasks)
        {
            var requirement = await AnalyzeTaskRequirements(task);
            taskRequirements[task.Id] = requirement;
        }
        
        // 3. Use intelligent matching algorithm
        var assignments = await CalculateOptimalAssignments(agentCapabilities, taskRequirements);
        
        // 4. Distribute tasks with context
        var results = new List<TaskAssignment>();
        foreach (var assignment in assignments)
        {
            var assignmentResult = await AssignTaskWithContext(assignment);
            results.Add(assignmentResult);
        }
        
        return new TaskAssignmentResult
        {
            Assignments = results,
            OverallEfficiencyScore = CalculateEfficiencyScore(results),
            RecommendedFollowUp = GenerateFollowUpRecommendations(results)
        };
    }
    
    private async Task<TaskAssignment> AssignTaskWithContext(TaskAssignmentCandidate candidate)
    {
        // Create rich context for the agent
        var contextualPrompt = await GenerateTaskContextPrompt(candidate.Task, candidate.Agent);
        
        // Assign with coordination tools available
        await _coordinationService.AssignTaskAsync(candidate.Task.Id, candidate.Agent.Id, contextualPrompt);
        
        // Set up monitoring and support
        await SetupTaskMonitoring(candidate.Task.Id, candidate.Agent.Id);
        
        return new TaskAssignment
        {
            TaskId = candidate.Task.Id,
            AgentId = candidate.Agent.Id,
            ContextProvided = contextualPrompt,
            MonitoringEnabled = true,
            EstimatedCompletionTime = candidate.EstimatedDuration
        };
    }
}
```

### 2. Real-time Collaboration Patterns

#### Synchronized Work Sessions Pattern
```csharp
public class SynchronizedWorkSessions
{
    public async Task<WorkSession> StartCollaborativeSessionAsync(CollaborativeSessionRequest request)
    {
        var session = new WorkSession
        {
            Id = Guid.NewGuid().ToString(),
            Title = request.Title,
            Participants = request.ParticipantIds,
            StartedAt = DateTime.UtcNow,
            WorkspaceSnapshot = await CaptureWorkspaceSnapshot()
        };
        
        // 1. Set up shared workspace state
        await SetupSharedWorkspace(session);
        
        // 2. Configure real-time synchronization
        await ConfigureRealTimeSync(session);
        
        // 3. Establish communication channels
        await EstablishCommunicationChannels(session);
        
        // 4. Start session monitoring
        await StartSessionMonitoring(session);
        
        // 5. Notify participants
        await NotifyParticipants(session);
        
        return session;
    }
    
    private async Task ConfigureRealTimeSync(WorkSession session)
    {
        // Set up file watching and synchronization
        foreach (var participantId in session.Participants)
        {
            await _geminiConfigService.UpdateParticipantConfigAsync(participantId, new
            {
                session = new
                {
                    id = session.Id,
                    syncMode = "real-time",
                    conflictResolution = "collaborative",
                    notificationLevel = "high"
                }
            });
        }
        
        // Configure VS Code live share equivalent
        await _vscodeService.StartLiveShareSessionAsync(session.Id, session.Participants);
    }
}
```

## Best Practices and Patterns

### 1. Error Handling and Resilience

#### Graceful Degradation Pattern
```csharp
public class ResilientCoordinationService
{
    public async Task<CoordinationResult> ExecuteWithResilienceAsync(Func<Task<CoordinationResult>> operation)
    {
        var retryPolicy = Policy
            .Handle<CoordinationException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Coordination operation failed, retrying in {Delay}ms (attempt {Retry})",
                        timespan.TotalMilliseconds, retryCount);
                });
        
        var fallbackPolicy = Policy<CoordinationResult>
            .Handle<Exception>()
            .FallbackAsync(
                fallbackValue: new CoordinationResult { Success = false, FallbackUsed = true },
                onFallbackAsync: (result, context) =>
                {
                    _logger.LogError("Coordination operation failed completely, using fallback");
                    return Task.CompletedTask;
                });
        
        var combinedPolicy = Policy.WrapAsync(fallbackPolicy, retryPolicy);
        
        return await combinedPolicy.ExecuteAsync(operation);
    }
}
```

### 2. Performance Optimization

#### Connection Pooling and Caching Pattern
```csharp
public class OptimizedCoordinationClient
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionSemaphores = new();
    private readonly IMemoryCache _responseCache;
    
    public async Task<TResult> ExecuteOptimizedAsync<TResult>(string operation, object parameters)
    {
        // 1. Check cache first
        var cacheKey = GenerateCacheKey(operation, parameters);
        if (_responseCache.TryGetValue(cacheKey, out TResult cachedResult))
        {
            return cachedResult;
        }
        
        // 2. Use connection pooling to prevent overwhelming
        var semaphore = _connectionSemaphores.GetOrAdd(operation, _ => new SemaphoreSlim(5, 5));
        await semaphore.WaitAsync();
        
        try
        {
            // 3. Execute with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await ExecuteOperationAsync<TResult>(operation, parameters, cts.Token);
            
            // 4. Cache successful results
            _responseCache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }
}
```

## Implementation Checklist

### Phase 1: Basic Integration
- [ ] Install and configure gemini-cli
- [ ] Set up basic MCP server connection
- [ ] Implement core coordination tools
- [ ] Test basic tool invocation

### Phase 2: Event Hooks
- [ ] Implement beforeRequest hook for context injection
- [ ] Implement afterResponse hook for signal extraction
- [ ] Implement configChanged hook for dynamic reconfiguration
- [ ] Test all hooks with real agent scenarios

### Phase 3: VS Code Integration
- [ ] Configure workspace-aware context loading
- [ ] Set up native diff-view integration
- [ ] Implement custom VS Code commands
- [ ] Test collaborative editing scenarios

### Phase 4: Extended Tools Integration
- [ ] Integrate Google Search capabilities
- [ ] Set up intelligent file operations
- [ ] Implement memory system integration
- [ ] Test multi-source research workflows

### Phase 5: Production Readiness
- [ ] Implement error handling and resilience
- [ ] Add performance optimization
- [ ] Set up monitoring and observability
- [ ] Create deployment and configuration guides

This guide provides a comprehensive foundation for implementing sophisticated agent coordination using the full capabilities of the gemini-cli tool.