using System.IO.Pipelines;
using System.Text.Json;
using AISwarm.Infrastructure;
using AISwarm.Server.Delegation;
using AISwarm.Server.McpTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using Moq;
using Shouldly;

namespace AISwarm.Tests.Integration;

[CollectionDefinition("OpenCode processes", DisableParallelization = true)]
public class OpenCodeProcessCollection;

[Collection("OpenCode processes")]
public class OpenCodeDelegationIntegrationTests
{
    [Fact]
    public async Task WhenStartingRealStdioServer_ShouldExposeMvpToolsWithoutWritingLogsToStdout()
    {
        var directory = Path.Join(Environment.CurrentDirectory, ".stdio-test-" + Guid.NewGuid().ToString("N"));
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
                    ["OpenCode__Executable"] = Path.Join(directory, "missing-opencode")
                }
            }), cancellationToken: timeout.Token);

            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

            tools.Select(tool => tool.Name).ShouldContain("list_models");
            tools.Select(tool => tool.Name).ShouldContain("delegate_task");
            tools.Select(tool => tool.Name).ShouldNotContain("get_task_result");
            tools.Select(tool => tool.Name).ShouldNotContain("cancel_task");
            tools.Select(tool => tool.Name).ShouldContain("save_memory");
            tools.Select(tool => tool.Name).ShouldNotContain("get_next_task");
            client.ServerCapabilities.Extensions!.ShouldContainKey(TasksProtocol.ExtensionId);
            File.Exists(Path.Join(directory, ".aiswarm", "aiswarm.db")).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WhenDelegatingOverMcp_ShouldPushCompletionAndExposeResultWithoutPolling()
    {
        var execution = new TaskCompletionSource<AgentResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = await Session.CreateAsync((_, token) => execution.Task.WaitAsync(token));

        var accepted = await session.Client.CallToolAsTaskAsync(Request(), session.Timeout.Token);

        accepted.IsTask.ShouldBeTrue();
        execution.Task.IsCompleted.ShouldBeFalse();
        accepted.TaskCreated!.Status.ShouldBe(McpTaskStatus.Working);
        accepted.TaskCreated.TimeToLive.ShouldBe(TimeSpan.FromDays(2));
        execution.SetResult(new AgentResult(0, "finished", ""));
        var notification = await session.Completed.Task.WaitAsync(session.Timeout.Token);
        notification.Status.ShouldBe(McpTaskStatus.Completed);
        notification.TaskId.ShouldBe(accepted.TaskCreated!.TaskId);
        var result = (await session.Client.GetTaskAsync(accepted.TaskCreated.TaskId, session.Timeout.Token))
            .ShouldBeOfType<CompletedTaskResult>();
        result.Result.GetProperty("isError").GetBoolean().ShouldBeFalse();
        result.Result.GetProperty("structuredContent").GetProperty("standardOutput").GetString().ShouldBe("finished");
    }

    [Fact]
    public async Task WhenClientLacksTasksCapability_ShouldRejectWithoutLaunching()
    {
        var launched = false;
        await using var session = await Session.CreateAsync((_, _) =>
        {
            launched = true;
            return Task.FromResult(new AgentResult(0, "", ""));
        });

        var error = await Should.ThrowAsync<ModelContextProtocol.McpProtocolException>(async () =>
            await session.Client.CallToolAsync(Request(), cancellationToken: session.Timeout.Token));

        error.Message.ShouldContain(TasksProtocol.ExtensionId);
        launched.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenCancellingNativeTask_ShouldSignalAgentAndPushCancelledStatus()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = await Session.CreateAsync(async (_, token) =>
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
                return new AgentResult(0, "", "");
            }
            finally
            {
                cancelled.TrySetResult();
            }
        });
        var accepted = await session.Client.CallToolAsTaskAsync(Request(), session.Timeout.Token);
        await started.Task.WaitAsync(session.Timeout.Token);

        await session.Client.CancelTaskAsync(accepted.TaskCreated!.TaskId, session.Timeout.Token);

        await cancelled.Task.WaitAsync(session.Timeout.Token);
        (await session.Completed.Task.WaitAsync(session.Timeout.Token)).Status.ShouldBe(McpTaskStatus.Cancelled);
        (await session.Client.GetTaskAsync(accepted.TaskCreated.TaskId, session.Timeout.Token))
            .ShouldBeOfType<CancelledTaskResult>();
    }

    [Theory]
    [InlineData(7, "partial", "provider failed", false)]
    [InlineData(0, "{\"type\":\"error\"}", "", true)]
    [InlineData(0, "{\"type\":\"text\"}\n[output truncated]", "", true)]
    [InlineData(0, "neutral agent failure", "agent reported failure", true)]
    public async Task WhenAgentFails_ShouldCompleteTaskWithToolErrorInsteadOfProtocolFailure(
        int exitCode,
        string output,
        string error,
        bool isError)
    {
        await using var session = await Session.CreateAsync((_, _) =>
            Task.FromResult(new AgentResult(exitCode, output, error) { IsError = isError }));

        var accepted = await session.Client.CallToolAsTaskAsync(Request(), session.Timeout.Token);
        var notification = await session.Completed.Task.WaitAsync(session.Timeout.Token);
        var result = (await session.Client.GetTaskAsync(accepted.TaskCreated!.TaskId, session.Timeout.Token))
            .ShouldBeOfType<CompletedTaskResult>();

        notification.Status.ShouldBe(McpTaskStatus.Completed);
        result.Result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        result.Result.GetProperty("structuredContent").GetProperty("standardOutput").GetString().ShouldBe(output);
        result.Result.GetProperty("structuredContent").GetProperty("exitCode").GetInt32().ShouldBe(exitCode);
    }

    [Fact]
    public async Task WhenTaskTimesOut_ShouldStopAgentAndPushToolErrorResult()
    {
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = await Session.CreateAsync(async (_, token) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
                return new AgentResult(0, "", "");
            }
            finally
            {
                stopped.TrySetResult();
            }
        });

        var accepted = await session.Client.CallToolAsTaskAsync(Request(timeoutSeconds: 1), session.Timeout.Token);
        await session.Completed.Task.WaitAsync(session.Timeout.Token);
        await stopped.Task.WaitAsync(session.Timeout.Token);
        var result = (await session.Client.GetTaskAsync(accepted.TaskCreated!.TaskId, session.Timeout.Token))
            .ShouldBeOfType<CompletedTaskResult>();

        result.Result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        result.Result.GetProperty("structuredContent").GetProperty("standardError").GetString()!.ShouldContain("timed out");
    }

    [Fact]
    public async Task WhenTaskCapableClientListsModels_ShouldReturnSynchronouslyWithoutCreatingTask()
    {
        await using var session = await Session.CreateAsync((_, _) =>
            Task.FromException<AgentResult>(new InvalidOperationException("must not run")));

        var result = await session.Client.CallToolAsTaskAsync(new CallToolRequestParams
        {
            Name = "list_models",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["workingDirectory"] = JsonSerializer.SerializeToElement(Environment.CurrentDirectory)
            }
        }, session.Timeout.Token);

        result.IsTask.ShouldBeFalse();
        result.Result!.IsError.ShouldNotBe(true);
        result.Result.Content.OfType<TextContentBlock>().Single().Text.ShouldContain("local/model");
        session.Completed.Task.IsCompleted.ShouldBeFalse();
    }

    private static CallToolRequestParams Request(int timeoutSeconds = 600) => new()
    {
        Name = "delegate_task",
        Arguments = new Dictionary<string, JsonElement>
        {
            ["instructions"] = JsonSerializer.SerializeToElement("Do work"),
            ["model"] = JsonSerializer.SerializeToElement("local/model"),
            ["workingDirectory"] = JsonSerializer.SerializeToElement(Environment.CurrentDirectory),
            ["timeoutSeconds"] = JsonSerializer.SerializeToElement(timeoutSeconds)
        }
    };

    private sealed class Session : IAsyncDisposable
    {
        private readonly IHost _host;
        public readonly CancellationTokenSource Timeout = new(TimeSpan.FromSeconds(15));
        public readonly TaskCompletionSource<TaskStatusNotificationParams> Completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public McpClient Client { get; private set; } = null!;

        private Session(IHost host) => _host = host;

        public static async Task<Session> CreateAsync(Func<string, CancellationToken, Task<AgentResult>> execute)
        {
            var cli = new Mock<IAgentService>();
            cli.Setup(service => service.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentModels("test-version", ["local/model"]));
            cli.Setup(service => service.RunAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string instructions, string _, string _, CancellationToken token) => execute(instructions, token));
            var input = new Pipe();
            var output = new Pipe();
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddOpenCodeDelegation(builder.Configuration);
            builder.Services.AddSingleton(cli.Object);
            builder.Services.AddMcpServer()
                .WithDelegationTasks()
                .WithStreamServerTransport(input.Reader.AsStream(), output.Writer.AsStream())
                .WithTools<OpenCodeMcpTool>();
            var session = new Session(builder.Build());
            await session._host.StartAsync(session.Timeout.Token);
            session.Client = await McpClient.CreateAsync(
                new StreamClientTransport(input.Writer.AsStream(), output.Reader.AsStream()),
                cancellationToken: session.Timeout.Token);
            session.Client.RegisterNotificationHandler(TasksProtocol.NotificationTaskStatus, (notification, _) =>
            {
                var status = notification.Params!.Deserialize(McpTasksJsonContext.Default.TaskStatusNotificationParams)!;
                if (status.Status is McpTaskStatus.Completed or McpTaskStatus.Failed or McpTaskStatus.Cancelled)
                    session.Completed.TrySetResult(status);
                return ValueTask.CompletedTask;
            });
            return session;
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(Timeout.Token);
            await Client.DisposeAsync();
            _host.Dispose();
            Timeout.Dispose();
        }
    }
}
