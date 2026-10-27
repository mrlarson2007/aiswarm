using System.IO.Pipelines;
using System.Text.Json;
using AISwarm.Infrastructure.OpenCode;
using AISwarm.Server.Delegation;
using AISwarm.Server.McpTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;
using Shouldly;

#pragma warning disable MCP9005 // Desktop lifecycle diagnostics are not an automatic task-resumption protocol.

namespace AISwarm.Tests.Integration;

[CollectionDefinition("OpenCode processes", DisableParallelization = true)]
public class OpenCodeProcessCollection
{
}

[Collection("OpenCode processes")]
public class OpenCodeDelegationIntegrationTests
{
    [Fact]
    public async Task WhenStartingRealStdioServer_ShouldExposeMvpToolsWithoutWritingLogsToStdout()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aiswarm-stdio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Command = "dotnet",
                Arguments = [typeof(OpenCodeMcpTool).Assembly.Location],
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["WorkingDirectory"] = directory,
                    ["OpenCode__Executable"] = Path.Combine(directory, "missing-opencode")
                }
            }), cancellationToken: timeout.Token);

            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

            tools.Select(tool => tool.Name).ShouldContain("list_models");
            tools.Select(tool => tool.Name).ShouldContain("delegate_task");
            tools.Select(tool => tool.Name).ShouldContain("get_task_result");
            tools.Select(tool => tool.Name).ShouldContain("cancel_task");
            tools.Select(tool => tool.Name).ShouldContain("save_memory");
            tools.Select(tool => tool.Name).ShouldNotContain("get_next_task");
            File.Exists(Path.Combine(directory, ".aiswarm", "opencode-mvp.db")).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WhenDelegatingOverMcp_ShouldPushCompletionAndExposeResultWithoutPolling()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var execution = new TaskCompletionSource<OpenCodeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cli = new Mock<IOpenCodeCli>();
        cli.Setup(service => service.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenCodeModels("test-version", ["local/model"]));
        cli.Setup(service => service.RunAsync(
                "Do work", "local/model", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(execution.Task);
        var input = new Pipe();
        var output = new Pipe();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddOpenCodeDelegation(builder.Configuration);
        builder.Services.AddSingleton(cli.Object);
        builder.Services.AddMcpServer(options => options.Capabilities = new() { Logging = new() })
            .WithStreamServerTransport(input.Reader.AsStream(), output.Writer.AsStream())
            .WithTools<OpenCodeMcpTool>()
            .WithResources<OpenCodeMcpTool>();
        using var host = builder.Build();
        await host.StartAsync(timeout.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(input.Writer.AsStream(), output.Reader.AsStream()),
            cancellationToken: timeout.Token);
        await using var registration = client.RegisterNotificationHandler(
            NotificationMethods.LoggingMessageNotification, (notification, _) =>
            {
                var data = notification.Params!["data"]!;
                if (data["state"]!.GetValue<string>() == "Completed")
                    completed.TrySetResult(data["resultUri"]!.GetValue<string>());
                return ValueTask.CompletedTask;
            });

        var models = await client.CallToolAsync("list_models",
            new Dictionary<string, object?> { ["workingDirectory"] = Path.GetTempPath() },
            cancellationToken: timeout.Token);
        ((TextContentBlock)models.Content.Single()).Text.ShouldContain("local/model");
        var accepted = await client.CallToolAsync("delegate_task", new Dictionary<string, object?>
        {
            ["instructions"] = "Do work",
            ["model"] = "local/model",
            ["workingDirectory"] = Path.GetTempPath()
        }, cancellationToken: timeout.Token);

        accepted.IsError.ShouldNotBe(true);
        execution.Task.IsCompleted.ShouldBeFalse();
        execution.SetResult(new OpenCodeResult(0, "finished", ""));
        var resultUri = await completed.Task.WaitAsync(timeout.Token);
        var result = await client.ReadResourceAsync(resultUri, cancellationToken: timeout.Token);
        using var payload = JsonDocument.Parse(((TextResourceContents)result.Contents.Single()).Text);
        payload.RootElement.GetProperty("state").GetString().ShouldBe("Completed");
        payload.RootElement.GetProperty("standardOutput").GetString().ShouldBe("finished");

        await host.StopAsync(timeout.Token);
    }
}
