using AISwarm.Infrastructure;
using Shouldly;

namespace AISwarm.Tests.Services;

public class ContextServiceTests : ISystemUnderTest<ContextService>
{
    private ContextService? _systemUnderTest;

    public ContextService SystemUnderTest =>
        _systemUnderTest ??= new ContextService();

    [Fact]
    public async Task WhenCreateContextFileWithNoAgentId_ShouldBehaveLikeNormalCreateContextFile()
    {
        // Arrange
        var agentType = "implementer";
        var workingDirectory = Path.GetTempPath();
        var tempDir = Path.Combine(workingDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var contextPath = await SystemUnderTest
                .CreateContextFileWithAgentId(
                    agentType,
                    tempDir,
                    null);

            // Assert
            contextPath.ShouldNotBeNullOrEmpty();
            File.Exists(contextPath).ShouldBeTrue();
            var contextContent = await File.ReadAllTextAsync(contextPath);
            contextContent.ShouldNotContain("Your Agent ID");
            contextContent.ShouldNotContain("mcp_aiswarm_get_next_task");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WhenCreateContextFileWithAgentId_ShouldAppendMcpToolInstructions()
    {
        // Arrange
        var agentType = "implementer";
        var workingDirectory = Path.GetTempPath();
        var tempDir = Path.Combine(workingDirectory, Guid.NewGuid().ToString());
        var agentId = "test-agent-123";
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var contextPath = await SystemUnderTest.CreateContextFileWithAgentId(
                agentType,
                tempDir,
                agentId);

            // Assert
            contextPath.ShouldNotBeNullOrEmpty();
            File.Exists(contextPath).ShouldBeTrue();
            var contextContent = await File.ReadAllTextAsync(contextPath);
            contextContent.ShouldContain("Your Agent ID");
            contextContent.ShouldContain($"Your unique agent ID is: `{agentId}`");
            contextContent.ShouldContain("mcp_aiswarm_get_next_task");
            contextContent.ShouldContain("mcp_aiswarm_create_task");
            contextContent.ShouldContain("mcp_aiswarm_report_task_completion");
            contextContent.ShouldContain($"mcp_aiswarm_get_next_task(agentId='{agentId}')");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
