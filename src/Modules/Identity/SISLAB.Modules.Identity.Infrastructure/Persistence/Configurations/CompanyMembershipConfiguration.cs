using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core da tabela de associação N:N company_user.
///
/// <see cref="CompanyMembership.LumenUserId"/> é armazenado por valor —
/// sem FK para a tabela de usuários da Lumen, garantindo isolamento de bounded context.
/// </summary>
internal sealed class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("company_memberships", schema: "tenancy");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.CompanyId)
            .IsRequired();

        // Referência por valor: lumen_user_id é apenas uma coluna Guid sem FK externa
        builder.Property(m => m.LumenUserId)
            .IsRequired()
            .HasColumnName("lumen_user_id");

        builder.Property(m => m.JoinedAt)
            .IsRequired();

        // Um usuário não pode ser membro duplicado na mesma empresa
        builder.HasIndex(m => new { m.CompanyId, m.LumenUserId })
            .IsUnique()
            .HasDatabaseName("ix_company_memberships_company_user");

        // Índice auxiliar para queries "em quais empresas este usuário está?"
        builder.HasIndex(m => m.LumenUserId)
            .HasDatabaseName("ix_company_memberships_lumen_user_id");
    }
}
