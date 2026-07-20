using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Domain.Subscriptions;

/// <summary>
/// A user's private iCal feed subscription (card [E10.10]): the opaque <see cref="Token"/> that lets an external
/// calendar client (Google Calendar, Outlook, Apple Calendar) poll <c>/api/agenda/calendar.ics</c> without a
/// session. The token is a capability — knowing it grants read access to that user's agenda within one company —
/// so it is unguessable (a GUID) and can be rotated with <see cref="RenewToken"/> to revoke old links.
/// </summary>
/// <remarks>
/// One subscription per (company, user): the calendar feed a person subscribes to always reflects the company
/// they were operating in when they generated it. The token is tenant-scoped on read, so a leaked token can never
/// reach another company's data.
/// </remarks>
public sealed class IcalSubscription : AggregateRoot<Guid>, ITenantEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>The Lumen user id whose agenda this feed exposes.</summary>
    public Guid UserId { get; private set; }

    /// <summary>The unguessable capability token embedded in the feed URL. Rotated by <see cref="RenewToken"/>.</summary>
    public Guid Token { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private IcalSubscription() : base(Guid.Empty) { }

    private IcalSubscription(Guid id, Guid companyId, Guid userId, Guid token, DateTime nowUtc) : base(id)
    {
        CompanyId = companyId;
        UserId = userId;
        Token = token;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Creates a subscription with a fresh random token for the given company/user.</summary>
    public static IcalSubscription Create(Guid companyId, Guid userId, DateTime nowUtc)
        => new(Guid.NewGuid(), companyId, userId, Guid.NewGuid(), nowUtc);

    /// <summary>Rotates the token, invalidating every previously shared feed URL for this user.</summary>
    public void RenewToken(DateTime nowUtc)
    {
        Token = Guid.NewGuid();
        UpdatedAtUtc = nowUtc;
    }
}
