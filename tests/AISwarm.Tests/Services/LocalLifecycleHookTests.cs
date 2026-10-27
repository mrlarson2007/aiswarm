using AISwarm.Server.Delegation;
using System.Net;
using System.Text.Json;
using Shouldly;

namespace AISwarm.Tests.Services;

public class LocalLifecycleHookTests
{
    [Theory]
    [InlineData("https://example.com/hook")]
    [InlineData("file:///tmp/hook")]
    [InlineData("******127.0.0.1/hook")]
    [InlineData("http://localhost/hook")]
    public void WhenCallbackIsNotAnUnauthenticatedLoopbackAddress_ShouldRejectConfiguration(string endpoint)
    {
        using var client = new HttpClient();

        Should.Throw<ArgumentException>(() => new LocalLifecycleHook(client, endpoint));
    }

    [Theory]
    [InlineData("http://127.0.0.1:8099/hook")]
    [InlineData("http://[::1]:8099/hook")]
    public async Task WhenTaskCompletes_ShouldPostMetadataWithoutSendingTaskOutput(string endpoint)
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var systemUnderTest = new LocalLifecycleHook(client, endpoint);
        var task = new DelegatedTask(
            "task-id", "Completed", "local/model", Path.GetTempPath(), "test", DateTimeOffset.UtcNow,
            ExitCode: 0, StandardOutput: "private result");

        await systemUnderTest.NotifyAsync(task, CancellationToken.None);

        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Endpoint.ShouldBe(new Uri(endpoint));
        using var payload = JsonDocument.Parse(handler.Body!);
        payload.RootElement.GetProperty("taskId").GetString().ShouldBe("task-id");
        payload.RootElement.GetProperty("state").GetString().ShouldBe("Completed");
        payload.RootElement.GetProperty("resultUri").GetString().ShouldBe("aiswarm://tasks/task-id");
        handler.Body!.ShouldNotContain("private result");
    }

    [Fact]
    public async Task WhenHostNotificationStalls_ShouldDeliverLocalCallbackIndependently()
    {
        var hostNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var systemUnderTest = new LocalLifecycleHook(client, "http://127.0.0.1:8099/hook");
        var snapshot = new DelegatedTask(
            "task-id", "Completed", "local/model", Path.GetTempPath(), "test", DateTimeOffset.UtcNow);

        var delivery = systemUnderTest.NotifyAsync(snapshot, CancellationToken.None, hostNotification.Task);
        await handler.Received.Task.WaitAsync(TimeSpan.FromSeconds(1));

        delivery.IsCompleted.ShouldBeFalse();
        handler.Body.ShouldNotBeNull();
        hostNotification.SetResult();
        await delivery;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Body;
        public HttpMethod? Method;
        public Uri? Endpoint;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Endpoint = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Received.SetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
