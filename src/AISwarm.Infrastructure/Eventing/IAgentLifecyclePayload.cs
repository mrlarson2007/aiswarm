namespace AISwarm.Infrastructure.Eventing;

public interface IAgentLifecyclePayload : IEventPayload
{
    string AgentId
    {
        get;
    }
}
