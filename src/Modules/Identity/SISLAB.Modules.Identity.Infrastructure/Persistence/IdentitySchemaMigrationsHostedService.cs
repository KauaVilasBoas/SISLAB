using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Hosted service que aplica as migrations do <see cref="IdentityDbContext"/> do SISLAB
/// (tabelas <c>companies</c> e <c>company_memberships</c> no schema <c>tenancy</c>) no startup.
///
/// Espelha o padrão dos hosted services de migrations da Lumen (Identity/Authorization):
/// cada DbContext aplica seu próprio schema no boot. As tabelas da Lumen continuam
/// sendo migradas pelos hosted services da própria Lumen — este cobre apenas o
/// bounded context de identidade do SISLAB.
///
/// Resolve um escopo próprio porque o <see cref="IdentityDbContext"/> é registrado como Scoped.
/// </summary>
internal sealed class IdentitySchemaMigrationsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdentitySchemaMigrationsHostedService> _logger;

    public IdentitySchemaMigrationsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<IdentitySchemaMigrationsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aplicando migrations do SISLAB Identity (schema 'identity')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Migrations do SISLAB Identity aplicadas.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
