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
@click.option('--default-branch', default=None, help='The default source branch for new tasks.')
def init(test_command, port, default_branch):
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
        if default_branch is None:
            default_branch = click.prompt(
                "Please enter the default source branch for new tasks (e.g., 'main', 'master')",
                default="master"
            )
            
        config_data = {
            "test_command": test_command,
            "mcp_jetbrains_server": "http://localhost:{}".format(port),
            "default_branch": default_branch
        }
        with open(config_file, 'w') as f:
            json.dump(config_data, f, indent=4)
        click.echo("Created configuration file: {}".format(config_file))
    else:
        click.echo("Configuration file already exists.")
        
    click.echo("Project initialized.")

def get_sanitized_branch_name(prompt):
    """Creates a sanitized branch name from the task prompt."""
    sanitized = re.sub(r'[^a-zA-Z0-9 ]', '', prompt).lower()
    sanitized = re.sub(r'\s+', '-', sanitized)
    return sanitized[:50]

@cli.command()
@click.argument('prompt', nargs=-1)
@click.option('--from-branch', default=None, help='The source branch for the new worktree.')
@click.option('--test-file', default=None, help='The path to the test file to be created or modified.')
@click.option('--test-action', type=click.Choice(['create', 'modify']), default='create', help='Action for the test file: create or modify.')
def task(prompt, from_branch, test_file, test_action):
    """Starts or continues a task in a worktree."""
    
    prompt_or_path = " ".join(prompt)
    if os.path.isfile(prompt_or_path):
        with open(prompt_or_path, 'r', encoding='utf-8') as f:
            prompt_text = f.read()
        branch_name = get_sanitized_branch_name(os.path.basename(prompt_or_path))
        click.echo(f"Received new task from file: {prompt_or_path}")
    else:
        prompt_text = prompt_or_path
        branch_name = get_sanitized_branch_name(prompt_text)
        click.echo(f"Received new task: {prompt_text}")

    # Determine the source branch
    source_branch = from_branch
    if source_branch is None:
        try:
            with open(os.path.join(".aiswarm", "config.json"), 'r') as f:
                config = json.load(f)
                source_branch = str(config.get("default_branch", "master")).strip().replace('"', '')
        except FileNotFoundError:
            source_branch = "master"

    worktree_path = os.path.join(".worktrees", branch_name)
    
    if os.path.exists(worktree_path):
        click.echo("Worktree for this task already exists.")
        return
        
    try:
        subprocess.run(["git", "worktree", "add", "-b", branch_name, worktree_path, source_branch], check=True)
        click.echo("Created new worktree in: {}".format(worktree_path))
        
        # Create the dynamic context file
        context_dir = os.path.join(worktree_path, ".aiswarm")
        os.makedirs(context_dir, exist_ok=True)
        context_file = os.path.join(context_dir, "context.md")
        
        next_action = "Your turn, Gemini. Please implement the requested feature."
        if test_file:
            if test_action == 'create':
                next_action = f"Your turn, Gemini. Please create a failing test in `{test_file}` that satisfies the user's request."
            elif test_action == 'modify':
                next_action = f"Your turn, Gemini. Please modify the test in `{test_file}` to add a new assertion that satisfies the user's request."

        context_content = f"""# AI Swarm: Active Task

**Task:** {prompt_text}

**Status:** Awaiting implementation.

**Worktree:** {os.path.abspath(worktree_path)}

**➡️ Next Action:** {next_action}
"""
        
        with open(context_file, 'w', encoding='utf-8') as f:
            f.write(context_content)
        click.echo("Created dynamic context file: {}".format(context_file))
        
        click.echo("Task '{}' started.".format(branch_name))
        
    except subprocess.CalledProcessError as e:
        click.echo("Error creating git worktree: {}".format(e), err=True)
    except FileNotFoundError:
        click.echo("Error: git command not found. Is git installed and in your PATH?", err=True)

@cli.command()
@click.argument('task_name')
def complete(task_name):
    """Commits, merges, and cleans up a completed task."""
    click.echo("Completing task: {}".format(task_name))
    
    worktree_path = os.path.join(".worktrees", task_name)
    branch_name = task_name # Assuming task_name is the branch name

    if not os.path.exists(worktree_path):
        click.echo("Error: Worktree for task '{}' not found.".format(task_name), err=True)
        return

    try:
        # 1. Commit any final changes in the worktree
        click.echo("Committing final changes in worktree...")
        subprocess.run(["git", "-C", worktree_path, "add", "."], check=True)
        # Use a generic commit message, or allow user to specify one
        commit_message = f"feat: Complete task {task_name}"
        # Use --allow-empty in case there are no manual changes to commit
        subprocess.run(["git", "-C", worktree_path, "commit", "--allow-empty", "-m", commit_message], check=True)

        # 2. Get current branch to merge into
        current_branch_proc = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], capture_output=True, text=True, check=True)
        target_branch = current_branch_proc.stdout.strip()
        click.echo(f"Target branch for merge is: {target_branch}")

        # 3. Merge the task branch into the current branch
        click.echo(f"Merging {branch_name} into {target_branch}...")
        subprocess.run(["git", "merge", "--no-ff", branch_name], check=True)

        # 4. Clean up the worktree and branch
        click.echo("Cleaning up worktree and branch...")
        subprocess.run(["git", "worktree", "remove", worktree_path], check=True)
        subprocess.run(["git", "branch", "-d", branch_name], check=True)

        click.echo("Task '{}' completed and merged successfully.".format(task_name))

    except subprocess.CalledProcessError as e:
        click.echo("Error during git operation: {}".format(e), err=True)
    except FileNotFoundError:
        click.echo("Error: git command not found. Is git installed and in your PATH?", err=True)

@cli.command()
@click.argument('task_name')
@click.argument('file_path')
def refactor(task_name, file_path):
    """Triggers a refactoring action on a file in a worktree."""
    click.echo(f"Refactoring {file_path} in task {task_name}...")
    # Future implementation: send request to JetBrains MCP server
    click.echo("Refactoring complete.")

@cli.command()
def status():
    """Lists all active tasks and their current status."""
    click.echo("Displaying status of all active tasks...")
    
    worktrees_dir = ".worktrees"
    if not os.path.exists(worktrees_dir) or not os.listdir(worktrees_dir):
        click.echo("No active tasks.")
        return

    click.echo("Active tasks (worktrees):")
    for task_name in os.listdir(worktrees_dir):
        click.echo(f"- {task_name}")

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
