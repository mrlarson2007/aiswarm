using AgentLauncher.Services;
using AgentLauncher.Tests.TestDoubles;
using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Entities;
using AISwarm.DataLayer.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Moq;

namespace AgentLauncher.Tests.Services;

/// <summary>
/// Tests for agent monitoring service that detects and terminates unresponsive agents
/// </summary>
public class AgentMonitoringServiceTests : IDisposable
{
    private readonly AgentMonitoringService _systemUnderTest;
    private readonly TestTimeService _timeService;
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly Mock<ILocalAgentService> _localAgentService;

    public AgentMonitoringServiceTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
            .Options;
        
        _dbContext = new CoordinationDbContext(options);
        _dbContext.Database.EnsureCreated();
        
        _timeService = new TestTimeService();
        _scopeService = new DatabaseScopeService(_dbContext);
        _localAgentService = new Mock<ILocalAgentService>();
        
        var config = new AgentMonitoringConfiguration
        {
            HeartbeatTimeoutMinutes = 5,
            CheckIntervalMinutes = 1
        };
        
        _systemUnderTest = new AgentMonitoringService(
            _scopeService,
            _localAgentService.Object,
            _timeService,
            config);
    }

    [Fact]
    public async Task WhenAgentHasStaleHeartbeat_ShouldKillAgent()
    {
        // Arrange - Create agent with old heartbeat
        var agent = new Agent
        {
            Id = "agent-123",
            PersonaId = "planner",
            AgentType = "planner",
            Status = AgentStatus.Running,
            ProcessId = "1234",
            LastHeartbeat = _timeService.UtcNow.AddMinutes(-10) // 10 minutes old
        };
        
        _dbContext.Agents.Add(agent);
        await _dbContext.SaveChangesAsync();

        // Act
        await _systemUnderTest.CheckForUnresponsiveAgentsAsync();

        // Assert
        _localAgentService.Verify(s => s.KillAgentAsync("agent-123"), Times.Once);
    }

    [Fact]
    public async Task WhenAgentHasRecentHeartbeat_ShouldNotKillAgent()
    {
        // Arrange - Create agent with recent heartbeat
        var agent = new Agent
        {
            Id = "agent-456",
            PersonaId = "implementer", 
            AgentType = "implementer",
            Status = AgentStatus.Running,
            ProcessId = "5678",
            LastHeartbeat = _timeService.UtcNow.AddMinutes(-2) // 2 minutes old (within threshold)
        };
        
        _dbContext.Agents.Add(agent);
        await _dbContext.SaveChangesAsync();

        // Act
        await _systemUnderTest.CheckForUnresponsiveAgentsAsync();

        // Assert
        _localAgentService.Verify(s => s.KillAgentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WhenAgentIsNotRunning_ShouldNotKillAgent()
    {
        // Arrange - Create stopped agent with old heartbeat
        var agent = new Agent
        {
            Id = "agent-789",
            PersonaId = "reviewer",
            AgentType = "reviewer", 
            Status = AgentStatus.Stopped,
            LastHeartbeat = _timeService.UtcNow.AddMinutes(-15)
        };
        
        _dbContext.Agents.Add(agent);
        await _dbContext.SaveChangesAsync();

        // Act
        await _systemUnderTest.CheckForUnresponsiveAgentsAsync();

        // Assert
        _localAgentService.Verify(s => s.KillAgentAsync(It.IsAny<string>()), Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}