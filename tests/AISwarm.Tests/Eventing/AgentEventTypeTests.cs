using AISwarm.Infrastructure.Eventing;
using Shouldly;

namespace AISwarm.Tests.Eventing;

public class AgentEventTypeTests
{
    [Fact]
    public void WhenAgentEventTypeExists_ShouldHaveRegisteredValue()
    {
        // Arrange & Act
        var eventType = AgentEventType.Registered;
        
        // Assert
        eventType.ShouldBe(AgentEventType.Registered);
    }

    [Fact]
    public void WhenAgentEventTypeExists_ShouldHaveKilledValue()
    {
        // Arrange & Act
        var eventType = AgentEventType.Killed;
        
        // Assert
        eventType.ShouldBe(AgentEventType.Killed);
    }

    [Fact]
    public void WhenAgentEventTypeExists_ShouldHaveStatusChangedValue()
    {
        // Arrange & Act
        var eventType = AgentEventType.StatusChanged;
        
        // Assert
        eventType.ShouldBe(AgentEventType.StatusChanged);
    }
}