using FluentValidation;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.Modules.Experiments.Application.Projects.Selection;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Applies the active company's inclusion criteria (SISLAB-02) to a batch's animals, marking each included/excluded
/// from its physiological readings and recording the deciding value/reason. Selection is per batch ("leva") — the
/// experimental model bound to the batch (SISLAB-04) decides which parameters apply, so a criterion on a parameter the
/// model does not list is ignored (non-blocking). Returns the number of animals a decision was taken for.
/// </summary>
/// <remarks>
/// The model's applicable parameters and the inclusion criteria are read across the module boundary through the
/// Configuration <see cref="ILabConfiguration"/> port (Contracts only — module isolation, section 2); the criteria are
/// adapted to the domain's <see cref="IInclusionRule"/> before the aggregate applies them, so the Experiments Domain
/// depends on no other module. A batch with no model bound has no applicable parameters, so nothing is decided —
/// safe by construction.
/// </remarks>
public sealed record ApplyInclusionCriteriaCommand(Guid ProjectId, Guid BatchId) : ICommand<int>;

internal sealed class ApplyInclusionCriteriaCommandValidator : AbstractValidator<ApplyInclusionCriteriaCommand>
{
    public ApplyInclusionCriteriaCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
    }
}

internal sealed class ApplyInclusionCriteriaCommandHandler : ICommandHandler<ApplyInclusionCriteriaCommand, int>
{
    private readonly IProjectRepository _projects;
    private readonly ILabConfiguration _labConfiguration;

    public ApplyInclusionCriteriaCommandHandler(IProjectRepository projects, ILabConfiguration labConfiguration)
    {
        _projects = projects;
        _labConfiguration = labConfiguration;
    }

    public async Task<int> HandleAsync(
        ApplyInclusionCriteriaCommand request,
        CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        Batch batch = project.FindBatch(request.BatchId);

        // The model bound to the batch gates which parameters apply; no model → no applicable parameter → no decision.
        IReadOnlySet<string> applicableParameters =
            await ResolveApplicableParametersAsync(batch.ExperimentalModelId, cancellationToken);

        IReadOnlyList<InclusionCriterionDto> criteria =
            await _labConfiguration.GetInclusionCriteriaAsync(cancellationToken);

        IReadOnlyList<IInclusionRule> rules = criteria
            .Select(criterion => (IInclusionRule)new InclusionCriterionRule(criterion))
            .ToList();

        int decided = project.ApplyInclusionCriteria(request.BatchId, rules, applicableParameters);

        await _projects.UpdateAsync(project, cancellationToken);

        return decided;
    }

    private async Task<IReadOnlySet<string>> ResolveApplicableParametersAsync(
        Guid? experimentalModelId,
        CancellationToken cancellationToken)
    {
        if (experimentalModelId is not { } modelId)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ExperimentalModelDto? model =
            await _labConfiguration.GetExperimentalModelAsync(modelId, cancellationToken);

        return model is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(model.Parameters, StringComparer.OrdinalIgnoreCase);
    }
}
