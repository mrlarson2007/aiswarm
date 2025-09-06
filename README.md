# AI Swarm - Multi-Agent Coordination Platform

[![Build and Test](https://github.com/mrlarson2007/aiswarm/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/mrlarson2007/aiswarm/actions/workflows/code-quality.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/code-quality.yml)
[![Security](https://github.com/mrlarson2007/aiswarm/actions/workflows/security.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/security.yml)
[![Release](https://github.com/mrlarson2007/aiswarm/actions/workflows/release.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/release.yml)

A comprehensive multi-agent coordination platform featuring both CLI tools for launching specialized AI agents and an MCP (Model Context Protocol) server for real-time agent task coordination and management.

> **🚀 A2A Integration Coming Soon**: We're implementing Agent-to-Agent (A2A) protocol support for push-based task delivery and direct agent communication. See [A2A Integration Plan](./docs/A2A_INTEGRATION_SUMMARY.md) for details.

## 🚀 Core Components

### 1. Agent Launcher CLI

A powerful command-line tool for launching specialized AI agents with isolated workspaces.

### 2. MCP Coordination Server  

A Model Context Protocol server providing real-time task coordination and agent management tools for VS Code and other MCP-compatible environments.

## 🌟 Key Features

### Agent Launcher

- **Multi-Agent Coordination**: Launch specialized AI agents (planner, implementer, reviewer, tester) with distinct personas
- **Git Worktree Integration**: Automatic isolation using git worktrees for conflict-free parallel work
- **Embedded Personas**: Built-in agent templates with comprehensive instructions
- **Custom Personas**: Support for external persona files via environment variables
- **Gemini CLI Integration**: Seamless integration with Google's Gemini CLI
- **Cross-Platform**: Windows, macOS, and Linux support

### MCP Coordination Server

- **Real-Time Task Management**: Create, assign, and track tasks across multiple agents
- **Agent Lifecycle Management**: Launch, monitor, and terminate agents programmatically
- **Task Coordination**: Distributed task queue with status tracking and completion reporting
- **VS Code Integration**: Native MCP protocol support for seamless IDE integration
- **Automatic Agent Status Management**: Dynamic status transitions and heartbeat monitoring
- **Failure Recovery**: Comprehensive error handling and task failure reporting

## 📋 Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [Gemini CLI](https://github.com/google-gemini/gemini-cli) installed and configured
- Git (for worktree management)
- **Windows**: Fully supported
- **Mac/Linux**: Fully supported
- **VS Code**: For MCP server integration (optional)

## 🔧 MCP Server Setup

The AI Swarm MCP server provides real-time task coordination and agent management tools that integrate directly with VS Code and other MCP-compatible environments.

### VS Code Integration

1. Add the following configuration to your VS Code workspace `.vscode/mcp.json`:

```json
{
    "servers": {
        "aiswarm": {
            "type": "stdio",
            "command": "dotnet",
            "args": [
                "run",
                "--project",
                "src/AISwarm.Server"
            ],
            "env": {
                "WorkingDirectory": "${workspaceFolder}"
            }
        }
    }
}
```

2. The server will automatically start when VS Code loads and MCP tools are accessed.

### Available MCP Tools

The MCP server provides the following tools for agent coordination:

#### Task Management Tools

- **`mcp_aiswarm_create_task`** - Create and assign tasks to agents with priority levels
- **`mcp_aiswarm_get_next_task`** - Agent polling endpoint with configurable timeout and retry logic
- **`mcp_aiswarm_get_task_status`** - Get detailed status information for a specific task
- **`mcp_aiswarm_get_tasks_by_status`** - Retrieve tasks filtered by status (Pending, InProgress, Completed, Failed)
- **`mcp_aiswarm_get_tasks_by_agent_id`** - Retrieve all tasks assigned to a specific agent
- **`mcp_aiswarm_get_tasks_by_agent_id_and_status`** - Get tasks for an agent filtered by status
- **`mcp_aiswarm_report_task_completion`** - Mark tasks as completed with results
- **`mcp_aiswarm_report_task_failure`** - Report task failures with error details

#### Agent Management Tools

- **`mcp_aiswarm_list_agents`** - List all registered agents with status, heartbeat, and persona filtering
- **`mcp_aiswarm_launch_agent`** - Launch new agents with specified personas and worktree isolation
- **`mcp_aiswarm_kill_agent`** - Terminate running agents and clean up associated resources

#### Memory & State Management Tools

- **`mcp_aiswarm_save_memory`** - Save data to memory for agent communication and state persistence
- **`mcp_aiswarm_read_memory`** - Reads stored memory entries with automatic access tracking

### MCP Tool Parameters

#### Task Creation

```typescript
mcp_aiswarm_create_task(
    agentId?: string,                                    // Optional agent ID for task assignment
    persona: string,                                     // Agent persona (implementer, reviewer, planner, etc.)
    description: string,                                 // Task description
    priority: "Low" | "Normal" | "High" | "Critical"    // Task priority level
)
```

#### Agent Launch

```typescript
mcp_aiswarm_launch_agent(
    persona: string,              // Agent persona type
    description: string,          // Agent task description  
    worktreeName?: string,        // Optional git worktree name
    model?: string,               // Optional AI model to use
    yolo: boolean = false         // Bypass permission prompts
)
```

#### Task Querying

```typescript
mcp_aiswarm_get_tasks_by_status(status: "Pending" | "InProgress" | "Completed" | "Failed")
mcp_aiswarm_get_tasks_by_agent_id_and_status(agentId: string, status: string)
mcp_aiswarm_get_next_task(agentId: string, timeoutMs?: number)  // Configurable timeout
```

#### Memory Operations

```typescript
mcp_aiswarm_save_memory(
    key: string,                  // Memory key
    value: string,                // Data to store
    type?: string,                // Content type (json, text, binary, etc.)
    metadata?: string,            // JSON metadata for queries
    namespace?: string            // Optional namespace for organization
)

mcp_aiswarm_read_memory(
    key: string,                  // Memory key to retrieve
    namespace?: string = ""       // Namespace (defaults to empty string)
)
```

### Key Features

- **Real-Time Coordination**: Event-driven architecture with InMemoryEventBus for immediate task and agent coordination
- **Configurable Task Polling**: Agents can poll for tasks with configurable timeouts, retry logic, and polling intervals
- **Automatic Status Management**: Agent status automatically transitions from Starting → Running during task polling
- **Event Notification System**: Comprehensive event system for TaskCreated, TaskClaimed, TaskCompleted, and TaskFailed events
- **Memory Management**: Persistent memory storage with namespace support and automatic access tracking
- **Failure Recovery**: Comprehensive error handling with configurable retry logic and task failure reporting
- **Heartbeat Monitoring**: Automatic agent heartbeat updates during task operations
- **Worktree Isolation**: Each agent runs in isolated git worktrees to prevent conflicts
- **Cross-Platform**: Works on Windows, macOS, and Linux
- **Yolo Mode**: Bypass permission prompts for autonomous agent operation

## 🛠️ Installation

### Agent Launcher CLI

#### Option 1: Install as Global Tool (Recommended)

```bash
# Install from NuGet (when published)
dotnet tool install --global AiSwarm.AgentLauncher

# Or install from GitHub Releases
# Download the latest release for your platform from:
# https://github.com/mrlarson2007/aiswarm/releases
```

#### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/mrlarson2007/aiswarm.git
cd aiswarm

# Build and install the tool globally
dotnet pack src/AgentLauncher --output .
dotnet tool install --global --add-source . AiSwarm.AgentLauncher

# Verify installation
aiswarm --list
```

#### Option 3: Run from Source

```bash
# Clone and run directly
git clone https://github.com/mrlarson2007/aiswarm.git
cd aiswarm
dotnet run --project src/AgentLauncher -- --list
```

### MCP Coordination Server

The MCP server runs directly from source and integrates with VS Code:

```bash
# Clone the repository (if not already done)
git clone https://github.com/mrlarson2007/aiswarm.git
cd aiswarm

# Test the server
dotnet run --project src/AISwarm.Server

# Configure VS Code integration (see MCP Server Setup section)
```

## 📖 Usage

### Basic Commands

```bash
# Initialize .aiswarm directory with template persona
aiswarm --init

# List available agent types and options
aiswarm --list

# Launch a planner agent in a new worktree
aiswarm --agent planner --worktree planning

# Launch an implementer for development work
aiswarm --agent implementer --worktree feature-auth

# Launch with specific Gemini model
aiswarm --agent reviewer --worktree code-review --model gemini-1.5-pro

# Launch in current directory (no worktree)
aiswarm --agent tester

# Test configuration without launching (dry run)
aiswarm --dry-run --agent planner --worktree test
```

### Agent Types

| Agent Type | Purpose | Best Use Cases |
|------------|---------|----------------|
| **planner** | Architecture and task planning | Breaking down features, creating ADRs, system design |
| **implementer** | Code development with TDD | Feature implementation, bug fixes, refactoring |
| **reviewer** | Code review and quality assurance | PR reviews, code quality checks, testing |
| **tester** | Test automation and validation | Writing tests, test automation, QA validation |

### Worktree Workflow

```bash
# 1. Plan a feature
aiswarm --agent planner --worktree feature-planning

# 2. Implement the feature (in separate worktree)
aiswarm --agent implementer --worktree feature-impl

# 3. Review the implementation
aiswarm --agent reviewer --worktree feature-review

# 4. Add comprehensive tests
aiswarm --agent tester --worktree feature-tests
```

## 🎭 Custom Personas

### Adding Custom Agents

Initialize your project with a template:

```bash
# Quick start: Initialize .aiswarm directory with template
aiswarm --init
```

Or manually create custom persona files in `.aiswarm/personas/` directory:

```bash
mkdir -p .aiswarm/personas
```

Create a file named `{agent_type}_prompt.md`:

```markdown
# Custom Agent Prompt

You are a specialized agent for...

## Your Responsibilities
- Task 1
- Task 2

## Example Tasks
- Example 1
- Example 2
```

### Environment Variable Support

Set `AISWARM_PERSONAS_PATH` to additional directories:

```bash
# Windows
$env:AISWARM_PERSONAS_PATH = "C:\path\to\personas;C:\another\path"

# macOS/Linux
export AISWARM_PERSONAS_PATH="/path/to/personas:/another/path"
```

## 🔧 Configuration

### Command Line Options

```bash
aiswarm [OPTIONS]

Options:
  --agent <type>        Agent type to launch (required)
  --worktree <name>     Create git worktree with specified name
  --model <model>       Gemini model to use (optional)
  --directory <path>    Working directory (default: current)
  --list               List available agents and exit
  --list-worktrees     List existing worktrees and exit
  --dry-run            Show what would be done without executing
  --init               Initialize .aiswarm directory with template persona files
  --help               Show help information
  --version            Show version information
```

### Git Worktree Management

The tool automatically:

- Creates git worktrees in `{repo-name}-{worktree-name}` format
- Validates worktree names (alphanumeric, hyphens, underscores)
- Sets up proper working directories
- Launches Gemini CLI in new PowerShell windows (Windows)

## 🏗️ Architecture

The AI Swarm platform consists of four main components:

- **Agent Launcher CLI**: Command-line tool for launching specialized AI agents with isolated workspaces
- **MCP Coordination Server**: Real-time task coordination and agent management via Model Context Protocol
- **Shared Infrastructure**: Core business logic services, interfaces, and agent persona templates
- **Data Layer**: Entity Framework database context with agent and task entities

## 🚀 Development

### Building from Source

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Build Agent Launcher CLI specifically
dotnet build src/AgentLauncher

# Build MCP Coordination Server specifically  
dotnet build src/AISwarm.Server

# Package Agent Launcher for distribution
dotnet pack src/AgentLauncher --output dist/
```

### Testing

The project includes comprehensive test coverage:

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test projects
dotnet test tests/AISwarm.Tests
```

#### Test Categories

- **MCP Tool Tests**: Comprehensive testing of all MCP server tools
- **Service Layer Tests**: Business logic and integration testing  
- **Database Tests**: Entity Framework and data layer testing
- **CLI Tests**: Command handler and validation testing

### MCP Server Development

```bash
# Start the MCP server in development mode
dotnet run --project src/AISwarm.Server

# The server runs on stdio transport for VS Code integration
# Use VS Code with MCP configuration to test interactively
```

### Updating the Tool

```bash
# 1. Make your changes
# 2. Build new package
dotnet pack src/AgentLauncher --output .

# 3. Uninstall old version
dotnet tool uninstall --global AiSwarm.AgentLauncher

# 4. Install new version
dotnet tool install --global --add-source . AiSwarm.AgentLauncher
```

### CI/CD Pipeline

This project uses GitHub Actions for continuous integration and deployment:

- **Build and Test**: Automatically builds and tests across Windows, Linux, and macOS
- **Code Quality**: Enforces code formatting, linting, and static analysis
- **Security**: Runs CodeQL analysis and dependency vulnerability scanning
- **Release**: Automated release creation with cross-platform binaries

📖 **[Complete CI/CD Documentation](docs/CICD.md)**

### Release Process

1. **Automated Releases**: Tag a commit with `v*.*.*` format to trigger automatic release
2. **Manual Releases**: Use the Release workflow with manual version input
3. **Artifacts**: Each release includes:
   - Cross-platform self-contained executables
   - NuGet package for global tool installation
   - SHA256 checksums for verification

### Platform Status

- **Windows**: ✅ Fully supported and tested
- **macOS**: ✅ Fully supported and tested
- **Linux**: ✅ Fully supported and tested

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [System.CommandLine](https://github.com/dotnet/command-line-api) for excellent CLI parsing
- [Gemini CLI](https://github.com/google-gemini/gemini-cli) for AI model integration
- Git worktrees for enabling parallel development workflows

## 📞 Support

- Create an [issue](https://github.com/mrlarson2007/aiswarm/issues) for bug reports
- Start a [discussion](https://github.com/mrlarson2007/aiswarm/discussions) for questions
- Check the [documentation](https://github.com/mrlarson2007/aiswarm/wiki) for detailed guides

---

Made with ❤️ for AI-powered development workflows

## 📚 Documentation & Plans

The following documents capture the current architecture, integration plans, and code samples for the A2A library approach and Gemini CLI integration:

- Implementation
- `docs/implementation/MASTER_IMPLEMENTATION_ROADMAP.md`
- `docs/implementation/MASTER_BRANCH_TRANSFER_SUMMARY.md`
- `docs/implementation/GEMINI_CLI_FORK_INTEGRATION_PLAN.md`
- `docs/implementation/A2A_SCHEMA_MIGRATION_PLAN.md`
- `docs/implementation/GEMINI_CLI_CODE_SAMPLES.md`
- `docs/implementation/A2A_SERVER_CODE_SAMPLES.md`

- System Design
- `docs/system-design/A2A_IMPLEMENTATION_ROADMAP.md`
- `docs/system-design/A2A_MCP_UNIFIED_INTEGRATION_PLAN.md`
- `docs/system-design/A2A_PRODUCTION_INTEGRATION_PLAN.md`
- `docs/system-design/GEMINI_CLI_A2A_CLIENT_PLAN.md`
- `docs/system-design/GEMINI_CLI_A2A_IMPLEMENTATION_SUMMARY.md`
- `docs/system-design/GEMINI_A2A_INTEGRATION_SUMMARY.md`
- `docs/system-design/VECTOR_EMBEDDINGS_INTEGRATION_PLAN.md`
- `docs/system-design/ACCELERATED_VECTOR_INTEGRATION_PLAN.md`

## 🔄 Roadmap: A2A Integration

AgentSwarm is evolving to support the Agent-to-Agent (A2A) protocol for next-generation multi-agent collaboration:

### Current Architecture (MCP-based)
- MCP server for task orchestration
- Polling-based agent task retrieval
- Manual agent coordination

### Future Architecture (MCP + A2A)
- **Push-based Task Delivery**: Immediate task dispatch via A2A protocol (zero latency vs. 5-30 second polling)
- **Agent-to-Agent Communication**: Direct communication between specialized agents
- **Dynamic Agent Discovery**: Automatic discovery of available agents and their capabilities
- **Enhanced Performance**: 90% reduction in database queries, 300% throughput increase

### Implementation Phases
1. **A2A Client Integration**: Add A2A client to AgentSwarm server for task dispatch
2. **A2A Agent Package**: Create reusable package for making gemini agents A2A-capable
3. **Push-based Dispatch**: Replace polling with immediate task delivery
4. **Agent Specialization**: Enable delegation to specialized agents (security, testing, documentation)

For detailed design and implementation plans, see:
- [A2A Integration Summary](./docs/A2A_INTEGRATION_SUMMARY.md)
- [A2A Implementation Plan](./docs/A2A_IMPLEMENTATION_PLAN.md)
- [A2A Push Architecture](./docs/A2A_PUSH_ARCHITECTURE.md)

---

- Features
- `docs/features/EMBEDDING_SETUP_GUIDE.md`

These are the canonical references for the unified MCP + A2A architecture and will be kept current as we implement the A2A library within `AISwarm.Server`.
