using System.Security.Claims;
using Lumen.Authorization.Contracts;
using Microsoft.AspNetCore.Http;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Experiments;

/// <summary>
/// Resolves the authenticated user's <b>Lumen user id</b> (a <see cref="Guid"/>) for the current request, so the
/// write-side handlers can enforce the responsibility-based edit authorization (card [E11]) and default a new
/// experiment's lead responsible to its creator.
///
/// <para>Distinct from <see cref="SISLAB.Modules.Audit.Contracts.IAuditActorAccessor"/>, which yields a
/// human-readable claim string ("who") for the audit trail: this yields the stable identity id the
/// responsibility model is keyed on.</para>
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// The authenticated user's Lumen id, or throws <see cref="ForbiddenException"/> (HTTP 403) when it cannot be
    /// resolved — the write path must know "who" to authorize.
    /// </summary>
    Guid RequireUserId();
}

/// <summary>
/// Resolves the current user id from the HTTP principal via Lumen's <see cref="IUserIdAccessor"/> — the same seam
/// the Agenda controller uses to stamp the responsible on a calendar entry. Lives in the Application layer (which
/// already hosts the module's controllers and references ASP.NET/Lumen), so the module's Infrastructure need not
/// reference Application.
/// </summary>
internal sealed class HttpContextCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserIdAccessor _userIdAccessor;

    public HttpContextCurrentUserContext(
        IHttpContextAccessor httpContextAccessor,
        IUserIdAccessor userIdAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _userIdAccessor = userIdAccessor;
    }

    public Guid RequireUserId()
    {
        ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;

        if (principal is null
            || !_userIdAccessor.TryGetUserId(principal, out Guid userId)
            || userId == Guid.Empty)
        {
            throw new ForbiddenException(
                "The current user could not be resolved from the request principal.");
        }

        return userId;
    }
}
