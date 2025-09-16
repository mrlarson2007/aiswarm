# A2A Server Extension for Gemini-CLI

## Overview

This specification describes creating a **Gemini CLI extension** that adds AISwarm-compatible A2A server functionality. The gemini-cli already contains a **fully functional A2A server** at `packages/a2a-server/` with complete agent capabilities including file writing, shell commands, web access, and all other gemini-cli tools.

**Key Discovery**: The existing a2a-server already supports full agent mode features through `CoreToolScheduler` and has access to the complete gemini-cli toolset.

**Our Approach**: Create a **Gemini extension** that wraps th1. Test with various AISwarm configuration scenarios
2. Validate compatibility with existing a2a-server features

### Total Implementation Time

Approximately 3-4 hours

## Extension Benefits

**Extension Benefits**:

- ✅ **No Core Modifications**: Extension system keeps changes isolated
- ✅ **Easy Installation**: `gemini extensions install aiswarm-a2a`
- ✅ **Version Management**: Independent versioning and updates
- ✅ **Clean Separation**: AISwarm-specific functionality as separate package
- ✅ **Backward Compatible**: Doesn't affect existing Gemini CLI functionality

## Core Requirements

### 1. Current State Analysis

✅ **Already Available in gemini-cli a2a-server**:

- Complete tool suite: `write_file`, `read_file`, `replace`, `run_shell_command`, `web_fetch`, `google_web_search`, `save_memory`, etc.
- Full `CoreToolScheduler` integration with tool confirmation flows
- A2A protocol compliance with `@a2a-js/sdk`
- Configuration system with approval modes and tool filtering
- Task management and execution with proper state handling
- Streaming responses and event handling

❌ **Missing for AISwarm Compatibility**:

- CLI parameters compatible with AISwarm test agent
- Dynamic agent configuration from command line
- Configuration file support
- Agent metadata customization (persona, skills, capabilities)

### 2. Extension Architecture

**Primary Goal**: Create a standalone Gemini extension that provides AISwarm-compatible A2A server functionality.

**Extension Structure**:

```text
aiswarm-a2a-extension/
├── package.json              # Extension metadata
├── src/
│   ├── index.ts             # Extension entry point
│   ├── commands/
│   │   └── a2a-server.ts    # A2A server command implementation
│   └── types/
│       └── config.ts        # AISwarm configuration types
└── README.md                # Installation and usage instructions
```

**Installation Flow**:

```bash
# Install the extension
gemini extensions install aiswarm-a2a

# Use the new command
gemini a2a-server --agent-name "my-agent" --persona "implementer"
```

### 3. Extension Implementation

**Key Insight**: Create a Gemini extension that wraps the existing a2a-server with AISwarm configuration.

```typescript
// aiswarm-a2a-extension/src/index.ts
import type { Extension } from '@google-gemini/gemini-cli';
import { a2aServerCommand } from './commands/a2a-server.js';

const extension: Extension = {
  name: 'aiswarm-a2a',
  version: '1.0.0',
  description: 'AISwarm-compatible A2A server for Gemini CLI',
  commands: {
    'a2a-server': a2aServerCommand
  },
  dependencies: {
    // Specify dependency on existing a2a-server package
    '@google-gemini/a2a-server': '^1.0.0'
  }
};

export default extension;
```

