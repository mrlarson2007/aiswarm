# Accelerated Vector Integration with Microsoft Kernel Memory

## 🚀 Quick Implementation Strategy

**Goal**: Rapidly integrate vector embeddings using **Microsoft Kernel Memory** - a production-ready, battle-tested solution that provides everything we need out of the box.

**Why Kernel Memory?**
- ✅ **Production Ready**: Used by Microsoft and enterprise customers
- ✅ **Local Vector Storage**: SQLite, PostgreSQL, Redis support built-in  
- ✅ **Local Embeddings**: ONNX Runtime support for self-hosted models
- ✅ **Zero Cloud Dependencies**: Fully self-contained deployment
- ✅ **Web Service**: RESTful API that integrates perfectly with our A2A architecture
- ✅ **Semantic Kernel Integration**: Natural fit with .NET ecosystem

## 🏗️ Simplified Architecture

```
AISwarm Unified Server (MCP + A2A + Kernel Memory)
├── MCP Tools Layer
│   ├── Existing MCP tools
│   ├── A2A Integration Tools  
│   └── Kernel Memory Integration Tools (NEW)
├── Dual Transport
│   ├── Stdio (MCP clients)
│   └── HTTP (A2A agents + MCP HTTP)
├── A2A Protocol Layer
│   ├── Agent Cards & Task Management
│   └── Vector-Enhanced Task Assignment (NEW)
├── Kernel Memory Integration (NEW)
│   ├── MemoryWebClient → Local KM Service
│   ├── Document Indexing Pipeline
│   ├── Semantic Search & Context Retrieval
│   └── Self-Hosted Embeddings (ONNX)
└── Local Kernel Memory Service
    ├── SQLite Vector Store (sqlite-vec)
    ├── ONNX Embedding Models (local)
    ├── Document Processing Pipeline
    └── RESTful API (port 9001)
```

## 🛠️ Implementation: Use Existing Components

### 1. Kernel Memory Service (External Process)
```bash
# Run Kernel Memory service locally with SQLite + ONNX embeddings
docker run -p 9001:9001 \
  -v ./km-data:/app/data \
  -v ./km-config:/app/config \
  kernelmemory/service
```

**Configuration** (`appsettings.Development.json`):
```json
{
  "KernelMemory": {
    "Service": {
      "RunWebService": true,
      "OpenApiEnabled": true
    },
    "DataIngestion": {
      "OrchestrationType": "InProcess",
      "DistributedOrchestration": {
        "QueueType": "SimpleQueues"
      },
      "EmbeddingGeneratorTypes": ["ONNX"],
      "VectorDbTypes": ["SimpleVectorDb"],
      "DefaultSteps": ["extract", "partition", "gen_embeddings", "save_records"]
    },
    "Retrieval": {
      "VectorDbType": "SimpleVectorDb",
      "EmbeddingGeneratorType": "ONNX"
    },
    "Services": {
      "ONNX": {
        "ModelPath": "./models/all-MiniLM-L6-v2.onnx",
        "VocabPath": "./models/vocab.txt"
      },
      "SimpleVectorDb": {
        "StorageType": "SQLite",
        "ConnectionString": "Data Source=./data/vectors.db"
      }
    }
  }
}
```

### 2. AISwarm Integration Layer
```csharp
// Add to AISwarm.Server dependencies
public class KernelMemoryService : IKernelMemoryService
{
    private readonly MemoryWebClient _memory;
    private readonly ILogger<KernelMemoryService> _logger;

    public KernelMemoryService(IConfiguration config, ILogger<KernelMemoryService> logger)
    {
        var endpoint = config.GetValue<string>("KernelMemory:ServiceEndpoint") ?? "http://localhost:9001/";
        var apiKey = config.GetValue<string>("KernelMemory:ApiKey");
        
        _memory = string.IsNullOrEmpty(apiKey) 
            ? new MemoryWebClient(endpoint)
            : new MemoryWebClient(endpoint, apiKey);
        _logger = logger;
    }

    public async Task<string> IndexWorkspaceAsync(string workspacePath)
    {
        var documentId = $"workspace_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        
        // Index common file types
        var files = Directory.GetFiles(workspacePath, "*", SearchOption.AllDirectories)
            .Where(f => IsCodeFile(f) || IsDocumentFile(f))
            .Take(100) // Limit for performance
            .ToArray();

        await _memory.ImportDocumentAsync(new Document(documentId)
            .AddFiles(files)
            .AddTag("type", "workspace")
            .AddTag("indexed_at", DateTime.UtcNow.ToString("O")));

        return documentId;
    }

    public async Task<ContextResult[]> GetRelevantContextAsync(string taskDescription, int maxResults = 5)
    {
        var searchResult = await _memory.SearchAsync(
            query: taskDescription,
            limit: maxResults,
            minRelevance: 0.7);

        return searchResult.Results.Select(r => new ContextResult
        {
            Content = r.Partitions.FirstOrDefault()?.Text ?? "",
            Source = r.SourceName,
            Relevance = r.Partitions.FirstOrDefault()?.Relevance ?? 0,
            Tags = r.Partitions.FirstOrDefault()?.Tags?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new()
        }).ToArray();
    }

    public async Task<A2ATask> EnhanceTaskWithContextAsync(A2ATask task)
    {
        // Get relevant context from vector store
        var contexts = await GetRelevantContextAsync(task.Description);
        
        // Enhance task with vector-sourced context
        task.RelevantContext = contexts.Select(c => new TaskContext
        {
            Content = c.Content,
            Source = c.Source,
            Relevance = c.Relevance,
            Type = "vector_search"
        }).ToArray();

        return task;
    }

    private bool IsCodeFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return new[] { ".cs", ".py", ".js", ".ts", ".md", ".txt", ".json", ".yml", ".yaml" }.Contains(ext);
    }

    private bool IsDocumentFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return new[] { ".pdf", ".docx", ".doc", ".txt", ".md" }.Contains(ext);
    }
}
```

