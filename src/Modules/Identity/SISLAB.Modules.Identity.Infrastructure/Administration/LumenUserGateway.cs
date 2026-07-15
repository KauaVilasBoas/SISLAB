using Lumen.Identity.Application.Users.GetDetail;
using Lumen.SharedKernel.Exceptions;
using MediatR;
using SISLAB.Modules.Identity.Contracts.Administration;

namespace SISLAB.Modules.Identity.Infrastructure.Administration;

/// <summary>
/// Lumen-backed implementation of <see cref="ILumenUserGateway"/> (card [E7] #105): the single adapter that
/// dispatches Lumen's <see cref="GetUserDetailQuery"/> through its MediatR pipeline and translates the result
/// into the SISLAB <see cref="MemberEnrichmentDto"/>.
///
/// <para>One Lumen call yields both the identity (username/e-mail) and the user's assigned profiles, so a
/// company member is enriched without a second round-trip. Profiles come back as Lumen resolves them for the
/// user; SISLAB does not filter them by company scope here — Lumen 1.0.0's authorization is effectively global,
/// so the profiles a member holds are the profiles shown. Company scoping of the <i>assignment</i> action still
/// lives in the assign/remove use cases.</para>
///
/// <para>The injected <see cref="IMediator"/> is <b>MediatR's</b> dispatcher (registered by
/// <c>AddLumenIdentity</c>), deliberately distinct from SISLAB's own
/// <see cref="SISLAB.SharedKernel.Messaging.IMediator"/>. Confining MediatR to this Infrastructure adapter is
/// what keeps the module's controllers and SISLAB handlers MediatR-free (§8).</para>
/// </summary>
internal sealed class LumenUserGateway : ILumenUserGateway
{
    private readonly IMediator _lumenMediator;

    public LumenUserGateway(IMediator lumenMediator) => _lumenMediator = lumenMediator;

    public async Task<MemberEnrichmentDto?> EnrichMemberAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        GetUserDetailResult user;
        try
        {
            user = await _lumenMediator.Send(new GetUserDetailQuery(userId), cancellationToken);
        }
        catch (NotFoundException)
        {
            // A membership can outlive its Lumen account (deleted user). Treat as un-enrichable rather than
            // failing the whole listing; the query handler skips it.
            return null;
        }

        var profiles = user.Profiles
            .Select(profile => new MemberProfileSummary(profile.ProfileId, profile.Name, profile.IsSystem))
            .ToList();

        return new MemberEnrichmentDto(user.Id, user.Username, user.Email, profiles);
    }
}
