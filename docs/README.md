# AI Swarm Documentation

This documentation is organized into three main categories for easy navigation and maintenance.

## 📐 System Design

**Location**: `/docs/system-design/`

Core architectural documentation, database schemas, and foundational design decisions.

### Architecture

- [SQLite Task Coordination Design](system-design/sqlite-task-coordination-design.md) - Complete system architecture with Mermaid diagrams
- [Coordination Server Structure](system-design/coordination-server-structure.md) - Clean architecture layout and implementation patterns

### Database

- [Migration Scripts](system-design/migration-scripts/) - Database schema and migration files
  - [001_InitialSchema.sql](system-design/migration-scripts/001_InitialSchema.sql) - Complete initial database schema

### Decisions

- [Architecture Decision Records (ADRs)](system-design/adr/) - Formal architectural decisions
  - [ADR-0001: Record Architecture Decisions](system-design/adr/0001-record-architecture-decisions.md)
  - [ADR-0002: Shared Context Between Agents](system-design/adr/0002-shared-context-between-agents.md)
  - [ADR-0003: Separate MCP Coordination Server](system-design/adr/0003-separate-mcp-coordination-server.md)

## 🚀 Features

**Location**: `/docs/features/`

Feature specifications, implementation plans, and user-facing functionality.

### Agent Coordination

- [Agent Coordination Instructions](features/agent-coordination-prompt.md) - Complete workflow guide for AI agents
- [Task Coordination Implementation Plan](features/sqlite-task-coordination-plan.md) - TDD-based implementation roadmap

### Development Process

- [Design Refinement Questions](features/design-refinement-questions.md) - Outstanding design considerations

## 📚 Help & Guides

**Location**: `/docs/help/`

User guides, operational procedures, and development workflows.

### Development

- [CI/CD Guide](help/CICD.md) - Continuous integration and deployment procedures
- [Copilot Integration](help/COPILOT.md) - AI coding assistant configuration and usage

## 🧭 Navigation Guidelines

### For Developers

1. **Start with**: [System Design](system-design/) to understand the architecture
2. **Review**: [ADRs](system-design/adr/) for context on design decisions
3. **Implement**: Follow [Feature Plans](features/) for specific functionality
4. **Reference**: [Help Guides](help/) for operational procedures

### For Contributors

1. **New Features**: Add specifications to `/docs/features/`
2. **Architecture Changes**: Document in `/docs/system-design/` and create ADRs
3. **User Guides**: Add to `/docs/help/`

### For System Administrators

1. **Deployment**: See [CI/CD Guide](help/CICD.md)
2. **Database**: Reference [Migration Scripts](system-design/migration-scripts/)
3. **Architecture**: Review [System Design](system-design/) documentation

## 📝 Documentation Standards

- **System Design**: Technical specifications, database schemas, architectural decisions
- **Features**: User-facing functionality, implementation plans, requirements
- **Help**: Operational guides, development workflows, troubleshooting

All documentation follows Markdown standards with proper linking and cross-references for easy navigation.

