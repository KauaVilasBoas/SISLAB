using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.Modules.Identity.Domain.Companies.Events;
using SISLAB.Modules.Identity.Infrastructure.Messaging;

namespace SISLAB.Modules.Identity.Tests.Infrastructure.Messaging;

/// <summary>
/// Covers the <see cref="CompanyCreatedEventTranslator"/> (card [E12] #75b): the internal
/// <see cref="CompanyCreated"/> domain event maps to the public, flattened
/// <see cref="CompanyCreatedIntegrationEvent"/> written to the Outbox before it crosses the module boundary.
/// </summary>
public sealed class CompanyCreatedEventTranslatorTests
{
    [Fact]
    public void Translate_maps_every_field_and_mints_a_fresh_event_id()
    {
        Guid companyId = Guid.NewGuid();
        Guid coordinatorId = Guid.NewGuid();
        var domainEvent = new CompanyCreated(companyId, "Acme Labs", coordinatorId);

        var integrationEvent = Assert.IsType<CompanyCreatedIntegrationEvent>(
            new CompanyCreatedEventTranslator().Translate(domainEvent));

        Assert.NotEqual(Guid.Empty, integrationEvent.EventId);
        Assert.Equal(domainEvent.OccurredOnUtc, integrationEvent.OccurredOnUtc);
        Assert.Equal(companyId, integrationEvent.CompanyId);
        Assert.Equal("Acme Labs", integrationEvent.Name);
        Assert.Equal(coordinatorId, integrationEvent.CoordinatorUserId);
        Assert.Equal(nameof(CompanyCreatedIntegrationEvent), integrationEvent.EventType);
    }

    [Fact]
    public void Translate_yields_a_new_event_id_on_each_call()
    {
        var domainEvent = new CompanyCreated(Guid.NewGuid(), "Acme Labs", Guid.NewGuid());
        var translator = new CompanyCreatedEventTranslator();

        Guid first = translator.Translate(domainEvent).EventId;
        Guid second = translator.Translate(domainEvent).EventId;

        Assert.NotEqual(first, second);
    }
}
