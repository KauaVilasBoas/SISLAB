using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Infrastructure.Multitenancy;

/// <summary>
/// Middleware de resolução de tenant (Opção A: company ativa em cookie httpOnly, FORA do JWT).
///
/// A cada requisição:
/// 1. Resolve o usuário autenticado a partir do principal (JWT) via <see cref="IUserIdAccessor"/>.
/// 2. Lê o cookie de company ativa (<see cref="ActiveCompanyCookie.Name"/>).
/// 3. VALIDA a associação usuário↔company contra <c>company_user</c> (defense-in-depth:
///    o cookie por si só não é confiável — a pertença é reconferida a cada request).
/// 4. Se válida, popula <see cref="TenantContext.CompanyId"/>.
///
/// Em rotas públicas, sem JWT, sem cookie ou com company não pertencente, o CompanyId
/// permanece vazio — o <see cref="SislabTenantScopeAccessor"/> trata isso retornando
/// escopo nulo. Este middleware NUNCA aborta a request (não é ele quem autoriza); a
/// exigência de tenant fica a cargo dos handlers/policies que dele dependem.
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

    // Serviços Scoped resolvidos por request via injeção no InvokeAsync.
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
                // Cookie aponta para uma company que o usuário não pertence (ou inativa).
                // Não populamos o tenant; o cookie será tratado como inválido.
                _logger.LogWarning(
                    "Cookie de company ativa ({CompanyId}) rejeitado para o usuário {UserId}: não é membro ativo.",
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

        // Precisa de usuário autenticado (JWT) para validar a pertença.
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
