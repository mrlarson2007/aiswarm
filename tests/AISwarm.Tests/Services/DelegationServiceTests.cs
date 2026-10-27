using AISwarm.Infrastructure.OpenCode;
using AISwarm.Server.Delegation;
using Moq;
using Shouldly;

namespace AISwarm.Tests.Services;

public class DelegationServiceTests
{
    [Fact]
    public async Task WhenModelIsUnavailable_ShouldRejectWithoutLaunching()
    {
        var cli = new Mock<IOpenCodeCli>();
        cli.Setup(service => service.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenCodeModels("test", ["local/available"]));
        await using var systemUnderTest = new DelegationService(cli.Object);

        await Should.ThrowAsync<ArgumentException>(() =>
            systemUnderTest.DelegateAsync("Do work", "local/missing", Path.GetTempPath()));

        cli.Verify(service => service.RunAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenDelegating_ShouldReturnBeforeExecutionAndDeliverCompletedResultToHook()
    {
        var execution = new TaskCompletionSource<OpenCodeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cli = CreateCli(execution.Task);
        var events = new List<DelegatedTask>();
        await using var systemUnderTest = new DelegationService(cli.Object);

        var accepted = await systemUnderTest.DelegateAsync(
            "Implement the task", "local/model", Path.GetTempPath(),
            hook: (snapshot, _) =>
            {
                events.Add(snapshot);
                return Task.CompletedTask;
            }).WaitAsync(TimeSpan.FromSeconds(5));

        accepted.State.ShouldBe("Running");
        execution.Task.IsCompleted.ShouldBeFalse();
        execution.SetResult(new OpenCodeResult(0, "task output", ""));
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId).WaitAsync(TimeSpan.FromSeconds(5));

        var result = systemUnderTest.GetTask(accepted.TaskId);
        result.State.ShouldBe("Completed");
        result.StandardOutput.ShouldBe("task output");
        result.ExitCode.ShouldBe(0);
        result.ResultUri.ShouldBe($"aiswarm://tasks/{accepted.TaskId}");
        events.Select(snapshot => snapshot.State).ShouldBe(["Running", "Completed"]);
        events.Last().ShouldBe(result);
        cli.Verify(service => service.RunAsync(
            "Implement the task", "local/model", Path.GetTempPath(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenProcessFails_ShouldRetainExitCodeAndNotifyFailure()
    {
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(7, "partial", "model failed")));
        var events = new List<string>();
        await using var systemUnderTest = new DelegationService(cli.Object);

        var accepted = await systemUnderTest.DelegateAsync(
            "Do work", "local/model", Path.GetTempPath(), (snapshot, _) =>
            {
                events.Add(snapshot.State);
                return Task.CompletedTask;
            });
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId);

        var result = systemUnderTest.GetTask(accepted.TaskId);
        result.State.ShouldBe("Failed");
        result.ExitCode.ShouldBe(7);
        result.StandardError.ShouldBe("model failed");
        events.ShouldBe(["Running", "Failed"]);
    }

    [Fact]
    public async Task WhenExecutionThrows_ShouldRetainFailureAndCompleteHook()
    {
        var cli = CreateCli(Task.FromException<OpenCodeResult>(new IOException("CLI disappeared")));
        await using var systemUnderTest = new DelegationService(cli.Object);

        var accepted = await systemUnderTest.DelegateAsync("Do work", "local/model", Path.GetTempPath());
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId);

        var result = systemUnderTest.GetTask(accepted.TaskId);
        result.State.ShouldBe("Failed");
        result.StandardError.ShouldContain("CLI disappeared");
        result.CompletedAt.ShouldNotBeNull();
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
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(0, "", "")));
        await using var systemUnderTest = new DelegationService(cli.Object);
        var path = directory == "valid" ? Path.GetTempPath()
            : directory == "missing" ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) : directory;

        await Should.ThrowAsync<ArgumentException>(() => systemUnderTest.DelegateAsync(instructions, model, path));

        cli.Verify(service => service.ListModelsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenHookFails_ShouldStillRunTaskAndRetainItsResult()
    {
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(0, "done", "")));
        await using var systemUnderTest = new DelegationService(cli.Object);

        var accepted = await systemUnderTest.DelegateAsync(
            "Do work", "local/model", Path.GetTempPath(),
            (_, _) => Task.FromException(new IOException("hook unavailable")));
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId);

        var result = systemUnderTest.GetTask(accepted.TaskId);
        result.State.ShouldBe("Completed");
        result.StandardOutput.ShouldBe("done");
        result.HookError.ShouldNotBeNull();
        result.HookError.ShouldContain("hook unavailable");
    }

    [Fact]
    public async Task WhenCancelling_ShouldSignalProcessAndNotifyCancelledOnce()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(0, "", "")));
        cli.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, CancellationToken token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new OpenCodeResult(0, "", "");
            });
        var states = new List<string>();
        await using var systemUnderTest = new DelegationService(cli.Object);
        var accepted = await systemUnderTest.DelegateAsync(
            "Do work", "local/model", Path.GetTempPath(), (snapshot, _) =>
            {
                states.Add(snapshot.State);
                return Task.CompletedTask;
            });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        systemUnderTest.CancelTask(accepted.TaskId).ShouldBeTrue();
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId).WaitAsync(TimeSpan.FromSeconds(5));

        systemUnderTest.GetTask(accepted.TaskId).State.ShouldBe("Cancelled");
        states.ShouldBe(["Running", "Cancelled"]);
        systemUnderTest.CancelTask(accepted.TaskId).ShouldBeFalse();
    }

    [Fact]
    public async Task WhenTimeoutExpires_ShouldCancelProcessAndRetainTimeoutFailure()
    {
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(0, "", "")));
        cli.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new OpenCodeResult(0, "", "");
            });
        await using var systemUnderTest = new DelegationService(cli.Object);

        var accepted = await systemUnderTest.DelegateAsync(
            "Do work", "local/model", Path.GetTempPath(), timeout: TimeSpan.FromMilliseconds(50));
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId).WaitAsync(TimeSpan.FromSeconds(5));

        var result = systemUnderTest.GetTask(accepted.TaskId);
        result.State.ShouldBe("Failed");
        result.StandardError.ShouldContain("timed out");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    public async Task WhenTimeoutIsInvalid_ShouldRejectBeforeDiscovery(int seconds)
    {
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(0, "", "")));
        await using var systemUnderTest = new DelegationService(cli.Object);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => systemUnderTest.DelegateAsync(
            "Do work", "local/model", Path.GetTempPath(), timeout: TimeSpan.FromSeconds(seconds)));

        cli.Verify(service => service.ListModelsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenHostStops_ShouldCancelOutstandingWorkAndRejectNewTasks()
    {
        var execution = new TaskCompletionSource<OpenCodeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cli = CreateCli(execution.Task);
        cli.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string _, string _, CancellationToken token) => execution.Task.WaitAsync(token));
        await using var systemUnderTest = new DelegationService(cli.Object);
        var accepted = await systemUnderTest.DelegateAsync("Do work", "local/model", Path.GetTempPath());

        await systemUnderTest.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        systemUnderTest.GetTask(accepted.TaskId).State.ShouldBe("Cancelled");
        await Should.ThrowAsync<ObjectDisposedException>(() =>
            systemUnderTest.DelegateAsync("More work", "local/model", Path.GetTempPath()));
    }

    [Fact]
    public async Task WhenCallerCancelsAfterAcceptance_ShouldNotCancelBackgroundExecution()
    {
        var execution = new TaskCompletionSource<OpenCodeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cli = CreateCli(execution.Task);
        using var request = new CancellationTokenSource();
        await using var systemUnderTest = new DelegationService(cli.Object);
        var accepted = await systemUnderTest.DelegateAsync(
            "Do work", "local/model", Path.GetTempPath(), cancellationToken: request.Token);

        request.Cancel();
        execution.SetResult(new OpenCodeResult(0, "done", ""));
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId);

        systemUnderTest.GetTask(accepted.TaskId).State.ShouldBe("Completed");
    }

    [Theory]
    [InlineData("{\"type\":\"error\",\"error\":{\"message\":\"provider failed\"}}")]
    [InlineData("{\"type\":\"text\"}\n{\"type\":\"error\"}\n")]
    [InlineData("{\"type\":\"text\"}\n[output truncated]")]
    public async Task WhenCliReportsAnErrorDespiteZeroExit_ShouldNotReportSuccess(string output)
    {
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(0, output, "")));
        await using var systemUnderTest = new DelegationService(cli.Object);

        var accepted = await systemUnderTest.DelegateAsync("Do work", "local/model", Path.GetTempPath());
        await systemUnderTest.WaitForCompletionAsync(accepted.TaskId);

        var result = systemUnderTest.GetTask(accepted.TaskId);
        result.State.ShouldBe("Failed");
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(output);
        result.StandardError.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WhenFourTasksAreRunning_ShouldRejectAdditionalProcesses()
    {
        var execution = new TaskCompletionSource<OpenCodeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cli = CreateCli(execution.Task);
        cli.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string _, string _, CancellationToken token) => execution.Task.WaitAsync(token));
        await using var systemUnderTest = new DelegationService(cli.Object);
        for (var index = 0; index < 4; index++)
            await systemUnderTest.DelegateAsync("Do work", "local/model", Path.GetTempPath());

        await Should.ThrowAsync<InvalidOperationException>(() =>
            systemUnderTest.DelegateAsync("Extra work", "local/model", Path.GetTempPath()));
    }

    [Fact]
    public async Task WhenHistoryIsFull_ShouldRejectRatherThanGrowMemoryOrDiscardResults()
    {
        var cli = CreateCli(Task.FromResult(new OpenCodeResult(0, "retained", "")));
        await using var systemUnderTest = new DelegationService(cli.Object);
        string? firstTaskId = null;
        for (var index = 0; index < 100; index++)
        {
            var task = await systemUnderTest.DelegateAsync("Do work", "local/model", Path.GetTempPath());
            firstTaskId ??= task.TaskId;
            await systemUnderTest.WaitForCompletionAsync(task.TaskId);
        }

        await Should.ThrowAsync<InvalidOperationException>(() =>
            systemUnderTest.DelegateAsync("Extra work", "local/model", Path.GetTempPath()));
        systemUnderTest.GetTask(firstTaskId!).StandardOutput.ShouldBe("retained");
    }

    private static Mock<IOpenCodeCli> CreateCli(Task<OpenCodeResult> execution)
    {
        var cli = new Mock<IOpenCodeCli>();
        cli.Setup(service => service.ListModelsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenCodeModels("test", ["local/model"]));
        cli.Setup(service => service.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(execution);
        return cli;
    }
}
