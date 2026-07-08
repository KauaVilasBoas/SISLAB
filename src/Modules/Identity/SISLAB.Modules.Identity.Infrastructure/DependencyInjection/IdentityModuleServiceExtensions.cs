using Lumen.Authorization;
using Lumen.Authorization.AspNetCore;
using Lumen.Authorization.Migrations.PostgreSQL;
using Lumen.Identity.AspNetCore;
using Lumen.Identity.Migrations.PostgreSQL;
using Lumen.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.Modules.Identity.Infrastructure.Persistence;
using SISLAB.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace SISLAB.Modules.Identity.Infrastructure.DependencyInjection;

/// <summary>
/// Composição do DI do módulo Identity.
///
/// Ordem de registro:
/// 1. DbContext EF (SISLAB — Company/CompanyMembership)
/// 2. Repositórios do domínio
/// 3. Lumen Identity (AddLumenIdentity — umbrella que registra:
///    AuthN, JWT Bearer, DbContext interno da Lumen, repositórios internos,
///    hosted service de token cleanup)
/// 4. Lumen Identity Postgres migrations hosted service
/// 5. Lumen Authorization (AddLumenAuthorization do AspNetCore — registra:
///    core do serviço de permissões, DbContext interno da Lumen Authz)
/// 6. Lumen Authorization discovery + enforcement (separados para composição granular)
/// 7. Lumen Authorization Postgres migrations hosted service
/// 8. SislabTenantScopeAccessor — sobrepõe o NoOp da Lumen para injetar tenant ativo
/// </summary>
public static class IdentityModuleServiceExtensions
{
    /// <summary>
    /// Registra todos os serviços do módulo Identity no container de DI.
    /// Deve ser chamado pelo <see cref="Application.IdentityModule.RegisterServices"/>.
    /// </summary>
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' não configurada. " +
                "Defina em appsettings.json ou User Secrets.");

        // 1. DbContext EF do módulo (Company / CompanyMembership — schema "tenancy").
        //    O schema "identity" é EXCLUSIVO da Lumen Identity (usuários/tokens); a
        //    multi-tenancy do SISLAB vive em "tenancy" para não colidir com dois DbContexts,
        //    dois históricos de migration e convenções de casing distintas no mesmo schema.
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "tenancy");
                npgsql.MigrationsAssembly(
                    typeof(IdentityModuleServiceExtensions).Assembly.GetName().Name);
            }));

        // 2. Repositórios do domínio SISLAB
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        // 2.1 Aplica as migrations do schema "identity" do SISLAB no boot
        //      (espelha o padrão de hosted service da Lumen para os schemas dela).
        services.AddHostedService<Persistence.IdentitySchemaMigrationsHostedService>();

        // 3. Lumen Identity — AuthN + JWT Bearer + infraestrutura interna (Provider=PostgreSQL)
        //    LumenIdentityOptions.Provider é do tipo Lumen.Authorization.DatabaseProvider.
        //
        //    A Lumen faz bind das seções de options a partir da RAIZ do IConfiguration
        //    ("Jwt", "App", "Smtp", "Hibp") com ValidateOnStart. Para preservar o namespace
        //    "LumenIdentity:*" (segredos e appsettings) sem poluir a raiz, rebaseamos a seção
        //    "LumenIdentity" como raiz de configuração da Lumen: nela, GetSection("Jwt")
        //    resolve para "LumenIdentity:Jwt", e assim por diante.
        IConfiguration lumenConfiguration = configuration.GetSection("LumenIdentity");

        services.AddLumenIdentity(connectionString, lumenConfiguration, options =>
        {
            options.Provider = DatabaseProvider.PostgreSQL;
        });

        // 3.1 Override do typed client HaveIBeenPwned (defeito no pacote Lumen.Identity 1.0.0).
        //     A Lumen registra AddHttpClient<IPwnedPasswordsClient, PwnedPasswordsClient> (com
        //     BaseAddress vindo de Hibp:ApiBaseUrl) e, em seguida, o SOBRESCREVE com
        //     AddScoped<IPwnedPasswordsClient, PwnedPasswordsClient> — anulando o BaseAddress e
        //     causando 500 ("An invalid request URI...") em register/change-password. Como a Lumen
        //     é lib externa black-box, sobrepomos com nosso typed client corretamente configurado.
        //     Ver SislabPwnedPasswordsClient. BaseAddress/UserAgent vêm de LumenIdentity:Hibp.
        IConfiguration hibpConfiguration = lumenConfiguration.GetSection("Hibp");
        string hibpBaseUrl = hibpConfiguration["ApiBaseUrl"] ?? "https://api.pwnedpasswords.com";
        string hibpUserAgent = hibpConfiguration["UserAgent"] ?? "SISLAB-Identity";

        services.AddHttpClient<Lumen.Identity.Domain.Security.IPwnedPasswordsClient, Security.SislabPwnedPasswordsClient>(
            client =>
            {
                client.BaseAddress = new Uri(hibpBaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(2);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(hibpUserAgent);
            });

        // 3.2 Override do renderer de templates de e-mail (defeito no pacote Lumen.Identity 1.0.0).
        //     O EmailTemplateRenderer da Lumen resolve os templates como recursos embedados NO
        //     assembly dela (prefixo "Lumen.Identity.Infrastructure.Notifications.Templates.Email."),
        //     que NÃO existem no pacote publicado — todo fluxo que dispara e-mail (register envia a
        //     confirmação) estoura 500 ("Email template '...' was not found..."). Decisão SISLAB:
        //     o e-mail é nosso — registramos o renderer do SISLAB com templates de marca própria,
        //     DEPOIS de AddLumenIdentity, vencendo o registro defeituoso. Ver SislabEmailTemplateRenderer.
        services.AddScoped<Lumen.Identity.Domain.Notifications.IEmailTemplateRenderer,
            Notifications.SislabEmailTemplateRenderer>();

        // 3.3 Serviço de envio de e-mail em DEV: no-op/log quando não há SMTP configurado.
        //     A Lumen registra MailKitEmailService (SMTP real via LumenIdentity:Smtp). Sem servidor
        //     SMTP em desenvolvimento, a entrega falharia. Se "LumenIdentity:Smtp:Host" não estiver
        //     definido, sobrepomos com um serviço que só loga — mantendo o fluxo (register etc.)
        //     verde. Em produção (Host preenchido) preservamos o MailKitEmailService da Lumen.
        bool smtpConfigured = !string.IsNullOrWhiteSpace(
            lumenConfiguration.GetSection("Smtp")["Host"]);
        if (!smtpConfigured)
        {
            services.AddScoped<Lumen.Identity.Domain.Notifications.IEmailService,
                Notifications.SislabLoggingEmailService>();
        }

        // 4. Lumen Identity — hosted service de migrations Postgres
        services.AddLumenIdentityPostgresMigrations();

        // 5. Lumen Authorization — SOMENTE o núcleo (namespace Lumen.Authorization, NÃO o do
        //    AspNetCore). O umbrella do AspNetCore chamaria internamente
        //    AddLumenAuthorizationMigrations() (SQL Server!) + Discovery + Enforcement, o que
        //    quebra em PostgreSQL e duplica hosted services. Compomos granular, conforme a regra
        //    do SISLAB. O RegisterCore escolhe o assembly de migrations do provider (PostgreSQL).
        //
        //    ApplyMigrationsOnStartup = true: o hosted service de migrations Postgres respeita
        //    esta flag; com false ele PULA e o schema "Lumen" nunca é criado (42P01 no discovery).
        LumenAuthorizationServiceCollectionExtensions.AddLumenAuthorization(
            services,
            connectionString,
            options =>
            {
                options.Provider = DatabaseProvider.PostgreSQL;
                options.ApplyMigrationsOnStartup = true;
            });

        // 6. Lumen Authorization — hosted service de migrations Postgres.
        //    IMPORTANTE: registrado ANTES do discovery. Hosted services executam em ordem
        //    de registro; o discovery consulta a tabela "Lumen"."Permission", que só existe
        //    após as migrations. Inverter a ordem causa 42P01 (relação não existe) no boot.
        services.AddLumenAuthorizationPostgresMigrations();

        // 7. Enforcement ([RequirePermission] + IUserIdAccessor via ClaimsUserIdAccessor) e
        //    Discovery/reconciliation de permissões. Rodam depois das migrations.
        services.AddLumenAuthorizationEnforcement();
        services.AddLumenAuthorizationDiscovery();

        // 8. Contexto de tenant do SISLAB (Scoped, uma instância por requisição).
        //    Registramos o concreto TenantContext e expomos a MESMA instância como
        //    ITenantContext — assim o TenantResolutionMiddleware (que resolve o concreto)
        //    e os consumidores (que dependem da abstração) compartilham o mesmo estado.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // 9. Bridge tenant: sobrepõe o NoOp da Lumen com o accessor do SISLAB.
        //    A Lumen registra seu NoOp via TryAdd; nosso AddScoped posterior torna-se a última
        //    registração e vence na resolução única de ITenantScopeAccessor.
        services.AddScoped<Lumen.Authorization.Contracts.ITenantScopeAccessor, SislabTenantScopeAccessor>();

        // 10. Event bus in-process da Lumen.Modularity. Os handlers CQRS da Lumen Identity
        //     (ex.: registro de usuário) publicam integration events via IEventBus; sem este
        //     registro o container não consegue construir esses handlers.
        //     O SISLAB ainda não consome eventos da Lumen — registro sem assemblies de handler.
        services.AddEventBus();

        // 11. Seed de desenvolvimento (empresa demo LAFTE + admin), atrás da flag "Seed:Enabled".
        //     Registrado por ÚLTIMO: o DevSeedHostedService roda depois de todos os hosted services
        //     de migrations (SISLAB + Lumen), garantindo que schemas e o seed de sistema da Lumen
        //     (profiles Administrator/User) já existam quando o seed do SISLAB executar.
        AddDevSeed(services, configuration);

        return services;
    }

    /// <summary>
    /// Registra as opções, o seeder e o hosted service do seed de desenvolvimento LAFTE.
    /// O seeder só executa efetivamente quando <c>Seed:Enabled=true</c> (opt-in por ambiente).
    /// </summary>
    private static void AddDevSeed(IServiceCollection services, IConfiguration configuration)
    {
        Seeding.DevSeedOptions seedOptions = new();
        configuration.GetSection(Seeding.DevSeedOptions.SectionName).Bind(seedOptions);

        // Options como singleton concreto: consumido pelo hosted service e pelo seeder.
        services.AddSingleton(seedOptions);
        services.AddScoped<Seeding.LafteDevSeeder>();
        services.AddHostedService<Seeding.DevSeedHostedService>();
    }
}
