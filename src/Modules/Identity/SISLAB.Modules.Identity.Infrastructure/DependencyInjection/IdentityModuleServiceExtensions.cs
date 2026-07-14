using Lumen.Authorization;
using Lumen.Authorization.AspNetCore;
using Lumen.Identity.AspNetCore;
using Lumen.Identity.Migrations.PostgreSQL;
using Lumen.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.Modules.Identity.Infrastructure.Persistence;
using SISLAB.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace SISLAB.Modules.Identity.Infrastructure.DependencyInjection;

/// <summary>
/// DI composition for the Identity module.
///
/// Registration order:
/// 1. EF DbContext (SISLAB — Company/CompanyMembership, schema "tenancy")
/// 2. Domain repositories
/// 3. MVC controllers (required for Lumen's permission discovery scanner)
/// 4. Identity schema migrations hosted service
/// 5. Lumen Identity (AddLumenIdentity — AuthN, JWT Bearer, Lumen's own DbContext + repos)
/// 6. Lumen Identity PostgreSQL migrations hosted service
/// 7. Lumen Authorization (umbrella AddLumenAuthorization — v2.0.0, CatalogMode=Validate)
/// 8. SISLAB permission-catalog seeder (hosted service AFTER Lumen startup — SISLAB owns the catalog)
/// 9. Tenant context + scope accessor (overrides Lumen's no-op)
/// 10. Dev seed (opt-in, registered last so it runs after all migration hosted services)
/// </summary>
public static class IdentityModuleServiceExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SislabDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SislabDb' is not configured. " +
                "Set it in appsettings.json or User Secrets.");

        // 1. EF DbContext for the module (Company / CompanyMembership — schema "tenancy").
        //    Schema "identity" is EXCLUSIVELY Lumen Identity's (users/tokens).
        //    SISLAB multi-tenancy lives in "tenancy" to avoid two DbContexts, two migration
        //    history tables, and different casing conventions in the same schema.
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "tenancy");
                npgsql.MigrationsAssembly(
                    typeof(IdentityModuleServiceExtensions).Assembly.GetName().Name);
            }));

        // 2. Domain repositories
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        // 3. MVC controllers for the module live in the Application assembly (co-located with the
        //    CQRS queries they dispatch). Their ApplicationPart is registered by IdentityModule
        //    (Application) — this Infrastructure project must not reference Application, so it
        //    cannot register that part here without inverting the dependency graph.

        // 4. Applies SISLAB schema "tenancy" migrations at startup
        //    (mirrors the hosted-service pattern Lumen uses for its own schemas).
        services.AddHostedService<Persistence.IdentitySchemaMigrationsHostedService>();

        // 5. Lumen Identity — AuthN + JWT Bearer + internal infrastructure (Provider=PostgreSQL).
        //
        //    Lumen binds its options sections from the ROOT of IConfiguration
        //    ("Jwt", "App", "Smtp", "Hibp") with ValidateOnStart. To keep the namespace
        //    "LumenIdentity:*" (secrets and appsettings) without polluting the root, SISLAB
        //    re-bases the "LumenIdentity" section as Lumen's IConfiguration root.
        //    GetSection("Jwt") on it resolves to "LumenIdentity:Jwt", and so on.
        IConfiguration lumenConfiguration = configuration.GetSection("LumenIdentity");

        services.AddLumenIdentity(connectionString, lumenConfiguration, options =>
        {
            options.Provider = DatabaseProvider.PostgreSQL;
        });

        // 5.1 HaveIBeenPwned typed client override (Lumen.Identity 1.0.0 bug workaround).
        //     Lumen registers AddHttpClient<IPwnedPasswordsClient, PwnedPasswordsClient>
        //     (with BaseAddress from Hibp:ApiBaseUrl), then immediately OVERRIDES it with
        //     AddScoped<IPwnedPasswordsClient, PwnedPasswordsClient> — erasing the BaseAddress
        //     and causing "An invalid request URI..." 500s on register/change-password.
        //     Because Lumen is a black-box NuGet, SISLAB registers its own typed client
        //     (correctly configured) AFTER AddLumenIdentity, winning the singleton override.
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

        // 5.2 Email template renderer override (Lumen.Identity 1.0.0 bug workaround).
        //     Lumen's EmailTemplateRenderer resolves templates as embedded resources in the
        //     Lumen assembly itself — those resources do not exist in the published package.
        //     Any email-sending flow throws InvalidOperationException("Email template '...' was not
        //     found as an embedded resource.") → HTTP 500. SISLAB provides its own renderer with
        //     product-branded templates, registered AFTER AddLumenIdentity to win the override.
        services.AddScoped<Lumen.Identity.Domain.Notifications.IEmailTemplateRenderer,
            Notifications.SislabEmailTemplateRenderer>();

        // 5.3 Dev email service — no-op/log when no SMTP is configured.
        //     Lumen registers MailKitEmailService (real SMTP via LumenIdentity:Smtp).
        //     Without an SMTP server in development, delivery would fail. When
        //     "LumenIdentity:Smtp:Host" is blank, SISLAB overrides with a logging-only service.
        //     In production (Host set) the Lumen MailKitEmailService is preserved.
        bool smtpConfigured = !string.IsNullOrWhiteSpace(
            lumenConfiguration.GetSection("Smtp")["Host"]);
        if (!smtpConfigured)
        {
            services.AddScoped<Lumen.Identity.Domain.Notifications.IEmailService,
                Notifications.SislabLoggingEmailService>();
        }

        // 6. Lumen Identity — PostgreSQL migrations hosted service
        services.AddLumenIdentityPostgresMigrations();

        // 7. Lumen Authorization — v2.0.0 umbrella (AddLumenAuthorization on the AspNetCore package).
        //    v2 inverted catalog ownership: the umbrella is now provider-aware and the unified
        //    LumenAuthorizationStartupService applies the PostgreSQL "Lumen"-schema migrations itself
        //    (assembly selected by Provider=PostgreSQL) — no separate AddLumenAuthorizationPostgresMigrations()
        //    and no granular AddLumenAuthorizationEnforcement()/Discovery() calls anymore.
        //
        //    CatalogMode defaults to Validate: on boot Lumen scans every [RequirePermission] code and only
        //    LOGS A WARNING for codes missing from "Lumen"."Permission" — it never writes the catalog. SISLAB
        //    is the owner and seeds the catalog itself (step 8). FailFastOnMissingPermission stays false so a
        //    seed gap degrades to a warning rather than aborting boot; flip it on in staging to catch drift.
        //    ApplyMigrationsOnStartup=true so the startup service creates/updates the "Lumen" schema.
        //    Explicitly qualified to the AspNetCore umbrella: both it and the core package expose an
        //    AddLumenAuthorization(IServiceCollection, string, Action<...>) with the same signature, so an
        //    unqualified call is ambiguous. The AspNetCore one is the umbrella (core + enforcement + startup).
        LumenAuthorizationAspNetCoreServiceCollectionExtensions.AddLumenAuthorization(
            services,
            connectionString,
            options =>
            {
                options.Provider = DatabaseProvider.PostgreSQL;
                options.ApplyMigrationsOnStartup = true;
                // CatalogMode = Validate (default) — SISLAB owns the catalog via LumenPermissionCatalogSeeder.
            });

        // 8. SISLAB permission-catalog seeder — hosted service registered IMMEDIATELY AFTER the Lumen umbrella.
        //    Hosted services run sequentially in registration order, so this executes once
        //    LumenAuthorizationStartupService has applied the "Lumen"-schema migrations (the "Permission" /
        //    "PermissionGroup" tables exist). Since v2 no longer syncs the catalog, SISLAB inserts every group
        //    and every [RequirePermission] code with idempotent raw SQL (INSERT ... ON CONFLICT DO NOTHING),
        //    including the pt-BR DisplayName. This is a boot-time seeder rather than an EF migration on
        //    IdentityDbContext because those two DbContexts own different schemas/histories and IdentityDbContext's
        //    migration hosted service (step 4) runs BEFORE Lumen creates its schema — a migration there would hit
        //    42P01. Failure is swallowed so a seed hiccup never blocks boot (Validate would only warn anyway).
        services.AddHostedService<Authorization.LumenPermissionCatalogSeeder>();

        // 9. SISLAB tenant context (Scoped — one instance per request).
        //     The concrete TenantContext is the REQUEST tenant source: TenantResolutionMiddleware resolves
        //     it directly and populates it from the httpOnly cookie. Consumers depend on the abstraction
        //     ITenantContext, which is composed as OverridableTenantContext — a Decorator that reports the
        //     background ITenantContextOverride when a job has set one, and otherwise falls back to this
        //     request context. On the HTTP path no override is ever set, so the effective context is
        //     behaviourally identical to the raw request TenantContext: the seam is invisible to requests
        //     and adds only the cross-tenant scan capability the E6 jobs need (#41/#42/#66). The override
        //     itself is registered by AddSislabInfrastructure (shared by request and job scopes).
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => new OverridableTenantContext(
            sp.GetRequiredService<TenantContext>(),
            sp.GetRequiredService<ITenantContextOverride>()));

        // 10. Tenant bridge: overrides Lumen's no-op ITenantScopeAccessor with SISLAB's impl.
        //     Lumen registers its no-op via TryAdd; this AddScoped wins as the last registration.
        services.AddScoped<Lumen.Authorization.Contracts.ITenantScopeAccessor, SislabTenantScopeAccessor>();

        // 10.1 Authorization gateway (card [E12] #101): the single anti-corruption adapter that dispatches
        //      Lumen's authorization use cases through its MediatR pipeline and maps the results to SISLAB
        //      Contracts DTOs. Registered AFTER AddLumenAuthorization so MediatR's IMediator (which the
        //      gateway depends on) is available. Every profile-management handler depends on this port,
        //      never on Lumen/MediatR directly — keeping Lumen confined to Identity's Infrastructure (§8).
        services.AddScoped<Contracts.Authorization.ILumenAuthorizationGateway,
            Authorization.LumenAuthorizationGateway>();

        // 11. Lumen.Modularity in-process event bus.
        //     Lumen Identity's CQRS handlers publish integration events via IEventBus;
        //     without this registration the container cannot build those handlers.
        //     SISLAB does not yet consume Lumen events — registered with no handler assemblies.
        services.AddEventBus();

        // 12. Dev seed (LAFTE company + admin user), behind the "Seed:Enabled" flag.
        //     Registered LAST so DevSeedHostedService runs after all migration hosted services
        //     (SISLAB + Lumen), ensuring schemas and Lumen's system seed (Administrator/User
        //     profiles) already exist when the SISLAB seed executes.
        AddDevSeed(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers options, the seeder, and the dev seed hosted service for LAFTE.
    /// The seeder only runs when <c>Seed:Enabled=true</c> (opt-in per environment).
    /// </summary>
    private static void AddDevSeed(IServiceCollection services, IConfiguration configuration)
    {
        Seeding.DevSeedOptions seedOptions = new();
        configuration.GetSection(Seeding.DevSeedOptions.SectionName).Bind(seedOptions);

        services.AddSingleton(seedOptions);
        services.AddScoped<Seeding.LafteDevSeeder>();
        services.AddHostedService<Seeding.DevSeedHostedService>();
    }
}
