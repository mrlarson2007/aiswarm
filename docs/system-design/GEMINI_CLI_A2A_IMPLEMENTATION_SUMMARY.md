# Gemini CLI A2A Client - Implementation Summary

## Quick Start Implementation Plan

### Goal
Add A2A client capabilities to Gemini CLI enabling:
- **Direct Mode**: `gemini "write a calculator"` (unchanged)
- **Swarm Mode**: `gemini --join-swarm http://localhost:5002 --agent-name "gemini-coder"`

### Architecture Approach

**Option 1: New A2A Client Package (Recommended)**

Create `packages/a2a-client/` in Gemini CLI monorepo:

```bash
packages/a2a-client/
├── src/
│   ├── client.ts              # Main A2A client
│   ├── agent-card.ts          # Gemini capabilities  
│   ├── message-handler.ts     # Route to Gemini core
│   └── index.ts               # Exports
├── package.json               # Dependencies
└── tsconfig.json
```

### Key Implementation Steps

#### Step 1: CLI Arguments (packages/cli/src/config/config.ts)

```typescript
.option('join-swarm', {
  type: 'string',
  description: 'Connect to AISwarm A2A server URL'
})
.option('agent-name', {
  type: 'string', 
  description: 'Agent name for swarm registration'
})
```

#### Step 2: Main Entry Integration (packages/cli/src/gemini.tsx)

```typescript
// After existing config loading...
if (argv.joinSwarm) {
  const a2aClient = new GeminiA2AClient({
    serverUrl: argv.joinSwarm,
    agentName: argv.agentName || `gemini-${sessionId}`,
    geminiConfig: config
  });
  
  await a2aClient.connect();
  await a2aClient.registerAgent();
  console.log('🤖 Joined AISwarm, waiting for tasks...');
  return; // Skip normal CLI flow
}
```

#### Step 3: A2A Client Core (packages/a2a-client/src/client.ts)

```typescript
export class GeminiA2AClient {
  async connect(): Promise<void> {
    // WebSocket to AISwarm A2A server
  }
  
  async registerAgent(): Promise<void> {
    // Send agent card with Gemini capabilities
  }
  
  async handleMessage(message: A2AMessage): Promise<void> {
    // Route to existing Gemini Config/GeminiClient
    // Stream results back to orchestrator
  }
}
```

#### Step 4: Agent Card Definition

```typescript
export const geminiAgentCard = {
  name: 'Gemini CLI Agent',
  skills: [{
    id: 'code_generation',
    name: 'Code Generation', 
    description: 'AI-powered code generation using Google Gemini'
  }],
  capabilities: { streaming: true }
};
```

### Integration with AISwarm

#### Update AISwarm A2A Server
- Recognize Gemini agent registrations
- Route appropriate tasks to Gemini agents
- Handle streaming responses

#### Update AISwarm MCP Tools
- Add MCP tool to launch Gemini agents
- Integrate with existing task creation workflow

### Development Phases

**Phase 1 (Week 1): Basic Connection**
- [ ] Create a2a-client package structure
- [ ] Implement WebSocket connection to AISwarm
- [ ] Add CLI arguments and basic integration

**Phase 2 (Week 2): Core Functionality** 
- [ ] Agent registration with capabilities
- [ ] Message routing to Gemini core logic
- [ ] Basic task execution and response

**Phase 3 (Week 3): Enhanced Features**
- [ ] Context integration with vector memory
- [ ] Streaming responses
- [ ] Error handling and reconnection

**Phase 4 (Week 4): Production Ready**
- [ ] Testing and optimization
- [ ] Documentation
- [ ] Integration with existing AISwarm workflow

### Technical Benefits

✅ **Zero regression** - Existing CLI functionality unchanged
✅ **Reuse existing infrastructure** - Leverages Gemini core classes
✅ **Clean integration** - Follows TypeScript monorepo patterns  
✅ **Future-proof** - Ready for Google's official A2A client

### Success Criteria

- Direct CLI mode works exactly as before
- Swarm mode successfully registers with AISwarm
- Tasks execute using full Gemini capabilities
- Context-aware generation via vector memory
- Seamless multi-agent coordination

This approach gives you the **best of both worlds**: direct Gemini CLI access when you want it, and orchestrated swarm capabilities when you need them!

### Next Action

Ready to start implementing? We can begin with:

1. Setting up the development environment (Gemini CLI fork)
2. Creating the basic package structure
3. Implementing the CLI argument integration

What would you like to tackle first?