using SISLAB.Modules.Agenda.Domain.Subscriptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Subscriptions.Commands;

/// <summary>
/// Generates or renews the calling user's private iCal feed token for the active company (card [E10.10]). The
/// user is the authenticated principal (never a client-supplied id) and the company comes from
/// <see cref="ITenantContext"/> — the token is a per-(company, user) capability. Calling it again rotates the
/// token, which is exactly how a user revokes a previously shared feed URL.
/// </summary>
public sealed record SubscribeToIcalCommand(Guid UserId) : ICommand<IcalSubscriptionResult>;

/// <summary>The feed token to embed in the <c>.ics</c> URL after subscribing/renewing.</summary>
public sealed record IcalSubscriptionResult(Guid Token);

internal sealed class SubscribeToIcalCommandHandler : ICommandHandler<SubscribeToIcalCommand, IcalSubscriptionResult>
{
    private readonly IIcalSubscriptionRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public SubscribeToIcalCommandHandler(
        IIcalSubscriptionRepository repository,
        ITenantContext tenantContext,
        IClock clock)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<IcalSubscriptionResult> HandleAsync(
        SubscribeToIcalCommand command, CancellationToken cancellationToken = default)
    {
        IcalSubscription? existing = await _repository.GetByUserAsync(command.UserId, cancellationToken);

        if (existing is null)
        {
            var created = IcalSubscription.Create(_tenantContext.CompanyId, command.UserId, _clock.UtcNow);
            _repository.Add(created);
            return new IcalSubscriptionResult(created.Token);
        }

        // Renew on every subscribe so the user can rotate a leaked/shared token on demand.
        existing.RenewToken(_clock.UtcNow);
        return new IcalSubscriptionResult(existing.Token);
    }
}