### 3. Vector-Enhanced MCP Tools
```csharp
[McpServerToolType]
public class VectorEnhancedA2ATools
{
    private readonly IKernelMemoryService _memory;
    private readonly IA2ATaskService _taskService;

    public VectorEnhancedA2ATools(IKernelMemoryService memory, IA2ATaskService taskService)
    {
        _memory = memory;
        _taskService = taskService;
    }

    [McpServerTool]
    public async Task<TaskCreationResult> CreateContextAwareTask(
        [Description("Task description")]
        string description,
        
        [Description("Task type")]
        string type = "code-generation",
        
        [Description("Include workspace context")]
        bool includeWorkspaceContext = true)
    {
        // Create base task
        var task = new A2ATask
        {
            Id = Guid.NewGuid().ToString(),
            Description = description,
            Type = type,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        // Enhance with vector context if requested
        if (includeWorkspaceContext)
        {
            task = await _memory.EnhanceTaskWithContextAsync(task);
        }

        // Save enhanced task
        await _taskService.CreateTaskAsync(task);

        return new TaskCreationResult
        {
            TaskId = task.Id,
            Description = task.Description,
            ContextSources = task.RelevantContext?.Length ?? 0,
            RelevantExamples = task.RelevantContext?.Where(c => c.Type == "vector_search").Count() ?? 0
        };
    }

    [McpServerTool]
    public async Task<string> IndexWorkspace(
        [Description("Workspace directory path")]
        string workspacePath)
    {
        var documentId = await _memory.IndexWorkspaceAsync(workspacePath);
        return $"Workspace indexed with document ID: {documentId}";
    }

    [McpServerTool]
    public async Task<SemanticSearchResult[]> SearchCodebase(
        [Description("Search query")]
        string query,
        
        [Description("Maximum results")]
        int maxResults = 10,
        
        [Description("Minimum relevance score")]
        double minRelevance = 0.7)
    {
        var contexts = await _memory.GetRelevantContextAsync(query, maxResults);
        
        return contexts.Where(c => c.Relevance >= minRelevance)
            .Select(c => new SemanticSearchResult
            {
                Content = c.Content,
                Source = c.Source,
                Relevance = c.Relevance,
                Tags = c.Tags
            }).ToArray();
    }
}
```

## 📦 Deployment Strategy

### Option 1: Docker Compose (Recommended for Development)
```yaml
# docker-compose.yml
version: '3.8'
services:
  kernel-memory:
    image: kernelmemory/service:latest
    ports:
      - "9001:9001"
    volumes:
      - ./km-data:/app/data
      - ./km-config/appsettings.Production.json:/app/appsettings.Production.json
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9001/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  aiswarm-server:
    build: .
    ports:
      - "5000:5000"  # MCP HTTP
      - "5001:5001"  # A2A HTTP
    depends_on:
      kernel-memory:
        condition: service_healthy
    environment:
      - KernelMemory__ServiceEndpoint=http://kernel-memory:9001/
    volumes:
      - ./workspace:/app/workspace:ro
```

### Option 2: Single Process (Production)
```csharp
// Host Kernel Memory as embedded service in AISwarm.Server
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Add Kernel Memory as embedded service
        builder.Services.AddKernelMemory(builder.Configuration)
            .WithSimpleVectorDb()  // SQLite vector storage
            .WithOnnxEmbeddings(); // Local ONNX embeddings
        
        // Add AISwarm services
        builder.Services.AddScoped<IKernelMemoryService, EmbeddedKernelMemoryService>();
        
        var app = builder.Build();
        
        // Configure both MCP and A2A endpoints
        app.ConfigureMcpServer();
        app.ConfigureA2AServer();
        
        await app.RunAsync();
    }
}

public class EmbeddedKernelMemoryService : IKernelMemoryService
{
    private readonly IKernelMemory _memory;
    
    public EmbeddedKernelMemoryService(IKernelMemory memory)
    {
        _memory = memory;
    }
    
    // Same implementation as MemoryWebClient version
    // but using direct IKernelMemory interface
}
```

## 🧠 Local Embedding Models

