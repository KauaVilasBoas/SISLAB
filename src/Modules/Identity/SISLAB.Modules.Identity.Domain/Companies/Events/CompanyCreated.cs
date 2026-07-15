using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Companies.Events;

/// <summary>
/// Raised when a new company (tenant) is created through self-service signup
/// (<see cref="Company.Register"/>, card [E12] #75a).
///
/// <para>This is the domain fact that a brand-new tenant now exists. Downstream, the tenant-provisioning
/// use case (card #75b — default profiles, units, categories, expiry policy) reacts to it to seed the
/// company's baseline configuration. Keeping that reaction on the event (rather than inline in the signup
/// handler) decouples "a company was created" from "how a company is provisioned": the two evolve
/// independently and the signup transaction stays focused on identity/tenancy.</para>
///
/// <para>The event is internal to the domain and rich; a public, flattened <c>IntegrationEvent</c> would be
/// translated from it before crossing a module boundary (§6). Today no cross-module consumer exists yet, so
/// the aggregate simply raises it and the module's unit of work drains it on save.</para>
/// </summary>
/// <param name="CompanyId">Identity of the newly created company.</param>
/// <param name="Name">The company's display name.</param>
/// <param name="CoordinatorUserId">
/// Lumen user id of the initial coordinator created alongside the company, referenced by value.
/// </param>
public sealed record CompanyCreated(Guid CompanyId, string Name, Guid CoordinatorUserId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
