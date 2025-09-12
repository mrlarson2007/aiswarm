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

        return ["implementer", "reviewer", "planner", "tester"];
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
            { "implementer", "Embedded" }, 
            { "reviewer", "Embedded" }, 
            { "planner", "Embedded" },
            { "tester", "Embedded" }
        };
    }

    public string GetPersonaPrompt(string agentType)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return agentType switch
        {
            "planner" => "# Planner Agent\n\nYou are a planning agent that breaks down tasks and creates structured plans.",
            "implementer" => "# Implementer Agent\n\nYou are an implementation agent that writes code using TDD methodology.",
            "reviewer" => "# Reviewer Agent\n\nYou are a code review agent that analyzes code quality and provides feedback.",
            "tester" => "# Tester Agent\n\nYou are a testing agent that validates functionality and writes comprehensive tests.",
            _ => $"# {agentType} Agent\n\nYou are a {agentType} agent."
        };
    }
}
