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
}