using FluentValidation;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Application.Biobank.Commands;

/// <summary>
/// Runs an analysis against a biobank sample (card [E11] #89), consuming an aliquot of its <b>derived</b> balance.
/// The consumed amount must share the sample's unit and cannot exceed what remains — the aggregate rejects an
/// over-consumption. Returns the new (pending) analysis id.
/// </summary>
/// <remarks>
/// The consuming unit is not taken from the payload as free choice: it must match the sample's collected unit, so
/// the balance stays a plain subtraction. The "who" comes from the audit actor accessor, the "when" from the
/// clock.
/// </remarks>
public sealed record AnalyseSampleCommand(
    Guid SampleId,
    string Name,
    decimal ConsumedQuantity,
    string Unit) : ICommand<Guid>;

internal sealed class AnalyseSampleCommandValidator : AbstractValidator<AnalyseSampleCommand>
{
    public AnalyseSampleCommandValidator()
    {
        RuleFor(command => command.SampleId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ConsumedQuantity).GreaterThan(0);
        RuleFor(command => command.Unit).NotEmpty().MaximumLength(30);
    }
}

internal sealed class AnalyseSampleCommandHandler : ICommandHandler<AnalyseSampleCommand, Guid>
{
    private readonly ISampleRepository _samples;
    private readonly IAuditActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public AnalyseSampleCommandHandler(
        ISampleRepository samples,
        IAuditActorAccessor actorAccessor,
        IClock clock)
    {
        _samples = samples;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<Guid> HandleAsync(AnalyseSampleCommand request, CancellationToken cancellationToken = default)
    {
        Sample sample = await _samples.FindByIdAsync(request.SampleId, cancellationToken)
            ?? throw new NotFoundException($"Sample '{request.SampleId}' was not found.");

        Analysis analysis = sample.Analyse(
            request.Name,
            SampleAmount.Of(request.ConsumedQuantity, request.Unit),
            _actorAccessor.GetCurrentActor(),
            _clock.UtcNow);

        await _samples.UpdateAsync(sample, cancellationToken);

        return analysis.Id;
    }
}
