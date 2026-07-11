using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="InventoryDbContext"/>.
/// Lets the EF Core CLI generate migrations without a running host. The tenant services are left
/// null — at design time there is no request scope, so the global query filter is not applied and
/// migrations see the full model (correct for schema generation). Uses a throwaway design-time
/// connection string that never reaches production.
/// </summary>
internal sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<InventoryDbContext> optionsBuilder =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=sislab_design;Username=postgres;Password=postgres",
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "inventory");
                        npgsql.MigrationsAssembly(
                            typeof(InventoryDbContextFactory).Assembly.GetName().Name);
                    });

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
