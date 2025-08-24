using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class GetTasksByStatusMcpToolTests
{
    private readonly FakeTimeService _timeService = new();
    private readonly IDatabaseScopeService _scopeService;

    public GetTasksByStatusMcpToolTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(context);
    }

    [Fact]
    public async Task GetTasksByStatusAsync_WhenInvalidStatus_ShouldReturnFailure()
    {
        // Arrange
        var tool = new GetTasksByStatusMcpTool(_scopeService);
        var invalidStatus = "InvalidStatus";

        // Act
        var result = await tool.GetTasksByStatusAsync(invalidStatus);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Invalid status");
        result.Tasks.ShouldBeNull();
    }
}