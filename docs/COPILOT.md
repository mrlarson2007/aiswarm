# GitHub Copilot Master Guide for the AI Swarm

This document is the central reference for using the `aiswarm` CLI and managing the AI development workflow. Keep this file open to provide context to GitHub Copilot.

## The Swarm Philosophy

Our development process is a partnership between a human manager (you) and a swarm of AI agents (Gemini). The process is designed to be rigorous, predictable, and efficient.

-   **Isolation:** Every task happens in an isolated `git worktree` to protect the `main` branch.
-   **Test-Driven:** We follow a strict Test-Driven Development (TDD) cycle. No code is written without a failing test first.
-   **Automated Refactoring:** We leverage a JetBrains IDE as a service to perform safe, automated code cleanup.
-   **Formal Review:** All code is formally reviewed via a GitHub Pull Request before being merged.

## The Workflow: A Step-by-Step Guide

1.  **Plan:** Define a new feature by creating a System Design doc and a `.feature` file.
2.  **Task (Test):** Start the work by creating a failing test.
    -   `aiswarm task "Create a failing test for the login button."`
3.  **Task (Code):** Implement the code to make the test pass.
    -   `aiswarm task "Implement the code for the login button to pass the test."`
4.  **Review:** When the feature is complete, create a Pull Request.
    -   `aiswarm review login-feature`
5.  **Complete:** After the PR is merged, clean up the local environment.
    -   `aiswarm complete login-feature`

## `aiswarm` CLI Command Cheat Sheet

-   `aiswarm init`
    -   **Purpose:** Sets up the project for the first time.
    -   **When to use:** Run this once when you clone the repository.

-   `aiswarm task "<prompt>"`
    -   **Purpose:** The primary command to get work done. It starts a new task or continues an existing one in a dedicated worktree.
    -   **Example:** `aiswarm task "Refactor the user model to include a profile picture URL."`

-   `aiswarm review <task_name>`
    -   **Purpose:** Creates a GitHub Pull Request for the specified task.
    -   **When to use:** When a feature is complete and ready for team review.

-   `aiswarm complete <task_name>`
    -   **Purpose:** Merges the completed task and cleans up the worktree.
    -   **When to use:** After a Pull Request has been approved and merged on GitHub.

-   `aiswarm status`
    -   **Purpose:** Shows all ongoing tasks and their current state.

-   `aiswarm adr "<decision>"`
    -   **Purpose:** Creates a new Architecture Decision Record.
    -   **Example:** `aiswarm adr "Switch from REST to GraphQL for the public API."`

-   `aiswarm cleanup`
    -   **Purpose:** A manual tool to remove any old worktrees that were not cleaned up properly.
