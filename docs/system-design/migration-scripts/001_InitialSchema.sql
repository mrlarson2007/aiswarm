-- Complete initial schema for SQLite task coordination system
-- Script: 001_InitialSchema.sql

-- Migration tracking system (must be first)
CREATE TABLE migration_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_name TEXT NOT NULL UNIQUE,
    script_hash TEXT NOT NULL,
    applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    execution_time_ms INTEGER NOT NULL,
    success BOOLEAN NOT NULL DEFAULT 1,
    reason TEXT,                    -- "New migration", "Script modified", etc.
    
    CONSTRAINT valid_success CHECK (success IN (0, 1))
);

-- Indexes for efficient migration queries
CREATE INDEX idx_migration_name ON migration_history(migration_name);
CREATE INDEX idx_migration_applied_at ON migration_history(applied_at);

-- Enable optimal SQLite settings
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = 1000;
PRAGMA foreign_keys = ON;
PRAGMA temp_store = MEMORY;

-- Persona definitions
CREATE TABLE personas (
    id TEXT PRIMARY KEY,           -- 'planner', 'implementer', etc.
    name TEXT NOT NULL,            -- Human readable name
    definition TEXT NOT NULL,      -- Markdown/JSON persona content
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Immutable task definitions
CREATE TABLE tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,           -- "Design login API"
    description TEXT NOT NULL,     -- Detailed task description
    persona_id TEXT NOT NULL,      -- Required persona for this task
    context_ref TEXT,              -- Reference to context files/data
    priority INTEGER DEFAULT 0,    -- Higher = more important
    created_by TEXT,               -- Agent ID that created task
    parent_task_id INTEGER,        -- For subtasks
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    metadata TEXT,                 -- JSON for extensibility
    
    FOREIGN KEY (persona_id) REFERENCES personas(id),
    FOREIGN KEY (parent_task_id) REFERENCES tasks(id)
);

-- Mutable task execution state
CREATE TABLE task_status (
    task_id INTEGER PRIMARY KEY,
    status TEXT NOT NULL CHECK (status IN ('pending', 'claimed', 'in_progress', 'completed', 'failed', 'cancelled')),
    assigned_agent_id TEXT,        -- Which agent claimed this
    claimed_at TEXT,               -- When task was claimed
    started_at TEXT,               -- When work actually began
    completed_at TEXT,             -- When task finished
    lease_expires_at TEXT,         -- Lease timeout for failure detection
    result_data TEXT,              -- JSON result from completion
    error_message TEXT,            -- Error details if failed
    retry_count INTEGER DEFAULT 0, -- Number of retry attempts
    progress_percent INTEGER DEFAULT 0 CHECK (progress_percent >= 0 AND progress_percent <= 100),
    
    FOREIGN KEY (task_id) REFERENCES tasks(id)
);

-- Planner-controlled worktrees
CREATE TABLE worktrees (
    name TEXT PRIMARY KEY,             -- Planner-assigned name (e.g., "user-auth-feature")
    branch_name TEXT NOT NULL,         -- Git branch name (e.g., "feature/user-auth")
    created_by TEXT NOT NULL,          -- Planner agent that created it
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'integrated', 'discarded')),
    integration_target TEXT,           -- Target branch for integration (e.g., "main")
    path TEXT NOT NULL,                -- Absolute path to worktree directory
    
    FOREIGN KEY (created_by) REFERENCES agents(id)
);

-- Active agents and their coordination state
CREATE TABLE agents (
    id TEXT PRIMARY KEY,           -- Unique agent identifier
    persona_id TEXT NOT NULL,      -- What type of agent this is
    assigned_worktree TEXT,        -- Current worktree assignment
    status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'idle', 'working', 'disconnected')),
    last_heartbeat TEXT NOT NULL DEFAULT (datetime('now')),
    process_id TEXT,               -- OS process ID if managed
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    metadata TEXT,                 -- JSON for agent-specific data
    
    FOREIGN KEY (persona_id) REFERENCES personas(id),
    FOREIGN KEY (assigned_worktree) REFERENCES worktrees(name)
);

-- Task dependencies for complex workflows
CREATE TABLE task_dependencies (
    task_id INTEGER,
    depends_on_task_id INTEGER,
    dependency_type TEXT NOT NULL DEFAULT 'blocks' CHECK (dependency_type IN ('blocks', 'soft')),
    
    PRIMARY KEY (task_id, depends_on_task_id),
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (depends_on_task_id) REFERENCES tasks(id),
    
    -- Prevent self-dependencies
    CONSTRAINT no_self_dependency CHECK (task_id != depends_on_task_id)
);

-- Task progress tracking
CREATE TABLE task_progress (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    agent_id TEXT NOT NULL,
    progress_note TEXT NOT NULL,
    percent_complete INTEGER CHECK (percent_complete >= 0 AND percent_complete <= 100),
    reported_at TEXT NOT NULL DEFAULT (datetime('now')),
    
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (agent_id) REFERENCES agents(id)
);

-- Worktree agent assignments
CREATE TABLE worktree_agents (
    worktree_name TEXT,
    agent_id TEXT,
    assigned_at TEXT NOT NULL DEFAULT (datetime('now')),
    role TEXT NOT NULL DEFAULT 'worker' CHECK (role IN ('worker', 'lead', 'observer')),
    
    PRIMARY KEY (worktree_name, agent_id),
    FOREIGN KEY (worktree_name) REFERENCES worktrees(name),
    FOREIGN KEY (agent_id) REFERENCES agents(id)
);

-- Performance indexes
CREATE INDEX idx_tasks_persona_id ON tasks(persona_id);
CREATE INDEX idx_tasks_created_by ON tasks(created_by);
CREATE INDEX idx_tasks_priority ON tasks(priority DESC);
CREATE INDEX idx_tasks_parent_task_id ON tasks(parent_task_id);

CREATE INDEX idx_task_status_status ON task_status(status);
CREATE INDEX idx_task_status_assigned_agent ON task_status(assigned_agent_id);
CREATE INDEX idx_task_status_lease_expires ON task_status(lease_expires_at);

CREATE INDEX idx_agents_persona_id ON agents(persona_id);
CREATE INDEX idx_agents_assigned_worktree ON agents(assigned_worktree);
CREATE INDEX idx_agents_last_heartbeat ON agents(last_heartbeat);

CREATE INDEX idx_worktrees_created_by ON worktrees(created_by);
CREATE INDEX idx_worktrees_status ON worktrees(status);

CREATE INDEX idx_task_progress_task_id ON task_progress(task_id);
CREATE INDEX idx_task_progress_agent_id ON task_progress(agent_id);
CREATE INDEX idx_task_progress_reported_at ON task_progress(reported_at);

CREATE INDEX idx_worktree_agents_worktree ON worktree_agents(worktree_name);
CREATE INDEX idx_worktree_agents_agent ON worktree_agents(agent_id);

-- Insert default personas
INSERT INTO personas (id, name, definition) VALUES 
    ('planner', 'Planner Agent', 'Strategic planning and task decomposition'),
    ('implementer', 'Implementer Agent', 'Code implementation and development'),
    ('reviewer', 'Reviewer Agent', 'Code review and quality assurance'),
    ('tester', 'Tester Agent', 'Testing and validation of implementations');