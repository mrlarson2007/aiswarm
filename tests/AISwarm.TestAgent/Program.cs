using AISwarm.TestAgent.Models;
using AISwarm.TestAgent.Services;

// Simple CLI argument parsing - Gemini CLI compatible arguments
string agentName = "aiswarm-test-agent";
string description = "AISwarm Test Agent for A2A integration testing";
int port = 0;
string? joinSwarm = null;
string? model = null;
bool yolo = false;

// Parse command line arguments
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--agent-name" when i + 1 < args.Length:
            agentName = args[++i];
            break;
        case "--description" when i + 1 < args.Length:
            description = args[++i];
            break;
        case "--port" when i + 1 < args.Length:
            int.TryParse(args[++i], out port);
            break;
        case "--join-swarm" when i + 1 < args.Length:
            joinSwarm = args[++i];
            break;
        case "--model" when i + 1 < args.Length:
            model = args[++i];
            break;
        case "--yolo":
            yolo = true;
            break;
        case "--help" or "-h":
            Console.WriteLine("AISwarm Test Agent - A2A protocol test server");
            Console.WriteLine("Usage: AISwarm.TestAgent [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --agent-name <name>     Agent name for identification (default: aiswarm-test-agent)");
            Console.WriteLine("  --description <desc>    Agent description");
            Console.WriteLine("  --port <port>          HTTP server port (0 = auto-assign)");
            Console.WriteLine("  --join-swarm <url>     A2A server URL (compatibility mode)");
            Console.WriteLine("  --model <model>        AI model (ignored by test agent)");
            Console.WriteLine("  --yolo                 Auto-confirm mode (ignored by test agent)");
            Console.WriteLine("  --help, -h             Show this help message");
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
Console.WriteLine($"📝 Description: {description}");
Console.WriteLine($"🌐 Port: {port}");
if (joinSwarm != null) Console.WriteLine($"🔗 Join Swarm: {joinSwarm} (compatibility mode)");
if (model != null) Console.WriteLine($"🧠 Model: {model} (ignored)");
if (yolo) Console.WriteLine($"⚡ YOLO Mode: Enabled (ignored)");

var builder = WebApplication.CreateBuilder();
builder.Services.AddSingleton<ITaskService, TaskService>();
builder.Services.AddSingleton<IAgentCardService>(_ => new AgentCardService(agentName, description));

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