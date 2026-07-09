using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="IdentityDbContext"/>.
/// Allows the EF Core CLI to generate migrations without a running host.
/// Uses a design-time connection string — never exposed in production.
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
