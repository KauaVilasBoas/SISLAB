using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ExpiryPolicy"/> aggregate (schema <c>configuration</c>).
/// The policy is a singleton per tenant, enforced by a unique index on <c>company_id</c>.
/// </summary>
internal sealed class ExpiryPolicyConfiguration : IEntityTypeConfiguration<ExpiryPolicy>
{
    public void Configure(EntityTypeBuilder<ExpiryPolicy> builder)
    {
        builder.ToTable("expiry_policies");

        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Id).ValueGeneratedNever();

        builder.Property(policy => policy.CompanyId).IsRequired();

        builder.Property(policy => policy.WarningWindowDays).IsRequired();

        // One expiry policy per tenant: a unique index on company_id makes the singleton a DB invariant.
        builder.HasIndex(policy => policy.CompanyId)
            .IsUnique()
            .HasDatabaseName("ux_expiry_policies_company_id");
    }
}
