using AISwarm.Infrastructure;
using AISwarm.Infrastructure.OpenCode;
using System.Diagnostics;
using Moq;
using Shouldly;

namespace AISwarm.Tests.Services;

[Collection("OpenCode processes")]
public class OpenCodeCliTests
{
    [Theory]
    [InlineData("{\"type\":\"error\",\"error\":{\"message\":\"provider failed\"}}")]
    [InlineData("{\"type\":\"text\"}\n{\"type\":\"error\"}\n")]
    [InlineData("{\"type\":\"text\"}\n[output truncated]")]
    public async Task WhenRunReportsErrorWithZeroExit_ShouldPreserveExitAndFlagNeutralFailure(string output)
    {
        var runner = new Mock<IOpenCodeProcessRunner>();
        runner.Setup(service => service.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(0, output, ""));
        var systemUnderTest = new OpenCodeCli(runner.Object);

        var result = await systemUnderTest.RunAsync("Do work", "local/model", Environment.CurrentDirectory);

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(output);
        result.IsError.ShouldBeTrue();
        result.StandardError.ShouldNotBeEmpty();
    }

    [Fact]
    public void WhenUsingOpenCodeCli_ShouldImplementProviderNeutralAgentService()
    {
        typeof(IAgentService).IsAssignableFrom(typeof(OpenCodeCli)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(8192)]
    [InlineData(65536)]
    public async Task WhenUtf8EncoderHasBufferedInput_ShouldCancelBeforeClosingStdin(int pipeCapacity)
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var token = cancellation.Token;
        var runner = new OpenCodeProcessRunner();
        // AutoFlush leaves a trailing surrogate in the UTF-8 encoder until its final flush.
        var prompt = new string('x', pipeCapacity) + "\ud800";

        await Should.ThrowAsync<OperationCanceledException>(() =>
            Task.Run(() => runner.RunAsync(new OpenCodeProcessRequest(
                    "/usr/bin/timeout", ["3", "/usr/bin/sleep", "60"],
                    Directory.GetCurrentDirectory(), prompt),
                token)).WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task WhenRootExitsButDescendantKeepsPipesOpen_ShouldBoundCancellationCleanup()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var runner = new OpenCodeProcessRunner();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var token = cancellation.Token;
        var elapsed = Stopwatch.StartNew();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            runner.RunAsync(new OpenCodeProcessRequest(
                "/usr/bin/setsid", ["--fork", "/usr/bin/sleep", "2"],
                Directory.GetCurrentDirectory()), token));

        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("ollama/hf.co/org/model:Q4_K_M")]
    [InlineData("openrouter/anthropic/claude")]
    public async Task WhenModelIdContainsNestedPaths_ShouldRetainItVerbatim(string model)
    {
        var cli = new OpenCodeCli(DiscoveryRunner(model).Object);

        var result = await cli.ListModelsAsync(Directory.GetCurrentDirectory());

        result.Models.ShouldBe(new[] { model });
    }

    [Fact]
    public async Task WhenDiscoveryFailureHasOnlyStdout_ShouldIncludeItsDiagnostic()
    {
        var runner = new Mock<IOpenCodeProcessRunner>();
        runner.Setup(x => x.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(4, "unsupported version option", ""));
        var cli = new OpenCodeCli(runner.Object);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => cli.ListModelsAsync(Directory.GetCurrentDirectory()));

