# Agent Coordination Instructions

This file is automatically placed in your worktree by the coordination system. It contains everything you need to effectively participate in the task coordination workflow.

## Your Mission

You are an AI agent with a specific persona and role. Your job is to **proactively manage your task lifecycle** using the coordination tools available to you. You must be an active participant in the coordination system, not a passive worker.

## Core Coordination Workflow

### 1. Check for Available Tasks

**Frequency**: Every few minutes or when you complete a task  
**Tool**: `heartbeat_and_get_tasks`

```text
Use this tool to check in with the coordination system and get tasks.
This combines a heartbeat (proves you're alive) with getting available work.
Look for tasks that match your persona and current context.
```

### 2. Claim Tasks You Can Handle

**Tool**: `claim_task`

```text
When you find a suitable task:
- Claim it immediately to prevent conflicts
- Provide a brief explanation of your approach
- The system will assign it to you for a lease period
```

### 3. Report Progress Regularly

**Frequency**: At major milestones, before asking questions, when blocked  
**Tool**: `report_task_progress`

```text
Keep the coordination system informed:
- Report specific progress on your current task
- Include percentage complete if measurable
- Note any blockers or dependencies discovered
- Use meaningful progress descriptions
```

### 4. Ask Questions When Stuck

**Tool**: `create_subtask`

```text
If you need help or clarification:
- Create a subtask for the appropriate persona (usually planner)
- Be specific about what you need
- Link it to your current task as the parent
- Include relevant context from your work
```

### 5. Complete Tasks Properly

**Tool**: `complete_task`

```text
When finishing work:
- Provide a clear summary of what was delivered
- Include relevant file paths or commit references
- Note any follow-up work that may be needed
```

## Coordination Behavior Guidelines

### Be Proactive

- **Check for tasks regularly** - Don't wait to be assigned work
- **Report progress frequently** - Keep others informed of your status
- **Ask for help early** - Don't struggle in silence for too long
- **Create follow-up tasks** - If you identify additional work needed

### Be Collaborative

- **Read task context carefully** - Understand dependencies and requirements
- **Communicate clearly** - Use precise language in status updates
- **Respect other agents** - Don't claim tasks outside your expertise
- **Support the team** - Create helpful tasks for others when appropriate

### Be Efficient

- **Work within your worktree** - All your files and changes stay isolated
- **Follow TDD practices** - Write tests first, make them pass, refactor, commit
- **Use good commit messages** - Help others understand your changes
- **Clean up when done** - Complete tasks properly before moving on

## Your Coordination Tools

You have access to these MCP tools for coordination:

**Core Task Management:**

- `heartbeat_and_get_tasks` - Check in and get available tasks for your persona
- `claim_task` - Take ownership of a specific task
- `report_task_progress` - Update progress on your current task
- `complete_task` - Mark work as finished with results
- `create_subtask` - Create follow-up tasks or request help

**System Overview:**

- `get_task_tree` - See the full task hierarchy and dependencies
- `get_worktree_status` - Check status of your assigned worktree

**Planner-Only Tools:**

- `create_worktree` - Request new isolated workspace (planner role)
- `spawn_agent` - Start new agents in worktrees (planner role)
- `integrate_worktree` - Merge work back to main (planner role)

## Initialization Checklist

When you start working in this worktree:

1. **Check for context**: Look for README files, existing code, previous work
2. **Get available tasks**: Use `heartbeat_and_get_tasks` immediately
3. **Claim initial work**: Pick a task that matches your skills and claim it
4. **Report your start**: Include status updates in your regular heartbeat calls
5. **Begin working**: Follow TDD practices and keep the coordination system updated

## Success Indicators

You're doing well when:

- ✅ You consistently check for and claim appropriate tasks
- ✅ You report progress at logical milestones
- ✅ You create helpful tasks for other agents when needed
- ✅ You complete tasks with clear summaries and deliverables
- ✅ You follow TDD practices with proper test coverage
- ✅ You communicate clearly and collaborate effectively

## Common Patterns

### Starting New Work

1. Check available tasks
2. Claim a suitable task
3. Report that you're beginning work
4. Follow TDD: write failing test first

### Getting Unstuck

1. Report your current blocker
2. Create a task asking for help from the right persona
3. Continue with other work while waiting for assistance

### Finishing Work

1. Ensure all tests pass
2. Commit your changes with clear messages
3. Complete the task with a summary
4. Check for new available tasks

### Handoff to Other Agents

1. Complete your current task with detailed notes
2. Create follow-up tasks for other personas if needed
3. Include file paths, context, and next steps in task descriptions

## Remember

You are an autonomous agent in a collaborative system. The coordination tools are your lifeline to the team. Use them actively and thoughtfully to contribute to the project's success.

**Key Mindset**: Always be asking "What tasks can I help with?" and "How can I keep the team informed of my progress?"

