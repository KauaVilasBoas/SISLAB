using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SISLAB.Modules.Audit.Contracts;

namespace SISLAB.Modules.Audit.Infrastructure;

/// <summary>
/// Resolves the audit actor from the current HTTP principal (card [E9] #57): the JWT <c>sub</c> claim,
/// surfaced by ASP.NET as <see cref="ClaimTypes.NameIdentifier"/>. Outside a request (background jobs, the
/// Outbox dispatcher) there is no <see cref="HttpContext"/>, so it falls back to
/// <see cref="IAuditActorAccessor.SystemActor"/>.
/// </summary>
internal sealed class HttpContextAuditActorAccessor : IAuditActorAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuditActorAccessor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public string GetCurrentActor()
    {
        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;

        string? actor = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(actor) ? IAuditActorAccessor.SystemActor : actor;
    }
}
