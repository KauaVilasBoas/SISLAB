using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.Modules.Identity.Domain.Invitations.Events;
using SISLAB.Modules.Identity.Infrastructure.Messaging;

namespace SISLAB.Modules.Identity.Tests.Infrastructure.Messaging;

/// <summary>
/// Covers the <see cref="MemberInvitedEventTranslator"/> (card [E12] #75c): the internal
/// <see cref="MemberInvited"/> domain event maps to the public, flattened
/// <see cref="MemberInvitedIntegrationEvent"/> written to the Outbox before it crosses the module boundary —
/// carrying the raw token so the e-mail handler can build the accept link.
/// </summary>
public sealed class MemberInvitedEventTranslatorTests
{
    [Fact]
    public void Translate_maps_every_field_and_mints_a_fresh_event_id()
    {
        var domainEvent = new MemberInvited(
            InvitationId: Guid.NewGuid(),
            CompanyId: Guid.NewGuid(),
            Email: "invitee@lab.test",
            ProfileId: Guid.NewGuid(),
            InvitedByUserId: Guid.NewGuid(),
            RawToken: "raw-token-abc");

        var integrationEvent = Assert.IsType<MemberInvitedIntegrationEvent>(
            new MemberInvitedEventTranslator().Translate(domainEvent));

        Assert.NotEqual(Guid.Empty, integrationEvent.EventId);
        Assert.Equal(domainEvent.OccurredOnUtc, integrationEvent.OccurredOnUtc);
        Assert.Equal(domainEvent.InvitationId, integrationEvent.InvitationId);
        Assert.Equal(domainEvent.CompanyId, integrationEvent.CompanyId);
        Assert.Equal("invitee@lab.test", integrationEvent.Email);
        Assert.Equal(domainEvent.ProfileId, integrationEvent.ProfileId);
        Assert.Equal(domainEvent.InvitedByUserId, integrationEvent.InvitedByUserId);
        Assert.Equal("raw-token-abc", integrationEvent.RawToken);
        Assert.Equal(nameof(MemberInvitedIntegrationEvent), integrationEvent.EventType);
    }

    [Fact]
    public void Translate_yields_a_new_event_id_on_each_call()
    {
        var domainEvent = new MemberInvited(
            Guid.NewGuid(), Guid.NewGuid(), "a@b.c", Guid.NewGuid(), Guid.NewGuid(), "tok");
        var translator = new MemberInvitedEventTranslator();

        Guid first = translator.Translate(domainEvent).EventId;
        Guid second = translator.Translate(domainEvent).EventId;

        Assert.NotEqual(first, second);
    }
}
