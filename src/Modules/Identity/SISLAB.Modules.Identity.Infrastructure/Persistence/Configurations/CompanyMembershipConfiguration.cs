using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the N:N company_memberships join entity.
/// <see cref="CompanyMembership.LumenUserId"/> is stored by value — no FK to Lumen's
/// user table, ensuring bounded-context isolation.
/// </summary>
internal sealed class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("company_memberships", schema: "tenancy");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.CompanyId)
            .IsRequired();

        // Value reference: lumen_user_id is a plain Guid column with no external FK.
        builder.Property(m => m.LumenUserId)
            .IsRequired()
            .HasColumnName("lumen_user_id");

        builder.Property(m => m.JoinedAt)
            .IsRequired();

        // A user cannot be a duplicate member of the same company.
        builder.HasIndex(m => new { m.CompanyId, m.LumenUserId })
            .IsUnique()
            .HasDatabaseName("ix_company_memberships_company_user");

        // Supporting index for "which companies does this user belong to?" queries.
        builder.HasIndex(m => m.LumenUserId)
            .HasDatabaseName("ix_company_memberships_lumen_user_id");
    }
}
