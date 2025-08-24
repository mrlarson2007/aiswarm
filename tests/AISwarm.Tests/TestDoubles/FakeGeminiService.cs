using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

public class FakeGeminiService : IGeminiService
{
    public string FailureMessage { get; set; } = string.Empty;
    public bool ShouldFail => !string.IsNullOrEmpty(FailureMessage);
    public bool LaunchResult { get; set; } = true;
    public bool? LastLaunchYoloParameter { get; private set; }

    public Task<bool> IsGeminiCliAvailableAsync()
    {
        return Task.FromResult(!ShouldFail);
    }

    public Task<string?> GetGeminiVersionAsync()
    {
        return Task.FromResult<string?>(ShouldFail ? null : "1.0.0");
    }

    public Task<bool> LaunchInteractiveAsync(
        string contextFilePath,
        string? model = null,
        string? workingDirectory = null,
        AgentSettings? agentSettings = null,
        bool yolo = false)
    {
        LastLaunchYoloParameter = yolo;
        
        if (ShouldFail)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        return Task.FromResult(LaunchResult);
    }

    public Task<bool> LaunchInteractiveWithSettingsAsync(string contextFilePath, string? model, AgentSettings agentSettings, string? workingDirectory = null)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        return Task.FromResult(LaunchResult);
    }
}
