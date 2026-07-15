using FluentValidation;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// Removes a company-scoped profile assignment from a member of the active company
/// (<c>UserProfile.ScopeId = companyId</c>, card [E12] #104). Only the assignment made in this tenant is
/// affected; the same profile assigned to the user in another company is untouched.
///
/// <para><b>Tenant isolation (invariant):</b> mirrors <see cref="AssignProfileToMemberCommand"/> — the target
/// must be a member of the active company (resolved from <c>ITenantContext</c>, never the body), so an operator
/// can neither reach nor probe assignments of users in another tenant. Removal is idempotent (a no-op when the
/// scoped assignment does not exist).</para>
/// </summary>
/// <param name="CompanyId">Active company (the assignment scope), from <c>ITenantContext</c>.</param>
/// <param name="UserId">The Lumen user whose assignment is removed; must be a member of the active company.</param>
/// <param name="ProfileId">The profile to unassign.</param>
public sealed record RemoveProfileFromMemberCommand(Guid CompanyId, Guid UserId, Guid ProfileId) : ICommand;

internal sealed class RemoveProfileFromMemberCommandValidator : AbstractValidator<RemoveProfileFromMemberCommand>
{
    public RemoveProfileFromMemberCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ProfileId).NotEmpty();
    }
}

internal sealed class RemoveProfileFromMemberCommandHandler : ICommandHandler<RemoveProfileFromMemberCommand>
{
    private readonly ICompanyRepository _companies;
    private readonly ILumenAuthorizationGateway _authorization;

    public RemoveProfileFromMemberCommandHandler(
        ICompanyRepository companies,
        ILumenAuthorizationGateway authorization)
    {
        _companies = companies;
        _authorization = authorization;
    }

    public async Task<Unit> HandleAsync(
        RemoveProfileFromMemberCommand request,
        CancellationToken cancellationToken = default)
    {
        Company company = await _companies.FindByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", request.CompanyId);

        if (!company.IsMember(request.UserId))
            throw new ForbiddenException(
                $"User '{request.UserId}' is not a member of the active company; its profile assignments in it cannot be managed.");

        await _authorization.RemoveProfileAsync(
            request.UserId, request.ProfileId, request.CompanyId, cancellationToken);

        return Unit.Value;
    }
}
