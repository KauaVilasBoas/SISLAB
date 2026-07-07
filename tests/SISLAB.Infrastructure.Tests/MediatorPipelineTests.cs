using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Messaging;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Tests;

public sealed class MediatorPipelineTests
{
    [Fact]
    public async Task SendAsync_executes_behaviors_in_registration_order_and_reaches_handler()
    {
        List<string> log = new();
        ServiceCollection services = new();
        services.AddSingleton<IRequestHandler<PingCommand, string>, PingCommandHandler>();
        services.AddSingleton<IPipelineBehavior<PingCommand, string>>(new RecordingBehavior<PingCommand, string>(log, "A"));
        services.AddSingleton<IPipelineBehavior<PingCommand, string>>(new RecordingBehavior<PingCommand, string>(log, "B"));
        services.AddSingleton<IMediator, Mediator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        string result = await provider.GetRequiredService<IMediator>().SendAsync(new PingCommand("pong"));

        Assert.Equal("pong", result);
        // A é registrado primeiro → é o behavior mais externo do pipeline.
        Assert.Equal(new[] { "A:before", "B:before", "B:after", "A:after" }, log);
    }

    [Fact]
    public async Task SendAsync_without_behaviors_invokes_handler_directly()
    {
        ServiceCollection services = new();
        services.AddSingleton<IRequestHandler<PingCommand, string>, PingCommandHandler>();
        services.AddSingleton<IMediator, Mediator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        string result = await provider.GetRequiredService<IMediator>().SendAsync(new PingCommand("x"));

        Assert.Equal("x", result);
    }
}
