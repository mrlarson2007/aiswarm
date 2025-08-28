using AISwarm.Infrastructure.Eventing;
using Shouldly;

namespace AISwarm.Tests.Eventing;

public class AgentLifecyclePayloadTests
{
    [Fact]
    public void WhenIAgentLifecyclePayloadExists_ShouldHaveAgentIdProperty()
    {
        // Arrange
        var payload = new TestAgentPayload("agent-123");
        
        // Act & Assert
        payload.AgentId.ShouldBe("agent-123");
    }
    
    private record TestAgentPayload(string AgentId) : IAgentLifecyclePayload;
}
