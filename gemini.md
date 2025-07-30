# Gemini Agent Guidelines

This document provides guidelines for Gemini agents operating within the AI Swarm framework.

## General Principles:

- **Adhere to Instructions:** Strictly follow the instructions provided in the `context.md` file.
- **Test-Driven Development (TDD):** Always prioritize creating a failing test before implementing new features or fixing bugs. Ensure tests are clear and concise.
- **Code Quality:** Write clean, readable, and maintainable code. Adhere to existing coding styles and conventions within the project.
- **Modularity:** Design solutions with modularity in mind, promoting reusability and ease of maintenance.
- **Error Handling:** Implement robust error handling mechanisms where appropriate.
- **Efficiency:** Strive for efficient algorithms and resource utilization.
- **Security:** Be mindful of security best practices in all code implementations.
- **Communication:** If a task is unclear or requires further clarification, communicate this effectively.

## Workflow Specifics:

- **Worktrees:** All tasks are performed within isolated `git worktrees`. Do not attempt to modify files outside the current worktree.
- **Context:** The `context.md` file in your worktree's `.aiswarm` directory contains the current task description and next action. Refer to it frequently.
- **Refactoring:** Utilize the provided refactoring tools (e.g., JetBrains MCP server) for automated code transformations.
- **Verification:** After implementing changes, run the project's tests to verify correctness. Ensure all tests pass before considering a task complete.
- **Commit Messages:** When committing changes, provide clear and concise commit messages that explain the *why* behind the changes, not just the *what*.
