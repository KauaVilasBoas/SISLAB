using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Preparations;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Calculates and persists an in vivo solution preparation (SISLAB-01) for a dose group of a batch: it builds the
/// <see cref="InVivoPreparationInput"/> value object from the request, lets the aggregate run the pure
/// <see cref="InVivoPreparationCalculator"/> and freeze the traceable, immutable <see cref="SolutionPreparation"/>
/// snapshot (inputs + result + author + instant), linked to the batch/group. The author comes from the audit actor
/// accessor and the instant from the clock — never the request body. Returns the new preparation id.
/// </summary>
/// <remarks>
/// Nothing lab-specific is fixed here: the dose (g/kg), the group weight, the animal-weight basis the g:µL relation is
/// applied to, the relation (µL per g), the compound state (powder/liquid) and its density, and the vehicle-only flag
/// are all request inputs. A vehicle-only preparation (the Controle arm) uses only the relation and the weight basis —
/// no compound, all diluent — modelled by <see cref="InVivoPreparationInput.ForVehicleOnly"/>.
/// </remarks>
public sealed record PrepareGroupSolutionCommand(
    Guid ProjectId,
    Guid BatchId,
    Guid GroupId,
    bool IsVehicleOnly,
    decimal RelationMicrolitresPerGram,
    decimal RelationWeightGrams,
    decimal? DoseAmountGramsPerKilogram,
    decimal? GroupWeightGrams,
    CompoundState? State,
    decimal? DensityGramsPerMillilitre) : ICommand<Guid>;

internal sealed class PrepareGroupSolutionCommandValidator : AbstractValidator<PrepareGroupSolutionCommand>
{
    public PrepareGroupSolutionCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.RelationMicrolitresPerGram).GreaterThan(0);
        RuleFor(command => command.RelationWeightGrams).GreaterThan(0);

        // Treatment arm (not vehicle-only): the dose, group weight and compound state are required — they are what
        // the compound mass and the density/subtraction step are computed from.
        When(command => !command.IsVehicleOnly, () =>
        {
            RuleFor(command => command.DoseAmountGramsPerKilogram)
                .NotNull().GreaterThan(0)
                .WithMessage("A dosed preparation requires a positive dose (g/kg).");
            RuleFor(command => command.GroupWeightGrams)
                .NotNull().GreaterThan(0)
                .WithMessage("A dosed preparation requires a positive group weight (g).");
            RuleFor(command => command.State)
                .NotNull()
                .WithMessage("A dosed preparation requires the compound state (powder or liquid).");
            RuleFor(command => command.DensityGramsPerMillilitre)
                .NotNull().GreaterThan(0)
                .When(command => command.State == CompoundState.Liquid)
                .WithMessage("A liquid compound requires its density (g/mL).");
        });
    }
}

internal sealed class PrepareGroupSolutionCommandHandler : ICommandHandler<PrepareGroupSolutionCommand, Guid>
{
    private readonly IProjectRepository _projects;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public PrepareGroupSolutionCommandHandler(
        IProjectRepository projects,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _projects = projects;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(PrepareGroupSolutionCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        InVivoPreparationInput input = BuildInput(request);

        SolutionPreparation preparation = project.PrepareGroupSolution(
            request.BatchId,
            request.GroupId,
            input,
            _actorAccessor.GetCurrentActor(),
            _clock.UtcNow);

        await _projects.UpdateAsync(project, cancellationToken);

        return preparation.Id;
    }

    // Maps the flat request to the domain input value object, choosing the vehicle-only vs treatment factory. The
    // value object's factories re-guard the physical invariants (positive dose/weights, density for liquids), so a
    // malformed input past validation still cannot build a snapshot.
    private static InVivoPreparationInput BuildInput(PrepareGroupSolutionCommand request)
    {
        GramMicrolitreRatio ratio = GramMicrolitreRatio.OfGramToMicrolitres(request.RelationMicrolitresPerGram);

        if (request.IsVehicleOnly)
            return InVivoPreparationInput.ForVehicleOnly(request.RelationWeightGrams, ratio);

        return InVivoPreparationInput.ForTreatment(
            request.DoseAmountGramsPerKilogram!.Value,
            request.GroupWeightGrams!.Value,
            request.RelationWeightGrams,
            ratio,
            request.State!.Value,
            request.DensityGramsPerMillilitre);
    }
}
