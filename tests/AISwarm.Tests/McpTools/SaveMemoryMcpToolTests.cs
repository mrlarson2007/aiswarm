using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class SaveMemoryMcpToolTests : ISystemUnderTest<SaveMemoryMcpTool>
{
    public SaveMemoryMcpTool SystemUnderTest => new ();

    public class ValidationTests : SaveMemoryMcpToolTests
    {
        [Fact]
        public async Task WhenSavingMemoryWithEmptyKey_ShouldReturnErrorMessage()
        {
            // Arrange
            var tool = SystemUnderTest;

            // Act
            var result = await tool.SaveMemory(
                key: string.Empty,
                value: "test-value");

            // Assert
            result.ShouldContain("Error");
            result.ShouldContain("key");
        }
    }
}
