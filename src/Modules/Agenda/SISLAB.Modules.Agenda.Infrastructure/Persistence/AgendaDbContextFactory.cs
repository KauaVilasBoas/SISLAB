using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence;

/// <summary>Design-time factory for <see cref="AgendaDbContext"/> — EF CLI migration generation without a running host.</summary>
internal sealed class AgendaDbContextFactory : IDesignTimeDbContextFactory<AgendaDbContext>
{
    public AgendaDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<AgendaDbContext> optionsBuilder =
            new DbContextOptionsBuilder<AgendaDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=sislab_design;Username=postgres;Password=postgres",
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "agenda");
                        npgsql.MigrationsAssembly(
                            typeof(AgendaDbContextFactory).Assembly.GetName().Name);
                    });

        return new AgendaDbContext(optionsBuilder.Options);
    }
}
