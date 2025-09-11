using System.Text.Json;
using AISwarm.Infrastructure.Models.A2A;

namespace AISwarm.Infrastructure
{
    public class A2AService : IA2AService
    {
        public static int MaxCommandLineLength = 4000; // A conservative limit for command line arguments

        private readonly IProcessLauncher _processLauncher;
        private readonly IFileSystemService _fileSystemService;
        private readonly IAppLogger _logger;

        private readonly string _agentExecutablePath;

        public A2AService(IProcessLauncher processLauncher, IFileSystemService fileSystemService, IAppLogger logger, string agentExecutablePath)
        {
            _processLauncher = processLauncher;
            _fileSystemService = fileSystemService;
            _logger = logger;
            _agentExecutablePath = agentExecutablePath;
        }

        public async Task<A2AAgentInstance> LaunchAgentAsync(A2AAgentConfig config)
        {
            if (config == null)
            {
                throw new ArgumentException("Configuration cannot be null.", "config");
            }

            // Use the centralized validation method
            config.Validate();

            var agentPath = _agentExecutablePath;
            string finalArgumentsString;

            // Build arguments for length check
            var tempArguments = new List<string>
            {
                $"--agent-name \"{config.AgentName}\"",
                $"--persona \"{config.Persona}\"",
                $"--persona-description \"{config.PersonaDescription}\""
            };

            if (!string.IsNullOrEmpty(config.Description))
                tempArguments.Add($"--description \"{config.Description}\"");
            if (!string.IsNullOrEmpty(config.Model))
                tempArguments.Add($"--model \"{config.Model}\"");
            if (config.Port.HasValue)
                tempArguments.Add($"--port {config.Port.Value}");
            if (config.Skills.Any())
                tempArguments.Add($"--skills \"{string.Join(",", config.Skills)}\"");
            if (config.Capabilities.Any())
                tempArguments.Add($"--capabilities \"{string.Join(",", config.Capabilities)}\"");

            var combinedArgumentsLength = string.Join(" ", tempArguments).Length;

            if (combinedArgumentsLength > MaxCommandLineLength)
            {
                _logger.Info($"Arguments too long ({combinedArgumentsLength} chars), writing config to file.");
                var configFileName = $"{config.AgentName}.json";
                var configFilePath = Path.Combine(config.WorkingDirectory, configFileName);
                var jsonConfig = JsonSerializer.Serialize(config);
                await _fileSystemService.WriteAllTextAsync(configFilePath, jsonConfig); // Blocking for simplicity in this context
                finalArgumentsString = $"--config-file \"{configFilePath}\"" ;
            }
            else
            {
                finalArgumentsString = string.Join(" ", tempArguments);
            }

            var processResult = _processLauncher.Launch(agentPath, finalArgumentsString, config.WorkingDirectory);

            var assignedPort = config.Port ?? 56789; // Use a default test port

            return new A2AAgentInstance
            {
                AgentId = Guid.NewGuid().ToString(),
                AgentName = config.AgentName,
                ProcessId = processResult.ProcessId,
                Status = A2AAgentStatus.Starting,
                AgentUrl = $"http://localhost:{assignedPort}",
                Capabilities = [.. config.Capabilities],
                LaunchedAt = DateTime.UtcNow
            };
        }
    }
}
