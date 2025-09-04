# Gemini CLI Fork Integration Plan

## 🎯 Objective

Fork Google's Gemini CLI to create `@aiswarm/gemini-cli` with A2A (Agent-to-Agent) protocol support, enabling Gemini agents to connect to AISwarm A2A servers while maintaining upstream compatibility.

## 📋 Repository Strategy

### Fork Setup
```bash
# 1. Fork repository
gh repo fork google/gemini-cli --clone --remote

# 2. Rename remote and add upstream
cd gemini-cli
git remote rename origin aiswarm
git remote add upstream https://github.com/google/gemini-cli.git

# 3. Create AISwarm development branch
git checkout -b aiswarm-a2a-integration
```

### Repository Structure
```
mrlarson2007/gemini-cli-aiswarm
├── packages/cli/src/
│   ├── a2a/                    # A2A integration code
│   │   ├── client.ts           # A2A client implementation  
│   │   ├── protocol.ts         # A2A protocol definitions
│   │   └── agent-manager.ts    # Agent lifecycle management
│   ├── commands/
│   │   └── a2a.ts              # A2A CLI commands
│   ├── config/
│   │   └── a2a-config.ts       # A2A configuration
│   └── gemini.tsx              # Modified main entry point
├── docs/
│   ├── A2A_INTEGRATION.md      # A2A usage documentation
│   └── UPSTREAM_SYNC.md        # Upstream synchronization guide
├── scripts/
│   ├── sync-upstream.sh        # Automated upstream sync
│   └── build-a2a.sh           # A2A-specific build
└── .github/workflows/
    ├── upstream-sync.yml       # Automated upstream sync
    └── a2a-release.yml         # A2A release pipeline
```

## 🔧 Implementation Details

### A2A Client Integration

