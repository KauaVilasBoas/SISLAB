using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.AspNetCore;

/// <summary>
/// Base class for every SISLAB MVC controller.
///
/// Lives in shared Infrastructure (not the Host) on purpose: module controllers are
/// physically hosted inside each module's Infrastructure project so Lumen's
/// PermissionDiscoveryScanner can materialize their [RequirePermission] codes. The Host
/// cannot be referenced by a module, so a Host-based base class would be unreachable.
/// Shared Infrastructure is referenced by every module and already carries
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

    /// <summary>Active company (tenant) for the current request.</summary>
    protected Guid GetCompanyId() => TenantContext.CompanyId;

    /// <summary>True when a valid active company was resolved for the current request.</summary>
    protected bool HasActiveTenant() => TenantContext.CompanyId != Guid.Empty;

    /// <summary>Standard 404 response for requests made without a resolved active company.</summary>
    protected IActionResult NoActiveCompany() =>
        Problem(
            title: "No active company",
            detail: "Select an active company via POST /api/companies/{companyId}/activate.",
            statusCode: StatusCodes.Status404NotFound);
}
