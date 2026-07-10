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
/// DI composition for the Identity module.
///
/// Registration order:
/// 1. EF DbContext (SISLAB — Company/CompanyMembership, schema "tenancy")
/// 2. Domain repositories
/// 3. MVC controllers (required for Lumen's permission discovery scanner)
/// 4. Identity schema migrations hosted service
/// 5. Lumen Identity (AddLumenIdentity — AuthN, JWT Bearer, Lumen's own DbContext + repos)
/// 6. Lumen Identity PostgreSQL migrations hosted service
/// 7. Lumen Authorization core (granular, no umbrella — see note below)
/// 8. Lumen Authorization PostgreSQL migrations hosted service (BEFORE discovery)
/// 9. Lumen Authorization enforcement + discovery
/// 10. Tenant context + scope accessor (overrides Lumen's no-op)
/// 11. Dev seed (opt-in, registered last so it runs after all migration hosted services)
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

        // 3. MVC controllers for the module (Administration/*Controller).
        //    Required for Lumen's permission discovery: PermissionDiscoveryScanner iterates
        //    IActionDescriptorCollectionProvider, which only sees ControllerActionDescriptor
        //    (MVC) — Minimal API is invisible to it. Registering this assembly as an
        //    ApplicationPart makes [RequirePermission]-decorated controllers discoverable at
        //    startup and reconciled into the Administrator profile. AddControllers is idempotent.
        services
            .AddControllers()
            .AddApplicationPart(typeof(IdentityModuleServiceExtensions).Assembly);

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

        // 7. Lumen Authorization — CORE ONLY (NOT the AspNetCore umbrella).
        //    The umbrella's AddLumenAuthorization internally calls AddLumenAuthorizationMigrations()
        //    which registers SQL Server migrations unconditionally → crash on PostgreSQL.
        //    SISLAB composes granularly as documented. Provider=PostgreSQL is required so the
        //    core selects the correct migrations assembly. ApplyMigrationsOnStartup=true is
        //    required: the migrations hosted service checks this flag and skips when false,
        //    leaving the "Lumen" schema uncreated (→ 42P01 on discovery).
        LumenAuthorizationServiceCollectionExtensions.AddLumenAuthorization(
            services,
            connectionString,
            options =>
            {
                options.Provider = DatabaseProvider.PostgreSQL;
                options.ApplyMigrationsOnStartup = true;
            });

        // 8. Lumen Authorization — PostgreSQL migrations hosted service.
        //    CRITICAL: registered BEFORE discovery. Hosted services run in registration order;
        //    discovery queries the "Lumen"."Permission" table, which only exists after migrations.
        //    Reversing the order causes 42P01 ("relation does not exist") on boot.
        services.AddLumenAuthorizationPostgresMigrations();

        // 9. Enforcement ([RequirePermission] + IUserIdAccessor) and
        //    permission discovery/reconciliation. Run after migrations.
        services.AddLumenAuthorizationEnforcement();
        services.AddLumenAuthorizationDiscovery();

        // 10. SISLAB tenant context (Scoped — one instance per request).
        //     The concrete TenantContext is registered AND exposed as ITenantContext so that
        //     TenantResolutionMiddleware (resolves the concrete) and consumers (depend on the
        //     abstraction) share the same instance within the request scope.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // 11. Tenant bridge: overrides Lumen's no-op ITenantScopeAccessor with SISLAB's impl.
        //     Lumen registers its no-op via TryAdd; this AddScoped wins as the last registration.
        services.AddScoped<Lumen.Authorization.Contracts.ITenantScopeAccessor, SislabTenantScopeAccessor>();

        // 12. Lumen.Modularity in-process event bus.
        //     Lumen Identity's CQRS handlers publish integration events via IEventBus;
        //     without this registration the container cannot build those handlers.
        //     SISLAB does not yet consume Lumen events — registered with no handler assemblies.
        services.AddEventBus();

        // 13. Dev seed (LAFTE company + admin user), behind the "Seed:Enabled" flag.
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
