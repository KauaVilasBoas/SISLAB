using FluentValidation;
using SISLAB.Modules.Configuration.Domain.InclusionCriteria;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.InclusionCriteria;

/// <summary>
/// Creates a per-tenant animal-inclusion criterion (SISLAB-02): the configurable "(parameter, operator, threshold,
/// unit)" rule a lab cadasters to select animals from a physiological reading (e.g. glicemia ≥ 250 mg/dL). Write-side:
/// it maps the flat payload onto the aggregate's validated threshold and lets the unit of work commit. Returns the new
/// criterion id.
/// </summary>
/// <remarks>
/// Nothing lab-specific is fixed — the parameter, operator, threshold and unit are all supplied by the caller. A
/// tenant may hold only one criterion per parameter; the handler pre-checks that (the DB unique index is the final
/// guard), so re-cadastering the same parameter is a conflict rather than a silent second row.
/// </remarks>
public sealed record CreateInclusionCriterionCommand(
    string ParameterCode,
    ComparisonOperator Operator,
    decimal Threshold,
    string Unit) : ICommand<Guid>;

internal sealed class CreateInclusionCriterionCommandValidator : AbstractValidator<CreateInclusionCriterionCommand>
{
    public CreateInclusionCriterionCommandValidator()
    {
        RuleFor(command => command.ParameterCode).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Operator).IsInEnum();
        RuleFor(command => command.Unit).NotEmpty().MaximumLength(30);
    }
}

internal sealed class CreateInclusionCriterionCommandHandler
    : ICommandHandler<CreateInclusionCriterionCommand, Guid>
{
    private readonly IInclusionCriterionRepository _criteria;

    public CreateInclusionCriterionCommandHandler(IInclusionCriterionRepository criteria) => _criteria = criteria;

    public async Task<Guid> HandleAsync(
        CreateInclusionCriterionCommand request,
        CancellationToken cancellationToken = default)
    {
        if (await _criteria.ParameterExistsAsync(request.ParameterCode, cancellationToken))
            throw new ConflictException(
                $"An inclusion criterion for parameter '{request.ParameterCode.Trim()}' already exists.");

        InclusionCriterion criterion = InclusionCriterion.Create(
            request.ParameterCode,
            request.Operator,
            request.Threshold,
            request.Unit);

        await _criteria.AddAsync(criterion, cancellationToken);

        return criterion.Id;
    }
}
