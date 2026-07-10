using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.Multitenancy;

/// <summary>
/// EF Core save interceptor that enforces write-side tenant isolation for every
/// <see cref="ITenantEntity"/> tracked by a SISLAB module DbContext.
///
/// On save, for each added entity:
/// <list type="bullet">
///   <item>if <c>company_id</c> is empty, it is stamped from the active
///   <see cref="ITenantContext.CompanyId"/> — developers never set it by hand;</item>
///   <item>if it is already set to a <b>different</b> company, the save is aborted
///   (<see cref="InvalidOperationException"/>) to prevent a cross-tenant write.</item>
/// </list>
/// For modified entities, a change to <c>company_id</c> is rejected — an entity may never be
/// re-parented to another tenant.
///
/// The whole check is skipped while an auditable <see cref="ITenantBypass"/> scope is active
/// (system/background work). When there is no active tenant and no bypass, adding a tenant
/// entity is a bug and fails fast rather than persisting an orphan row.
/// </summary>
public sealed class TenantStampingInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantBypass _tenantBypass;

    public TenantStampingInterceptor(ITenantContext tenantContext, ITenantBypass tenantBypass)
    {
        _tenantContext = tenantContext;
        _tenantBypass = tenantBypass;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        StampTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampTenant(DbContext? context)
    {
        if (context is null || _tenantBypass.IsActive)
            return;

        Guid activeCompanyId = _tenantContext.CompanyId;

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    StampAddedEntity(entry, activeCompanyId);
                    break;

                case EntityState.Modified:
                    RejectTenantReparenting(entry);
                    break;
            }
        }
    }

    private static void StampAddedEntity(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ITenantEntity> entry,
        Guid activeCompanyId)
    {
        Guid current = entry.Entity.CompanyId;

        if (current == Guid.Empty)
        {
            if (activeCompanyId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Cannot persist tenant entity '{entry.Entity.GetType().Name}' without an active " +
                    "company. Ensure the request resolved a tenant, or open an explicit ITenantBypass scope.");

            entry.Property(nameof(ITenantEntity.CompanyId)).CurrentValue = activeCompanyId;
            return;
        }

        // Entity already carries a company (e.g. seed): it must match the active tenant.
        if (activeCompanyId != Guid.Empty && current != activeCompanyId)
            throw new InvalidOperationException(
                $"Cross-tenant write blocked: entity '{entry.Entity.GetType().Name}' targets company " +
                $"'{current}' but the active company is '{activeCompanyId}'.");
    }

    private static void RejectTenantReparenting(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ITenantEntity> entry)
    {
        var companyIdProperty = entry.Property(nameof(ITenantEntity.CompanyId));
        if (companyIdProperty.IsModified &&
            !Equals(companyIdProperty.OriginalValue, companyIdProperty.CurrentValue))
        {
            throw new InvalidOperationException(
                $"Re-parenting a tenant entity is not allowed: '{entry.Entity.GetType().Name}' " +
                "attempted to change company_id.");
        }
    }
}
