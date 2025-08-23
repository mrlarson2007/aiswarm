# Known Bugs

## Gemini CLI Launch Issue

**Date**: August 23, 2025  
**Status**: IN PROGRESS - Logging Improvements Added  
**Priority**: High  

### Description
The Gemini CLI agent launch process has a bug where Gemini starts but exits shortly after launch instead of staying in interactive mode.

### Recent Progress
✅ **FIXED**: Added comprehensive error logging to `ProcessLauncher.StartInteractive()`:
- Logs successful process starts with PID
- Logs detailed error messages if process fails to start
- Logs if process exits immediately with exit code
- Logs any exceptions during process launch

### Current Status
- ✅ Process logging now working: `Successfully started interactive process: 'pwsh.exe gemini -i "D:\dev\projects\aiswarm\reviewer_context.md"' (PID: 15928)`
- ❌ Gemini still exits after starting (Process ID 15928 not found in process list)

### Observed Behavior

1. AgentLauncher successfully registers agent in database (e.g., ID: `80023700-59b4-4231-a209-2a50d5c07412`)
2. Context file is created correctly (`reviewer_context.md`)
3. Gemini configuration is set up properly (`.gemini/settings.json` with MCP server `http://localhost:8081`)
4. ✅ **NEW**: ProcessLauncher successfully starts the process with detailed logging
5. ❌ Gemini process exits shortly after starting (needs investigation)

### Expected Behavior
Gemini should remain in interactive mode, allowing the agent to:
- Use MCP tools (`mcp_aiswarm_get_next_task`, `mcp_aiswarm_create_task`, etc.)
- Pick up the unassigned task (ID: `077becad-ee14-4816-bcab-033da2db7190`)
- Complete the end-to-end integration test

### Investigation Notes

- MCP Server is running correctly with dual transport (stdio + HTTP on port 8081)
- VS Code MCP integration works perfectly (stdio transport tested)
- Database connectivity confirmed working
- Task creation successful via VS Code tools
- ✅ **NEW**: ProcessLauncher now provides detailed error logging for debugging

### Next Steps for Debugging

1. ✅ **COMPLETED**: Add detailed logging to ProcessLauncher for better error diagnostics
2. **TODO**: Investigate why Gemini process exits after successful start
3. **TODO**: Check if Gemini CLI configuration or MCP server setup is causing early exit
4. **TODO**: Test Gemini CLI manually outside of AgentLauncher to isolate the issue
5. **TODO**: Review the context file content for any issues causing Gemini to exit

### Related Files

- `src/AISwarm.Infrastructure/ProcessLauncher.cs` - ✅ Updated with comprehensive logging
- `src/AgentLauncher/Commands/LaunchAgentCommandHandler.cs` - Agent launch logic
- `src/AgentLauncher/Services/GeminiService.cs` - Gemini CLI interaction
- `.gemini/settings.json` - Gemini configuration
- `reviewer_context.md` - Agent context file

### Test Environment

- Windows with PowerShell
- .NET 9.0
- Gemini CLI 0.1.22
- MCP Server with dual transport on port 8081
- All unit tests passing (77/77)