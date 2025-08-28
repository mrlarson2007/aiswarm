using AISwarm.Infrastructure.Eventing;
using Shouldly;

namespace AISwarm.Tests.Eventing;

public class AgentEventEnvelopeTests
{
    [Fact]
    public void WhenAgentEventEnvelopeCreated_ShouldHaveCorrectProperties()
    {
        // Arrange
        var eventType = AgentEventType.Registered;
        var timestamp = DateTimeOffset.UtcNow;
        var payload = new TestAgentPayload("agent-123");
        
        // Act
        var envelope = new AgentEventEnvelope(eventType, timestamp, payload);
        
        // Assert
        envelope.Type.ShouldBe(eventType);
        envelope.Timestamp.ShouldBe(timestamp);
        envelope.Payload.ShouldBe(payload);
    }
    
    private record TestAgentPayload(string AgentId) : IAgentLifecyclePayload;
}