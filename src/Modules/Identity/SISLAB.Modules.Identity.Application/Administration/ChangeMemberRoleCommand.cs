using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Authorization;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>
/// Changes the business <see cref="Role"/> of a member of the active company (card [E12] #77e).
///
/// <para>The company is the active tenant resolved from <c>ITenantContext</c> (httpOnly cookie), never
/// from the client. The target user comes from the route. The command drives the <see cref="Company"/>
/// aggregate — which enforces the "≥1 active Coordinator" invariant and raises
/// <c>MemberRoleChangedEvent</c> — and then reconciles the member's Lumen authorization profile
/// (<see cref="IMemberAuthorizationProfileService"/>, card #77d) so <c>[RequirePermission]</c> reflects
/// the new role immediately, scoped to this company.</para>
/// </summary>
/// <param name="CompanyId">Active company resolved from <c>ITenantContext</c>, never from the client.</param>
/// <param name="UserId">Lumen user (member) whose role is being changed.</param>
/// <param name="Role">The role to assign to the member.</param>
public sealed record ChangeMemberRoleCommand(Guid CompanyId, Guid UserId, Role Role) : ICommand;

internal sealed class ChangeMemberRoleCommandHandler : ICommandHandler<ChangeMemberRoleCommand>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IMemberAuthorizationProfileService _authorizationProfileService;

    public ChangeMemberRoleCommandHandler(
        ICompanyRepository companyRepository,
        IMemberAuthorizationProfileService authorizationProfileService)
    {
        _companyRepository = companyRepository;
        _authorizationProfileService = authorizationProfileService;
    }

    public async Task<Unit> HandleAsync(
        ChangeMemberRoleCommand request, CancellationToken cancellationToken = default)
    {
        Company company = await _companyRepository.FindByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", request.CompanyId);

        // Enforces the ≥1-Coordinator invariant and raises MemberRoleChangedEvent on a real change.
        // A no-op change (same role) neither mutates nor raises — the reconciliation below stays idempotent.
        company.AssignMemberRole(request.UserId, request.Role);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        // Translate the new role into the company-scoped Lumen profile assignment (card #77d), so the
        // member's effective permissions reflect the change as soon as this command returns.
        await _authorizationProfileService.ReconcileAsync(
            request.UserId, request.CompanyId, request.Role, cancellationToken);

        return Unit.Value;
    }
}
