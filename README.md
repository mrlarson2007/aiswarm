# AISwarm — OpenCode CLI MVP

AISwarm is a .NET 9 MCP server that delegates work to noninteractive OpenCode CLI
processes. Claude Desktop connects over **stdio only**; stdout carries MCP traffic,
logs go to stderr, and AISwarm does not run an HTTP server.

## Requirements and build

- .NET 9 SDK and runtime.
- A native OpenCode executable, installed and configured for your providers/models.
- Claude Desktop or another stdio MCP client.

From the repository root:

```bash
dotnet build -c Release
```

## Claude Desktop configuration

Add this entry to your Claude Desktop MCP configuration, replacing example paths
with absolute paths on your machine:

```json
{
  "mcpServers": {
    "aiswarm": {
      "command": "dotnet",
      "args": [
        "/absolute/path/to/aiswarm/src/AISwarm.Server/bin/Release/net9.0/AISwarm.Server.dll"
      ],
      "env": {
        "WorkingDirectory": "/absolute/path/to/aiswarm"
      }
    }
  }
}
```

Restart Desktop after changing its configuration. `WorkingDirectory` is the
absolute repository directory, not a shell variable or Desktop's current folder.

Optional environment settings:

| Setting | Meaning |
| --- | --- |
| `OpenCode__Executable` | Absolute path to a native executable; defaults to `opencode` on `PATH`. |
| `OpenCode__HookEndpoint` | Fixed local observer URL, for example `http://127.0.0.1:8099/events`. |

Omit `OpenCode__HookEndpoint` unless you separately run a local callback observer.
Desktop has no native arbitrary callback receiver configured by this example;
there is no built-in Desktop wake mechanism.

## Discover and delegate

The two core tools are:

| Tool | Parameters |
| --- | --- |
| `list_models` | `workingDirectory` |
| `delegate_task` | `instructions`, `model`, `workingDirectory`, optional `timeoutSeconds` |

Use an absolute existing `workingDirectory`. First call `list_models` there, then
pass an **exact discovered model ID** to `delegate_task`: `provider/model`,
including any additional slashes in the model portion. AISwarm never
automatically selects a model or falls back to another one.

Example tool arguments (replace the model placeholder with a discovered ID):

```json
{
  "instructions": "Review the repository and summarize the relevant code.",
  "model": "provider/model",
  "workingDirectory": "/absolute/path/to/aiswarm",
  "timeoutSeconds": 600
}
```

`delegate_task` returns a task ID for later retrieval. The timeout defaults to
600 seconds and cannot exceed 86,400 seconds.

Configure local models in OpenCode yourself (for example, using Ollama).
Catalog membership does **not** prove a model is local, downloaded, or reachable.
AISwarm does not bypass OpenCode permissions or provide a YOLO mode.

### Results and retained memory tools

- `get_task_result(taskId)` retrieves task state and captured results.
- `cancel_task(taskId)` cancels a task with best-effort termination of its live root process tree.
- Read MCP resource `aiswarm://tasks/{taskId}` for task details/results.
- Existing save/read/list/wait memory tools remain available.

Use separate directories or Git worktrees for concurrent edits to avoid conflicts.
AISwarm does not create a sandbox: OpenCode inherits the user's permissions.

## CLI contract

Discovery has a 30-second timeout. AISwarm checks `opencode --version` and
`opencode run --help` for `--model` and `--format` support, then reads newline
`provider/model` IDs from `opencode models`.

Tasks run noninteractively as:

```text
opencode run --model <exact-discovered-id> --format json
```

Instructions are supplied through stdin, not an interactive terminal. Raw NDJSON
stdout is retained rather than converted into a fabricated final answer.

## Lifecycle notifications and optional local hooks

The supervisor emits `Running`, `Completed`, `Failed`, and `Cancelled` hooks as
MCP `notifications/message` diagnostics with logger `aiswarm.tasks`. If configured,
it also sends a local JSON callback containing only
`{taskId,state,resultUri,exitCode}` — never the prompt or captured output.

Callback URLs must use HTTP(S) and a literal loopback IP address: no DNS hostnames,
credentials, or fragments. Redirects and proxy use are disabled. Hooks are local
only; there is no arbitrary shell hook. Delivery failures are retained as
`HookError` and do not discard task results.

**MCP diagnostic notifications DO NOT guarantee that Desktop resumes.** The user
may request the result once using the task ID or resource URI. There is no
automated polling or UI automation.

## Limits and persistence

- At most four concurrent tasks and 100 task records over the server's lifetime.
- Each output stream is capped at 1 Mi characters, with a truncation marker.
- Error NDJSON or truncated stdout makes a task `Failed` even with exit code 0;
  these failures retain the actual process exit code. Startup failure, cancellation,
  and timeout use synthetic exit code `-1`.
- Termination is best-effort: deliberately detached descendants can outlive the
  root. The MVP does not provide process-group/job-object containment.
- Host shutdown requests task termination. Task records/results exist in memory only and
  expire when the process exits: no durable replay or restart recovery.
- Retained memory uses a fresh SQLite database at `.aiswarm/opencode-mvp.db`.
  There is no migration; the old database is left untouched.

## Development and verification scope

```bash
dotnet build
dotnet test
dotnet format
```

OpenCode and Claude Desktop were unavailable in the validation environment.
The CLI contract was checked against current official development sources
([`cli.mdx`](https://github.com/anomalyco/opencode/blob/dev/packages/web/src/content/docs/cli.mdx)
and implementations under [`packages/cli`](https://github.com/anomalyco/opencode/tree/dev/packages/cli)), not a live
OpenCode/Desktop session. An in-process MCP test verifies transport from lifecycle
hook to resource retrieval using a simulated CLI; native process tests cover
Linux. This does not verify Desktop auto-resume.

OpenCode server API, A2A, distributed queues, and standard MCP task integration
are deferred, not MVP features. The [historical documentation index](docs/README.md)
is **superseded** for current usage by this README.

Licensed under the [MIT License](LICENSE).
