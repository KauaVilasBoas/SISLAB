using SISLAB.Modules.Agenda.Application.Subscriptions.Commands;
using SISLAB.Modules.Agenda.Domain.Subscriptions;
using SISLAB.Modules.Agenda.Tests.Fakes;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Agenda.Tests.Application;

/// <summary>
/// Handler tests for the iCal subscribe/renew command (card [E10.10]): first subscribe mints a token scoped to
/// the active company; a repeat subscribe rotates the existing token so a shared feed URL can be revoked.
/// </summary>
public sealed class SubscribeToIcalCommandHandlerTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly FixedClock Clock = new(new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
    private static readonly StubTenantContext Tenant = new(Company);

    [Fact]
    public async Task First_subscribe_creates_subscription_scoped_to_active_company()
    {
        var repo = new FakeIcalSubscriptionRepository();
        var handler = new SubscribeToIcalCommandHandler(repo, Tenant, Clock);

        IcalSubscriptionResult result = await handler.HandleAsync(new SubscribeToIcalCommand(User));

        IcalSubscription created = Assert.Single(repo.Added);
        Assert.Equal(Company, created.CompanyId);
        Assert.Equal(User, created.UserId);
        Assert.Equal(created.Token, result.Token);
        Assert.NotEqual(Guid.Empty, result.Token);
    }

    [Fact]
    public async Task Repeat_subscribe_rotates_existing_token_without_creating_a_new_row()
    {
        var existing = IcalSubscription.Create(Company, User, Clock.UtcNow);
        Guid originalToken = existing.Token;
        var repo = new FakeIcalSubscriptionRepository().Seed(existing);
        var handler = new SubscribeToIcalCommandHandler(repo, Tenant, Clock);

        IcalSubscriptionResult result = await handler.HandleAsync(new SubscribeToIcalCommand(User));

        Assert.Empty(repo.Added); // renewed in place, no new subscription
        Assert.NotEqual(originalToken, result.Token);
        Assert.Equal(existing.Token, result.Token);
    }
}
