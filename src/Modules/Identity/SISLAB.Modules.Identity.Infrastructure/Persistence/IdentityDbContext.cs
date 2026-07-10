using Microsoft.EntityFrameworkCore;
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
public sealed class IdentityDbContext : SislabDbContextBase
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // snake_case naming convention applied by the base
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyMembershipConfiguration());
    }
}