```typescript
// aiswarm-a2a-extension/src/commands/a2a-server.ts
import type { CommandModule } from 'yargs';
import fs from 'fs/promises';

interface A2AServerArgs {
  agentName: string;
  description?: string;
  port?: number;
  model?: string;
  persona?: string;
  skills?: string;
  capabilities?: string;
  workingDir?: string;
  yolo?: boolean;
  config?: string;
}

export const a2aServerCommand: CommandModule<{}, A2AServerArgs> = {
  command: 'a2a-server',
  describe: 'Run Gemini CLI as an A2A protocol server (AISwarm compatible)',
  builder: (yargs) => 
    yargs
      .option('agent-name', {
        type: 'string',
        describe: 'Agent name for identification',
        demandOption: true
      })
      .option('description', {
        type: 'string', 
        describe: 'Agent description',
        default: 'Gemini CLI A2A Agent'
      })
      .option('port', {
        type: 'number',
        describe: 'HTTP server port (auto-assigns if not specified)',
        default: 0
      })
      .option('model', {
        type: 'string',
        describe: 'Gemini model to use',
        default: 'gemini-2.0-flash-exp'
      })
      .option('persona', {
        type: 'string',
        describe: 'Agent persona type',
        default: 'implementer'
      })
      .option('skills', {
        type: 'string',
        describe: 'Comma-separated list of skills'
      })
      .option('capabilities', {
        type: 'string',
        describe: 'Comma-separated list of capabilities'
      })
      .option('working-dir', {
        type: 'string',
        describe: 'Working directory for the agent',
        default: process.cwd()
      })
      .option('yolo', {
        type: 'boolean',
        describe: 'Auto-confirm mode (bypass tool confirmations)',
        default: false
      })
      .option('config', {
        type: 'string',
        describe: 'JSON configuration file path'
      }),
  handler: async (argv) => {
    try {
      console.log('🚀 Starting Gemini CLI A2A Server with AISwarm configuration...');
      
      // Load configuration (CLI + file)
      const config = await loadConfiguration(argv);
      
      // Configure the existing a2a-server
      await configureA2AServer(config);
      
      // Import and start the existing a2a-server
      const { main: startA2AServer } = await import('@google-gemini/a2a-server');
      await startA2AServer();
      
    } catch (error) {
      console.error('❌ Failed to start A2A server:', error);
      process.exit(1);
    }
  }
};

// Configuration loading with file support
async function loadConfiguration(args: A2AServerArgs) {
  let config = { ...args };
  
  if (args.config) {
    try {
      const configFile = await fs.readFile(args.config, 'utf8');
      const fileConfig = JSON.parse(configFile);
      
      // Merge configs: CLI args override file config
      config = { ...fileConfig, ...args };
      
      console.log(`📄 Loaded configuration from: ${args.config}`);
    } catch (error) {
      console.error(`❌ Failed to load config file: ${args.config}`, error);
      process.exit(1);
    }
  }
  
  return config;
}

// Configure existing a2a-server via environment variables
async function configureA2AServer(config: A2AServerArgs): Promise<void> {
  // Configure port
  if (config.port && config.port > 0) {
    process.env['CODER_AGENT_PORT'] = config.port.toString();
  }
  
  // Configure model
  if (config.model) {
    process.env['GEMINI_MODEL'] = config.model;
  }
  
  // Configure approval mode
  if (config.yolo) {
    process.env['APPROVAL_MODE'] = 'yolo';
  }
  
  // Store agent metadata for agent card customization
  process.env['AGENT_NAME'] = config.agentName;
  process.env['AGENT_DESCRIPTION'] = config.description || 'Gemini CLI A2A Agent';
  process.env['AGENT_PERSONA'] = config.persona || 'implementer';
  process.env['AGENT_SKILLS'] = config.skills || '';
  process.env['AGENT_CAPABILITIES'] = config.capabilities || '';
  
  console.log(`📋 Agent: ${config.agentName} (${config.persona})`);
  console.log(`🔧 Model: ${config.model}`);
  console.log(`📁 Working Directory: ${config.workingDir}`);
  if (config.skills) console.log(`🎯 Skills: ${config.skills}`);
  if (config.capabilities) console.log(`⚡ Capabilities: ${config.capabilities}`);
}
```

### 4. Minimal Agent Card Enhancement

**Approach**: Modify the existing `coderAgentCard` to read from environment variables:

```typescript
// Minor enhancement to packages/a2a-server/src/http/app.ts
import type { AgentCard } from '@a2a-js/sdk';

// Enhanced agent card that reads from environment variables
function createDynamicAgentCard(): AgentCard {
  const agentName = process.env['AGENT_NAME'] || 'Gemini SDLC Agent';
  const description = process.env['AGENT_DESCRIPTION'] || 'An agent that generates code based on natural language instructions';
  const persona = process.env['AGENT_PERSONA'] || 'implementer';
  const skills = process.env['AGENT_SKILLS']?.split(',').filter(s => s.trim()) || [];
  const capabilities = process.env['AGENT_CAPABILITIES']?.split(',').filter(s => s.trim()) || [];
  
  return {
    name: agentName,
    description: description,
    url: 'http://localhost:41242/', // Updated dynamically by existing code
    provider: {
      organization: 'Google',
      url: 'https://google.com',
    },
    protocolVersion: '0.3.0',
    version: '0.0.2',
    capabilities: {
      streaming: true,
      pushNotifications: false,
      stateTransitionHistory: true,
    },
    defaultInputModes: ['text'],
    defaultOutputModes: ['text'],
    skills: [
      // Always include the core code generation skill
      {
        id: 'code_generation',
        name: 'Code Generation',
        description: 'Generates code snippets or complete files based on user requests',
        tags: ['code', 'development', 'programming'],
        examples: [
          'Write a python function to calculate fibonacci numbers.',
          'Create an HTML file with a basic button that alerts "Hello!" when clicked.',
        ],
        inputModes: ['text'],
        outputModes: ['text'],
      },
      // Add dynamic skills from environment
      ...skills.map(skill => ({
        id: skill.toLowerCase().replace(/\s+/g, '_'),
        name: skill,
        description: `${skill} capability`,
        tags: [skill.toLowerCase()],
        examples: [`Perform ${skill} tasks`],
        inputModes: ['text'],
        outputModes: ['text'],
      }))
    ],
    // Add metadata for persona and capabilities
    metadata: {
      persona,
      capabilities,
      aiswarm_compatible: true
    }
  };
}

// Replace the static coderAgentCard with dynamic version
const coderAgentCard: AgentCard = createDynamicAgentCard();
```

