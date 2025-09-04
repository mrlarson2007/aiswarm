# Gemini CLI A2A Client Implementation Plan

## Overview

This document outlines the implementation plan for adding A2A client capabilities to Google's Gemini CLI, enabling seamless integration with the AISwarm orchestration platform while preserving all existing CLI functionality.

## Vision

Enable Gemini CLI to operate in two modes:
1. **Direct Mode** (Current): `gemini "write a calculator"`
2. **Swarm Mode** (New): `gemini --join-swarm http://localhost:5002 --agent-name "gemini-coder"`

## Architecture Analysis

### Gemini CLI Structure (TypeScript Monorepo)
```
gemini-cli/
├── packages/
│   ├── cli/                    # Main CLI application (React Ink UI)
│   ├── core/                   # Shared functionality (@google/gemini-cli-core)
│   ├── a2a-server/            # Existing A2A server implementation
│   └── vscode-ide-companion/  # VS Code integration
├── scripts/                   # Build and development scripts
└── package.json              # Workspace configuration
```

### Key Integration Points
- **CLI Entry**: `packages/cli/index.ts` -> `packages/cli/src/gemini.tsx`
- **Config System**: `packages/cli/src/config/config.ts` (argument parsing)
- **Core Logic**: `@google/gemini-cli-core` exports (Config, GeminiClient, etc.)
- **A2A Infrastructure**: `@a2a-js/sdk` (already imported in a2a-server)

## Implementation Strategy

### Option 1: Separate A2A Client Package (Recommended) ⭐⭐⭐⭐⭐

**Benefits:**
- Clean separation of concerns
- Reusable across other projects
- Follows monorepo patterns
- Minimal impact on existing code

**Structure:**
```
packages/a2a-client/
├── src/
│   ├── index.ts                # Main exports
│   ├── client.ts              # A2AClient class
│   ├── agent-card.ts          # Gemini agent capabilities
│   ├── message-handler.ts     # Route A2A messages to Gemini core
│   ├── registration.ts        # Agent registration logic
│   └── types.ts               # TypeScript interfaces
├── package.json               # Dependencies (@a2a-js/sdk, @google/gemini-cli-core)
└── tsconfig.json              # TypeScript configuration
```

### Option 2: Extend CLI Package ⭐⭐⭐

**Benefits:**
- Direct integration
- Simpler dependency management

**Structure:**
```
packages/cli/src/
├── a2a-client/                # New directory
│   ├── client.ts
│   ├── agent-card.ts
│   └── message-handler.ts
└── gemini.tsx                 # Modified main entry
```

## Detailed Implementation Plan

### Phase 1: Core A2A Client Infrastructure

#### 1.1 Package Setup (Option 1)
```typescript
// packages/a2a-client/package.json
{
  "name": "@google/gemini-cli-a2a-client",
  "version": "0.1.0",
  "dependencies": {
    "@a2a-js/sdk": "^0.3.0",
    "@google/gemini-cli-core": "workspace:*",
    "express": "^4.18.0",
    "winston": "^3.10.0"
  }
}
```

#### 1.2 Agent Card Definition
```typescript
// packages/a2a-client/src/agent-card.ts
import type { AgentCard } from '@a2a-js/sdk';

export const geminiAgentCard: AgentCard = {
  name: 'Gemini CLI Agent',
  description: 'Google Gemini AI agent for code generation and development tasks',
  version: '1.0.0',
  protocolVersion: '0.3.0',
  capabilities: {
    streaming: true,
    pushNotifications: false,
    stateTransitionHistory: true,
  },
  skills: [
    {
      id: 'code_generation',
      name: 'Code Generation',
      description: 'Generate code in multiple programming languages with context awareness',
      tags: ['code', 'development', 'ai', 'gemini'],
      examples: [
        'Write a Python function to sort a list',
        'Create a React component for user authentication',
        'Generate SQL queries for data analysis'
      ],
      inputModes: ['text'],
      outputModes: ['text', 'file'],
    },
    {
      id: 'code_review',
      name: 'Code Review',
      description: 'Analyze and review code for best practices and potential issues',
      tags: ['review', 'analysis', 'quality'],
      inputModes: ['text', 'file'],
      outputModes: ['text'],
    },
    {
      id: 'documentation',
      name: 'Documentation Generation',
      description: 'Generate comprehensive documentation for code and APIs',
      tags: ['docs', 'documentation', 'api'],
      inputModes: ['text', 'file'],
      outputModes: ['text', 'file'],
    }
  ],
  defaultInputModes: ['text'],
  defaultOutputModes: ['text'],
};
```

