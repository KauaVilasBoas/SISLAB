using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SISLAB.Infrastructure.Messaging;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Tests;

public sealed class InMemoryEventBusTests
{
    [Fact]
    public async Task Publishes_to_registered_handler()
    {
        var handler = new RecordingIntegrationHandler();
        ServiceCollection services = new();
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(handler);
        using ServiceProvider provider = services.BuildServiceProvider();

        var bus = new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance);
        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "TestIntegrationEvent"));

        Assert.Equal(1, handler.HandleCallCount);
    }

    [Fact]
    public async Task Isolates_handler_failure_so_others_still_run()
    {
        var recording = new RecordingIntegrationHandler();
        ServiceCollection services = new();
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(new ThrowingIntegrationHandler());
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(recording);
        using ServiceProvider provider = services.BuildServiceProvider();

        var bus = new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance);

        // Não deve propagar a exceção do primeiro handler.
        await bus.PublishAsync(new TestIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, "TestIntegrationEvent"));

        Assert.Equal(1, recording.HandleCallCount);
    }
}
