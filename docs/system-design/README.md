# System Design Documentation

Core architectural documentation, database schemas, and foundational design decisions for the AI Swarm project.

## 📐 Architecture

### System Overview

- [SQLite Task Coordination Design](sqlite-task-coordination-design.md) - Complete system architecture with Mermaid diagrams, multi-project structure, and MCP server integration
- [Coordination Server Structure](coordination-server-structure.md) - Clean architecture layout, service patterns, and implementation examples
- [Gemini CLI Integration Guide](gemini-cli-integration-guide.md) - Comprehensive implementation patterns for plugin hooks, VS Code integration, and extended Tools API

## 🗃️ Database Design

### Schema & Migrations

- [Migration Scripts](migration-scripts/) - Database schema and migration files
  - [001_InitialSchema.sql](migration-scripts/001_InitialSchema.sql) - Complete initial database schema with all tables, indexes, and constraints

### Key Features

- **Migration Tracking**: Hash-based change detection with SHA-256 verification
- **WAL Mode**: Optimized for concurrent access with multiple agents
- **Transaction Safety**: Each migration runs atomically with rollback on failure

## 🏛️ Architecture Decisions

### ADR Documentation

- [Architecture Decision Records (ADRs)](adr/) - Formal architectural decisions with context and rationale
  - [ADR-0001: Record Architecture Decisions](adr/0001-record-architecture-decisions.md) - ADR process establishment
  - [ADR-0002: Shared Context Between Agents](adr/0002-shared-context-between-agents.md) - SQLite coordination decision
  - [ADR-0003: Separate MCP Coordination Server](adr/0003-separate-mcp-coordination-server.md) - Project separation rationale
  - [ADR-0004: Use Official Gemini CLI with MCP Integration](adr/0004-gemini-cli-mcp-integration.md) - Agent coordination via gemini-cli

## 🎯 Design Principles

The system follows these core architectural principles:

- **Clean Architecture**: Clear separation between MCP tools, services, and data layers
- **MCP Compliance**: Standard Model Context Protocol patterns for agent communication  
- **SOLID Principles**: Single responsibility, dependency inversion, and interface segregation
- **Testability**: All layers designed for unit testing with dependency injection
- **Scalability**: SQLite WAL mode supports concurrent multi-agent coordination

## 🔗 Cross-References

- **Implementation Plans**: See [Features Documentation](../features/)
- **User Guides**: See [Help Documentation](../help/)
- **Project Root**: Back to [Main Documentation](../README.md)
