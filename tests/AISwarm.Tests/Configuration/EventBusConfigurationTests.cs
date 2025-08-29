using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AISwarm.Tests.Configuration;

public class EventBusConfigurationTests
{
    [Fact(Timeout = 5000)]
    public async Task WhenEventBusConfigured_ShouldApplyBackpressure()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["EventBus:Capacity"] = "1";
        builder.Configuration["EventBus:FullMode"] = "Wait";

        builder.Services.AddInfrastructureServices(builder.Configuration);

        using var sp = builder.Services.BuildServiceProvider();
        var bus = sp.GetService<IEventBus<TaskEventType, ITaskLifecyclePayload>>();
        bus.ShouldNotBeNull(); // Expect DI to provide an event bus

        var service = sp.GetRequiredService<IWorkItemNotificationService>();
        var agentId = "agent-di-backpressure";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        // Create subscription (do not consume yet)
        var stream = service.SubscribeForAgent(agentId, token);

        // Fill capacity
        await service.PublishTaskCreated("t1", agentId, null, CancellationToken.None);

        // Second publish should be blocked until we read
        var second = service.PublishTaskCreated("t2", agentId, null, CancellationToken.None).AsTask();
        await Task.Delay(100, token);
        second.IsCompleted.ShouldBeFalse();

        // Drain one item
        await using var e = stream.GetAsyncEnumerator(token);
        (await e.MoveNextAsync()).ShouldBeTrue();

        await second; // should complete after capacity freed
    }
}
