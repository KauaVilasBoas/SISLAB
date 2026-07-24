using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Collection.Commands;

/// <summary>
/// Creates the collection plan for a batch (leva) — SISLAB-08. Write-side: it validates the batch belongs to the
/// project (through the Project aggregate), guarantees one plan per batch, then creates the empty plan the routings and
/// role assignments are later attached to. Returns the new plan id.
/// </summary>
/// <remarks>
/// The company is taken from <see cref="ITenantContext"/> (never the client). The batch must exist in the project —
/// <see cref="Project.FindBatch"/> throws when it does not, so a plan can never point at a batch of another study.
/// </remarks>
public sealed record CreateCollectionPlanCommand(Guid ProjectId, Guid BatchId) : ICommand<Guid>;

internal sealed class CreateCollectionPlanCommandValidator : AbstractValidator<CreateCollectionPlanCommand>
{
    public CreateCollectionPlanCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
    }
}

internal sealed class CreateCollectionPlanCommandHandler : ICommandHandler<CreateCollectionPlanCommand, Guid>
{
    private readonly ICollectionPlanRepository _plans;
    private readonly IProjectRepository _projects;
    private readonly ITenantContext _tenantContext;

    public CreateCollectionPlanCommandHandler(
        ICollectionPlanRepository plans,
        IProjectRepository projects,
        ITenantContext tenantContext)
    {
        _plans = plans;
        _projects = projects;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> HandleAsync(CreateCollectionPlanCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        // Validates the batch belongs to the project (throws NotFoundException when it does not).
        project.FindBatch(request.BatchId);

        if (await _plans.ExistsForBatchAsync(request.BatchId, cancellationToken))
            throw new ConflictException($"A collection plan already exists for batch '{request.BatchId}'.");

        CollectionPlan plan = CollectionPlan.Create(_tenantContext.CompanyId, request.ProjectId, request.BatchId);

        await _plans.AddAsync(plan, cancellationToken);

        return plan.Id;
    }
}
