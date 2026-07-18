using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Biobank.Commands;

/// <summary>
/// Collects a biobank sample from a study animal during an experiment's collection step (card [E11] #89,
/// decision F4). It records the collection hand-off on the source <see cref="BehavioralExperiment"/> (a
/// <see cref="ExperimentStepKind.Collection"/> step) and creates the <see cref="Sample"/> aggregate that
/// originates from it, holding the animal/project/batch/experiment ids by value. Returns the new sample id.
/// </summary>
/// <remarks>
/// The collection is the origin of the sample, so the two writes happen together in the command's transaction: the
/// experiment's step is marked performed and the sample is added. The company is stamped by the write-side tenant
/// machinery on <c>SaveChanges</c>; the collector comes from the audit actor accessor, the instant from the clock.
/// The sample code is kept unique per company by the handler (a fast pre-check; the DB unique index is the final
/// guard).
/// </remarks>
public sealed record CollectSampleCommand(
    Guid SourceExperimentId,
    Guid AnimalId,
    string Code,
    SampleType Type,
    decimal Quantity,
    string Unit,
    decimal? ConservationTempMinCelsius,
    decimal? ConservationTempMaxCelsius,
    string? StorageLabel,
    string? Notes) : ICommand<Guid>;

internal sealed class CollectSampleCommandValidator : AbstractValidator<CollectSampleCommand>
{
    public CollectSampleCommandValidator()
    {
        RuleFor(command => command.SourceExperimentId).NotEmpty();
        RuleFor(command => command.AnimalId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Quantity).GreaterThan(0);
        RuleFor(command => command.Unit).NotEmpty().MaximumLength(30);
        RuleFor(command => command.StorageLabel).MaximumLength(120);
        RuleFor(command => command.Notes).MaximumLength(2000);
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

internal sealed class CollectSampleCommandHandler : ICommandHandler<CollectSampleCommand, Guid>
{
    private readonly ISampleRepository _samples;
    private readonly IExperimentRepository _experiments;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public CollectSampleCommandHandler(
        ISampleRepository samples,
        IExperimentRepository experiments,
        IAuditActorAccessor actorAccessor,
        ITenantContext tenantContext,
        IClock clock)
    {
        _samples = samples;
        _experiments = experiments;
        _actorAccessor = actorAccessor;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(CollectSampleCommand request, CancellationToken cancellationToken = default)
    {
        if (await _samples.CodeExistsAsync(request.Code, cancellationToken))
            throw new ConflictException($"A sample with code '{request.Code.Trim()}' already exists.");

        BehavioralExperiment experiment =
            await _experiments.FindBehavioralExperimentAsync(request.SourceExperimentId, cancellationToken)
            ?? throw new NotFoundException(
                $"Behavioural experiment '{request.SourceExperimentId}' was not found.");

        string actor = _actorAccessor.GetCurrentActor();
        DateTime now = _clock.UtcNow;

        // Record the collection hand-off on the experiment's flow (origin of the sample).
        experiment.RecordCollection($"Coleta {request.Code.Trim()}", actor, now);
        await _experiments.UpdateAsync(experiment, cancellationToken);

        TemperatureRange? conservation =
            request.ConservationTempMinCelsius.HasValue && request.ConservationTempMaxCelsius.HasValue
                ? TemperatureRange.Between(
                    request.ConservationTempMinCelsius.Value, request.ConservationTempMaxCelsius.Value)
                : null;

        Sample sample = Sample.Collect(
            _tenantContext.CompanyId,
            request.Code,
            request.Type,
            experiment.ProjectId,
            experiment.BatchId,
            request.AnimalId,
            request.SourceExperimentId,
            SampleAmount.Of(request.Quantity, request.Unit),
            actor,
            now,
            conservation,
            request.StorageLabel,
            request.Notes);

        await _samples.AddAsync(sample, cancellationToken);

        return sample.Id;
    }
}
