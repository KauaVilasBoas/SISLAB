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

        return services;
    }
}
