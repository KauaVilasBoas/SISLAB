using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// Lists every active (non-deleted) authorization profile (card [E12] #103) so the profile-management screen
/// can render the "Profiles" tab. A profile is a global, reusable set of permissions; scoping only happens when
/// a profile is <i>assigned</i> to a company member (see <c>MemberProfilesController</c>).
///
/// <para>Like the rest of the profile-management use cases, this query owns no Lumen/MediatR knowledge — it
/// delegates to <see cref="ILumenAuthorizationGateway"/>, the single anti-corruption seam over Lumen, and
/// projects its result as <see cref="ProfileDto"/>.</para>
/// </summary>
public sealed record ListProfilesQuery : IQuery<ListProfilesResult>;

/// <param name="Profiles">Active profiles ordered by Lumen; never null, empty when none exist.</param>
public sealed record ListProfilesResult(IReadOnlyList<ProfileDto> Profiles);

internal sealed class ListProfilesQueryHandler
    : IQueryHandler<ListProfilesQuery, ListProfilesResult>
{
    private readonly ILumenAuthorizationGateway _authorization;

    public ListProfilesQueryHandler(ILumenAuthorizationGateway authorization)
        => _authorization = authorization;

    public async Task<ListProfilesResult> HandleAsync(
        ListProfilesQuery request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProfileDto> profiles = await _authorization.ListProfilesAsync(cancellationToken);
        return new ListProfilesResult(profiles);
    }
}
