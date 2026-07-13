using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.DependencyInjection;
using SISLAB.Infrastructure.Modules;
using SISLAB.Modules.Audit.Infrastructure.DependencyInjection;

namespace SISLAB.Modules.Audit.Application;

/// <summary>
/// Audit module entry point for the Composition Root (card [E9] #57). The host references this assembly for
/// auto-discovery via reflection; it never references the internal Infrastructure project directly.
/// </summary>
/// <remarks>
/// <see cref="Order"/> is 40 — after Identity (10), Inventory (20) and Notifications (30). The audit trail
/// is a cross-cutting sink that the business modules write into; registering it after them keeps the
/// "later than the domains that feed it" intent and does not collide with any existing order.
/// </remarks>
public sealed class AuditModule : IModule
{
    /// <inheritdoc />
    public int Order => 40;

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // AuditController lives in this assembly, co-located with the CQRS queries it dispatches. Registering
        // this assembly as an ApplicationPart makes its actions discoverable by MVC. AddControllers is
        // idempotent across modules.
        services
            .AddControllers()
            .AddApplicationPart(typeof(AuditModule).Assembly);

        // Read-side query handlers (listing + export) discovered by scan against IRequestHandler<,>.
        services.AddHandlersFromAssembly(typeof(AuditModule).Assembly);

        // Write-side composition: the public IAuditWriter port (Dapper) + the schema bootstrapper hosted
        // service. There is no aggregate/DbContext/UnitOfWork — the trail is append-only, so no
        // TransactionBehavior is registered for this module.
        services.AddAuditModule(configuration);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // AuditController is an attribute-routed MVC controller, discovered via AddApplicationPart and mapped
        // by the host's MapControllers. No per-endpoint wiring is needed here.
    }
}
