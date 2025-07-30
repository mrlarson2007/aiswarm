import click
import os
import json
import re
import subprocess

@click.group()
def cli():
    """AI Swarm CLI to manage development tasks with Gemini and JetBrains."""
    pass

@cli.command()
@click.option('--test-command', default=None, help='The command to run tests.')
@click.option('--port', default=None, help='The port for the JetBrains MCP server.')
def init(test_command, port):
    """Initializes the aiswarm project configuration."""
    click.echo("Initializing project...")
    
    config_dir = ".aiswarm"
    config_file = os.path.join(config_dir, "config.json")
    
    if not os.path.exists(config_dir):
        os.makedirs(config_dir)
        click.echo("Created configuration directory: {}".format(config_dir))
        
    if not os.path.exists(config_file):
        if test_command is None:
            test_command = click.prompt(
                "Please enter the command to run your project's tests (e.g., 'pytest', 'npm test')",
                default=""
            )
        if port is None:
            port = click.prompt(
                "Please enter the port for the JetBrains MCP server",
                default="63342"
            )
            
        config_data = {
            "test_command": test_command,
            "mcp_jetbrains_server": "http://localhost:{}".format(port)
        }
        with open(config_file, 'w') as f:
            json.dump(config_data, f, indent=4)
        click.echo("Created configuration file: {}".format(config_file))
    else:
        click.echo("Configuration file already exists.")
        
    click.echo("Project initialized.")

import re
import subprocess

def get_sanitized_branch_name(prompt):
    """Creates a sanitized branch name from the task prompt."""
    sanitized = re.sub(r'[^a-zA-Z0-9 ]', '', prompt).lower()
    sanitized = re.sub(r'\s+', '-', sanitized)
    return sanitized[:50]

@cli.command()
@click.argument('prompt')
def task(prompt):
    """Starts or continues a task in a worktree."""
    click.echo("Received new task: {}".format(prompt))
    
    branch_name = get_sanitized_branch_name(prompt)
    worktree_path = os.path.join(".worktrees", branch_name)
    
    if os.path.exists(worktree_path):
        click.echo("Worktree for this task already exists.")
        return
        
    try:
        subprocess.run(["git", "worktree", "add", "-b", branch_name, worktree_path, "main"], check=True)
        click.echo("Created new worktree in: {}".format(worktree_path))
        click.echo("Task '{}' started.".format(prompt))
    except subprocess.CalledProcessError as e:
        click.echo("Error creating git worktree: {}".format(e), err=True)
    except FileNotFoundError:
        click.echo("Error: git command not found. Is git installed and in your PATH?", err=True)

@cli.command()
@click.argument('task_name')
def review(task_name):
    """Pushes a branch and creates a pull request for review."""
    click.echo("Creating review for task: {}".format(task_name))
    # Future implementation: git push, PR creation
    click.echo("Review for '{}' created.".format(task_name))

@cli.command()
@click.argument('task_name')
def complete(task_name):
    """Merges a completed task and cleans up the environment."""
    click.echo("Completing task: {}".format(task_name))
    # Future implementation: git merge, worktree cleanup
    click.echo("Task '{}' completed.".format(task_name))

@cli.command()
def status():
    """Lists all active tasks and their current status."""
    click.echo("Displaying status of all tasks...")
    # Future implementation: read state file and display
    click.echo("No active tasks.")

@cli.command()
@click.argument('decision_summary')
def adr(decision_summary):
    """Creates a new ADR file from a template."""
    click.echo("Creating new ADR: {}".format(decision_summary))
    # Future implementation: create ADR file from template
    click.echo("ADR created.")

@cli.command()
def cleanup():
    """Removes any stale worktrees or branches."""
    click.echo("Cleaning up stale environment...")
    # Future implementation: find and remove old worktrees
    click.echo("Cleanup complete.")

if __name__ == '__main__':
    cli()
