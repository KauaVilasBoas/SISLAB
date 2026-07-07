using Lumen.Authorization;
using Lumen.Authorization.AspNetCore;
using Lumen.Authorization.Migrations.PostgreSQL;
using Lumen.Identity.AspNetCore;
using Lumen.Identity.Migrations.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;
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

        // 1. DbContext EF do módulo (Company / CompanyMembership — schema "identity")
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "identity");
                npgsql.MigrationsAssembly(
                    typeof(IdentityModuleServiceExtensions).Assembly.GetName().Name);
            }));

        // 2. Repositórios do domínio SISLAB
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        // 3. Lumen Identity — AuthN + JWT Bearer + infraestrutura interna (Provider=PostgreSQL)
        //    LumenIdentityOptions.Provider é do tipo Lumen.Authorization.DatabaseProvider.
        services.AddLumenIdentity(connectionString, configuration, options =>
        {
            options.Provider = DatabaseProvider.PostgreSQL;
        });

        // 4. Lumen Identity — hosted service de migrations Postgres
        services.AddLumenIdentityPostgresMigrations();

        // 5. Lumen Authorization core (registra DbContext interno da Lumen Authz + serviços)
        //    Qualificação estática necessária: dois assemblies expõem AddLumenAuthorization
        //    com a mesma assinatura (Lumen.Authorization e Lumen.Authorization.AspNetCore).
        //    Usamos o do AspNetCore para ter suporte ao IHttpContextAccessor no enforcement.
        //    ApplyMigrationsOnStartup=false: migrations aplicadas via hosted service separado.
        LumenAuthorizationAspNetCoreServiceCollectionExtensions.AddLumenAuthorization(
            services,
            connectionString,
            options =>
            {
                options.Provider = DatabaseProvider.PostgreSQL;
                options.ApplyMigrationsOnStartup = false;
            });

        // 6. Discovery (escaneia endpoints p/ sincronizar permissões) + Enforcement ([RequirePermission])
        services.AddLumenAuthorizationDiscovery();
        services.AddLumenAuthorizationEnforcement();

        // 7. Lumen Authorization — hosted service de migrations Postgres
        services.AddLumenAuthorizationPostgresMigrations();

        // 8. Bridge tenant: sobrepõe o NoOp da Lumen com o accessor do SISLAB.
        //    TryAdd é usado internamente pela Lumen, então registramos com Replace para garantir override.
        services.AddScoped<Lumen.Authorization.Contracts.ITenantScopeAccessor, SislabTenantScopeAccessor>();

        return services;
    }
}
