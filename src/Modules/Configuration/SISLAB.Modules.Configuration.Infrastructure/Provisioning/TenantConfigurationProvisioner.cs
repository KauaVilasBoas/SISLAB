using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;
using SISLAB.Modules.Configuration.Domain.ItemCategories;
using SISLAB.Modules.Configuration.Domain.Units;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Infrastructure.Provisioning;

/// <summary>
/// Seeds the sensible per-tenant configuration defaults at provisioning time (card [E12] #76): an
/// <see cref="ExpiryPolicy"/> with the default 30-day warning window, the base <see cref="ItemCategory"/>
/// catalogue (the nine legacy enum values) and the base <see cref="Unit"/> catalogue. Generalizes the
/// LAFTE-specific dev seed into a company-agnostic provisioner reusable by the onboarding flow (card #75
/// wires the trigger).
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotent and deterministic.</b> Categories and units are seeded at deterministic ids derived from
/// <c>(company, code/symbol)</c> and the policy at a deterministic id derived from the company, so a re-run
/// never duplicates a row (each step checks existence by id first). This is the same reproducibility the
/// enum→category data backfill relies on.
/// </para>
/// <para>
/// <b>Cross-tenant write.</b> Provisioning runs outside any HTTP request and targets a specific company, so
/// it opens an auditable <see cref="ITenantBypass"/> scope: this disables the global query filter (so the
/// existence checks see the target company's rows) and lets the tenant-stamping interceptor accept the rows
/// that already carry their <c>company_id</c>. The company id is set explicitly on each aggregate here.
/// </para>
/// </remarks>
public sealed class TenantConfigurationProvisioner
{
    private readonly ConfigurationDbContext _dbContext;
    private readonly ITenantBypass _tenantBypass;
    private readonly ILogger<TenantConfigurationProvisioner> _logger;

    public TenantConfigurationProvisioner(
        ConfigurationDbContext dbContext,
        ITenantBypass tenantBypass,
        ILogger<TenantConfigurationProvisioner> logger)
    {
        _dbContext = dbContext;
        _tenantBypass = tenantBypass;
        _logger = logger;
    }

    /// <summary>Deterministic id of a company's singleton expiry policy — stable across re-runs.</summary>
    public static Guid ExpiryPolicyId(Guid companyId) => DefaultExpiryPolicy.DeterministicId(companyId);

    /// <summary>
    /// Seeds the configuration defaults for <paramref name="companyId"/> if they are not already present.
    /// Safe to call repeatedly (idempotent by deterministic id).
    /// </summary>
    public async Task ProvisionAsync(Guid companyId, CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("A company id is required to provision configuration.", nameof(companyId));

        using IDisposable _ = _tenantBypass.BeginScope("configuration-provisioning");

        await EnsureExpiryPolicyAsync(companyId, ct);
        await EnsureCategoriesAsync(companyId, ct);
        await EnsureUnitsAsync(companyId, ct);

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Configuration defaults ensured for company {CompanyId}.", companyId);
    }

    private async Task EnsureExpiryPolicyAsync(Guid companyId, CancellationToken ct)
    {
        Guid policyId = ExpiryPolicyId(companyId);
        bool exists = await _dbContext.ExpiryPolicies.AsNoTracking().AnyAsync(p => p.Id == policyId, ct);
        if (exists)
            return;

        ExpiryPolicy policy = DefaultExpiryPolicy.ForCompany(companyId);
        await _dbContext.ExpiryPolicies.AddAsync(policy, ct);
        StampCompany(policy, companyId);
    }

    private async Task EnsureCategoriesAsync(Guid companyId, CancellationToken ct)
    {
        HashSet<Guid> existing = (await _dbContext.ItemCategories
                .AsNoTracking()
                .Select(category => category.Id)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (ItemCategory category in DefaultItemCategories.ForCompany(companyId))
        {
            if (existing.Contains(category.Id))
                continue;

            await _dbContext.ItemCategories.AddAsync(category, ct);
            StampCompany(category, companyId);
        }
    }

    private async Task EnsureUnitsAsync(Guid companyId, CancellationToken ct)
    {
        HashSet<Guid> existing = (await _dbContext.Units
                .AsNoTracking()
                .Select(unit => unit.Id)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (Unit unit in DefaultUnits.ForCompany(companyId))
        {
            if (existing.Contains(unit.Id))
                continue;

            await _dbContext.Units.AddAsync(unit, ct);
            StampCompany(unit, companyId);
        }
    }

    /// <summary>
    /// Stamps the target company on a just-tracked seeded aggregate. The <c>CompanyId</c> setter is private
    /// (assigned by the tenant interceptor at runtime); provisioning targets a specific company outside a
    /// request, so once the entity is tracked (after <c>AddAsync</c>) it sets the property explicitly through
    /// the change tracker. The bypass scope keeps the tenant interceptor from rejecting the explicit stamp.
    /// </summary>
    private void StampCompany(object aggregate, Guid companyId)
        => _dbContext.Entry(aggregate).Property(nameof(ITenantEntity.CompanyId)).CurrentValue = companyId;
}
