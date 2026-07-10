using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Tenant resolution middleware (Option A: active company in an httpOnly cookie, outside the JWT).
///
/// Per request:
/// 1. Resolves the authenticated user from the JWT principal via <see cref="IUserIdAccessor"/>.
/// 2. Reads the active-company cookie (<see cref="ActiveCompanyCookie.Name"/>).
/// 3. VALIDATES membership against <c>company_memberships</c> (defense-in-depth:
///    the cookie alone is not trusted — membership is re-checked on every request).
/// 4. If valid, populates <see cref="TenantContext.CompanyId"/>.
///
/// On public routes, unauthenticated requests, missing cookie, or non-member company,
/// CompanyId remains empty — <see cref="SislabTenantScopeAccessor"/> handles that by
/// returning a null scope. This middleware NEVER aborts the request; authorization
/// enforcement is the responsibility of handlers and policies that depend on the tenant.
/// </summary>
internal sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Scoped services resolved per-request via InvokeAsync injection.
    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        IUserIdAccessor userIdAccessor,
        ICompanyRepository companyRepository)
    {
        if (TryResolveActiveCompany(context, userIdAccessor, out Guid userId, out Guid companyId))
        {
            bool isMember = await companyRepository.IsActiveMemberAsync(
                companyId, userId, context.RequestAborted);

            if (isMember)
            {
                tenantContext.SetCompany(companyId);
            }
            else
            {
                // Cookie points to a company the user does not belong to (or is inactive).
                // Leave the tenant unpopulated; the cookie is treated as invalid.
                _logger.LogWarning(
                    "Active company cookie ({CompanyId}) rejected for user {UserId}: not an active member.",
                    companyId, userId);
            }
        }

        await _next(context);
    }

    private static bool TryResolveActiveCompany(
        HttpContext context,
        IUserIdAccessor userIdAccessor,
        out Guid userId,
        out Guid companyId)
    {
        userId = Guid.Empty;
        companyId = Guid.Empty;

        // Requires an authenticated user (JWT) to validate membership.
        if (context.User?.Identity?.IsAuthenticated != true)
            return false;

        if (!userIdAccessor.TryGetUserId(context.User, out userId) || userId == Guid.Empty)
            return false;

        if (!context.Request.Cookies.TryGetValue(ActiveCompanyCookie.Name, out string? raw)
            || !Guid.TryParse(raw, out companyId)
            || companyId == Guid.Empty)
        {
            return false;
        }

        return true;
    }
}
