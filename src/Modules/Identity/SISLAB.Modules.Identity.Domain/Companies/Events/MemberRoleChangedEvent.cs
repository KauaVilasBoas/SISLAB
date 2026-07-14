using SISLAB.SharedKernel.Authorization;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Companies.Events;

/// <summary>
/// Raised by the <see cref="Company"/> aggregate when a member's <see cref="Role"/> changes.
///
/// <para>Consumed by the Role→Lumen-Profile translation (card [E12] #77d): the member's Lumen
/// authorization Profile assignment, scoped to <see cref="CompanyId"/>, is reconciled to match the
/// new role. The <see cref="LumenUserId"/> is carried by value — the aggregate holds no FK to Lumen.</para>
/// </summary>
/// <param name="CompanyId">Company (authorization scope) in which the member's role changed.</param>
/// <param name="LumenUserId">Lumen user whose role changed, referenced by value.</param>
/// <param name="PreviousRole">Role the member held before the change.</param>
/// <param name="NewRole">Role the member holds after the change.</param>
public sealed record MemberRoleChangedEvent(
    Guid CompanyId,
    Guid LumenUserId,
    Role PreviousRole,
    Role NewRole) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
