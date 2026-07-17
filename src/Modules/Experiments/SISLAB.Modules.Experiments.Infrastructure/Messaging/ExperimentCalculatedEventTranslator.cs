using SISLAB.Modules.Experiments.Contracts.Events;
using SISLAB.Modules.Experiments.Domain.Experiments.Events;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Infrastructure.Messaging;

/// <summary>
/// Translates the internal <see cref="ExperimentCalculatedEvent"/> into the public
/// <see cref="ExperimentCalculatedIntegrationEvent"/> before it is written to the Outbox. The domain event is
/// rich and module-internal; the integration event is flat and public (decision card #68 — DomainEvent ≠
/// IntegrationEvent).
/// </summary>
internal sealed class ExperimentCalculatedEventTranslator
    : IDomainEventToIntegrationEventTranslator<ExperimentCalculatedEvent>
{
    public IIntegrationEvent Translate(ExperimentCalculatedEvent domainEvent) =>
        new ExperimentCalculatedIntegrationEvent(
            eventId: Guid.NewGuid(),
            occurredOnUtc: domainEvent.OccurredOnUtc,
            companyId: domainEvent.CompanyId,
            experimentId: domainEvent.ExperimentId,
            experimentType: domainEvent.Type.ToString(),
            formulaName: domainEvent.FormulaName,
            appliedAtUtc: domainEvent.AppliedAtUtc);
}
