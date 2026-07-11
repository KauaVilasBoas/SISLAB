using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Persistence;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Infrastructure.Tests;

/// <summary>
/// Guards the production DI wiring of the CQRS pipeline (card [E2] #74).
///
/// The shared infrastructure registers the module-agnostic behaviors (Logging, Validation);
/// each write-side module contributes the TransactionBehavior (it depends on the module's
/// IUnitOfWork). Because the <see cref="Messaging.Mediator"/> reverses the resolved sequence,
/// the FIRST registered behavior is the OUTERMOST wrapper — these tests pin that contract so a
/// future reorder cannot silently break "Logging → Validation → Transaction → Handler".
/// </summary>
public sealed class PipelineBehaviorRegistrationTests
{
    [Fact]
    public void AddSislabInfrastructure_registers_logging_then_validation_as_outer_behaviors()
    {
        ServiceCollection services = new();

        services.AddSislabInfrastructure();

        Type[] behaviorImplementations = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType!)
            .ToArray();

        Assert.Equal(
            new[] { typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>) },
            behaviorImplementations);
    }

    [Fact]
    public void Full_write_side_wiring_orders_behaviors_logging_validation_transaction()
    {
        ServiceCollection services = new();

        // Shared infrastructure first (mirrors Program.cs: AddSislabInfrastructure runs before
        // the modules register), then the module-owned TransactionBehavior.
        services.AddSislabInfrastructure();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        Type[] behaviorImplementations = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType!)
            .ToArray();

        // Registration order == pipeline order (first registered = outermost after the reverse).
        Assert.Equal(
            new[]
            {
                typeof(LoggingBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(TransactionBehavior<,>)
            },
            behaviorImplementations);
    }

    [Fact]
    public async Task Command_dispatched_through_real_mediator_runs_behaviors_in_order_and_commits()
    {
        List<string> executionLog = new();
        FakeUnitOfWork unitOfWork = new();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSislabInfrastructure();

        // Simulate the write-side module contribution.
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // Probe behaviors bracket the real pipeline to observe ordering without altering it:
        // one registered before AddSislabInfrastructure would be too outer, so we register a
        // pair that records around each real stage boundary via the terminal handler and validator.
        services.AddScoped<IValidator<TrackingCommand>>(_ => new TrackingCommandValidator(executionLog));
        services.AddScoped<IRequestHandler<TrackingCommand, string>>(
            _ => new TrackingCommandHandler(executionLog, unitOfWork));

        await using ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new TrackingCommand("ok"));

        Assert.Equal("handled", result);

        // Validation runs before the handler; the handler records the UoW state at execution time
        // (still 0 — TransactionBehavior commits AFTER the handler returns).
        Assert.Equal(new[] { "validator", "handler" }, executionLog);
        Assert.Equal(0, unitOfWork.SaveChangesCallCountAtHandler);

        // TransactionBehavior committed exactly once, after the handler.
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }
}

// ------------------------- Tracking test doubles -------------------------

public sealed record TrackingCommand(string Value) : ICommand<string>;

public sealed class TrackingCommandValidator : AbstractValidator<TrackingCommand>
{
    public TrackingCommandValidator(List<string> log)
    {
        RuleFor(x => x.Value)
            .Must(_ =>
            {
                log.Add("validator");
                return true;
            });
    }
}

public sealed class TrackingCommandHandler : IRequestHandler<TrackingCommand, string>
{
    private readonly List<string> _log;
    private readonly FakeUnitOfWork _unitOfWork;

    public TrackingCommandHandler(List<string> log, FakeUnitOfWork unitOfWork)
    {
        _log = log;
        _unitOfWork = unitOfWork;
    }

    public Task<string> HandleAsync(TrackingCommand request, CancellationToken cancellationToken = default)
    {
        _log.Add("handler");
        _unitOfWork.CaptureSaveChangesCallCountAtHandler();
        return Task.FromResult("handled");
    }
}
