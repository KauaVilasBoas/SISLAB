using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SISLAB.Jobs.Scheduling;
using SISLAB.Modules.Identity.Application.Administration;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Template Method base for the E6 cross-tenant alert jobs (#41 validity, #42 low-stock, #66 calibration).
///
/// <para>
/// Every alert job shares the same shape: on each tick, enumerate every active company and, for each one,
/// run a tenant-scoped read query and raise a notification per at-risk row — deduplicated per cycle. This
/// base owns that multi-tenant orchestration so a concrete job only expresses <b>what</b> to scan for one
/// company (<see cref="ScanCompanyAsync"/>), never <b>how</b> the cross-tenant loop, the bypass and the
/// per-company tenant override are wired.
/// </para>
///
/// <para>
/// Orchestration per tick (Fork #1 → A seam of override, Fork #2 → E1 enumeration):
/// <list type="number">
///   <item>Open an auditable <see cref="ITenantBypass"/> scope (legitimate cross-tenant system work).</item>
///   <item>Resolve <see cref="IMediator"/> and send <see cref="ListAllCompanyIdsQuery"/> to get the ids of
///     every active company (the enumeration lives in Identity.Application, not here).</item>
///   <item>For EACH company: open a fresh child DI scope, set that company on the scoped
///     <see cref="ITenantContextOverride"/>, and delegate to <see cref="ScanCompanyAsync"/>. Inside that
///     scope the untouched E4 read queries resolve the effective <c>ITenantContext</c> and see exactly this
///     company — no company id flows through their signatures. The override is cleared and the child scope
///     disposed before moving on, so a tenant's data can never leak into the next.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Resilience.</b> A failure while scanning one company is logged and swallowed so the remaining
/// companies are still processed on the same tick (one bad tenant never blocks the others). The base tick
/// itself is further guarded by <see cref="TimedBackgroundService"/>, which keeps the worker alive across
/// ticks.
/// </para>
/// </summary>
public abstract class CompanyScanAlertJob : TimedBackgroundService
{
    /// <summary>
    /// Page size used when draining a company's at-risk set — the read side clamps this to its own maximum.
    /// The scans are low-volume (a lab's at-risk set is small), so paging only bounds each round-trip.
    /// </summary>
    protected const int ScanPageSize = 200;

    private readonly IClock _clock;
    private readonly ILogger _logger;

    protected CompanyScanAlertJob(IServiceScopeFactory scopeFactory, IClock clock, ILogger logger)
        : base(scopeFactory, logger)
    {
        _clock = clock;
        _logger = logger;
    }

    /// <summary>The audit reason logged when the job opens its cross-tenant <see cref="ITenantBypass"/> scope.</summary>
    protected abstract string BypassReason { get; }

    /// <summary>
    /// Scans a single company for at-risk rows and raises the corresponding notifications. Runs inside a
    /// fresh child DI <paramref name="companyScope"/> whose <see cref="ITenantContextOverride"/> already
    /// reports <paramref name="companyId"/> as the active tenant, so E4 read queries dispatched here are
    /// automatically scoped to it. Resolve the mediator / notification publisher from
    /// <paramref name="companyScope"/>. <paramref name="scanDay"/> is captured once for the whole tick, so
    /// every company on the same tick shares the same day-bucketed dedupe key even if the tick crosses
    /// midnight. Throwing is safe: the base logs it and continues with the next company.
    /// </summary>
    protected abstract Task ScanCompanyAsync(
        IServiceScope companyScope,
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    protected sealed override async Task ExecuteTickAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        ITenantBypass tenantBypass = scope.ServiceProvider.GetRequiredService<ITenantBypass>();
        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Capture the scan day ONCE for the whole tick. Recomputing it per company would let a tick that
        // crosses midnight hand different companies different day-bucketed dedupe keys, producing duplicate
        // alerts for the same at-risk row.
        DateOnly scanDay = DateOnly.FromDateTime(_clock.UtcNow);

        // Enumerating companies and scanning across them is legitimate cross-tenant system work: open an
        // auditable bypass for the whole tick so the enumeration query and every per-company scan are traceable.
        using IDisposable _ = tenantBypass.BeginScope(BypassReason);

        ListAllCompanyIdsQueryResult companies =
            await mediator.SendAsync(new ListAllCompanyIdsQuery(), cancellationToken);

        foreach (Guid companyId in companies.CompanyIds)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await ScanSingleCompanySafelyAsync(companyId, scanDay, cancellationToken);
        }
    }

    private async Task ScanSingleCompanySafelyAsync(
        Guid companyId,
        DateOnly scanDay,
        CancellationToken cancellationToken)
    {
        try
        {
            // Fresh child scope per company: the scoped ITenantContextOverride (and any scoped read
            // collaborators) are isolated to this company and disposed before the next one, so no tenant's
            // data bleeds across iterations.
            using IServiceScope companyScope = ScopeFactory.CreateScope();

            ITenantContextOverride tenantOverride =
                companyScope.ServiceProvider.GetRequiredService<ITenantContextOverride>();
            tenantOverride.SetCompany(companyId);

            try
            {
                await ScanCompanyAsync(companyScope, companyId, scanDay, cancellationToken);
            }
            finally
            {
                tenantOverride.Clear();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One company's failure must not stop the scan of the others on this tick.
            _logger.LogError(ex,
                "Alert job {JobName} failed while scanning company {CompanyId}; skipping to the next company.",
                JobName, companyId);
        }
    }
}
