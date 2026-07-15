using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.AspNetCore;

/// <summary>
/// Base class for every SISLAB MVC controller.
///
/// Lives in shared Infrastructure (not the Host) on purpose: module controllers are
/// hosted inside each module's Application project, co-located with their CQRS handlers.
/// The Host cannot be referenced by a module, so a Host-based base class would be
/// unreachable. Shared Infrastructure is referenced by every module and already carries
/// Microsoft.AspNetCore.App, making it the single reachable home for the base type.
///
/// Controllers dispatch via IMediator and NEVER touch repositories, DbContext or Dapper
/// directly. Tenant helpers below read the active company from the request-scoped
/// <see cref="ITenantContext"/>, resolved lazily to keep derived constructors clean.
/// </summary>
[ApiController]
public abstract class SislabControllerBase : ControllerBase
{
    private ITenantContext? _tenantContext;

    private ITenantContext TenantContext =>
        _tenantContext ??= HttpContext.RequestServices.GetRequiredService<ITenantContext>();

    /// <summary>
    /// Active company (tenant) for the current request. Returns <see cref="Guid.Empty"/> when no
    /// active company was resolved; downstream query/command handlers translate a non-existent
    /// company into a <c>NotFoundException</c>, surfaced by the exception-handling middleware.
    /// </summary>
    protected Guid GetCompanyId() => TenantContext.CompanyId;
}
