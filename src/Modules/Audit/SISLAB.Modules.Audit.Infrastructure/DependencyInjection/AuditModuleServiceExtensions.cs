using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Audit.Infrastructure;
using SISLAB.Modules.Audit.Infrastructure.Persistence;

namespace SISLAB.Modules.Audit.Infrastructure.DependencyInjection;

/// <summary>
/// DI composition for the Audit module (card [E9] #57).
///
/// The module is intentionally minimal — an append-only trail, not an aggregate:
/// <list type="number">
///   <item>The public <see cref="IAuditWriter"/> port, implemented by the Dapper writer.</item>
///   <item>The schema bootstrapper hosted service that applies the <c>audit</c> DDL at boot.</item>
/// </list>
/// The read-side query handlers live in the Application project and are registered there via
/// <c>AddHandlersFromAssembly</c>, resolving <see cref="SISLAB.Infrastructure.Data.DbConnectionFactory"/>
/// from shared infrastructure.
/// </summary>
public static class AuditModuleServiceExtensions
{
    public static IServiceCollection AddAuditModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Public write port — Dapper INSERT, its own connection, idempotent (ON CONFLICT DO NOTHING).
        services.AddScoped<IAuditWriter, AuditWriter>();

        // Resolves the audit actor ("who") from the current HTTP principal (JWT sub), falling back to
        // "system" for background work. AddHttpContextAccessor is idempotent.
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditActorAccessor, HttpContextAuditActorAccessor>();

        // Applies the 'audit' schema + table at startup (mirrors the other modules' schema-at-boot pattern).
        services.AddHostedService<AuditSchemaBootstrapper>();

        return services;
    }
}
