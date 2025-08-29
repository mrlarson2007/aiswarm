# ADR-0006: Practical Memory System Implementation

## Status
Accepted (Supersedes ADR-0005)

## Context

After implementing the initial SaveMemoryMcpTool and analyzing Claude Flow's proven memory system patterns, we need to refine our memory table design to be more practical and aligned with real-world usage patterns.

### Key Learnings from Initial Implementation
- Basic save functionality works well with simple schema
- TDD approach with RED-GREEN-REFACTOR cycles is effective
- Integration testing with real database provides confidence
- Claude Flow's actual implementation is simpler than initially proposed

### Analysis of Claude Flow's Proven Patterns
Research into Claude Flow's actual memory system revealed these essential patterns:
- **Content Type Tracking**: `type` field for different data formats (json, text, binary)
- **Rich Metadata**: JSON metadata field for extensibility
- **Access Tracking**: `accessed_at` and `access_count` for usage analytics
- **Creation Timestamps**: `created_at` separate from `updated_at`
- **Size Tracking**: `size` field for storage optimization
- **Compression Support**: `compressed` flag for large entries
- **No TTL Initially**: TTL can be added later when production needs justify complexity

## Decision

Implement a **Practical Memory System** that incorporates Claude Flow's proven patterns while maintaining our successful TDD approach.

### Enhanced MemoryEntry Schema

```csharp
public class MemoryEntry
{
    // Core identification (existing)
    public string Id { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    
    // Content management (new)
    public string Type { get; set; } = "json";        // Content type
    public string? Metadata { get; set; }             // JSON metadata
    public bool IsCompressed { get; set; }            // Compression flag
    public int Size { get; set; }                     // Content size
    
    // Temporal tracking (enhanced)
    public DateTime CreatedAt { get; set; }           // Creation time
    public DateTime LastUpdatedAt { get; set; }       // Last modification
    public DateTime? AccessedAt { get; set; }         // Last access time
    public int AccessCount { get; set; }              // Access frequency
}
```

### Service Interface Enhancements

```csharp
public interface IMemoryService
{
    // Core operations
    Task SaveMemoryAsync(string key, string value, string? @namespace = null, 
                        string? metadata = null, string type = "json");
    Task<MemoryEntry?> ReadMemoryAsync(string key, string? @namespace = null);
    Task<List<MemoryEntry>> ListMemoriesAsync(string? @namespace = null, int limit = 100);
    Task<bool> DeleteMemoryAsync(string key, string? @namespace = null);
    
    // Cleanup operations (manual, not TTL-based)
    Task<int> ClearNamespaceAsync(string @namespace);
    Task<int> ClearOldEntriesAsync(DateTime olderThan);
}
```

### Database Schema Updates

```sql
-- Enhanced memory table matching Claude Flow patterns
CREATE TABLE MemoryEntries (
    Id VARCHAR(100) PRIMARY KEY,
    Namespace VARCHAR(100) NOT NULL,
    Key VARCHAR(200) NOT NULL,
    Value TEXT NOT NULL,
    Type VARCHAR(50) NOT NULL DEFAULT 'json',
    Metadata TEXT NULL,
    IsCompressed BOOLEAN NOT NULL DEFAULT 0,
    Size INTEGER NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL,
    LastUpdatedAt DATETIME NOT NULL,
    AccessedAt DATETIME NULL,
    AccessCount INTEGER NOT NULL DEFAULT 0,
    
    UNIQUE(Namespace, Key)
);

-- Indexes for common query patterns
CREATE INDEX IX_MemoryEntries_Namespace ON MemoryEntries(Namespace);
CREATE INDEX IX_MemoryEntries_CreatedAt ON MemoryEntries(CreatedAt);
CREATE INDEX IX_MemoryEntries_AccessedAt ON MemoryEntries(AccessedAt);
CREATE INDEX IX_MemoryEntries_Type ON MemoryEntries(Type);
```

## Implementation Approach

### Phase 1: Enhance Core Schema (TDD)
1. **RED**: Update existing SaveMemoryMcpTool test to expect new fields
2. **GREEN**: Add new properties to MemoryEntry entity
3. **RED**: Test MemoryService populates all fields correctly
4. **GREEN**: Update MemoryService to set new field values
5. **REFACTOR**: Clean up any implementation issues
6. **COMMIT**: Enhanced schema with backward compatibility

### Phase 2: Add Read Operations (TDD)
1. **RED**: Write failing test for ReadMemoryMcpTool
2. **GREEN**: Implement ReadMemoryMcpTool with access tracking
3. **RED**: Test access count and timestamp updates
4. **GREEN**: Update read operations to track access
5. **REFACTOR**: Optimize read performance
6. **COMMIT**: Complete read functionality

### Phase 3: Complete CRUD Operations
1. Implement ListMemoryMcpTool with filtering
2. Implement DeleteMemoryMcpTool with cleanup
3. Add namespace and temporal cleanup operations

## Consequences

### Positive
- **Proven Patterns**: Based on Claude Flow's real-world implementation
- **Rich Analytics**: Access tracking enables usage optimization
- **Content Management**: Type and metadata support diverse use cases
- **Performance Ready**: Size tracking and compression support scalability
- **Incremental**: Can be implemented without breaking existing functionality
- **TDD Compliant**: Fits existing development methodology

### Negative
- **Schema Complexity**: More fields to manage and test
- **Migration Required**: Existing data needs schema updates
- **Storage Overhead**: Additional metadata increases storage requirements

### Neutral
- **No TTL Initially**: Keeps implementation simple, can add later
- **Manual Cleanup**: Explicit control over data lifecycle
- **Flexible Metadata**: JSON field allows evolution without schema changes

## Migration Strategy

### Backward Compatibility
```csharp
// Existing SaveMemoryMcpTool calls continue to work
await memory.SaveMemoryAsync("key", "value", "namespace");

// New optional parameters available
await memory.SaveMemoryAsync("key", "value", "namespace", 
    metadata: "{\"priority\": \"high\"}", type: "json");
```

### Database Migration
```sql
-- Add new columns with sensible defaults
ALTER TABLE MemoryEntries ADD COLUMN Type VARCHAR(50) DEFAULT 'json';
ALTER TABLE MemoryEntries ADD COLUMN Metadata TEXT NULL;
ALTER TABLE MemoryEntries ADD COLUMN IsCompressed BOOLEAN DEFAULT 0;
ALTER TABLE MemoryEntries ADD COLUMN Size INTEGER DEFAULT 0;
ALTER TABLE MemoryEntries ADD COLUMN CreatedAt DATETIME;
ALTER TABLE MemoryEntries ADD COLUMN AccessedAt DATETIME NULL;
ALTER TABLE MemoryEntries ADD COLUMN AccessCount INTEGER DEFAULT 0;

-- Populate missing timestamps for existing data
UPDATE MemoryEntries SET CreatedAt = LastUpdatedAt WHERE CreatedAt IS NULL;
```

## Success Criteria

1. **Functional**: All existing memory operations continue to work
2. **Enhanced**: New fields populated correctly in all operations
3. **Performance**: Access tracking doesn't significantly impact performance
4. **Testable**: All new functionality covered by TDD tests
5. **Usable**: Clear patterns for different content types and metadata usage

## References
- ADR-0005: Memory Table System for Agent Communication (superseded)
- Claude Flow Memory System: github.com/ruvnet/claude-flow
- AISwarm TDD Development Patterns
- Entity Framework Core Documentation