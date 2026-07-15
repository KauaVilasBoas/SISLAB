namespace SISLAB.Modules.Identity.Contracts.Administration;

/// <summary>
/// Anti-corruption seam (Adapter/Facade) over Lumen's user read model (card [E7] #105): resolves a Lumen
/// user's identity (username/e-mail) and the profiles assigned to them, so SISLAB can enrich the flat
/// membership rows of the active company into <see cref="EnrichedMemberDto"/>.
///
/// <para>Mirrors <c>ILumenAuthorizationGateway</c>: the single place that dispatches Lumen's user query and
/// translates its result into SISLAB <see cref="Administration">Contracts</see> DTOs. The enrich query handler
/// depends on this abstraction, never on Lumen or MediatR directly, so Lumen/MediatR stay confined to the
/// Identity module's Infrastructure (§8) and the handler stays unit-testable against a fake gateway.</para>
/// </summary>
public interface ILumenUserGateway
{
    /// <summary>
    /// Resolves the identity and assigned profiles of the given Lumen user, or <see langword="null"/> when no
    /// such user exists (e.g. a membership pointing at a deleted account).
    /// </summary>
    Task<MemberEnrichmentDto?> EnrichMemberAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The Lumen-owned facet of a member: identity plus assigned profiles, as translated by
/// <see cref="ILumenUserGateway"/>. The tenancy facet (membership id) is joined by the SISLAB query handler.
/// </summary>
/// <param name="UserId">The Lumen user id.</param>
/// <param name="Username">The user's username.</param>
/// <param name="Email">The user's e-mail.</param>
/// <param name="AssignedProfiles">The profiles assigned to the user; never null, possibly empty.</param>
public sealed record MemberEnrichmentDto(
    Guid UserId,
    string Username,
    string Email,
    IReadOnlyList<MemberProfileSummary> AssignedProfiles);