### 5. Enhanced A2A Server Implementation

**Note**: Build upon the existing `packages/a2a-server/src/http/app.ts` implementation:

```typescript
// Enhanced packages/a2a-server/src/http/app.ts 
import express from 'express';
import type { AgentCard } from '@a2a-js/sdk';
import { DefaultRequestHandler, InMemoryTaskStore } from '@a2a-js/sdk/server';
import { A2AExpressApp } from '@a2a-js/sdk/server/express';
import { CoderAgentExecutor } from '../agent/executor.js';
import type { AgentSettings } from '../types.js';

// Enhanced agent card with dynamic configuration
function createDynamicAgentCard(settings: AgentSettings): AgentCard {
  return {
    name: settings.agentName || 'Gemini SDLC Agent',
    description: settings.description || 'An agent that generates code based on natural language instructions',
    url: 'http://localhost:41242/', // Updated dynamically
    provider: {
      organization: 'Google',
      url: 'https://google.com',
    },
    protocolVersion: '0.3.0',
    version: '0.0.2',
    capabilities: {
      streaming: true,
      pushNotifications: false,
      stateTransitionHistory: true,
    },
    defaultInputModes: ['text'],
    defaultOutputModes: ['text'],
    skills: settings.skills?.map(skill => ({
      id: skill.toLowerCase().replace(/\s+/g, '_'),
      name: skill,
      description: `${skill} capability`,
      tags: [skill],
      examples: [`Perform ${skill} tasks`],
      inputModes: ['text'],
      outputModes: ['text'],
    })) || [],
    // Add persona and capabilities to metadata
    metadata: {
      persona: settings.persona,
      capabilities: settings.capabilities,
      workingDirectory: settings.workingDirectory,
      model: settings.model
    }
  };
}

// Enhanced createApp function to accept agent settings
export async function createApp(agentSettings?: AgentSettings) {
  try {
    const bucketName = process.env['GCS_BUCKET_NAME'];
    let taskStoreForExecutor: TaskStore;
    let taskStoreForHandler: TaskStore;

    if (bucketName) {
      logger.info(`Using GCSTaskStore with bucket: ${bucketName}`);
      const gcsTaskStore = new GCSTaskStore(bucketName);
      taskStoreForExecutor = gcsTaskStore;
      taskStoreForHandler = new NoOpTaskStore(gcsTaskStore);
    } else {
      logger.info('Using InMemoryTaskStore');
      const inMemoryTaskStore = new InMemoryTaskStore();
      taskStoreForExecutor = inMemoryTaskStore;
      taskStoreForHandler = inMemoryTaskStore;
    }

    // Enhanced agent executor with settings
    const agentExecutor = new CoderAgentExecutor(taskStoreForExecutor, agentSettings);
    
    // Dynamic agent card based on settings
    const dynamicAgentCard = agentSettings 
      ? createDynamicAgentCard(agentSettings)
      : coderAgentCard;

    const requestHandler = new DefaultRequestHandler(
      dynamicAgentCard,
      taskStoreForHandler,
      agentExecutor,
    );

    let expressApp = express();
    
    // Add agent settings middleware
    if (agentSettings) {
      expressApp.use((req, res, next) => {
        req.agentSettings = agentSettings;
        next();
      });
    }

    // Existing A2A protocol setup
    const appBuilder = new A2AExpressApp(requestHandler);
    expressApp = appBuilder.setupRoutes(expressApp, '');
    expressApp.use(express.json());

    // Enhanced task creation with agent settings
    expressApp.post('/tasks', async (req, res) => {
      try {
        const taskId = uuidv4();
        const taskAgentSettings = req.body.agentSettings as AgentSettings | undefined;
        const mergedSettings = { ...agentSettings, ...taskAgentSettings };
        const contextId = req.body.contextId || uuidv4();
        
        const wrapper = await agentExecutor.createTask(
          taskId,
          contextId,
          mergedSettings,
        );
        await taskStoreForExecutor.save(wrapper.toSDKTask());
        res.status(201).json(wrapper.id);
      } catch (error) {
        logger.error('[CoreAgent] Error creating task:', error);
        const errorMessage = error instanceof Error ? error.message : 'Unknown error creating task';
        res.status(500).json({ error: errorMessage });
      }
    });

    return expressApp;
  } catch (error) {
    logger.error('[CoreAgent] Error creating app:', error);
    throw error;
  }
}
```

