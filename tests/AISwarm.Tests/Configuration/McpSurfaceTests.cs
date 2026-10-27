using System.Reflection;
using AISwarm.Server.McpTools;
using ModelContextProtocol.Server;
using Shouldly;

namespace AISwarm.Tests.Configuration;

public class McpSurfaceTests
{
    [Fact]
    public void WhenDiscoveringTools_ShouldNotExposeLegacyAgentWorkflow()
    {
        var toolNames = typeof(SaveMemoryMcpTool).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => name != null)
            .ToArray();

        toolNames.ShouldNotContain("launch_agent");
        toolNames.ShouldNotContain("get_next_task");
        toolNames.ShouldNotContain("create_task");
        toolNames.ShouldNotContain("report_task_completion");
    }

    [Fact]
    public void WhenInspectingInfrastructure_ShouldNotContainGeminiOrPersonaServices()
    {
        var types = typeof(AISwarm.Infrastructure.ServiceRegistration).Assembly.GetTypes();

        types.ShouldNotContain(type => type.Name == "GeminiService");
        types.ShouldNotContain(type => type.Name == "ContextService");
        typeof(AISwarm.DataLayer.CoordinationDbContext).GetProperties()
            .ShouldNotContain(property => property.Name == "Agents" || property.Name == "Tasks");
    }
}
