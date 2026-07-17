using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Experiments.Commands;

/// <summary>
/// Lays out the 8×12 plate of a viability experiment (decision card #68): the full set of wells with their
/// coordinate, assay <see cref="PlateWellDefinition.Role"/> and optional concentration/sample metadata.
/// Designing the plate as a whole (not well-by-well) matches how the operator sets up a plate and lets the
/// aggregate guarantee coordinate uniqueness in one pass. Moves a draft experiment into execution.
/// </summary>
public sealed record DesignPlateCommand(
    Guid ExperimentId,
    IReadOnlyList<PlateWellDefinition> Wells) : ICommand;

/// <summary>One well in a plate design: its coordinate, role and optional concentration/sample metadata.</summary>
public sealed record PlateWellDefinition(
    char Row,
    int Column,
    WellRole Role,
    decimal? ConcentrationUm,
    string? SampleId);

internal sealed class DesignPlateCommandValidator : AbstractValidator<DesignPlateCommand>
{
    public DesignPlateCommandValidator()
    {
        RuleFor(command => command.ExperimentId).NotEmpty();
        RuleFor(command => command.Wells).NotEmpty();
        RuleForEach(command => command.Wells).ChildRules(well =>
        {
            well.RuleFor(w => w.Column).InclusiveBetween(1, Plate.Columns);
            well.RuleFor(w => w.Role).IsInEnum();
            well.RuleFor(w => w.ConcentrationUm).GreaterThanOrEqualTo(0).When(w => w.ConcentrationUm.HasValue);
            well.RuleFor(w => w.SampleId).MaximumLength(120);
        });
    }
}

internal sealed class DesignPlateCommandHandler : ICommandHandler<DesignPlateCommand>
{
    private readonly IExperimentRepository _experiments;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public DesignPlateCommandHandler(
        IExperimentRepository experiments,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _experiments = experiments;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<Unit> HandleAsync(DesignPlateCommand request, CancellationToken cancellationToken = default)
    {
        PlateExperiment experiment =
            await _experiments.FindPlateExperimentWithPlateAsync(request.ExperimentId, cancellationToken)
            ?? throw new NotFoundException($"Plate experiment '{request.ExperimentId}' was not found.");

        IEnumerable<Well> wells = request.Wells.Select(definition =>
            Well.Create(definition.Row, definition.Column, definition.Role, definition.ConcentrationUm, definition.SampleId));

        experiment.DesignPlate(wells, _actorAccessor.GetCurrentActor(), _clock.UtcNow);

        await _experiments.UpdateAsync(experiment, cancellationToken);

        return Unit.Value;
    }
}
