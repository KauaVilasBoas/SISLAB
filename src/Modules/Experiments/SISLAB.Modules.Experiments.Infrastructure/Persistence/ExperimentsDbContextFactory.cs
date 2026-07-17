using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="ExperimentsDbContext"/>. Lets the EF Core CLI generate migrations
/// without a running host. The tenant services are left null — at design time there is no request scope, so
/// the global query filter is not applied and migrations see the full model (correct for schema generation).
/// Uses a throwaway design-time connection string that never reaches production.
/// </summary>
internal sealed class ExperimentsDbContextFactory : IDesignTimeDbContextFactory<ExperimentsDbContext>
{
    public ExperimentsDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ExperimentsDbContext> optionsBuilder =
            new DbContextOptionsBuilder<ExperimentsDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=sislab_design;Username=postgres;Password=postgres",
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "experiments");
                        npgsql.MigrationsAssembly(
                            typeof(ExperimentsDbContextFactory).Assembly.GetName().Name);
                    });

        return new ExperimentsDbContext(optionsBuilder.Options);
    }
}
