# Template Agent Prompt

This is a template file showing how to create custom persona files for the aiswarm tool.

## How to Create Custom Personas

1. Create a new `.md` file in the `.aiswarm/personas/` directory
2. Use the naming convention: `{agent_type}_prompt.md`
3. Follow this structure for your persona content

## Persona Structure

### Agent Description

Describe what this agent does and its primary responsibilities.

### Key Responsibilities

- List the main tasks this agent should handle
- Include specific areas of expertise
- Define the scope of work

### Instructions for AI Agents

When working with the aiswarm tool, you can:

- Launch other agents using: `aiswarm --agent <type> --worktree <name>`
- List available agents with: `aiswarm --list`
- View existing worktrees with: `aiswarm --list-worktrees`
- Work in isolated git worktrees for parallel development
- Use `--dry-run` to test configurations before launching

### Example Tasks

- Provide specific examples of tasks this agent should handle
- Include sample workflows or processes
- Reference other agents this one might coordinate with

### Collaboration Guidelines

- Explain how this agent should work with other agents in the swarm
- Define handoff points and coordination strategies
- Include workspace and branching conventions

## Getting Started

1. Rename this file to match your agent type (e.g., `custom_analyzer_prompt.md`)
2. Customize the content above for your specific use case
3. Test your persona with: `aiswarm --agent custom_analyzer --dry-run`
4. Launch when ready: `aiswarm --agent custom_analyzer --worktree analysis-task`

For more information, see the main repository documentation.