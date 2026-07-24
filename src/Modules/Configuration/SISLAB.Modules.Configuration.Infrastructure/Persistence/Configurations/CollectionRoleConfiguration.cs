using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.CollectionRoles;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="CollectionRole"/> aggregate (schema <c>configuration</c>, SISLAB-08). A
/// tenant has at most one role per name, so <c>(company_id, name)</c> is unique.
/// </summary>
internal sealed class CollectionRoleConfiguration : IEntityTypeConfiguration<CollectionRole>
{
    public void Configure(EntityTypeBuilder<CollectionRole> builder)
    {
        builder.ToTable("collection_roles");

        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).ValueGeneratedNever();

        builder.Property(role => role.CompanyId).IsRequired();

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(role => role.Description)
            .HasMaxLength(500);

        // A tenant has one role per name: the natural key is unique per company.
        builder.HasIndex(role => new { role.CompanyId, role.Name })
            .IsUnique()
            .HasDatabaseName("ux_collection_roles_company_id_name");

        // Tenant isolation access path.
        builder.HasIndex(role => new { role.CompanyId, role.Id })
            .HasDatabaseName("ix_collection_roles_company_id_id");
    }
}
