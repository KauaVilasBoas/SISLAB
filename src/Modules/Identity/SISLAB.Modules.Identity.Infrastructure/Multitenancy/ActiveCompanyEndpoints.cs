using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SISLAB.Modules.Identity.Contracts.ActiveCompany;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// SISLAB endpoints for active company selection and switching (post-login).
///
/// Authentication is entirely Lumen's responsibility (MapLumenIdentityEndpoints).
/// This class only contains SISLAB logic: resolving the user's companies via
/// <c>company_memberships</c> and writing the active company into an httpOnly cookie.
/// Company switching does NOT require re-login — it is the same activation endpoint.
/// </summary>
public static class ActiveCompanyEndpoints
{
    public static IEndpointRouteBuilder MapActiveCompanyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/companies")
            .WithTags("Companies")
            .RequireAuthorization();

        // Lists the authenticated user's companies (via company_memberships).
        // The SPA uses this for: 1 company → auto-select; N → show picker.
        group.MapGet("/mine", GetMyCompaniesAsync)
            .Produces<IReadOnlyList<CompanyMembershipDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Selects or switches the active company. Validates membership; 403 if not a member.
        // Writes the httpOnly active-company cookie. Used for both first selection and switching.
        group.MapPost("/{companyId:guid}/activate", ActivateCompanyAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Returns the active company as resolved by TenantResolutionMiddleware for the current
        // request (from the httpOnly cookie, re-validated against company_memberships).
        // 404 when no valid tenant is resolved (no cookie or cookie rejected).
        group.MapGet("/active", GetActiveCompanyAsync)
            .Produces<ActiveCompanyDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult GetActiveCompanyAsync(ITenantContext tenantContext)
    {
        if (tenantContext.CompanyId == Guid.Empty)
        {
            return Results.Problem(
                title: "No active company",
                detail: "Select an active company via POST /api/companies/{companyId}/activate.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(new ActiveCompanyDto(tenantContext.CompanyId));
    }

    private static async Task<IResult> GetMyCompaniesAsync(
        HttpContext context,
        IUserIdAccessor userIdAccessor,
        ICompanyRepository companyRepository,
        CancellationToken ct)
    {
        if (!userIdAccessor.TryGetUserId(context.User, out Guid userId) || userId == Guid.Empty)
            return Results.Unauthorized();

        IReadOnlyList<Company> companies = await companyRepository.ListForMemberAsync(userId, ct);

        var payload = companies
            .Select(c => new CompanyMembershipDto(c.Id, c.Name))
            .ToList();

        return Results.Ok(payload);
    }

    private static async Task<IResult> ActivateCompanyAsync(
        Guid companyId,
        HttpContext context,
        IUserIdAccessor userIdAccessor,
        ICompanyRepository companyRepository,
        CancellationToken ct)
    {
        if (!userIdAccessor.TryGetUserId(context.User, out Guid userId) || userId == Guid.Empty)
            return Results.Unauthorized();

        bool isMember = await companyRepository.IsActiveMemberAsync(companyId, userId, ct);
        if (!isMember)
        {
            return Results.Problem(
                title: "Company not allowed",
                detail: "The user is not an active member of the requested company.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        CookieOptions options = ActiveCompanyCookie.BuildOptions(isSecure: context.Request.IsHttps);
        context.Response.Cookies.Append(ActiveCompanyCookie.Name, companyId.ToString(), options);

        return Results.NoContent();
    }
}