**File: `packages/cli/src/a2a/client.ts`**
```typescript
export interface A2AConfig {
  serverUrl: string;
  agentName: string;
  capabilities: string[];
  pollingInterval: number;
  timeout: number;
}

export class A2AClient extends EventEmitter {
  private config: A2AConfig;
  private isConnected: boolean = false;
  private isProcessingTask: boolean = false;

  constructor(config: A2AConfig) {
    super();
    this.config = config;
  }

  async connect(): Promise<void> {
    // Register agent with A2A server
    await this.registerAgent();
    
    // Start polling for tasks
    this.startTaskPolling();
    
    this.isConnected = true;
    this.emit('connected');
  }

  private async registerAgent(): Promise<void> {
    const agentCard = {
      name: this.config.agentName,
      capabilities: this.config.capabilities,
      version: "1.0.0",
      status: "available"
    };

    const response = await fetch(`${this.config.serverUrl}/agents/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(agentCard)
    });

    if (!response.ok) {
      throw new Error(`Agent registration failed: ${response.statusText}`);
    }
  }

  private async startTaskPolling(): Promise<void> {
    setInterval(async () => {
      if (this.isProcessingTask) return;
      
      try {
        const task = await this.checkForTasks();
        if (task) {
          this.isProcessingTask = true;
          await this.claimTask(task.id);
          this.emit('task-received', task);
        }
      } catch (error) {
        this.emit('error', error);
      }
    }, this.config.pollingInterval);
  }

  async completeTask(taskId: string, result: any): Promise<void> {
    await fetch(`${this.config.serverUrl}/tasks/${taskId}/complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ result })
    });
    
    this.isProcessingTask = false;
  }

  async failTask(taskId: string, error: string): Promise<void> {
    await fetch(`${this.config.serverUrl}/tasks/${taskId}/fail`, {
      method: 'POST', 
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ error })
    });
    
    this.isProcessingTask = false;
  }
}
```

### CLI Command Integration

**File: `packages/cli/src/commands/a2a.ts`**
```typescript
import { Command } from 'commander';
import { A2AClient } from '../a2a/client.js';

export function createA2ACommand(): Command {
  const command = new Command('a2a');
  
  command
    .description('Connect to A2A server as agent')
    .option('--server <url>', 'A2A server URL', 'http://localhost:5001')
    .option('--agent-name <name>', 'Agent name', 'gemini-agent')
    .option('--capabilities <caps...>', 'Agent capabilities', ['code-generation'])
    .option('--polling-interval <ms>', 'Task polling interval', '5000')
    .action(async (options) => {
      const client = new A2AClient({
        serverUrl: options.server,
        agentName: options.agentName,
        capabilities: options.capabilities,
        pollingInterval: parseInt(options.pollingInterval),
        timeout: 300000 // 5 minutes
      });

      client.on('connected', () => {
        console.log(`✅ Connected to A2A server: ${options.server}`);
        console.log(`🤖 Agent: ${options.agentName}`);
      });

      client.on('task-received', async (task) => {
        console.log(`📝 Received task: ${task.id}`);
        
        try {
          // Process task using existing Gemini CLI functionality
          const result = await processTaskWithGemini(task);
          await client.completeTask(task.id, result);
          console.log(`✅ Completed task: ${task.id}`);
        } catch (error) {
          await client.failTask(task.id, error.message);
          console.log(`❌ Failed task: ${task.id} - ${error.message}`);
        }
      });

      await client.connect();
      
      // Keep process alive
      process.on('SIGINT', () => {
        console.log('🛑 Shutting down A2A agent...');
        process.exit(0);
      });
    });

  return command;
}
```

### Main Entry Point Integration

**File: `packages/cli/src/gemini.tsx` (modifications)**
```typescript
// Add A2A command import
import { createA2ACommand } from './commands/a2a.js';

// In main CLI setup
program.addCommand(createA2ACommand());

// Add A2A mode detection
if (process.argv.includes('a2a')) {
  // A2A mode - minimal UI for agent operation
  console.log('🚀 Starting in A2A agent mode...');
}
```

## 🔄 Upstream Synchronization Strategy

### Automated Sync Workflow

**File: `.github/workflows/upstream-sync.yml`**
```yaml
name: Sync with Upstream

on:
  schedule:
    - cron: '0 2 * * 1' # Weekly on Monday at 2 AM
  workflow_dispatch:

jobs:
  sync:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          token: ${{ secrets.GITHUB_TOKEN }}
          
      - name: Setup upstream remote
        run: |
          git remote add upstream https://github.com/google/gemini-cli.git
          git fetch upstream
          
      - name: Merge upstream changes
        run: |
          git checkout main
          git merge upstream/main --no-edit
          
      - name: Test A2A functionality
        run: |
          npm install
          npm run build
          npm test
          
      - name: Create PR if changes
        if: ${{ github.event_name == 'schedule' }}
        run: |
          if [ -n "$(git diff HEAD~1)" ]; then
            git checkout -b upstream-sync-$(date +%Y%m%d)
            git push origin upstream-sync-$(date +%Y%m%d)
            gh pr create --title "Sync with upstream $(date +%Y-%m-%d)" --body "Automated upstream synchronization"
          fi
```

### Manual Sync Process

**File: `scripts/sync-upstream.sh`**
```bash
#!/bin/bash
set -e

echo "🔄 Syncing with upstream Gemini CLI..."

# Fetch latest upstream changes
git fetch upstream

# Create sync branch
SYNC_BRANCH="upstream-sync-$(date +%Y%m%d)"
git checkout -b $SYNC_BRANCH

# Merge upstream changes
git merge upstream/main --no-edit

# Test A2A functionality
echo "🧪 Testing A2A functionality..."
npm install
npm run build
npm run test:a2a

# Check for conflicts
if [ $? -eq 0 ]; then
    echo "✅ Sync successful - no conflicts"
    echo "🔀 Create PR: git push origin $SYNC_BRANCH"
else
    echo "❌ Conflicts detected - manual resolution required"
    echo "📝 Resolve conflicts and run: npm run test:a2a"
fi
```

## 📦 Distribution Strategy

### NPM Package

**Package Name**: `@aiswarm/gemini-cli`

**File: `package.json` (modifications)**
```json
{
  "name": "@aiswarm/gemini-cli",
  "version": "1.0.0-a2a.1",
  "description": "Google Gemini CLI with A2A protocol support",
  "keywords": ["gemini", "ai", "a2a", "agent", "aiswarm"],
  "repository": {
    "type": "git", 
    "url": "https://github.com/mrlarson2007/gemini-cli-aiswarm.git"
  },
  "scripts": {
    "test:a2a": "jest --testPathPattern=a2a",
    "build:a2a": "npm run build && npm run test:a2a"
  }
}
```

### Docker Image

**File: `Dockerfile.a2a`**
```dockerfile
FROM node:20-alpine

WORKDIR /app

# Install dependencies
COPY package*.json ./
RUN npm ci --only=production

# Copy built application
COPY dist/ ./dist/
COPY node_modules/ ./node_modules/

# Create non-root user
RUN adduser -D -s /bin/sh gemini
USER gemini

# Default to A2A mode
ENTRYPOINT ["node", "dist/index.js", "a2a"]
CMD ["--server", "http://host.docker.internal:5001", "--agent-name", "gemini-docker-agent"]
```

### GitHub Releases

**File: `.github/workflows/a2a-release.yml`**
```yaml
name: A2A Release

on:
  push:
    tags:
      - 'a2a-v*'

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          registry-url: 'https://registry.npmjs.org'
          
      - name: Install and build
        run: |
          npm ci
          npm run build
          npm run test:a2a
          
      - name: Publish to NPM
        run: npm publish --access public
        env:
          NODE_AUTH_TOKEN: ${{ secrets.NPM_TOKEN }}
          
      - name: Build Docker image
        run: |
          docker build -f Dockerfile.a2a -t aiswarm/gemini-cli:${{ github.ref_name }} .
          docker build -f Dockerfile.a2a -t aiswarm/gemini-cli:latest .
          
      - name: Push Docker image
        run: |
          echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
          docker push aiswarm/gemini-cli:${{ github.ref_name }}
          docker push aiswarm/gemini-cli:latest
```

## 🧪 Testing Strategy

### A2A Integration Tests

**File: `packages/cli/tests/a2a.test.ts`**
```typescript
import { A2AClient } from '../src/a2a/client.js';

describe('A2A Integration', () => {
  let mockServer: any;
  
  beforeEach(() => {
    // Setup mock A2A server
    mockServer = setupMockA2AServer();
  });

  test('should register agent successfully', async () => {
    const client = new A2AClient({
      serverUrl: mockServer.url,
      agentName: 'test-agent',
      capabilities: ['test'],
      pollingInterval: 1000,
      timeout: 5000
    });

    await client.connect();
    expect(mockServer.registeredAgents).toContain('test-agent');
  });

  test('should process task and complete', async () => {
    const client = new A2AClient(/* config */);
    const task = { id: 'test-task', description: 'Test task' };
    
    client.on('task-received', async (receivedTask) => {
      expect(receivedTask.id).toBe('test-task');
      await client.completeTask(receivedTask.id, { success: true });
    });

    await client.connect();
    mockServer.sendTask(task);
    
    // Verify task completion
    expect(mockServer.completedTasks).toContain('test-task');
  });
});
```

## 📚 Documentation

### A2A Integration Guide

**File: `docs/A2A_INTEGRATION.md`**
```markdown
# A2A Integration Guide

## Usage

### Basic A2A Agent
```bash
npx @aiswarm/gemini-cli a2a \
  --server http://localhost:5001 \
  --agent-name my-gemini-agent \
  --capabilities code-generation,review
```

### Docker Deployment
```bash
docker run -d \
  --name gemini-a2a-agent \
  aiswarm/gemini-cli:latest \
  --server http://host.docker.internal:5001 \
  --agent-name docker-agent
```

### Configuration Options
- `--server`: A2A server URL (default: http://localhost:5001)
- `--agent-name`: Unique agent identifier
- `--capabilities`: Comma-separated list of agent capabilities
- `--polling-interval`: Task polling interval in milliseconds
```

## 🚀 Implementation Timeline

### Step 1: Repository Setup
- [ ] Fork Gemini CLI repository
- [ ] Set up development branch and CI/CD
- [ ] Implement basic A2A client structure

### Step 2: A2A Integration
- [ ] Complete A2A client implementation
- [ ] Add CLI command interface
- [ ] Integrate with existing Gemini functionality

### Step 3: Testing & Documentation
- [ ] Comprehensive testing suite
- [ ] Documentation and usage guides
- [ ] Docker image and NPM package setup

### Step 4: Release & Integration
- [ ] First A2A release (v1.0.0-a2a.1)
- [ ] Integration testing with AISwarm A2A server
- [ ] Production deployment validation

## 🔗 Integration with AISwarm

Once this fork is complete, it integrates with AISwarm via:

1. **MCP Tool**: `LaunchGeminiAgent` tool in AISwarm can spawn these A2A-enabled agents
2. **A2A Server**: Agents connect to AISwarm's A2A server for task assignment
3. **Task Processing**: Agents use existing Gemini CLI capabilities to process tasks
4. **Result Reporting**: Completed work sent back through A2A protocol

This creates a seamless bridge between AISwarm's task management and Gemini's AI capabilities while maintaining compatibility with the broader A2A ecosystem.