using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SISLAB.Migrations;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string connectionString =
    configuration.GetConnectionString("SislabDb")
    ?? Environment.GetEnvironmentVariable("SISLAB_DB")
    ?? throw new InvalidOperationException(
        "Provide the connection string via ConnectionStrings:SislabDb (appsettings.json) or the SISLAB_DB environment variable.");

await using SislabSeedDbContext context = new(
    new DbContextOptionsBuilder<SislabSeedDbContext>()
        .UseNpgsql(connectionString, o =>
            o.MigrationsHistoryTable("__ef_migrations_history", schema: "seed"))
        .Options);

IReadOnlyList<string> pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

if (pending.Count == 0)
{
    Console.WriteLine("No pending seed migrations.");
    return;
}

Console.WriteLine($"Applying {pending.Count} seed migration(s):");
foreach (string name in pending)
    Console.WriteLine($"  {name}");

await context.Database.MigrateAsync();
Console.WriteLine("Done.");
