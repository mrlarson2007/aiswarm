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

            // Ensure we have a port assigned before launching the agent
            int assignedPort;
            if (config.Port.HasValue)
            {
                // Use the specified port
                assignedPort = config.Port.Value;
            }
            else
            {
                // Auto-assign an available port
                assignedPort = GetAvailablePort();
                _logger.Info($"Auto-assigned port {assignedPort} for agent {config.AgentName}");
            }

            var agentPath = _agentExecutablePath;
            string finalArgumentsString;

            // Build arguments for length check
            var tempArguments = new List<string>
            {
                $"--agent-name \"{config.AgentName}\"",
                $"--persona \"{config.Persona}\"",
                $"--persona-description \"{config.PersonaDescription}\"",
                $"--port {assignedPort}"
            };

            if (!string.IsNullOrEmpty(config.Description))
                tempArguments.Add($"--description \"{config.Description}\"");
            if (!string.IsNullOrEmpty(config.Model))
                tempArguments.Add($"--model \"{config.Model}\"");
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
                
                // Update config with assigned port for serialization
                var configForFile = new A2AAgentConfig
                {
                    AgentName = config.AgentName,
                    Port = assignedPort,
                    Model = config.Model,
                    Description = config.Description,
                    Persona = config.Persona,
                    PersonaDescription = config.PersonaDescription,
                    Skills = config.Skills,
                    Capabilities = config.Capabilities,
                    WorkingDirectory = config.WorkingDirectory
                };
                
                var jsonConfig = JsonSerializer.Serialize(configForFile);
                await _fileSystemService.WriteAllTextAsync(configFilePath, jsonConfig);
                finalArgumentsString = $"--config-file \"{configFilePath}\"";
            }
            else
            {
                finalArgumentsString = string.Join(" ", tempArguments);
            }

            var processResult = _processLauncher.Launch(agentPath, finalArgumentsString, config.WorkingDirectory);

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

        /// <summary>
        /// Gets an available port by creating a temporary TCP listener on port 0 (auto-assign).
        /// </summary>
        /// <returns>An available port number</returns>
        private static int GetAvailablePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
