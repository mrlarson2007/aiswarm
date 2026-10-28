# AISwarm — OpenCode CLI MVP

AISwarm is a .NET 9 MCP server that delegates work to noninteractive OpenCode CLI
processes. Claude Desktop connects over **stdio only**; stdout carries MCP traffic,
logs go to stderr, and AISwarm does not run an HTTP server.

## Requirements and build

- .NET 9 SDK and runtime.
- A native OpenCode executable, installed and configured for your providers/models.
- A stdio MCP client supporting the official Tasks extension for delegation
  (MCP protocol `2026-07-28` or later).

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
Desktop's support for the Tasks extension and automatic conversation resumption
has not been verified. Unsupported clients can discover models, but delegation
fails explicitly rather than silently falling back to a blocking call.

Optional environment settings:

| Setting | Meaning |
| --- | --- |
| `OpenCode__Executable` | Absolute path to a native executable; defaults to `opencode` on `PATH`. |

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

`delegate_task` uses the official MCP Tasks extension to return a task ID while
OpenCode continues in the background. The timeout defaults to
600 seconds and cannot exceed 86,400 seconds.

Configure local models in OpenCode yourself (for example, using Ollama).
Catalog membership does **not** prove a model is local, downloaded, or reachable.
AISwarm does not bypass OpenCode permissions or provide a YOLO mode.

### Results and retained memory tools

- Use the extension's `tasks/get` to retrieve task state and final results.
- Use `tasks/cancel` to cancel a task with best-effort termination of its live root process tree.
- Custom `get_task_result`, `cancel_task`, and task resources have been removed.
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

## MCP task lifecycle

AISwarm uses `ModelContextProtocol` and the official
`ModelContextProtocol.Extensions.Tasks` package at version **2.3.0**, the latest
release verified for this change. The SDK manages task creation, background
execution, cancellation, state, and results; AISwarm does not maintain a parallel
task registry. OpenCode implements the provider-neutral `IAgentService` contract.

The client must opt into `io.modelcontextprotocol/tasks` in the request's
`io.modelcontextprotocol/clientCapabilities` metadata. The previous diagnostic
logging notifications and custom HTTP lifecycle callbacks have been removed.

SDK 2.3.0 does not automatically publish task transitions. A small stdio-only
adapter sends the SDK's typed `notifications/tasks/status` messages after SDK
store updates, including the final result, without periodic status calls. These
are best-effort transport notifications, not a negotiated subscription or durable
event stream; the SDK's subscription API does not yet expose task subscriptions.
Clients must handle the notification method explicitly. `tasks/get` remains
available for one-time retrieval or recovery after a missed notification.
Notifications may be duplicated or arrive out of order; use `lastUpdatedAt` and
the authoritative task state rather than treating arrival order as state order.

**A protocol notification does not guarantee that Claude Desktop resumes.**
There is no UI automation or built-in Desktop wake mechanism. Live client
compatibility must be checked before relying on unattended delegation.

## Limits and persistence

- At most four concurrent agent executions.
- Each output stream is capped at 1 Mi characters, with a truncation marker.
- Process errors, error NDJSON, truncated stdout, and timeouts produce tool errors.
  Under SDK semantics, a completed task can contain a `CallToolResult` with
  `IsError = true`; clients must check that flag, not just task status.
- Termination is best-effort: deliberately detached descendants can outlive the
  root. The MVP does not provide process-group/job-object containment.
- Host shutdown requests task termination and awaits active execution cleanup.
  The SDK's task records/results are
  retained in memory with a two-day TTL and are lost when the process exits:
  no durable replay or restart recovery.
- Shared memory uses the existing SQLite filename `.aiswarm/aiswarm.db`.
  No schema migration or compatibility with earlier installations is provided.

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
OpenCode/Desktop session. In-process MCP tests exercise the SDK task lifecycle
with a simulated agent; native process tests cover Linux. This does not verify
Desktop auto-resume.

OpenCode server API, A2A, and distributed queues remain deferred.
The [historical documentation index](docs/README.md)
is **superseded** for current usage by this README.

Licensed under the [MIT License](LICENSE).
