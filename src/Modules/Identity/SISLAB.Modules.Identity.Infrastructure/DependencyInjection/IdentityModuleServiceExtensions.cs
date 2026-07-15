using Lumen.Authorization;
using Lumen.Authorization.AspNetCore;
using Lumen.Identity.AspNetCore;
using Lumen.Identity.Migrations.PostgreSQL;
using Lumen.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Messaging;
using SISLAB.Infrastructure.Messaging.Behaviors;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.Modules.Identity.Contracts.Invitations;
using SISLAB.Modules.Identity.Contracts.Onboarding;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Domain.Invitations;
using SISLAB.Modules.Identity.Infrastructure.Invitations;
using SISLAB.Modules.Identity.Infrastructure.Messaging;
using SISLAB.Modules.Identity.Infrastructure.Multitenancy;
using SISLAB.Modules.Identity.Infrastructure.Notifications;
using SISLAB.Modules.Identity.Infrastructure.Onboarding;
using SISLAB.SharedKernel.Messaging;
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
/// 3. MVC controllers (co-located with their CQRS handlers in the Application assembly)
/// 4. Identity schema migrations hosted service
/// 5. Lumen Identity (AddLumenIdentity — AuthN, JWT Bearer, Lumen's own DbContext + repos)
/// 6. Lumen Identity PostgreSQL migrations hosted service
/// 7. Lumen Authorization (umbrella AddLumenAuthorization — v3.0.0; auto-migrates the "Lumen" schema)
/// 8. Tenant context + scope accessor (overrides Lumen's no-op)
/// 9. Dev seed (opt-in, registered last so it runs after all migration hosted services)
///
/// <para>The permission catalogue is NOT seeded here. Lumen.Authorization 3.0.0 never populates
/// permissions (no discovery, no catalogue sync — it only creates empty schema tables on boot). SISLAB
/// owns the permission data and seeds it out-of-band via the <c>SISLAB.Migrations</c> EF project
/// (idempotent <c>SeedLumenPermission*</c> migrations), decoupled from the app boot path.</para>
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
        services.AddScoped<ICompanyInvitationRepository, CompanyInvitationRepository>();

        // 2.1 Write-side unit of work for this module's commands (card [E12] #75a — signup; #75b — outbox).
        //      Now a full Transactional Outbox participant (mirrors the Inventory module): on signup the
        //      Company aggregate raises CompanyCreated; the real DomainEventDispatcher translates it and the
        //      OutboxWriter enqueues the flattened CompanyCreatedIntegrationEvent into tenancy.outbox_messages
        //      in the SAME transaction as the aggregate. The background OutboxDispatcher (SISLAB.Jobs) then
        //      publishes it after commit, so tenant provisioning (Configuration) is eventual and retried on
        //      failure — a provisioning fault never rolls back or blocks signup.
        //      - IOutboxDbContext points at THIS module's DbContext so the outbox write shares the txn.
        //      - OutboxWriter serializes integration events into outbox_messages.
        //      - IUnitOfWork = EfUnitOfWork<IdentityDbContext>: SaveChanges runs from TransactionBehavior.
        services.AddScoped<IOutboxDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<OutboxWriter>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork<IdentityDbContext>>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // 2.1.1 DomainEvent → IntegrationEvent translators. The DomainEventDispatcher resolves these by
        //        domain-event type during SaveChanges and enqueues the flattened public contract into the
        //        Outbox, in the aggregate's transaction. Events with no translator stay module-internal.
        services.AddScoped<IDomainEventToIntegrationEventTranslator<Domain.Companies.Events.CompanyCreated>,
            Messaging.CompanyCreatedEventTranslator>();

        // 2.1.2 Member-invitation translator (card #75c): MemberInvited → MemberInvitedIntegrationEvent, enqueued
        //        in the tenancy Outbox in the invite transaction. The e-mail handler below consumes it after commit.
        services.AddScoped<IDomainEventToIntegrationEventTranslator<Domain.Invitations.Events.MemberInvited>,
            Messaging.MemberInvitedEventTranslator>();

        // 2.1.3 Invitation e-mail consumer (card #75c): reacts to the published MemberInvitedIntegrationEvent to
        //        render and send the branded MemberInvitation e-mail. Eventual + retried via the Outbox, so a
        //        mail failure never rolls back or blocks the invitation. Registered against the closed
        //        IIntegrationEventHandler<T> so the InMemoryEventBus resolves it by event type.
        services.AddScoped<SISLAB.SharedKernel.Messaging.IIntegrationEventHandler<MemberInvitedIntegrationEvent>,
            SendInvitationEmailOnMemberInvitedHandler>();

        // 2.3 Member-invitation gateway (card #75c): the anti-corruption adapter that resolves or provisions the
        //      invitee's Lumen account when an invitation is accepted (Fork 1: link existing, else create). The
        //      accept handler depends on this port, never on Lumen directly (Lumen stays in Infrastructure, §8).
        services.AddScoped<IMemberInvitationGateway, LumenMemberInvitationGateway>();

        // 2.2 Company onboarding gateway (card #75a): the anti-corruption adapter that provisions the
        //      coordinator user (Lumen Identity) and grants company-scoped coordinator access (Lumen
        //      Authorization) for self-service signup. The signup handler depends on this port, never on
        //      Lumen directly, keeping Lumen confined to Infrastructure (§8).
        services.AddScoped<ICompanyOnboardingGateway, LumenCompanyOnboardingGateway>();

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

        // 7. Lumen Authorization — v3.0.0 umbrella (AddLumenAuthorization on the AspNetCore package).
        //    The umbrella is provider-aware: with Provider=PostgreSQL it selects the PostgreSQL migrations
        //    assembly and its LumenAuthorizationMigrationsHostedService creates/updates the "Lumen" schema on
        //    boot (empty "Permission"/"PermissionGroup" tables). v3.0.0 dropped all catalogue machinery
        //    (no CatalogMode, no discovery scanner, no FailFastOnMissingPermission) — Lumen never seeds
        //    permissions. SISLAB owns the permission data and seeds it out-of-band via the SISLAB.Migrations
        //    EF project (SeedLumenPermission* migrations), so nothing is seeded on this boot path.
        //    ApplyMigrationsOnStartup=true keeps the schema self-provisioning.
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
            });

        // 8. SISLAB tenant context (Scoped — one instance per request).
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

        // 9. Tenant bridge: overrides Lumen's no-op ITenantScopeAccessor with SISLAB's impl.
        //     Lumen registers its no-op via TryAdd; this AddScoped wins as the last registration.
        services.AddScoped<Lumen.Authorization.Contracts.ITenantScopeAccessor, SislabTenantScopeAccessor>();

        // 9.1 Authorization gateway (card [E12] #101): the single anti-corruption adapter that dispatches
        //      Lumen's authorization use cases through its MediatR pipeline and maps the results to SISLAB
        //      Contracts DTOs. Registered AFTER AddLumenAuthorization so MediatR's IMediator (which the
        //      gateway depends on) is available. Every profile-management handler depends on this port,
        //      never on Lumen/MediatR directly — keeping Lumen confined to Identity's Infrastructure (§8).
        services.AddScoped<Contracts.Authorization.ILumenAuthorizationGateway,
            Authorization.LumenAuthorizationGateway>();

        // 10. Lumen.Modularity in-process event bus.
        //     Lumen Identity's CQRS handlers publish integration events via IEventBus;
        //     without this registration the container cannot build those handlers.
        //     SISLAB does not yet consume Lumen events — registered with no handler assemblies.
        services.AddEventBus();

        // 11. Dev seed (LAFTE company + admin user), behind the "Seed:Enabled" flag.
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
