
# C# CLI Agent Launcher Plan

## Purpose
- Launch Gemini CLI agents with a context file containing detailed instructions (persona).
- All instruction/context files are built-in resources (embedded in the executable).
- The CLI is self-contained for easy installation and deployment.

## Features (Initial Version)
- **Agent Launching:**
  - Launch Gemini CLI in a new console window for each agent.
  - Pass a context file (persona/instructions) to Gemini using the correct flag.
  - Allow selection of Gemini model (e.g., default, pro) per agent launch; default is configurable, can override per launch.
  - Use Gemini CLI flags: `-m <model>` for model selection, `-i <prompt>` for interactive session with initial prompt.
- **Context File Management:**
  - Provide built-in context/instruction files for planner, implementer, reviewer, etc.
  - Option to select which agent persona to launch.
- **Worktree Management:**
  - Optionally create a git worktree for each agent (except planner, which stays on main/master).
  - Automatically switch to the correct branch/worktree for each agent.
- **Simple CLI Interface:**
  - List available agent types/personas.
  - Launch agent with selected persona and context.
  - Optionally specify worktree/branch for agent.

## Future Features
- Add/modify context files (personas) via CLI.
- Support for custom agent types.
- Integration with other LLMs or tools.
- Automated merging of agent worktrees.
- More advanced agent communication.

## Flow
1. User runs CLI tool.
2. User selects agent type/persona.
3. User selects Gemini model (default or override).
4. Tool creates worktree (if needed) and context file from built-in resources.
5. Tool launches Gemini CLI in a new console window, passing the context file and model flag using `-m <model> -i <prompt>`.
6. Planner agent stays on main/master; other agents get their own worktree/branch.

---

## Updated Design Decisions & Clarifications

### Persona/Context Files
- Use Markdown for default personas and instructions (human-friendly, easy to edit).
- Default templates are embedded resources; users can add more in a well-known directory (per OS).
- CLI will support loading additional templates from this directory in future versions.

### Worktree Management
- CLI will create git worktrees for agents (except planner, which stays on main/master).
- Automated merging/cleanup will be added after basic worktree creation is working.
- User confirmation for destructive actions (e.g., cleanup) can be added as needed.

### Gemini CLI Integration
- Assume Gemini CLI is available in PATH.
- Use PowerShell on Windows, bash/shell on Mac/Linux for launching agents.
- Design is flexible to support other LLM agent tools in the future.

### Agent Communication
- Agents communicate with the user or a planning agent via markdown files in their workspace.
- No direct agent-to-agent communication for now; keep it simple.

### Logging & Activity
- Logging is optional and can be added for debugging or development.

### Cross-Platform Support
- Focus on Windows for first pass, but avoid hard-coding Windows-only logic.
- Keep code open for future Mac/Linux support.

### Deployment
- Prefer .NET tool install (`dotnet tool install -g ...`) for easy installation, updates, and cross-platform support.

### Security & Permissions
- No restrictions for now; can be added later if needed.

---


---

## Implementation Roadmap (Phased)

### Phase 1: Scaffold CLI Tool
- Create a new dotnet CLI tool using the built-in template (`dotnet new tool`).
- Set up basic project structure and ensure it builds and runs.

### Phase 2: Command Line Arguments
- Add support for command line arguments:
  - Agent type/persona selection
  - Gemini model selection (with default and override)
  - Worktree/branch specification
- Validate argument parsing and help output.

### Phase 3: Context File Management
- Embed default markdown persona/instruction files as resources.
- Add logic to copy or generate context files in agent workspace.
- Support loading additional templates from a well-known directory (future).

### Phase 4: Worktree Creation
- Implement git worktree creation for agents (except planner).
- Switch to correct branch/worktree for each agent.
- Add user confirmation for destructive actions (cleanup, delete).

### Phase 5: Gemini CLI Launch
- Launch Gemini CLI in a new console window for each agent.
- Pass context file and model flag using `-m <model> -i <prompt>`.
- Use PowerShell on Windows, bash/shell on Mac/Linux.

### Phase 6: Review & Polish
- Test all features end-to-end.
- Add optional logging for debugging/development.
- Refine CLI help, error handling, and user experience.

### Phase 7: Future Enhancements
- Add/modify context files via CLI.
- Support custom agent types and other LLMs.
- Implement automated merging/cleanup of worktrees.
- Add advanced agent communication and central state tracking.

---

Work through each phase sequentially, ensuring each piece is working before moving to the next. Adjust priorities and add features as needed based on feedback and usage.