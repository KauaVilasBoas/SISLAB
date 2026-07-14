using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Tests.Domain.ExpiryPolicies;

/// <summary>
/// Covers the single invariant of the per-tenant <see cref="ExpiryPolicy"/> (card [E12] #76): the warning
/// window is always a strictly positive, sensible number of days, whether the policy is created, seeded or
/// changed. This is the rule that replaces the retired hardcoded 30-day constant in the Inventory read-side.
/// </summary>
public sealed class ExpiryPolicyTests
{
    [Fact]
    public void Create_keeps_the_configured_window()
    {
        ExpiryPolicy policy = ExpiryPolicy.Create(45);

        Assert.Equal(45, policy.WarningWindowDays);
    }

    [Fact]
    public void ExpiryPolicy_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(ExpiryPolicy.Create(30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_window(int window)
    {
        Assert.Throws<DomainException>(() => ExpiryPolicy.Create(window));
    }

    [Fact]
    public void Create_rejects_a_window_beyond_two_years()
    {
        Assert.Throws<DomainException>(() => ExpiryPolicy.Create(731));
    }

    [Fact]
    public void ChangeWarningWindow_updates_the_window()
    {
        ExpiryPolicy policy = ExpiryPolicy.Create(30);

        policy.ChangeWarningWindow(60);

        Assert.Equal(60, policy.WarningWindowDays);
    }

    [Fact]
    public void ChangeWarningWindow_rejects_a_non_positive_window_and_keeps_the_previous_value()
    {
        ExpiryPolicy policy = ExpiryPolicy.Create(30);

        Assert.Throws<DomainException>(() => policy.ChangeWarningWindow(0));
        Assert.Equal(30, policy.WarningWindowDays);
    }

    [Fact]
    public void The_default_policy_uses_the_sensible_thirty_day_window()
    {
        ExpiryPolicy policy = DefaultExpiryPolicy.ForCompany(Guid.NewGuid());

        Assert.Equal(ExpiryPolicy.DefaultWarningWindowDays, policy.WarningWindowDays);
        Assert.Equal(30, policy.WarningWindowDays);
    }

    [Fact]
    public void The_default_policy_id_is_deterministic_per_company()
    {
        Guid company = Guid.NewGuid();

        Assert.Equal(
            DefaultExpiryPolicy.DeterministicId(company),
            DefaultExpiryPolicy.ForCompany(company).Id);
    }

    [Fact]
    public void The_default_policy_id_differs_between_companies()
    {
        Assert.NotEqual(
            DefaultExpiryPolicy.DeterministicId(Guid.NewGuid()),
            DefaultExpiryPolicy.DeterministicId(Guid.NewGuid()));
    }
}
