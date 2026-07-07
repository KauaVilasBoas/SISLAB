using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Infrastructure.Persistence.Configurations;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Identity.
/// Gerencia exclusivamente entidades do bounded context de identidade do SISLAB:
/// Company e CompanyMembership (schema "identity").
///
/// As tabelas da Lumen (usuários, tokens, perfis, permissões) são gerenciadas pelos
/// DbContexts internos da Lumen via hosted service de migrations — este contexto
/// nunca referencia aquelas entidades.
/// </summary>
public sealed class IdentityDbContext : SislabDbContextBase
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // snake_case naming convention aplicada pelo base
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CompanyConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyMembershipConfiguration());
    }
}
