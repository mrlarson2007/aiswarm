using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

public class FakeContextService : IContextService
{
    public string FailureMessage
    {
        get;
        set;
    } = string.Empty;

    public bool ShouldFail => !string.IsNullOrEmpty(FailureMessage);

    public string CreatedContextPath
    {
        get;
        set;
    } = "/test/context.md";

    public Task<string> CreateContextFile(string agentType, string workingDirectory)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(CreatedContextPath);
    }

    public Task<string> CreateContextFileWithAgentId(string agentType, string workingDirectory, string? agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(CreatedContextPath);
    }

    public IEnumerable<string> GetAvailableAgentTypes()
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return ["implementer", "reviewer", "planner"];
    }

    public bool IsValidAgentType(string agentType)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return GetAvailableAgentTypes().Contains(agentType);
    }

    public Dictionary<string, string> GetAgentTypeSources()
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return new Dictionary<string, string>
        {
            { "implementer", "Embedded" }, { "reviewer", "Embedded" }, { "planner", "Embedded" }
        };
    }

    public string GetAgentPrompt(string agentType)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return $"Fake prompt for {agentType}";
    }
}
