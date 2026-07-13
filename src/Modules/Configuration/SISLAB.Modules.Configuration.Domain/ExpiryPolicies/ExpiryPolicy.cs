using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.ExpiryPolicies;

/// <summary>
/// Per-tenant expiry policy (card [E12] #76): the single knob that decides how many days ahead of a
/// batch's last valid day the lab wants to be warned that stock is "about to expire". It replaces the
/// hardcoded <c>ExpiryStatusRule.DefaultWarningWindowDays = 30</c> the Inventory read-side used, so each
/// laboratory tunes its own warning window without touching code.
/// </summary>
/// <remarks>
/// <para>
/// <b>Singleton per tenant.</b> A company has exactly one expiry policy — it is a lab-wide setting, not a
/// per-item one. The infrastructure enforces that with a unique index on <c>company_id</c>; the aggregate
/// keeps the domain invariant (a strictly positive window) so an out-of-range value can never be persisted.
/// </para>
/// <para>
/// <b>Why an aggregate and not a bare int.</b> The warning window carries an invariant (it must be a
/// positive, sensible number of days) and is the seed of future expiry-related settings; modelling it as a
/// rich aggregate keeps that rule in one place and lets the value be changed through an intention-revealing
/// method (<see cref="ChangeWarningWindow"/>) rather than a naked setter.
/// </para>
/// </remarks>
public sealed class ExpiryPolicy : AggregateRoot<Guid>, ITenantEntity
{
    /// <summary>Sensible default applied at tenant provisioning — mirrors the retired Inventory constant.</summary>
    public const int DefaultWarningWindowDays = 30;

    /// <summary>Upper bound guarding against nonsensical windows (two years ahead is already extreme for a lab).</summary>
    private const int MaxWarningWindowDays = 730;

    // Parameterless constructor for EF Core materialization.
    private ExpiryPolicy() : base(Guid.Empty) { }

    private ExpiryPolicy(Guid id, int warningWindowDays) : base(id)
        => WarningWindowDays = warningWindowDays;

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>Days ahead of a batch's last valid day at which it starts being flagged as "expiring soon".</summary>
    public int WarningWindowDays { get; private set; }

    /// <summary>
    /// Creates the tenant's expiry policy with the given warning window. Used both when a lab explicitly
    /// configures its window and, with <see cref="DefaultWarningWindowDays"/>, at provisioning time.
    /// </summary>
    public static ExpiryPolicy Create(int warningWindowDays)
    {
        EnsureValidWindow(warningWindowDays);
        return new ExpiryPolicy(Guid.NewGuid(), warningWindowDays);
    }

    /// <summary>
    /// Rehydrates a policy at a caller-supplied deterministic id — used by the idempotent tenant seeder so a
    /// re-run does not create a second policy for the same company.
    /// </summary>
    internal static ExpiryPolicy Seed(Guid id, int warningWindowDays)
    {
        EnsureValidWindow(warningWindowDays);
        return new ExpiryPolicy(id, warningWindowDays);
    }

    /// <summary>Changes the warning window, keeping the positive/sensible-range invariant.</summary>
    public void ChangeWarningWindow(int warningWindowDays)
    {
        EnsureValidWindow(warningWindowDays);
        WarningWindowDays = warningWindowDays;
    }

    private static void EnsureValidWindow(int warningWindowDays)
    {
        if (warningWindowDays <= 0)
            throw new DomainException("The expiry warning window must be a positive number of days.");

        if (warningWindowDays > MaxWarningWindowDays)
            throw new DomainException(
                $"The expiry warning window cannot exceed {MaxWarningWindowDays} days.");
    }
}
