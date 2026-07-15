namespace SISLAB.Modules.Identity.Contracts.Administration;

/// <summary>
/// Public flattened DTO for a company member enriched with the human-readable identity (username/e-mail) and
/// the authorization profiles assigned to them (card [E7] #105), so the "Members" tab can render each row with
/// a name, an e-mail and profile chips without the SPA fanning out N extra calls.
///
/// <para>Combines SISLAB's tenancy link (<see cref="MembershipId"/>/<see cref="UserId"/>, guarded by
/// <c>company_id</c>) with data owned by Lumen (identity + profile assignments), resolved server-side through
/// the <see cref="ILumenUserGateway"/> anti-corruption seam. It exposes only the Lumen user by value — never a
/// cross-store FK or any internal aggregate.</para>
/// </summary>
/// <param name="MembershipId">Identifier of the membership link (CompanyMembership).</param>
/// <param name="UserId">Identifier of the Lumen user (by value).</param>
/// <param name="Username">The member's Lumen username.</param>
/// <param name="Email">The member's Lumen e-mail.</param>
/// <param name="AssignedProfiles">The authorization profiles the member holds; never null, possibly empty.</param>
public sealed record EnrichedMemberDto(
    Guid MembershipId,
    Guid UserId,
    string Username,
    string Email,
    IReadOnlyList<MemberProfileSummary> AssignedProfiles);

/// <summary>A profile assigned to a member, as shown in the profile chips of the "Members" tab.</summary>
/// <param name="ProfileId">The Lumen profile id.</param>
/// <param name="ProfileName">The profile display name.</param>
/// <param name="IsSystem">True for Lumen's built-in profiles (rendered with a "Sistema" badge).</param>
public sealed record MemberProfileSummary(Guid ProfileId, string ProfileName, bool IsSystem);
