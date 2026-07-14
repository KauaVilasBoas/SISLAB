using FluentValidation;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Authorization;

/// <summary>
/// Assigns a Lumen profile to a member of the active company, scoped to that company
/// (<c>UserProfile.ScopeId = companyId</c>, card [E12] #104). The assignment grants the profile's permissions
/// to the user <b>inside this tenant only</b> — the same user may have different profiles in another company.
///
/// <para><b>Tenant isolation (invariant):</b> the profile can only be assigned to an actual member of the
/// active company. The handler loads the company from the <see cref="CompanyId"/> resolved from
/// <c>ITenantContext</c> (never the request body) and refuses a target user who is not a member — so an
/// operator cannot grant profiles to users of another tenant. The assignment itself is delegated to Lumen's
/// scope-aware command and is idempotent (re-assigning is a no-op).</para>
/// </summary>
/// <param name="CompanyId">Active company (the assignment scope), from <c>ITenantContext</c>.</param>
/// <param name="UserId">The Lumen user to receive the profile; must be a member of the active company.</param>
/// <param name="ProfileId">The profile to assign.</param>
public sealed record AssignProfileToMemberCommand(Guid CompanyId, Guid UserId, Guid ProfileId) : ICommand;

internal sealed class AssignProfileToMemberCommandValidator : AbstractValidator<AssignProfileToMemberCommand>
{
    public AssignProfileToMemberCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.ProfileId).NotEmpty();
    }
}

internal sealed class AssignProfileToMemberCommandHandler : ICommandHandler<AssignProfileToMemberCommand>
{
    private readonly ICompanyRepository _companies;
    private readonly ILumenAuthorizationGateway _authorization;

    public AssignProfileToMemberCommandHandler(
        ICompanyRepository companies,
        ILumenAuthorizationGateway authorization)
    {
        _companies = companies;
        _authorization = authorization;
    }

    public async Task<Unit> HandleAsync(
        AssignProfileToMemberCommand request,
        CancellationToken cancellationToken = default)
    {
        Company company = await _companies.FindByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", request.CompanyId);

        if (!company.IsMember(request.UserId))
            throw new ForbiddenException(
                $"User '{request.UserId}' is not a member of the active company and cannot be assigned a profile in it.");

        await _authorization.AssignProfileAsync(
            request.UserId, request.ProfileId, request.CompanyId, cancellationToken);

        return Unit.Value;
    }
}
