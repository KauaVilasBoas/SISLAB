using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Factory de design-time para o <see cref="IdentityDbContext"/>.
/// Permite ao CLI do EF Core gerar migrations sem um host rodando.
/// Usa uma connection string de design-time — nunca exposta em produção.
/// </summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<IdentityDbContext> optionsBuilder =
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=sislab_design;Username=postgres;Password=postgres",
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "tenancy");
                        npgsql.MigrationsAssembly(
                            typeof(IdentityDbContextFactory).Assembly.GetName().Name);
                    });

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
