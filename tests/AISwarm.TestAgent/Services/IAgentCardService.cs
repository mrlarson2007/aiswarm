using AISwarm.TestAgent.Models;

namespace AISwarm.TestAgent.Services;

public interface IAgentCardService
{
    AgentCard GetAgentCard(int port);
}