        exception.Message.ShouldContain("unsupported version option");
    }

    [Fact]
    public async Task WhenProcessDoesNotReadPrompt_ShouldCancelWhileWritingStdin()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var runner = new OpenCodeProcessRunner();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var token = cancellation.Token;
        var elapsed = Stopwatch.StartNew();

        await Should.ThrowAsync<OperationCanceledException>(() => Task.Run(() =>
            runner.RunAsync(new OpenCodeProcessRequest(
                "/usr/bin/timeout", ["3", "/usr/bin/sleep", "60"],
                Directory.GetCurrentDirectory(), new string('x', 65537)), token)));

        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task WhenDiscoveryIsCancelled_ShouldPropagateCancellationAndStop(
        int cancelledCommand)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        Action cancel = cancellation.Cancel;
        var runner = new Mock<IOpenCodeProcessRunner>();
        var outputs = new[] { "1.2.3", "--model --format", "provider/model" };
        var calls = 0;
        runner.Setup(x => x.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(), token))
            .Returns((OpenCodeProcessRequest _, CancellationToken callbackToken) =>
            {
                var index = calls++;
                if (index != cancelledCommand)
                    return Task.FromResult(new AgentResult(0, outputs[index], ""));

                cancel();
                return Task.FromCanceled<AgentResult>(callbackToken);
            });
        var cli = new OpenCodeCli(runner.Object);

        var exception = await Should.ThrowAsync<OperationCanceledException>(
            () => cli.ListModelsAsync(Directory.GetCurrentDirectory(), token));

        exception.CancellationToken.ShouldBe(token);
        calls.ShouldBe(cancelledCommand + 1);
    }

    [Fact]
    public void WhenCaptureLimitIsNegative_ShouldRejectConfiguration()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new OpenCodeProcessRunner(maxCapturedCharacters: -1));
    }

    [Fact]
    public async Task WhenDiscoverySucceeds_ShouldReturnVersionAndModelsAndUseRequestedContext()
    {
        var runner = DiscoveryRunner("\nprovider/model-one\r\n\nother/model.2:free\n");
        var cli = new OpenCodeCli(runner.Object, "custom-opencode");
        using var cancellation = new CancellationTokenSource();
        var directory = Directory.GetCurrentDirectory();

        var result = await cli.ListModelsAsync(directory, cancellation.Token);

        result.Version.ShouldBe("1.2.3");
        result.Models.ShouldBe(new[] { "provider/model-one", "other/model.2:free" });
        var requests = runner.Invocations.Select(call =>
            (OpenCodeProcessRequest)call.Arguments[0]).ToArray();
        requests.Select(request => string.Join(" ", request.Arguments))
            .ShouldBe(new[] { "--version", "run --help", "models" });
        requests.ShouldAllBe(request =>
            request.Executable == "custom-opencode" &&
            request.WorkingDirectory == directory &&
            request.StandardInput == null);
        runner.Invocations.ShouldAllBe(call =>
            (CancellationToken)call.Arguments[1] == cancellation.Token);
    }

    [Fact]
    public async Task WhenProcessRejectsInputAndExitsNonzero_ShouldPreserveItsError()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var runner = new OpenCodeProcessRunner();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await runner.RunAsync(new OpenCodeProcessRequest(
            "/usr/bin/head", ["--invalid-opencode-option"], Directory.GetCurrentDirectory(),
            new string('x', 300000)), timeout.Token);

        result.ExitCode.ShouldNotBe(0);
        result.StandardError.ShouldContain("invalid-opencode-option");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenAlreadyCancelled_ShouldNotStartDiscoveryOrExecution(bool discovery)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cli = new OpenCodeCli(Path.Join(
            Directory.GetCurrentDirectory(), "must-not-be-started"));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            if (discovery)
                await cli.ListModelsAsync(Directory.GetCurrentDirectory(), cancellation.Token);
            else
                await cli.RunAsync(
                    "prompt", "provider/model", Directory.GetCurrentDirectory(), cancellation.Token);
        });
    }

    [Fact]
    public async Task WhenBothOutputStreamsExceedLimit_ShouldMarkTruncationAndKeepDraining()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var runner = new OpenCodeProcessRunner(maxCapturedCharacters: 1024);
        var prompt = new string('x', 300000);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await runner.RunAsync(new OpenCodeProcessRequest(
            "/usr/bin/tee", ["/dev/stderr"], Directory.GetCurrentDirectory(), prompt),
            timeout.Token);

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(new string('x', 1024) + "\n[output truncated]");
        result.StandardError.ShouldBe(new string('x', 1024) + "\n[output truncated]");
    }

    [Fact]
    public async Task WhenCancelled_ShouldKillAndAwaitEntireProcessTree()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using var cancellation = new CancellationTokenSource();
        var runner = new OpenCodeProcessRunner();
        var running = runner.RunAsync(new OpenCodeProcessRequest(
            "/usr/bin/timeout", ["60", "/usr/bin/sleep", "59"],
            Directory.GetCurrentDirectory()), cancellation.Token);
        Process? parent = null;
        Process? child = null;
        try
        {
            await WaitUntilAsync(() =>
            {
                var ids = Directory.EnumerateDirectories($"/proc/{Environment.ProcessId}/task")
                    .SelectMany(directory => File.ReadAllText(Path.Join(directory, "children"))
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Distinct();
                parent = ids.Select(id => Process.GetProcessById(int.Parse(id)))
                    .FirstOrDefault(process => process.ProcessName == "timeout");
                if (parent == null)
                    return false;
                var childIds = File.ReadAllText($"/proc/{parent.Id}/task/{parent.Id}/children")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (childIds.Length == 0)
                    return false;
                child = Process.GetProcessById(int.Parse(childIds[0]));
                return true;
            });

            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => running);

            parent.ShouldNotBeNull();
            child.ShouldNotBeNull();
            parent.HasExited.ShouldBeTrue();
            await WaitUntilAsync(() => child.HasExited || IsZombie(child.Id));
        }
        finally
        {
            using (parent)
            using (child)
            {
                if (parent is { HasExited: false })
                    parent.Kill(true);
                if (child is { HasExited: false })
                    child.Kill();
            }
        }
    }

    [Fact]
    public async Task WhenPromptIsLarge_ShouldWriteUtf8StdinAndCloseItWhileDrainingOutput()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var runner = new OpenCodeProcessRunner();
        var prompt = string.Concat(Enumerable.Repeat("héllo \" $(ignored)\n", 10000));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await runner.RunAsync(new OpenCodeProcessRequest(
            "/usr/bin/cat", ["-"], Directory.GetCurrentDirectory(), prompt), timeout.Token);

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(prompt);
        result.StandardError.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenProcessArgumentsContainShellSyntax_ShouldTreatThemAsLiteralArguments()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var runner = new OpenCodeProcessRunner();
        const string literal = "spaces \"quotes\" ' $(touch unsafe) ; --model";

        var result = await runner.RunAsync(new OpenCodeProcessRequest(
            "/usr/bin/printf", ["%s", literal], Directory.GetCurrentDirectory()));

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(literal);
        result.StandardError.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenExecutableIsMissing_ShouldExplainHowToFixDiscovery()
    {
        var executable = Path.Join(Directory.GetCurrentDirectory(), "missing-opencode-executable");
        IAgentService cli = new OpenCodeCli(executable);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => cli.ListModelsAsync(Directory.GetCurrentDirectory()));

        exception.Message.ShouldContain(executable);
        exception.Message.ShouldContain("install");
        exception.InnerException.ShouldBeOfType<System.ComponentModel.Win32Exception>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public async Task WhenRunning_ShouldPassExplicitArgumentsAndStdinAndPreserveResult(int exitCode)
    {
        var runner = new Mock<IOpenCodeProcessRunner>();
        OpenCodeProcessRequest? captured = null;
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var expected = new AgentResult(exitCode, "json output", "diagnostic");
        runner.Setup(x => x.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(),
                token))
            .Callback<OpenCodeProcessRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(expected);
        IAgentService cli = new OpenCodeCli(runner.Object, "custom opencode");
        const string instructions = "quotes \" ' $(touch unsafe)\n--model other/model";
        var directory = Directory.GetCurrentDirectory();

        var result = await cli.RunAsync(
            instructions, "provider/model", directory, token);

        result.ShouldBe(expected);
        captured.ShouldNotBeNull();
        captured.Executable.ShouldBe("custom opencode");
        captured.Arguments.ShouldBe(new[] { "run", "--model", "provider/model", "--format", "json" });
        captured.WorkingDirectory.ShouldBe(directory);
        captured.StandardInput.ShouldBe(instructions);
        runner.Verify(x => x.RunAsync(
            It.IsAny<OpenCodeProcessRequest>(), token), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("provider/model --other")]
    [InlineData("model")]
    [InlineData(null)]
    public async Task WhenRunModelIsInvalid_ShouldRejectWithoutStartingProcess(string? model)
    {
        var runner = new Mock<IOpenCodeProcessRunner>(MockBehavior.Strict);
        var cli = new OpenCodeCli(runner.Object);

        await Should.ThrowAsync<ArgumentException>(() => cli.RunAsync(
            "instructions", model!, Directory.GetCurrentDirectory()));

        runner.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, "--version")]
    [InlineData(1, "run --help")]
    [InlineData(2, "models")]
    public async Task WhenDiscoveryCommandFails_ShouldReportCommandAndError(
        int failedCommand,
        string command)
    {
        var runner = new Mock<IOpenCodeProcessRunner>();
        var outputs = new[] { "1.2.3", "--model --format", "provider/model" };
        var call = 0;
        runner.Setup(x => x.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var index = call++;
                return new AgentResult(
                    index == failedCommand ? 7 : 0,
                    outputs[index],
                    index == failedCommand ? "discovery failed" : "");
            });
        var cli = new OpenCodeCli(runner.Object);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => cli.ListModelsAsync(Directory.GetCurrentDirectory()));

        exception.Message.ShouldContain(command);
        exception.Message.ShouldContain("7");
        exception.Message.ShouldContain("discovery failed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n")]
    [InlineData("model")]
    [InlineData("/model")]
    [InlineData("provider/")]
    [InlineData("provider/model extra")]
    [InlineData("provider/model\nunstructured header")]
    public async Task WhenModelListingIsMalformed_ShouldRejectDiscovery(string models)
    {
        var cli = new OpenCodeCli(DiscoveryRunner(models).Object);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => cli.ListModelsAsync(Directory.GetCurrentDirectory()));

        exception.Message.ShouldContain("model");
    }

    [Theory]
    [InlineData("--model")]
    [InlineData("--format")]
    [InlineData("--model-selection --formatter")]
    public async Task WhenRequiredRunCapabilitiesAreMissing_ShouldRejectDiscovery(string help)
    {
        var runner = new Mock<IOpenCodeProcessRunner>();
        runner.SetupSequence(x => x.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(0, "1.2.3", ""))
            .ReturnsAsync(new AgentResult(0, help, ""));
        var cli = new OpenCodeCli(runner.Object);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => cli.ListModelsAsync(Directory.GetCurrentDirectory()));

        exception.Message.ShouldContain("--model");
        exception.Message.ShouldContain("--format");
    }

    [Fact]
    public async Task WhenVersionIsEmpty_ShouldRejectDiscovery()
    {
        var runner = new Mock<IOpenCodeProcessRunner>();
        runner.Setup(x => x.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(0, " \n", ""));
        var cli = new OpenCodeCli(runner.Object);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => cli.ListModelsAsync(Directory.GetCurrentDirectory()));

        exception.Message.ShouldContain("version");
    }

    private static Mock<IOpenCodeProcessRunner> DiscoveryRunner(string models)
    {
        var runner = new Mock<IOpenCodeProcessRunner>();
        runner.SetupSequence(x => x.RunAsync(
                It.IsAny<OpenCodeProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult(0, "1.2.3\n", ""))
            .ReturnsAsync(new AgentResult(0, "--model MODEL --format json", ""))
            .ReturnsAsync(new AgentResult(0, models, ""));
        return runner;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException("Expected process state was not reached.");
            await Task.Delay(20);
        }
    }

    private static bool IsZombie(int processId) =>
        File.Exists($"/proc/{processId}/stat") &&
        File.ReadAllText($"/proc/{processId}/stat").Split(' ')[2] == "Z";
}
