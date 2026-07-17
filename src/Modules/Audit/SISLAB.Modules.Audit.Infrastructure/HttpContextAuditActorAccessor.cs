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
        if (user is null) return IAuditActorAccessor.SystemActor;

        // Prefer a human-readable identifier so audit records show a name the lab team recognises,
        // not an opaque UUID. Email is the most reliable cross-system identifier; name/username is
        // the fallback; sub (UUID) is the last resort for tokens that carry only the identity claim.
        string? actor =
            user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(actor) ? IAuditActorAccessor.SystemActor : actor;
    }
}
