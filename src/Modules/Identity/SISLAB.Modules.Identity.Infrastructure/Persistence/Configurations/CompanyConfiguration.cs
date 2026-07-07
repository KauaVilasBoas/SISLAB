using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core do agregado <see cref="Company"/>.
/// Schema: "tenancy" — bounded context de multi-tenancy do SISLAB, isolado do schema
/// "identity" (exclusivo da Lumen Identity: usuários/tokens) e das tabelas do Inventory.
/// </summary>
internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies", schema: "tenancy");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.TaxId)
            .HasMaxLength(20);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.HasIndex(c => c.Name)
            .HasDatabaseName("ix_companies_name");

        // Navigation para memberships — carregamento explícito pelo repositório
        builder.HasMany(c => c.Memberships)
            .WithOne()
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
