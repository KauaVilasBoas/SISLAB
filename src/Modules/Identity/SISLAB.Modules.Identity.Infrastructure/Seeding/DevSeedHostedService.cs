using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Hosted service que executa o <see cref="LafteDevSeeder"/> no boot, atrás da flag
/// <c>Seed:Enabled</c>. Registrado APÓS os hosted services de migrations (SISLAB + Lumen)
/// para que todos os schemas/seed de sistema já existam quando o seed rodar
/// (hosted services executam em ordem de registro).
///
/// Falhas do seed são logadas mas NÃO derrubam a aplicação — é uma conveniência de dev,
/// nunca um pré-requisito de disponibilidade.
/// </summary>
internal sealed class DevSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DevSeedOptions _options;
    private readonly ILogger<DevSeedHostedService> _logger;

    public DevSeedHostedService(
        IServiceScopeFactory scopeFactory,
        DevSeedOptions options,
        ILogger<DevSeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            LafteDevSeeder seeder = scope.ServiceProvider.GetRequiredService<LafteDevSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao executar o seed de desenvolvimento LAFTE. Boot prossegue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
