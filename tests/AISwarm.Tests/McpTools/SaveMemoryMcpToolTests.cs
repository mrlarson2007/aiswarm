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
            // Act
            var result = await SystemUnderTest.SaveMemory(
                key: string.Empty,
                value: "test-value");

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("key");
        }

        [Fact]
        public async Task  WhenSavingMemoryWithEmptyValue_ShouldReturnErrorMessage()
        {
            var result = await SystemUnderTest.SaveMemory(
                key: "test-key",
                value: string.Empty);

            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("value");
        }
    }
}
