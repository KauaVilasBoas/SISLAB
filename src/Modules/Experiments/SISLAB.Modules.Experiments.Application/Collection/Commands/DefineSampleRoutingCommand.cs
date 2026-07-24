using FluentValidation;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Collection.Commands;

/// <summary>
/// Defines (or replaces) one matrix row of a collection plan (SISLAB-08): for a sample type, the planned analyses and
/// the storage (an optional Configuration room + free-text label + conservation range) — e.g. "Sangue → Hemograma →
/// Fiocruz / −20 °C". Idempotent by sample type: re-defining a type overwrites its routing, so the matrix keeps one row
/// per type.
/// </summary>
/// <remarks>
/// The storage room, when supplied, is a Configuration <c>Room</c> validated across the boundary through the
/// <see cref="ILabConfiguration"/> port (Contracts only — module isolation, section 2) and kept by value on the routing.
/// Nothing lab-specific is fixed: the sample type, analyses and storage are all inputs.
/// </remarks>
public sealed record DefineSampleRoutingCommand(
    Guid PlanId,
    SampleType SampleType,
    IReadOnlyList<string> PlannedAnalyses,
    Guid? StorageRoomId,
    string? StorageLabel,
    decimal? ConservationTempMinCelsius,
    decimal? ConservationTempMaxCelsius) : ICommand;

internal sealed class DefineSampleRoutingCommandValidator : AbstractValidator<DefineSampleRoutingCommand>
{
    public DefineSampleRoutingCommandValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.SampleType).IsInEnum();
        RuleFor(command => command.PlannedAnalyses).NotEmpty();
        RuleForEach(command => command.PlannedAnalyses).NotEmpty().MaximumLength(200);
        RuleFor(command => command.StorageLabel).MaximumLength(120);
        RuleFor(command => command.ConservationTempMaxCelsius)
            .GreaterThanOrEqualTo(command => command.ConservationTempMinCelsius!.Value)
            .When(command =>
                command.ConservationTempMinCelsius.HasValue && command.ConservationTempMaxCelsius.HasValue)
            .WithMessage("The conservation maximum temperature must not be below the minimum.");
        RuleFor(command => command)
            .Must(command =>
                command.ConservationTempMinCelsius.HasValue == command.ConservationTempMaxCelsius.HasValue)
            .WithMessage("Provide both conservation temperature bounds or neither.");
    }
}

internal sealed class DefineSampleRoutingCommandHandler : ICommandHandler<DefineSampleRoutingCommand>
{
    private readonly ICollectionPlanRepository _plans;
    private readonly ILabConfiguration _labConfiguration;

    public DefineSampleRoutingCommandHandler(ICollectionPlanRepository plans, ILabConfiguration labConfiguration)
    {
        _plans = plans;
        _labConfiguration = labConfiguration;
    }

    public async Task<Unit> HandleAsync(DefineSampleRoutingCommand request, CancellationToken cancellationToken = default)
    {
        CollectionPlan plan = await _plans.FindByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException($"Collection plan '{request.PlanId}' was not found.");

        // A supplied storage room must be a real room of the active company (validated via the Configuration port).
        if (request.StorageRoomId is { } roomId
            && !await _labConfiguration.RoomExistsAsync(roomId, cancellationToken))
        {
            throw new BusinessException(
                $"Storage room '{roomId}' does not exist for the active company and cannot be a routing's storage.");
        }

        TemperatureRange? conservation =
            request.ConservationTempMinCelsius.HasValue && request.ConservationTempMaxCelsius.HasValue
                ? TemperatureRange.Between(
                    request.ConservationTempMinCelsius.Value, request.ConservationTempMaxCelsius.Value)
                : null;

        plan.DefineRouting(
            request.SampleType,
            request.PlannedAnalyses,
            request.StorageRoomId,
            request.StorageLabel,
            conservation);

        await _plans.UpdateAsync(plan, cancellationToken);

        return Unit.Value;
    }
}
