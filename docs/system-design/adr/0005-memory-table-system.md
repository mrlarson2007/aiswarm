# ADR-0005: Memory Table System for Agent Communication

## Status
Superseded by ADR-0006: Practical Memory System Implementation

## Context
We need a persistent memory system for agents to communicate results, share state, and coordinate activities. The current system only handles task lifecycle events but lacks a mechanism for agents to store and retrieve arbitrary data.

### Requirements Analysis
Based on user feedback: "We need to log events and get events so we can debug things. We also need a better way of getting the output of a completed task" and "we can create memory table that allows for communication and saving state."

### Claude Flow Inspiration
Analysis of Claude Flow's memory system revealed key patterns:
- Namespace isolation for multi-tenant scenarios
- Dual backend (SQLite for performance, file system for human readability)
- Rich metadata support with compression for large values
- Simple API that hides storage implementation details
- Time-based cleanup and access tracking

## Decision
Implement a Memory Table System with the following design:

### Core Components

#### 1. MemoryEntry Entity
```csharp
// Pseudo-code for memory entry structure
class MemoryEntry {
    string Id;                    // Unique identifier
    string Namespace;             // Isolation boundary (e.g., "planning", "agent-{id}", "shared")
    string Key;                   // Key within namespace
    string Value;                 // JSON, markdown, or plain text content
    string ContentType;           // "json", "markdown", "text"
    string? Metadata;             // JSON metadata for rich queries
    string? Tags;                 // Comma-separated tags for categorization
    string? CreatedBy;            // Agent ID that created entry
    DateTime CreatedAt;           // Creation timestamp
    DateTime UpdatedAt;           // Last modification
    DateTime LastAccessedAt;      // For cleanup and analytics
    DateTime? ExpiresAt;          // Optional auto-expiration
    int Version;                  // Optimistic concurrency
    bool IsCompressed;            // For large values (>1KB)
    int OriginalSize;             // Metrics
    int CompressedSize;           // Metrics
}
```

#### 2. Memory Service Interface
```csharp
// Pseudo-code for service operations
interface IMemoryService {
    Task<MemoryEntry> SaveMemoryAsync(namespace, key, value, contentType, metadata, tags, createdBy, expiresAt);
    Task<MemoryEntry?> ReadMemoryAsync(namespace, key);
    Task<List<MemoryEntry>> SearchMemoriesAsync(namespace, searchTerm, contentType, tags, createdBy, dateFilters, limit);
    Task<bool> DeleteMemoryAsync(namespace, key);
    Task<List<MemoryEntry>> ListMemoriesAsync(namespace, limit);
    Task<MemoryStatistics> GetStatisticsAsync(namespace);
    Task<int> CleanupExpiredAsync();
}
```

#### 3. MCP Tools for Agent Interface
```csharp
// Pseudo-code for MCP tool interface
class SaveMemoryMcpTool {
    // Parameters: namespace, key, value, content_type?, metadata?, tags?, expires_hours?
    // Returns: success status and entry details
}

class ReadMemoryMcpTool {
    // Parameters: namespace, key
    // Returns: value and metadata if found
}

class SearchMemoriesMcpTool {
    // Parameters: namespace?, search_term?, content_type?, tags?, limit?
    // Returns: list of matching entries
}

class DeleteMemoryMcpTool {
    // Parameters: namespace, key
    // Returns: success status
}
```

### Database Schema
```sql
-- Memory table with indexes for performance
CREATE TABLE MemoryEntries (
    Id VARCHAR(100) PRIMARY KEY,
    Namespace VARCHAR(100) NOT NULL,
    Key VARCHAR(200) NOT NULL,
    Value TEXT NOT NULL,
    ContentType VARCHAR(50) DEFAULT 'text',
    Metadata TEXT NULL,
    Tags VARCHAR(1000) NULL,
    CreatedBy VARCHAR(50) NULL,
    CreatedAt DATETIME NOT NULL,
    UpdatedAt DATETIME NOT NULL,
    LastAccessedAt DATETIME NOT NULL,
    ExpiresAt DATETIME NULL,
    Version INTEGER DEFAULT 1,
    IsCompressed BOOLEAN DEFAULT 0,
    OriginalSize INTEGER DEFAULT 0,
    CompressedSize INTEGER DEFAULT 0,
    
    UNIQUE(Namespace, Key)
);

-- Indexes for common query patterns
CREATE INDEX IX_MemoryEntries_Namespace ON MemoryEntries(Namespace);
CREATE INDEX IX_MemoryEntries_CreatedBy ON MemoryEntries(CreatedBy);
CREATE INDEX IX_MemoryEntries_CreatedAt ON MemoryEntries(CreatedAt);
CREATE INDEX IX_MemoryEntries_ExpiresAt ON MemoryEntries(ExpiresAt);
CREATE INDEX IX_MemoryEntries_Tags ON MemoryEntries(Tags);
```

