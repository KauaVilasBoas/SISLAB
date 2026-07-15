using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SISLAB.Migrations;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build <see cref="SislabSeedDbContext"/> without a running host.
/// The connection string is read from the <c>SISLAB_DB</c> environment variable (never hardcoded, never in the
/// repo). The migration history lives in a dedicated <c>seed</c> schema so it never collides with the module
/// or Lumen migration histories.
/// </summary>
public sealed class SislabSeedDbContextFactory : IDesignTimeDbContextFactory<SislabSeedDbContext>
{
    public SislabSeedDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("SISLAB_DB")
            ?? throw new InvalidOperationException(
                "Set the SISLAB_DB environment variable to the PostgreSQL connection string before running 'dotnet ef'.");

        DbContextOptionsBuilder<SislabSeedDbContext> builder = new();
        builder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "seed"));

        return new SislabSeedDbContext(builder.Options);
    }
}
