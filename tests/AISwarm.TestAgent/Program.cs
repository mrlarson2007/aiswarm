using AISwarm.TestAgent.Models;
using AISwarm.TestAgent.Services;
using AISwarm.Infrastructure.Models.A2A;
using System.Text.Json;

// Configuration file path (first argument) or use CLI arguments
string? configFilePath = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;

// Default values
string agentName = "aiswarm-test-agent";
string description = "AISwarm Test Agent for A2A integration testing";
int port = 0;
string? joinSwarm = null;
string? model = null;
bool yolo = false;
string[]? skills = null;
string[]? capabilities = null;
string? persona = null;
string? systemPrompt = null;

// Load configuration file if provided
if (configFilePath != null && File.Exists(configFilePath))
{
    try
    {
        var configJson = await File.ReadAllTextAsync(configFilePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<A2AAgentConfig>(configJson, options);
        
        if (config != null)
        {
            // Apply configuration values directly from A2AAgentConfig
            agentName = config.AgentName;
            persona = config.Persona;
            model = config.Model;
            description = config.Description ?? description;
            systemPrompt = config.PersonaDescription;
            port = config.Port ?? port;
            skills = config.Skills.ToArray();
            capabilities = config.Capabilities.ToArray();
            
            Console.WriteLine($"📄 Loaded configuration from: {configFilePath}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Failed to load configuration file {configFilePath}: {ex.Message}");
        Console.WriteLine("Falling back to CLI arguments...");
    }
}

// Parse command line arguments (can override config file values)
var argsToProcess = configFilePath != null ? args.Skip(1).ToArray() : args;
for (int i = 0; i < argsToProcess.Length; i++)
{
    switch (argsToProcess[i])
    {
        case "--agent-name" when i + 1 < argsToProcess.Length:
            agentName = argsToProcess[++i];
            break;
        case "--description" when i + 1 < argsToProcess.Length:
            description = argsToProcess[++i];
            break;
        case "--port" when i + 1 < argsToProcess.Length:
            int.TryParse(argsToProcess[++i], out port);
            break;
        case "--join-swarm" when i + 1 < argsToProcess.Length:
            joinSwarm = argsToProcess[++i];
            break;
        case "--model" when i + 1 < argsToProcess.Length:
            model = argsToProcess[++i];
            break;
        case "--skills" when i + 1 < argsToProcess.Length:
            skills = argsToProcess[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
            break;
        case "--capabilities" when i + 1 < argsToProcess.Length:
            capabilities = argsToProcess[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
            break;
        case "--yolo":
            yolo = true;
            break;
        case "--help" or "-h":
            Console.WriteLine("AISwarm Test Agent - A2A protocol test server");
            Console.WriteLine("Usage: AISwarm.TestAgent [config-file] [options]");
            Console.WriteLine("       AISwarm.TestAgent [options]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  config-file            Path to JSON configuration file (A2AAgentConfig format)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --agent-name <name>     Agent name for identification (default: aiswarm-test-agent)");
            Console.WriteLine("  --description <desc>    Agent description");
            Console.WriteLine("  --port <port>          HTTP server port (0 = auto-assign)");
            Console.WriteLine("  --join-swarm <url>     A2A server URL (compatibility mode)");
            Console.WriteLine("  --model <model>        AI model (ignored by test agent)");
            Console.WriteLine("  --skills <skills>      Comma-separated list of skills (e.g., 'javascript,python,csharp')");
            Console.WriteLine("  --capabilities <caps>  Comma-separated list of capabilities (e.g., 'task-execution,code-generation')");
            Console.WriteLine("  --yolo                 Auto-confirm mode (ignored by test agent)");
            Console.WriteLine("  --help, -h             Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  AISwarm.TestAgent agent-config.json");
            Console.WriteLine("  AISwarm.TestAgent --agent-name 'my-agent' --skills 'javascript,python'");
            Console.WriteLine("  AISwarm.TestAgent agent-config.json --port 3100");
            Console.WriteLine();
            Console.WriteLine("Config file format (A2AAgentConfig):");
            Console.WriteLine("  { \"agentName\": \"my-agent\", \"persona\": \"implementer\", \"skills\": [\"javascript\"] }");
            return 0;
    }
}

// Auto-assign port if not specified
if (port == 0)
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
}

Console.WriteLine($"🤖 Starting AISwarm Test Agent: {agentName}");
if (!string.IsNullOrEmpty(persona)) Console.WriteLine($"👤 Persona: {persona}");
Console.WriteLine($"📝 Description: {description}");
Console.WriteLine($"🌐 Port: {port}");
if (joinSwarm != null) Console.WriteLine($"🔗 Join Swarm: {joinSwarm} (compatibility mode)");
if (model != null) Console.WriteLine($"🧠 Model: {model} (ignored)");
if (skills != null) Console.WriteLine($"🛠️ Skills: {string.Join(", ", skills)}");
if (capabilities != null) Console.WriteLine($"🔧 Capabilities: {string.Join(", ", capabilities)}");
if (yolo) Console.WriteLine($"⚡ YOLO Mode: Enabled (ignored)");

var builder = WebApplication.CreateBuilder();
builder.Services.AddSingleton<ITaskService, TaskService>();
builder.Services.AddSingleton<IAgentCardService>(_ => new AgentCardService(agentName, description, skills, capabilities, persona, systemPrompt));

var app = builder.Build();

app.MapGet("/.well-known/agent.json", (IAgentCardService agentCardService) => 
    agentCardService.GetAgentCard(port));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", agent = agentName, timestamp = DateTime.UtcNow }));

app.MapGet("/tasks", (ITaskService taskService) => taskService.GetTasks());
app.MapGet("/tasks/pending", (ITaskService taskService) => taskService.GetPendingTasks());

app.MapPost("/tasks", (CreateTaskRequest request, ITaskService taskService) =>
{
    var task = taskService.CreateTask(request);
    return Results.Created($"/tasks/{task.Id}", task);
});

app.MapPut("/tasks/{id}/claim", (string id, ClaimTaskRequest request, ITaskService taskService) =>
{
    var task = taskService.ClaimTask(id, request);
    return task == null ? Results.NotFound() : Results.Ok(task);
});

app.MapPut("/tasks/{id}/complete", (string id, CompleteTaskRequest request, ITaskService taskService) =>
{
    var task = taskService.CompleteTask(id, request);
    return task == null ? Results.NotFound() : Results.Ok(task);
});

var url = $"http://localhost:{port}";
Console.WriteLine($"🚀 Test agent running at: {url}");
Console.WriteLine($"📄 Agent card available at: {url}/.well-known/agent.json");
Console.WriteLine("Press Ctrl+C to stop...");

await app.RunAsync(url);
return 0;