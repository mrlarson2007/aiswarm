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

        await using var sp = builder.Services.BuildServiceProvider();
        var bus = sp.GetService<IEventBus<MemoryEventType, IMemoryLifecyclePayload>>();
        bus.ShouldNotBeNull(); // Expect DI to provide an event bus

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        // Create subscription (do not consume yet)
        var stream = bus.Subscribe(new EventFilter<MemoryEventType, IMemoryLifecyclePayload>(), token);

        // Fill capacity
        var firstEvent = new EventEnvelope<MemoryEventType, IMemoryLifecyclePayload>(
            MemoryEventType.Created, DateTimeOffset.UtcNow,
            new MemoryCreatedPayload(new AISwarm.Infrastructure.Entities.MemoryEntryDto("key", "value", "", "text", 5, null)));
        await bus.PublishAsync(firstEvent);

        // Second publish should be blocked until we read
        var second = bus.PublishAsync(firstEvent).AsTask();
        await Task.Delay(100, token);
        second.IsCompleted.ShouldBeFalse();

        // Drain one item
        await using var e = stream.GetAsyncEnumerator(token);
        (await e.MoveNextAsync()).ShouldBeTrue();

        await second; // should complete after capacity freed
    }
}