#### 1.3 A2A Client Implementation
```typescript
// packages/a2a-client/src/client.ts
import { EventEmitter } from 'events';
import type { Config } from '@google/gemini-cli-core';
import type { AgentCard, Message, Task as A2ATask } from '@a2a-js/sdk';
import { geminiAgentCard } from './agent-card.js';

export interface A2AClientConfig {
  serverUrl: string;
  agentName?: string;
  geminiConfig: Config;
  workspaceDir?: string;
}

export class GeminiA2AClient extends EventEmitter {
  private config: A2AClientConfig;
  private registered = false;
  private ws: WebSocket | null = null;

  constructor(config: A2AClientConfig) {
    super();
    this.config = config;
  }

  async connect(): Promise<void> {
    // Establish WebSocket connection to AISwarm A2A server
    // Handle reconnection logic
  }

  async registerAgent(): Promise<void> {
    // Register with AISwarm using agent card
    // Send capabilities and configuration
  }

  async handleMessage(message: Message): Promise<void> {
    // Route A2A messages to Gemini core logic
    // Use existing Config and GeminiClient classes
  }

  private async executeTask(task: A2ATask): Promise<void> {
    // Execute code generation task using Gemini
    // Stream results back to orchestrator
  }

  async disconnect(): Promise<void> {
    // Clean disconnection from swarm
  }
}
```

#### 1.4 Message Handler Integration
```typescript
// packages/a2a-client/src/message-handler.ts
import type { Config, GeminiClient } from '@google/gemini-cli-core';
import type { Message, Task as A2ATask } from '@a2a-js/sdk';

export class GeminiMessageHandler {
  constructor(
    private geminiConfig: Config,
    private geminiClient: GeminiClient
  ) {}

  async handleCodeGenerationRequest(message: Message): Promise<string> {
    // Use existing Gemini core logic to process requests
    // Leverage Config.getGeminiClient() and streaming capabilities
  }

  async handleCodeReviewRequest(message: Message): Promise<string> {
    // Code review using Gemini's analysis capabilities
  }

  async handleDocumentationRequest(message: Message): Promise<string> {
    // Documentation generation
  }

  private async streamResponse(task: A2ATask, content: string): Promise<void> {
    // Stream results back using A2A protocol
  }
}
```

### Phase 2: CLI Integration

#### 2.1 Command Line Arguments
```typescript
// packages/cli/src/config/config.ts (modifications)
export interface CliArgs {
  // ... existing args
  joinSwarm?: string;          // A2A server URL
  agentName?: string;          // Agent identifier
  swarmMode?: boolean;         // Enable swarm mode
}

export async function parseArguments(settings: Settings): Promise<CliArgs> {
  const yargsInstance = yargs(hideBin(process.argv))
    // ... existing options
    .option('join-swarm', {
      type: 'string',
      description: 'Connect to AISwarm A2A server at specified URL'
    })
    .option('agent-name', {
      type: 'string',
      description: 'Agent name for swarm registration (default: gemini-{hostname})'
    })
    .option('swarm-mode', {
      type: 'boolean',
      description: 'Run in continuous swarm mode',
      default: false
    });
}
```

#### 2.2 Main Entry Point Integration
```typescript
// packages/cli/src/gemini.tsx (modifications)
import { GeminiA2AClient } from '@google/gemini-cli-a2a-client';

export async function main() {
  // ... existing initialization

  const config = await loadCliConfig(settings.merged, extensions, sessionId, argv);

  // NEW: A2A Swarm Mode
  if (argv.joinSwarm) {
    const agentName = argv.agentName || `gemini-${os.hostname()}-${sessionId.slice(0, 8)}`;
    
    const a2aClient = new GeminiA2AClient({
      serverUrl: argv.joinSwarm,
      agentName,
      geminiConfig: config,
      workspaceDir: process.cwd()
    });

    console.log(`🤖 Joining AISwarm at ${argv.joinSwarm} as agent "${agentName}"`);
    
    await a2aClient.connect();
    await a2aClient.registerAgent();
    
    if (argv.swarmMode) {
      // Keep running in swarm mode
      console.log('🔄 Running in continuous swarm mode. Press Ctrl+C to exit.');
      
      process.on('SIGINT', async () => {
        console.log('\n🛑 Disconnecting from swarm...');
        await a2aClient.disconnect();
        process.exit(0);
      });
      
      // Keep process alive
      return new Promise(() => {});
    } else {
      // Execute single task then disconnect
      console.log('📝 Waiting for task assignment...');
      // Handle single task execution
    }
    
    return;
  }

  // ... existing CLI logic for direct mode
}
```

### Phase 3: Advanced Features