### 6. Enhanced CoderAgentExecutor

```typescript
// Enhanced packages/a2a-server/src/agent/executor.ts
import type { AgentSettings } from '../types.js';
import { loadConfig } from '../config/config.js';

export class CoderAgentExecutor {
  private taskStore: TaskStore;
  private agentSettings?: AgentSettings;
  
  constructor(taskStore: TaskStore, agentSettings?: AgentSettings) {
    this.taskStore = taskStore;
    this.agentSettings = agentSettings;
  }
  
  async createTask(
    taskId: string,
    contextId: string,
    taskAgentSettings?: AgentSettings
  ): Promise<TaskWrapper> {
    // Merge global and task-specific settings
    const effectiveSettings = { ...this.agentSettings, ...taskAgentSettings };
    
    // Load configuration with agent settings context
    const config = await loadConfig(
      await this.loadSettings(),
      await this.loadExtensions(),
      taskId,
      effectiveSettings
    );
    
    // Create task with persona and capabilities context
    const task = new Task({
      id: taskId,
      contextId,
      config,
      agentSettings: effectiveSettings
    });
    
    return new TaskWrapper(task, this.taskStore);
  }
  
  // ... existing methods enhanced with agent settings support
}
```

### 9. Integration with Main Gemini CLI

```typescript
// packages/cli/src/gemini.tsx (enhance existing structure)
import { a2aServerCommand } from './commands/a2a.js';
import { mcpCommand } from './commands/mcp.js';
import { extensionsCommand } from './commands/extensions.js';

// In the existing yargs configuration
export async function parseArguments(settings: Settings): Promise<CliArgs> {
  return await yargs(hideBin(process.argv))
    .scriptName('gemini')
    .usage('Usage: $0 [prompt]')
    // ... existing options ...
    
    // Add the enhanced a2a-server command
    .command(a2aServerCommand)
    
    // Existing commands
    .command(mcpCommand)
    .command(extensionsCommand)
    // ... other existing commands ...
    .parse();
}
```

### 10. Usage Examples

```bash
# Start A2A server with AISwarm-compatible parameters
gemini a2a-server \
  --agent-name "my-implementer" \
  --persona "implementer" \
  --skills "typescript,react,node.js" \
  --capabilities "code-generation,testing,debugging" \
  --port 3001

# Auto-assign port with specific model
gemini a2a-server \
  --agent-name "code-reviewer" \
  --persona "reviewer" \
  --model "gemini-2.0-flash-exp" \
  --description "Code review specialist agent"

# Use configuration file (new feature)
gemini a2a-server --config ./agent-config.json

# Current gemini CLI a2a-server (before enhancement)
# CODER_AGENT_PORT=3001 node packages/a2a-server/dist/src/http/server.js

# After enhancement - same functionality with better CLI
gemini a2a-server --agent-name "coder-agent" --port 3001
```

### 11. Configuration File Support

```json
{
  "agentName": "my-implementer-agent",
  "persona": "implementer", 
  "description": "An expert TypeScript developer specializing in React applications with TDD methodology",
  "model": "gemini-2.0-flash-exp",
  "port": 3001,
  "skills": ["typescript", "react", "jest", "cypress"],
  "capabilities": ["code-generation", "testing", "debugging", "code-review"],
  "workingDirectory": "/path/to/project",
  "yolo": false
}
```

### 12. Enhanced Configuration Loading

