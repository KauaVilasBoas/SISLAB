namespace SISLAB.Modules.Identity.Contracts.Administration;

/// <summary>
/// Public flattened result of a member removal dry-run for the active company.
/// </summary>
/// <param name="UserId">Lumen user evaluated (by value).</param>
/// <param name="IsMember">Whether the user is currently a member of the active company.</param>
/// <param name="CanRemove">Whether the member is eligible for removal.</param>
public sealed record MemberRemovalEligibilityDto(Guid UserId, bool IsMember, bool CanRemove);