#### 3.1 Context Sharing with Vector Memory
```typescript
// Integration with AISwarm vector memory system
export class ContextAwareMessageHandler extends GeminiMessageHandler {
  async enrichWithContext(message: Message): Promise<Message> {
    // Query AISwarm vector database for relevant context
    // Enhance Gemini prompts with workspace context
  }
}
```

#### 3.2 Multi-Model Coordination
```typescript
// Enable coordination with other AI agents
export interface SwarmCoordinationConfig {
  enablePeerCommunication: boolean;
  shareWorkspaceContext: boolean;
  acceptDelegatedTasks: boolean;
}
```

#### 3.3 Workspace Integration
```typescript
// Leverage existing file system tools
import { 
  EditTool, 
  WriteFileTool, 
  ShellTool 
} from '@google/gemini-cli-core';

export class WorkspaceAwareA2AClient extends GeminiA2AClient {
  async executeFileOperations(operations: FileOperation[]): Promise<void> {
    // Use Gemini CLI's existing file tools
    // Integrate with git checkpointing if enabled
  }
}
```

## Development Phases

### Phase 1: Foundation (Week 1)
- [ ] Create `packages/a2a-client` package
- [ ] Implement basic A2A client connection
- [ ] Create Gemini agent card definition
- [ ] Add CLI argument parsing

### Phase 2: Core Integration (Week 2)
- [ ] Implement message routing to Gemini core
- [ ] Add swarm mode to main entry point
- [ ] Test basic code generation via A2A
- [ ] Implement proper error handling and logging

### Phase 3: Enhanced Features (Week 3)
- [ ] Add context awareness via vector memory
- [ ] Implement streaming responses
- [ ] Add workspace file operations
- [ ] Create comprehensive test suite

### Phase 4: Production Ready (Week 4)
- [ ] Performance optimization
- [ ] Documentation and examples
- [ ] Integration with existing AISwarm MCP tools
- [ ] Deployment and distribution planning

## Testing Strategy

### Unit Tests
```typescript
// packages/a2a-client/src/__tests__/
├── client.test.ts           # A2A client functionality
├── message-handler.test.ts  # Message routing logic
└── agent-card.test.ts       # Agent capabilities
```

### Integration Tests
```typescript
// Test with mock AISwarm A2A server
// Validate Gemini core integration
// End-to-end swarm communication
```

### Manual Testing Scenarios
1. **Direct Mode**: Verify existing CLI functionality unchanged
2. **Swarm Registration**: Connect to AISwarm and register capabilities
3. **Task Execution**: Receive and execute code generation tasks
4. **Context Integration**: Use workspace context for enhanced generation
5. **Multi-Agent Coordination**: Work alongside other agents

## Deployment Strategy

### Development
- Fork Gemini CLI repository
- Create feature branch for A2A client
- Implement in phases with incremental testing

### Integration with AISwarm
- Update AISwarm A2A server to recognize Gemini agents
- Enhance MCP tools to leverage Gemini capabilities
- Create seamless workflow examples

### Distribution Options
1. **Fork Maintenance**: Maintain AISwarm-enhanced Gemini CLI fork
2. **Upstream Contribution**: Propose A2A client as upstream feature
3. **Plugin Architecture**: If Google adds plugin support, convert to plugin

## Success Metrics

### Technical
- [ ] Zero regression in existing Gemini CLI functionality
- [ ] Successful A2A protocol communication
- [ ] Context-aware code generation improvements
- [ ] Stable multi-agent coordination

### User Experience
- [ ] Seamless mode switching (direct vs swarm)
- [ ] Intuitive command line interface
- [ ] Clear status and error messaging
- [ ] Comprehensive documentation

## Risk Mitigation

### Technical Risks
- **Gemini CLI Updates**: Regular rebasing on upstream changes
- **A2A Protocol Changes**: Version compatibility handling
- **Performance Impact**: Minimal overhead in direct mode

### Strategic Risks
- **Google Official A2A Client**: Monitor for official implementation
- **Breaking Changes**: Maintain compatibility layers
- **Maintenance Burden**: Automated testing and CI/CD

## Conclusion

This implementation plan provides a comprehensive roadmap for adding A2A client capabilities to Gemini CLI while preserving its core functionality. The modular approach ensures clean integration and future maintainability.

The resulting system will enable your vision of seamless AI agent orchestration while maintaining the direct CLI access that users expect.

**Next Steps:**
1. Set up development environment with Gemini CLI fork
2. Begin Phase 1 implementation
3. Create integration tests with existing AISwarm infrastructure
4. Validate end-to-end workflow with vector memory system