### ONNX Models (Recommended)
```bash
# Download pre-converted ONNX models
mkdir -p ./km-data/models

# all-MiniLM-L6-v2 (384 dimensions, 22MB)
wget https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx \
  -O ./km-data/models/all-MiniLM-L6-v2.onnx

# Download tokenizer files
wget https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json \
  -O ./km-data/models/tokenizer.json
```

### Alternative: Hugging Face Transformers with ONNX Runtime
```csharp
// Microsoft.ML.OnnxRuntime + sentence-transformers models
public class OnnxEmbeddingService : IEmbeddingGenerator
{
    private readonly InferenceSession _session;
    private readonly Tokenizer _tokenizer;

    public OnnxEmbeddingService()
    {
        // Load pre-trained ONNX model
        _session = new InferenceSession("./models/all-MiniLM-L6-v2.onnx");
        _tokenizer = Tokenizer.Create("./models/tokenizer.json");
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text)
    {
        // Tokenize input
        var tokens = _tokenizer.Encode(text);
        
        // Create input tensors
        var inputIds = new DenseTensor<long>(tokens.Ids.Select(id => (long)id).ToArray(), [1, tokens.Ids.Length]);
        var attentionMask = new DenseTensor<long>(tokens.AttentionMask.Select(mask => (long)mask).ToArray(), [1, tokens.AttentionMask.Length]);
        
        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };
        
        using var results = _session.Run(inputs);
        var embedding = results.First().AsTensor<float>();
        
        // Apply mean pooling and normalization
        var pooled = MeanPooling(embedding, attentionMask);
        var normalized = Normalize(pooled);
        
        return normalized.ToArray();
    }
}
```

## 🔧 Configuration Integration

### Enhanced AISwarm Configuration
```json
{
  "AISwarm": {
    "MCP": {
      "Transport": "dual",
      "HttpPort": 5000
    },
    "A2A": {
      "HttpPort": 5001,
      "VectorEnhanced": true
    },
    "KernelMemory": {
      "ServiceEndpoint": "http://localhost:9001/",
      "ApiKey": null,
      "AutoIndexWorkspace": true,
      "IndexingPatterns": ["*.cs", "*.py", "*.js", "*.ts", "*.md", "*.txt"],
      "ContextRetrievalSettings": {
        "MaxContextItems": 5,
        "MinRelevanceScore": 0.7,
        "IncludeWorkspaceContext": true
      }
    }
  },
  "KernelMemory": {
    "Service": {
      "RunWebService": true,
      "OpenApiEnabled": true
    },
    "DataIngestion": {
      "EmbeddingGeneratorTypes": ["ONNX"],
      "VectorDbTypes": ["SimpleVectorDb"]
    },
    "Services": {
      "ONNX": {
        "ModelPath": "./models/all-MiniLM-L6-v2.onnx",
        "TokenizerPath": "./models/tokenizer.json"
      },
      "SimpleVectorDb": {
        "StorageType": "SQLite",
        "ConnectionString": "Data Source=./data/vectors.db"
      }
    }
  }
}
```

## 🚀 Quick Start Implementation Plan

### Week 1: Foundation Setup
1. **Deploy Kernel Memory Service** (Day 1)
   - Set up Docker container with SQLite + ONNX
   - Configure local embedding models
   - Test basic document indexing and search

2. **Add KernelMemoryService Integration** (Day 2-3)
   - Create IKernelMemoryService interface
   - Implement MemoryWebClient wrapper
   - Add to AISwarm.Server DI container

3. **Enhance A2A Tasks with Context** (Day 4-5)
   - Modify A2ATask model to include context fields
   - Update task creation to include vector search
   - Test context-enhanced task generation

### Week 2: MCP Tools & Workspace Indexing
1. **Vector-Enhanced MCP Tools** (Day 1-2)
   - Implement CreateContextAwareTask MCP tool
   - Add IndexWorkspace MCP tool
   - Add SearchCodebase MCP tool

2. **Workspace Indexing Pipeline** (Day 3-4)
   - Automatic workspace indexing on startup
   - File watching for incremental updates
   - Performance optimization for large codebases

3. **End-to-End Testing** (Day 5)
   - Test full workflow: workspace indexing → context-aware tasks → enhanced code generation
   - Performance benchmarks and optimization

## 🎯 Benefits of This Approach

**Speed**: 
- ✅ No custom vector database implementation needed
- ✅ No custom embedding service development required
- ✅ Production-ready components from day one

**Quality**:
- ✅ Enterprise-grade Kernel Memory service (used by Microsoft)
- ✅ Optimized ONNX models for fast local inference
- ✅ Advanced document processing pipeline included

**Maintainability**:
- ✅ Well-documented Microsoft solution
- ✅ Active community and support
- ✅ Regular updates and improvements

**Deployment Flexibility**:
- ✅ Docker containers for easy deployment
- ✅ Embedded mode for single-binary distribution
- ✅ Horizontal scaling ready

This approach gets us to production-quality vector embeddings in **2 weeks instead of 2 months**, while maintaining all the advanced features we planned!