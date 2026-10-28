using AISwarm.Infrastructure;
using AISwarm.Server.Delegation;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;
using Moq;
using Shouldly;

namespace AISwarm.Tests.Services;

public class DelegationServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenStatusDeliveryStalls_ShouldNotDelaySdkTransition(bool cancel)
    {
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new Mock<McpServer>();
        server.Setup(service => service.SendMessageAsync(
                It.IsAny<JsonRpcMessage>(), It.IsAny<CancellationToken>()))
            .Returns((JsonRpcMessage _, CancellationToken token) =>
            {
                attempted.TrySetResult();
                return releaseNotification.Task.WaitAsync(token);
            });
        IMcpTaskStore store = new TaskNotifications { Server = server.Object };
        var task = await store.CreateTaskAsync();

        Task transition = cancel
            ? store.SetCancelledAsync(task.TaskId)
            : store.SetCompletedAsync(task.TaskId, JsonSerializer.SerializeToElement(new
            {
                content = "done"
            }));
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        try
        {
            transition.IsCompleted.ShouldBeTrue();
            (await store.GetTaskAsync(task.TaskId))!.Status.ShouldBe(
                cancel ? McpTaskStatus.Cancelled : McpTaskStatus.Completed);
        }
        finally
        {
            releaseNotification.TrySetResult();
            await transition;
        }
    }

    [Fact]
    public async Task WhenStoppingHost_ShouldWaitForAgentCancellationCleanup()
    {
        using var stopping = new CancellationTokenSource();
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(service => service.ApplicationStopping).Returns(stopping.Token);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaningUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanedUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = CreateAgent(Task.FromResult(new AgentResult(0, "", "")));
        agent.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, CancellationToken token) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return new AgentResult(0, "", "");
                }
                finally
                {
                    cleaningUp.TrySetResult();
                    await cleanedUp.Task;
                }
            });
        var systemUnderTest = new DelegationService(agent.Object, lifetime.Object);
        var execution = systemUnderTest.ExecuteAsync("Do work", "local/model", Environment.CurrentDirectory);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        stopping.Cancel();
        await cleaningUp.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var shutdown = systemUnderTest.StopAsync(CancellationToken.None);

        shutdown.IsCompleted.ShouldBeFalse();
        cleanedUp.SetResult();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        await Should.ThrowAsync<OperationCanceledException>(() => execution);
    }

    [Fact]
    public async Task WhenInputResponseCallbackCancelsRequest_ShouldNotFailStoredTransition()
    {
        IMcpTaskStore store = new TaskNotifications();
        var task = await store.CreateTaskAsync();
        await store.SetInputRequestsAsync(task.TaskId, new Dictionary<string, InputRequest>
        {
            ["input"] = new() { Method = "elicitation/create" }
        });
        using var request = new CancellationTokenSource();
        Action cancelRequest = request.Cancel;
        store.InputResponseReceived += _ => cancelRequest();

        await store.ResolveInputRequestsAsync(task.TaskId, new Dictionary<string, InputResponse>
        {
            ["input"] = new() { RawValue = JsonSerializer.SerializeToElement(new { action = "accept" }) }
        }, request.Token);

        request.IsCancellationRequested.ShouldBeTrue();
        (await store.GetTaskAsync(task.TaskId))!.Status.ShouldBe(McpTaskStatus.Working);
    }

    [Fact]
    public async Task WhenModelIsUnavailable_ShouldRejectWithoutLaunching()
    {
        var cli = CreateAgent(Task.FromResult(new AgentResult(0, "", "")));
        var systemUnderTest = CreateExecutor(cli);

        await Should.ThrowAsync<ArgumentException>(() =>
            systemUnderTest.ExecuteAsync("Do work", "local/missing", Environment.CurrentDirectory));

        cli.Verify(service => service.RunAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenExecutingInline_ShouldReturnAgentOutputWithoutCreatingApplicationTaskState()
    {
        var cli = CreateAgent(Task.FromResult(new AgentResult(0, "done", "")));
        var systemUnderTest = CreateExecutor(cli);

        var result = await systemUnderTest.ExecuteAsync("Do work", "local/model", Environment.CurrentDirectory);

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("done");
    }

    [Fact]
    public async Task WhenProcessFails_ShouldRetainExitCodeAndOutput()
    {
        var systemUnderTest = CreateExecutor(CreateAgent(Task.FromResult(new AgentResult(7, "partial", "model failed"))));

        var result = await systemUnderTest.ExecuteAsync("Do work", "local/model", Environment.CurrentDirectory);

        result.ExitCode.ShouldBe(7);
        result.StandardOutput.ShouldBe("partial");
        result.StandardError.ShouldBe("model failed");
    }

    [Fact]
    public async Task WhenExecutionThrows_ShouldReturnAgentFailure()
    {
        var systemUnderTest = CreateExecutor(CreateAgent(Task.FromException<AgentResult>(new IOException("CLI disappeared"))));

        var result = await systemUnderTest.ExecuteAsync("Do work", "local/model", Environment.CurrentDirectory);

        result.ExitCode.ShouldBe(-1);
        result.StandardError.ShouldContain("CLI disappeared");
    }

    [Theory]
    [InlineData("", "local/model", "valid")]
    [InlineData("work", "", "valid")]
    [InlineData("work", "local/model", "relative")]
    [InlineData("work", "local/model", "missing")]
    public async Task WhenInputIsInvalid_ShouldRejectBeforeDiscovery(
        string instructions,
        string model,
        string directory)
    {
        var cli = CreateAgent(Task.FromResult(new AgentResult(0, "", "")));
        var systemUnderTest = CreateExecutor(cli);
        var path = directory == "valid" ? Environment.CurrentDirectory
            : directory == "missing" ? Path.Join(Environment.CurrentDirectory, Guid.NewGuid().ToString()) : directory;

        await Should.ThrowAsync<ArgumentException>(() => systemUnderTest.ExecuteAsync(instructions, model, path));

        cli.Verify(service => service.ListModelsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenCancelling_ShouldSignalProcessAndPropagateCancellationToSdk()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cli = CreateBlockingAgent(started);
        var systemUnderTest = CreateExecutor(cli);
        using var cancellation = new CancellationTokenSource();
        var execution = systemUnderTest.ExecuteAsync(
            "Do work", "local/model", Environment.CurrentDirectory, cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => execution);
    }

    [Fact]
    public async Task WhenTimeoutExpires_ShouldCancelProcessAndReturnTimeoutFailure()
    {
        var systemUnderTest = CreateExecutor(CreateBlockingAgent());

        var result = await systemUnderTest.ExecuteAsync(
            "Do work", "local/model", Environment.CurrentDirectory, timeout: TimeSpan.FromMilliseconds(50));

        result.ExitCode.ShouldBe(-1);
        result.StandardError.ShouldContain("timed out");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    public async Task WhenTimeoutIsInvalid_ShouldRejectBeforeDiscovery(int seconds)
    {
        var cli = CreateAgent(Task.FromResult(new AgentResult(0, "", "")));
        var systemUnderTest = CreateExecutor(cli);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => systemUnderTest.ExecuteAsync(
            "Do work", "local/model", Environment.CurrentDirectory, timeout: TimeSpan.FromSeconds(seconds)));

        cli.Verify(service => service.ListModelsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenHostStops_ShouldCancelOutstandingWorkAndRejectNewExecution()
    {
        using var stopping = new CancellationTokenSource();
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(service => service.ApplicationStopping).Returns(stopping.Token);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var systemUnderTest = new DelegationService(CreateBlockingAgent(started).Object, lifetime.Object);
        var execution = systemUnderTest.ExecuteAsync("Do work", "local/model", Environment.CurrentDirectory);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        stopping.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => execution);
        await Should.ThrowAsync<OperationCanceledException>(() =>
            systemUnderTest.ExecuteAsync("More work", "local/model", Environment.CurrentDirectory));
    }

    [Theory]
    [InlineData("provider event output")]
    [InlineData("partial response")]
    [InlineData("")]
    public async Task WhenAgentReportsFailureDespiteZeroExit_ShouldPreserveNeutralFailure(string output)
    {
        var systemUnderTest = CreateExecutor(CreateAgent(Task.FromResult(
            new AgentResult(0, output, "provider failed") { IsError = true })));

        var result = await systemUnderTest.ExecuteAsync("Do work", "local/model", Environment.CurrentDirectory);

        result.ExitCode.ShouldBe(0);
        result.IsError.ShouldBeTrue();
        result.StandardOutput.ShouldBe(output);
        result.StandardError.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WhenFourTasksAreRunning_ShouldRejectAdditionalProcesses()
    {
        var systemUnderTest = CreateExecutor(CreateBlockingAgent());
        using var cancellation = new CancellationTokenSource();
        var executions = Enumerable.Range(0, 4).Select(_ => systemUnderTest.ExecuteAsync(
            "Do work", "local/model", Environment.CurrentDirectory, cancellationToken: cancellation.Token)).ToArray();

        var rejected = await systemUnderTest.ExecuteAsync("Extra work", "local/model", Environment.CurrentDirectory);

        rejected.ExitCode.ShouldBe(-1);
        rejected.StandardError.ShouldContain("four tasks");
        cancellation.Cancel();
        foreach (var execution in executions)
            await Should.ThrowAsync<OperationCanceledException>(() => execution);
    }

    private static DelegationService CreateExecutor(Mock<IAgentService> agent) =>
        new(agent.Object, Mock.Of<IHostApplicationLifetime>());

    private static Mock<IAgentService> CreateBlockingAgent(TaskCompletionSource? started = null)
    {
        var cli = CreateAgent(Task.FromResult(new AgentResult(0, "", "")));
        cli.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, CancellationToken token) =>
            {
                started?.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new AgentResult(0, "", "");
            });
        return cli;
    }

    private static Mock<IAgentService> CreateAgent(Task<AgentResult> execution)
    {
        var cli = new Mock<IAgentService>();
        cli.Setup(service => service.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentModels("test", ["local/model"]));
        cli.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(execution);
        return cli;
    }
}
