using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SISLAB.Modules.Identity.Contracts.ActiveCompany;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Endpoints do SISLAB para seleção e troca da company ativa (pós-login).
///
/// A AuthN é 100% da Lumen (MapLumenIdentityEndpoints). Aqui vive apenas a lógica do SISLAB:
/// resolver as companies do usuário via <c>company_user</c> e materializar a company ativa
/// num cookie httpOnly. A troca de company NÃO exige novo login — é o mesmo endpoint de seleção.
/// </summary>
public static class ActiveCompanyEndpoints
{
    public static IEndpointRouteBuilder MapActiveCompanyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/companies")
            .WithTags("Companies")
            .RequireAuthorization();

        // Lista as companies do usuário autenticado (via company_user).
        // O SPA usa isto para: 1 company → seleção automática; N → tela de escolha.
        group.MapGet("/mine", GetMyCompaniesAsync)
            .Produces<IReadOnlyList<CompanyMembershipDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Seleciona/troca a company ativa. Valida a pertença; 403 se o usuário não é membro.
        // Grava o cookie httpOnly de company ativa. Serve tanto para a 1ª seleção quanto para troca.
        group.MapPost("/{companyId:guid}/activate", ActivateCompanyAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Retorna a company ativa resolvida pelo TenantResolutionMiddleware para a requisição
        // atual (a partir do cookie httpOnly, revalidado contra company_user). O SPA usa para
        // saber qual empresa está ativa; 404 quando não há tenant resolvido (sem cookie válido).
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
            // Autenticado, porém sem company ativa válida (sem cookie ou cookie rejeitado).
            return Results.Problem(
                title: "Nenhuma company ativa",
                detail: "Selecione uma empresa ativa via POST /api/companies/{companyId}/activate.",
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
            // Defense-in-depth: rejeita ativar company que o usuário não pertence (ou inativa).
            return Results.Problem(
                title: "Company não permitida",
                detail: "O usuário não é membro ativo da empresa informada.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        CookieOptions options = ActiveCompanyCookie.BuildOptions(isSecure: context.Request.IsHttps);
        context.Response.Cookies.Append(ActiveCompanyCookie.Name, companyId.ToString(), options);

        return Results.NoContent();
    }
}
