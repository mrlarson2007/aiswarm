import os
import subprocess

# agent_package.py
import os
import shutil

TEMPLATES = {
    "planner_prompt.md": "# Planner Agent Prompt\n\nYou are a planning agent. Your job is to:\n\n- Create feature files describing user stories and requirements.\n- Write ADRs (Architecture Decision Records) for major decisions.\n- Produce system design documents in markdown.\n- Organize and prioritize tasks for implementation agents.\n\n## Example Tasks\n\n- Write a feature file for user authentication.\n- Create an ADR for database technology selection.\n- Document the system architecture for the new module.\n",
    "implementer_prompt.md": "# Implementation Agent Prompt\n\nYou are an implementation agent. Your job is to:\n\n- Use Test-Driven Development (TDD) for all new features.\n- Write failing tests before implementing code.\n- Refactor code as needed to pass tests and improve quality.\n- Document your work in markdown files.\n\n## Example Tasks\n\n- Create a failing test for a new API endpoint.\n- Implement the endpoint to pass the test.\n- Refactor the code for clarity and performance.\n",
    "reviewer_prompt.md": "# Reviewer Agent Prompt\n\nYou are a code review agent. Your job is to:\n\n- Review code for correctness, style, and maintainability.\n- Suggest improvements and catch bugs.\n- Ensure all code changes are covered by tests.\n- Document review findings in markdown files.\n\n## Example Tasks\n\n- Review a pull request for a new feature.\n- Suggest improvements to test coverage.\n- Document review notes and action items.\n",
    "COPILOT_INSTRUCTIONS.md": "# Copilot Instructions\n\nThis file contains standard instructions for agents using GitHub Copilot or similar tools.\n\n## General Guidelines\n\n- Use the provided prompt files for your assigned role.\n- Follow TDD for implementation tasks.\n- Document all planning, design, and review activities in markdown files.\n- Communicate progress and blockers in your workspace.\n\n## Example Instructions\n\n- Planner: Focus on feature files, ADRs, and system design docs.\n- Implementer: Write failing tests first, then code to pass them.\n- Reviewer: Check for correctness, style, and test coverage.\n\n---\nAdd more instructions as needed for your workflow.\n"
}

def install_templates(target_dir=None):
    """Write all template files to the target directory (default: current directory)."""
    if target_dir is None:
        target_dir = os.getcwd()
    for fname, content in TEMPLATES.items():
        fpath = os.path.join(target_dir, fname)
        with open(fpath, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Installed template: {fpath}")

if __name__ == "__main__":
    install_templates()
        prompt_text = f.read().replace('"', '\"')