### Usage Patterns

#### Agent Result Storage
```javascript
// Planning agent stores task results
await save_memory("task-results", "task-123", JSON.stringify({
    taskId: "task-123",
    status: "completed",
    output: "Unit tests created successfully",
    artifacts: ["test-file.cs"],
    metrics: { duration: "5m", testsCreated: 12 }
}), "json", JSON.stringify({ priority: "high" }), "completed,testing");
```

#### Inter-Agent Communication
```javascript
// Agent leaves message for another agent
await save_memory("agent-coordination", "handoff-task-456", 
    "Task requires additional authentication setup. See implementation notes in auth-service.md", 
    "markdown", null, "handoff,auth,pending");
```

#### Debugging and Logging
```javascript
// Store debug information
await save_memory("debug", "error-analysis-2024-08-27", JSON.stringify({
    error: "Connection timeout",
    context: "Database migration",
    stackTrace: "...",
    resolution: "Increased timeout values"
}), "json", null, "error,debug,resolved");
```

## Consequences

### Positive
- **Simplified Agent Communication**: Agents can easily share results and state
- **Namespace Isolation**: Different use cases don't interfere with each other
- **Rich Metadata Support**: Enables complex queries and organization
- **Automatic Compression**: Handles large values efficiently
- **MCP Tool Integration**: Seamless integration with existing agent workflow
- **Debugging Support**: Centralized logging and state inspection
- **Cleanup Automation**: Prevents unlimited growth with expiration

### Negative
- **Additional Complexity**: New service layer and database schema
- **Storage Overhead**: Metadata and compression add storage requirements
- **Query Performance**: Large datasets may require additional optimization

### Risks
- **Namespace Collisions**: Agents must coordinate namespace usage
- **Data Growth**: Without proper cleanup, storage could grow unbounded
- **Concurrency**: Multiple agents modifying same keys need conflict resolution

## Implementation Plan

### Phase 1: Core Infrastructure (TDD)
1. **RED**: Test MemoryEntry entity creation and validation
2. **GREEN**: Implement minimal MemoryEntry class
3. **RED**: Test database context with MemoryEntry
4. **GREEN**: Add MemoryEntry to CoordinationDbContext
5. **RED**: Test memory service save operation with edge cases
6. **GREEN**: Implement MemoryService.SaveMemoryAsync
7. **RED**: Test memory service read operation with missing keys
8. **GREEN**: Implement MemoryService.ReadMemoryAsync

### Phase 2: Advanced Operations (TDD)
1. **RED**: Test search functionality with various filters
2. **GREEN**: Implement MemoryService.SearchMemoriesAsync
3. **RED**: Test delete operation with non-existent keys
4. **GREEN**: Implement MemoryService.DeleteMemoryAsync
5. **RED**: Test compression for large values
6. **GREEN**: Add compression logic to MemoryService

### Phase 3: MCP Integration (TDD)
1. **RED**: Test SaveMemoryMcpTool with invalid parameters
2. **GREEN**: Implement SaveMemoryMcpTool
3. **RED**: Test ReadMemoryMcpTool with missing entries
4. **GREEN**: Implement ReadMemoryMcpTool
5. **RED**: Test SearchMemoriesMcpTool with complex queries
6. **GREEN**: Implement SearchMemoriesMcpTool

### Phase 4: End-to-End Testing
1. Test agent coordination scenarios
2. Test debugging and logging workflows
3. Performance testing with large datasets
4. Cleanup and expiration testing

## References
- Claude Flow Memory System: https://github.com/ruvnet/claude-flow
- ADR-0003: Separate MCP Coordination Server
- AISwarm Event Bus Architecture