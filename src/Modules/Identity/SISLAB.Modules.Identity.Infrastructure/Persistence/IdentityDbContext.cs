using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Infrastructure.Persistence.Configurations;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext for the Identity module.
/// Exclusively manages SISLAB multi-tenancy entities — Company and CompanyMembership
/// — in the <c>tenancy</c> schema. Schema <c>identity</c> is reserved for Lumen Identity
/// (users, tokens); this context never touches it.
/// </summary>
/// <remarks>
/// It participates in the Transactional Outbox pattern (<see cref="IOutboxDbContext"/>): the module raises
/// the <c>CompanyCreated</c> domain event on self-service signup that <see cref="EfUnitOfWork{TContext}"/>
/// translates and writes to <c>tenancy.outbox_messages</c> in the same transaction as the aggregate change
/// (card [E12] #75b). The Company aggregate is the tenant root itself (not an <c>ITenantEntity</c>), so — as
/// before — no tenant query filter is applied here; the outbox is likewise cross-tenant system data.
/// </remarks>
public sealed class IdentityDbContext : SislabDbContextBase, IOutboxDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();

    /// <inheritdoc />
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Default schema for the module. Company/CompanyMembership already pin "tenancy" explicitly, so this
        // is a no-op for them; its purpose is to place the shared OutboxMessageConfiguration's table in
        // "tenancy" too (it maps to_table without a schema of its own), keeping the aggregate write and the
        // outbox write in the same schema/transaction (local transactional consistency).
        modelBuilder.HasDefaultSchema("tenancy");

        // snake_case naming convention applied by the base
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
