# AI Swarm Agent Launcher

[![Build and Test](https://github.com/mrlarson2007/aiswarm/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/mrlarson2007/aiswarm/actions/workflows/code-quality.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/code-quality.yml)
[![Security](https://github.com/mrlarson2007/aiswarm/actions/workflows/security.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/security.yml)
[![Release](https://github.com/mrlarson2007/aiswarm/actions/workflows/release.yml/badge.svg)](https://github.com/mrlarson2007/aiswarm/actions/workflows/release.yml)

A powerful CLI tool for launching and coordinating AI agents with specialized personas using git worktrees for isolated workspaces.

## 🚀 Features

- **Multi-Agent Coordination**: Launch specialized AI agents (planner, implementer, reviewer, tester) with distinct personas
- **Git Worktree Integration**: Automatic isolation using git worktrees for conflict-free parallel work
- **Embedded Personas**: Built-in agent templates with comprehensive instructions
- **Custom Personas**: Support for external persona files via environment variables
- **Gemini CLI Integration**: Seamless integration with Google's Gemini CLI
- **Cross-Platform**: Designed for future cross-platform support
- **Self-Contained**: No external dependencies beyond .NET and Gemini CLI

## 📋 Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [Gemini CLI](https://ai.google.dev/gemini-api/docs/cli) installed and configured
- Git (for worktree management)
- **Windows**: Fully supported
- **Linux**: Fully supported  
- **Mac**: Coming soon (contributions welcome!)

## 🛠️ Installation

### Option 1: Install as Global Tool (Recommended)

```bash
# Install from NuGet (when published)
dotnet tool install --global AiSwarm.AgentLauncher

# Or install from GitHub Releases
# Download the latest release for your platform from:
# https://github.com/mrlarson2007/aiswarm/releases
```

### Option 2: Build from Source

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

### Option 3: Run from Source

```bash
# Clone and run directly
git clone https://github.com/mrlarson2007/aiswarm.git
cd aiswarm
dotnet run --project src/AgentLauncher -- --list
```

## 📖 Usage

### Basic Commands

```bash
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

Create custom persona files in `.aiswarm/personas/` directory:

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

## 🐧 Linux Support

### Terminal Requirements

The tool requires a terminal emulator to launch Gemini CLI sessions. The following are supported (tried in order):

- **gnome-terminal** (GNOME Terminal)
- **konsole** (KDE Terminal)  
- **xterm** (X Terminal Emulator)
- **x-terminal-emulator** (Generic terminal launcher)

Install at least one terminal emulator:

```bash
# Ubuntu/Debian
sudo apt install gnome-terminal

# Fedora/RHEL
sudo dnf install gnome-terminal

# Arch Linux
sudo pacman -S gnome-terminal
```

### Linux Installation

```bash
# Install .NET 9.0 SDK
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install dotnet-sdk-9.0

# Install Gemini CLI (follow Google's instructions)
# https://ai.google.dev/gemini-api/docs/cli

# Clone and install aiswarm
git clone https://github.com/mrlarson2007/aiswarm.git
cd aiswarm
dotnet pack src/AgentLauncher --output .
dotnet tool install --global --add-source . AiSwarm.AgentLauncher
```

### Troubleshooting Linux Issues

**No terminal emulator found:**
```bash
# Install a supported terminal
sudo apt install gnome-terminal  # or konsole, xterm

# Or run commands manually when prompted
cd /path/to/working/directory && gemini -i context_file.md
```

**Permission errors:**
```bash
# Ensure .NET tools are in PATH
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.bashrc
source ~/.bashrc
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

```
src/AgentLauncher/
├── Program.cs              # CLI entry point and command parsing
├── ContextManager.cs       # Persona management and context file creation
├── GitManager.cs          # Git worktree operations
├── GeminiManager.cs       # Gemini CLI integration
├── Resources/             # Embedded persona templates
│   ├── planner_prompt.md
│   ├── implementer_prompt.md
│   └── reviewer_prompt.md
└── AgentLauncher.csproj   # Project configuration
```

## 🚀 Development

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
- **Linux**: ✅ Fully supported with bash and terminal emulators
- **macOS**: 🔄 Coming soon - needs terminal/shell adaptation

Linux support includes automatic detection of terminal emulators (gnome-terminal, konsole, xterm, x-terminal-emulator) and cross-platform file path handling.

### Building from Source

```bash
# Build the project
dotnet build src/AgentLauncher

# Run tests (if available)
dotnet test

# Package for distribution
dotnet pack src/AgentLauncher --output dist/
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
- [Gemini CLI](https://ai.google.dev/gemini-api/docs/cli) for AI model integration
- Git worktrees for enabling parallel development workflows

## 📞 Support

- Create an [issue](https://github.com/mrlarson2007/aiswarm/issues) for bug reports
- Start a [discussion](https://github.com/mrlarson2007/aiswarm/discussions) for questions
- Check the [documentation](https://github.com/mrlarson2007/aiswarm/wiki) for detailed guides

---

**Made with ❤️ for AI-powered development workflows**
