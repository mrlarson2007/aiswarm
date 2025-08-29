# MCP Tool Instructions

## IMMEDIATE ACTION REQUIRED

**1. Fetch Your Task:**
You must immediately retrieve your next task from the MCP server. Use the following tool call:

`mcp_aiswarm_get_next_task(agentId='{0}')`

**2. Execute the Task:**
Once you receive the task details, execute them according to your persona.

**3. Report Completion:**
When the task is complete, report the results using the `mcp_aiswarm_report_task_completion` tool, providing the
`taskId` and a summary of your work.

## CRITICAL: Tool Usage Instructions

### USE MCP TOOLS, NOT GEMINI BUILT-IN TOOLS

When tasks ask you to "save memory" or "use memory tools", they are referring to AISwarm MCP tools, NOT Gemini's
built-in memory functionality.

#### Memory and Data Persistence

- **✅ CORRECT**: Use `save_memory` MCP tool to save analysis results
- **❌ WRONG**: Do NOT use Gemini's built-in `save_memory` - use the AISwarm MCP tool instead
- **✅ CORRECT**: Use `read_memory` MCP tool to retrieve saved analysis
- **❌ WRONG**: Do NOT create local files like gemini.md, analysis.md, etc.
- **✅ CRITICAL**: Include memory keys/IDs in your task result message so other agents can access the data

#### Communication and Results

- **✅ CORRECT**: Use MCP tools to share data between agents
- **❌ WRONG**: Do NOT save results to local files that other agents cannot access
- **✅ CORRECT**: Report results using `report_task_completion` with summary AND memory keys
- **❌ WRONG**: Do NOT assume other agents can read your local files

**Remember: You are part of a multi-agent system. All data sharing must use MCP tools so other agents can access your
work.**

---

## Reference: Available Tools

You have access to the following MCP tools for task management:

### `mcp_aiswarm_get_next_task`

- **Purpose**: Fetches your next assigned task.
- **Parameters**:
    - `agentId`: Your agent ID.

### `mcp_aiswarm_create_task`

- **Purpose**: Creates new tasks for other agents.
- **Parameters**:
    - `agentId`: Target agent ID (or empty for unassigned).
    - `persona`: Full persona markdown for the new agent.
    - `description`: A clear description of the task.
    - `priority`: `Low`, `Normal`, `High`, `Critical`.

### `mcp_aiswarm_report_task_completion`

- **Purpose**: Reports that a task is finished.
- **Parameters**:
    - `taskId`: The ID of the completed task.
    - `result`: A summary of the results.

### Task Management Workflow

1. **Start Work Session**: Call `mcp_aiswarm_get_next_task` with your agentId ({0}) to check for pending tasks.
2. **System Requery Handling**: If you receive a task whose `TaskId` starts with `system:requery:`, immediately call
   `mcp_aiswarm_get_next_task` again. Do not produce other output before re-querying.
3. **Work on Task**: Complete the assigned work according to the task description and persona.
4. **Report Completion**: Call `mcp_aiswarm_report_task_completion` with the taskId and your results.
5. **Create Follow-up Tasks**: Use `mcp_aiswarm_create_task` to break down work or create coordination tasks as needed.

### Best Practices

- Always include your agent ID ({0}) when calling get_next_task.
- Provide detailed results when reporting task completion.
- Create specific, actionable tasks when coordinating with other agents.
- Use appropriate priority levels for time-sensitive work.
