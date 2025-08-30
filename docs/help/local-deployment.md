# Local AISwarm Server Deployment Guide

This guide explains how to deploy AISwarm.Server locally for meta-development - using AISwarm to develop AISwarm itself!

## 🎯 Why Local Deployment?

When developing AISwarm, running the MCP server directly from source locks the files and can interfere with development workflows. Local deployment creates a standalone executable that:

- ✅ Doesn't lock source files during execution
- ✅ Allows using AISwarm MCP tools to develop AISwarm
- ✅ Provides a stable development environment
- ✅ Enables true meta-development workflows

## 🚀 Quick Start

The local deployment process is now streamlined into a single script that handles everything:

### 1. Deploy the Server

**Windows (PowerShell):**
```powershell
.\deploy-local-tool.ps1
```

**macOS/Linux (Bash):**
```bash
./deploy-local-tool.sh
```

### 2. Setup MCP Configuration

The deployment script automatically configures MCP for VS Code. No additional setup needed!

### 3. Start Meta-Development

- Open VS Code and the MCP tools will be automatically available
- Use Ctrl/Cmd+Shift+P → "AISwarm" to access MCP tools
- Create agents to work on AISwarm features
- Use AISwarm to coordinate AISwarm development

## 📁 What Gets Deployed

The deployment script creates a local dotnet tool that:

### Local Tool Installation

- **Package Location:** `./tools-packages/aiswarm-server.1.0.0-dev.nupkg`
- **Tool Command:** `dotnet tool run aiswarm-server`
- **MCP Integration:** Automatically configured in `.vscode/mcp.json`
- **Database:** Stored in `.aiswarm/local.db`

## 🔧 Advanced Configuration

### Script Options

Both deployment scripts support these options:

**PowerShell script:**
- `-Clean` - Perform a clean build (remove bin/obj first)  
- `-SkipTests` - Skip running tests before deployment

**Bash script:**
- `--clean` - Perform a clean build
- `--skip-tests` - Skip running tests before deployment

### MCP Configuration Options

The MCP configuration supports these environment variables:

- `WorkingDirectory` - Sets the working directory for the server
- `AISWARM_DB_PATH` - Custom database file location
- `ASPNETCORE_ENVIRONMENT` - ASP.NET Core environment (Development/Production)

## 🔄 Update Workflow

When you make changes to AISwarm.Server source code:

1. **Test changes:** Run tests and verify functionality
2. **Redeploy:** Run the deployment script again
3. **Restart VS Code:** Reload the MCP server (if needed)
4. **Continue meta-development:** Use updated tool for development

**Quick redeploy:**
```powershell
.\deploy-local-tool.ps1 -Clean
```

## 🧪 Testing the Setup

### Verify Deployment

1. Check that the local tool is installed:
   ```bash
   dotnet tool list --local
   ```

2. Test the tool directly:
   ```bash
   dotnet tool run aiswarm-server --help
   ```

3. Verify MCP tools are available in VS Code:
   - Open Command Palette (Ctrl/Cmd+Shift+P)
   - Search for "AISwarm"
   - Should see MCP tools like "mcp_aiswarm_list_agents"

### Test Meta-Development

1. **Create a task using MCP tools:**
   ```typescript
   mcp_aiswarm_create_task(
     agentId: null,
     persona: "implementer", 
     description: "Add a new feature to AISwarm",
     priority: "Normal"
   )
   ```

2. **Launch an agent:**
   ```typescript
   mcp_aiswarm_launch_agent(
     persona: "implementer",
     description: "Work on AISwarm feature development",
     worktreeName: "meta-dev-feature"
   )
   ```

3. **Monitor with list agents:**
   ```typescript
   mcp_aiswarm_list_agents()
   ```

## 🐛 Troubleshooting

### Common Issues

**"Server executable not found"**
- Run deployment script first
- Check output path permissions
- Verify .NET 9.0 is installed

**"MCP tools not appearing in VS Code"**
- Restart VS Code after MCP configuration
- Check `.vscode/mcp.json` exists and is valid
- Verify server executable path in MCP config

**"Permission denied" (Unix systems)**
- Ensure executable permissions: `chmod +x ~/.aiswarm/server/AISwarm.Server`
- Check deployment script permissions: `chmod +x scripts/deploy-local-server.sh`

**"Database connection issues"**
- Ensure `.aiswarm` directory exists in workspace
- Check database permissions
- Verify `AISWARM_DB_PATH` environment variable

### Debug Mode

Run the server in debug mode to see detailed logs:

```bash
# Set environment variable for verbose logging
export ASPNETCORE_ENVIRONMENT=Development
~/.aiswarm/server/AISwarm.Server
```

### Logs and Diagnostics

Check these locations for debugging:

- **VS Code Output Panel:** MCP Server logs
- **Terminal:** Server console output
- **Version info:** `./AISwarm.Server/version.json`

## 🔄 Development Workflow

### Typical Meta-Development Session

1. **Start with deployment:**
   ```bash
   # Deploy latest server
   .\deploy-local-tool.ps1
   ```

2. **Open VS Code:**
   ```bash
   code .
   ```

3. **Plan work with AISwarm:**
   - Use `mcp_aiswarm_create_task` to define work items
   - Launch agents with `mcp_aiswarm_launch_agent`
   - Monitor progress with list/status tools

4. **Iterate and improve:**
   - Make changes to source code
   - Test with regular development tools
   - Redeploy for meta-development testing
   - Use AISwarm to validate changes

### Best Practices

- **Keep deployments fresh:** Redeploy after significant changes
- **Use worktrees:** Let agents work in isolated git worktrees
- **Monitor agent status:** Regularly check agent health and task progress
- **Version tracking:** Use the local tool for consistent behavior
- **Backup databases:** Keep local.db backups for important sessions

## 🎯 Meta-Development Examples

### Use Case 1: Feature Development

```typescript
// Create a task for implementing a new MCP tool
mcp_aiswarm_create_task(
  agentId: null,
  persona: "implementer",
  description: "Implement new MCP tool for agent health monitoring",
  priority: "High"
)

// Launch an implementer agent
mcp_aiswarm_launch_agent(
  persona: "implementer",
  description: "Implement health monitoring MCP tool",
  worktreeName: "health-monitoring",
  yolo: false
)
```

### Use Case 2: Code Review

```typescript
// Create review task
mcp_aiswarm_create_task(
  agentId: null,
  persona: "reviewer",
  description: "Review PR #42: Add health monitoring feature",
  priority: "Normal"
)

// Launch reviewer agent
mcp_aiswarm_launch_agent(
  persona: "reviewer",
  description: "Review health monitoring implementation",
  worktreeName: "review-health-monitoring"
)
```

### Use Case 3: Testing and Quality

```typescript
// Create testing task
mcp_aiswarm_create_task(
  agentId: null,
  persona: "tester", 
  description: "Create comprehensive tests for health monitoring feature",
  priority: "High"
)

// Launch tester agent
mcp_aiswarm_launch_agent(
  persona: "tester",
  description: "Test health monitoring feature",
  worktreeName: "test-health-monitoring"
)
```

This local deployment setup enables true meta-development where AISwarm can be used to coordinate its own development, creating a powerful and recursive development workflow!