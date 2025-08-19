# Features Documentation

Feature specifications, implementation plans, and user-facing functionality for the AI Swarm coordination system.

## 🤖 Agent Coordination

### Agent Workflow
- [Agent Coordination Instructions](agent-coordination-prompt.md) - Complete workflow guide placed in each agent worktree
  - MCP tool usage patterns for task management
  - Behavioral guidelines for proactive collaboration
  - Success indicators and common workflow patterns

## 📋 Implementation Plans

### Task Coordination System
- [SQLite Task Coordination Plan](sqlite-task-coordination-plan.md) - TDD-based implementation roadmap
  - Phase-by-phase development approach
  - Test-driven development workflow
  - Integration milestones and deliverables

## 🔧 Development Process

### Design Evolution
- [Design Refinement Questions](design-refinement-questions.md) - Outstanding design considerations and architectural decisions

## 🎯 Feature Overview

### Core Features
- **Task Coordination**: SQLite-based task queue with persona-specific assignment
- **Agent Management**: Automatic agent spawning, heartbeat monitoring, and lifecycle management
- **Worktree Isolation**: Git worktree-based workspace isolation for parallel development
- **MCP Integration**: Model Context Protocol for seamless agent communication

### User Experience
- **Proactive Agents**: Agents actively check for tasks and report progress
- **Clear Instructions**: Single comprehensive guide file per worktree
- **Collaborative Workflow**: Agents create subtasks and request help when needed
- **Progress Tracking**: Real-time visibility into task status and agent activity

## 🔗 Cross-References

- **System Architecture**: See [System Design Documentation](../system-design/)
- **User Guides**: See [Help Documentation](../help/)
- **Project Root**: Back to [Main Documentation](../README.md)