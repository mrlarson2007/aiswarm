# AI Swarm Development Plan: Gemini + JetBrains

This document outlines the plan for creating a "swarm" development model where a managing agent (GitHub Copilot + User) directs tasks to worker agents (Gemini) that operate in isolated environments, with a JetBrains IDE acting as a dedicated refactoring and code analysis server.

## Core Concepts

1.  **Manager/Orchestrator:** The user, assisted by GitHub Copilot, acts as the high-level manager.
2.  **Worker Agents (Gemini):** Gemini instances execute discrete tasks.
3.  **Isolated Environments (`git worktrees`):** Each task is performed in a separate `git worktree`.
4.  **Guideline Enforcement (`gemini.md`):** A `gemini.md` file provides instructions for Gemini agents.
5.  **BDD & TDD:** The workflow combines Behavior-Driven and Test-Driven Development.
6.  **Automated Refactoring Server (JetBrains + `mcp-jetbrains`):** A JetBrains IDE performs programmatic refactoring.
7.  **Formal Documentation:** The process includes System Design docs, Feature Files, and ADRs.
8.  **State Management:** A central log tracks the status of all tasks.
9.  **Context Reinforcement:** A multi-layered strategy ensures GitHub Copilot remains aware of the workflow and current task state.

---

## Packaging & Distribution

The `aiswarm` tool will be developed as a proper Python package and installed via `pip`. This allows for easy installation, dependency management, and makes the `aiswarm` command globally available in the user's environment.

-   **Setup:** A `setup.py` file will define the package metadata, dependencies (e.g., `click`), and the console script entry point.
-   **Installation:** The tool will be installed in editable mode (`pip install -e .`) during development.

---

## The End-to-End Workflow

### Phase 1: Project Setup

1.  In a new project, the user runs `aiswarm init`.
2.  The command creates a `.aiswarm/config.json` file in that project's directory, prompting the user for the project-specific test command and JetBrains server port.

### Phase 2: Task Execution (TDD Cycle)

1.  **Task Definition (User):** The user defines a clear, small task.
2.  **Task Initiation (CLI):**
    ```bash
    aiswarm task "Create a failing test for the login button."
    ```
3.  **Worktree & Context Setup (CLI):**
    *   The script creates a `git worktree` and a dynamic `.aiswarm/context.md` file.
    *   It prepends `gemini.md` guidelines to the prompt for the Gemini agent.
4.  **Code Generation & Verification:** The cycle of code generation, test verification, and automated refactoring proceeds as planned.

### Phase 3: Review & Integration

1.  The `aiswarm review` and `aiswarm complete` commands manage the Pull Request and cleanup workflow as planned.

---

## GitHub Copilot Integration

The multi-layered context strategy (Master Guide, Dynamic Context File, State-Aware CLI, IDE Snippets) remains a core part of the plan.

## Next Steps

1.  **Restructure Project:** Convert the existing code into a proper Python package structure.
    *   Create a `setup.py` file.
    *   Rename `aiswarm-cli` to `aiswarm_cli`.
    *   Move the script logic into `aiswarm_cli/main.py`.
2.  **Implement `init` Command:** Finalize the `init` command logic within the new package structure.
3.  **Implement `task` Command:** Begin implementation of the `task` command, focusing on the `git worktree` creation logic.