```typescript
// packages/a2a-server/src/config/config.ts (enhance existing)
export async function loadConfig(
  settings: Settings,
  extensions: Extension[],
  taskId: string,
  agentSettings?: AgentSettings
): Promise<Config> {
  // Existing configuration logic enhanced with agent settings
  const configParams: ConfigParameters = {
    sessionId: taskId,
    model: agentSettings?.model || DEFAULT_GEMINI_MODEL,
    // ... existing parameters ...
    
    // Add agent-specific context
    agentContext: agentSettings ? {
      name: agentSettings.agentName,
      persona: agentSettings.persona,
      skills: agentSettings.skills,
      capabilities: agentSettings.capabilities
    } : undefined
  };
  
  return new Config(configParams);
}
```

## Key Enhancements to Existing A2A Server

1. **AISwarm Compatibility**: Adds CLI parameters compatible with AISwarm test agent (--agent-name, --persona, --skills, etc.)
2. **Dynamic Agent Configuration**: Allows runtime configuration of agent metadata, skills, and capabilities
3. **Persona-Driven Behavior**: Integrates persona descriptions into the existing CoderAgentExecutor
4. **Enhanced Agent Card**: Dynamically generates agent cards based on CLI parameters
5. **Improved CLI Interface**: Replaces environment variable configuration with user-friendly CLI options
6. **Configuration File Support**: Adds JSON configuration file support for complex setups
7. **Backward Compatibility**: Maintains compatibility with existing A2A protocol and infrastructure

## Extension Implementation Priority

**Key Insight**: Use Gemini's extension system for clean, modular integration.

### **Phase 1: Extension Setup (1 hour)**

1. Create extension package structure with `package.json`
2. Implement extension entry point (`src/index.ts`)
3. Add dependency on existing `@google-gemini/a2a-server`
4. Create basic command structure

### **Phase 2: Command Implementation (1-2 hours)**

1. Implement `a2a-server` command with AISwarm-compatible parameters
2. Add configuration file loading logic
3. Add environment variable configuration for existing server
4. Test basic functionality

### **Phase 3: Extension Publishing (30 minutes)**

1. Package the extension for distribution
2. Test installation via `gemini extensions install`
3. Verify command registration and execution

### **Phase 4: Documentation and Testing (1 hour)**

1. Create installation and usage documentation
2. Test with various AISwarm configuration scenarios
3. Validate compatibility with existing a2a-server features

Total Implementation Time: Approximately 3-4 hours

## Key Benefits of Extension Approach

✅ **Clean Architecture**: Extension system provides proper isolation
✅ **Easy Installation**: Standard Gemini extension installation process
✅ **Version Management**: Independent versioning and updates
✅ **No Core Changes**: Doesn't modify existing Gemini CLI codebase
✅ **Modular Distribution**: Can be distributed separately from Gemini CLI
✅ **Backward Compatible**: Existing Gemini functionality remains unchanged
✅ **Full Tool Access**: Extension has access to complete Gemini toolset

## Installation and Usage

### **Installation**

```bash
# Install the AISwarm A2A extension
gemini extensions install aiswarm-a2a

# Verify installation
gemini extensions list
```

### **Usage Examples**

```bash
# AISwarm-compatible launch
gemini a2a-server \
  --agent-name "typescript-expert" \
  --persona "implementer" \
  --skills "typescript,react,jest" \
  --capabilities "code-generation,testing,debugging" \
  --port 3001 \
  --yolo

# Configuration file approach
gemini a2a-server --config ./my-agent.json

# Quick development setup  
gemini a2a-server --agent-name "dev-assistant" --yolo
```

### **Extension Package Structure**

```text
aiswarm-a2a-extension/
├── package.json
├── README.md
├── src/
│   ├── index.ts                 # Extension entry point
│   ├── commands/
│   │   └── a2a-server.ts       # A2A server command
│   └── types/
│       └── config.ts           # Configuration types
└── dist/                       # Compiled output
```

## Usage Examples (After Implementation)

```bash
# AISwarm-compatible launch
gemini a2a-server \
  --agent-name "typescript-expert" \
  --persona "implementer" \
  --skills "typescript,react,jest" \
  --capabilities "code-generation,testing,debugging" \
  --port 3001 \
  --yolo

# Configuration file approach
gemini a2a-server --config ./my-agent.json

# Quick development setup
gemini a2a-server --agent-name "dev-assistant" --yolo
```

**Result**: A powerful A2A agent with the complete gemini-cli toolset, configured through AISwarm-compatible CLI parameters, ready for immediate deployment in agent swarms.
