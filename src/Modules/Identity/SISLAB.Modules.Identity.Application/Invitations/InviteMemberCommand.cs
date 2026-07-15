using FluentValidation;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.Modules.Identity.Contracts.Invitations;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Domain.Invitations;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Identity.Application.Invitations;

/// <summary>
/// Invites someone to the active company by e-mail, choosing the Lumen profile they will receive on accept
/// (card [E12] #75c). The invitation carries a <b>profile</b>, not a role — SISLAB models no roles; authority
/// is a company-scoped Lumen profile granted on accept (same path as card #104).
///
/// <para><b>Tenant isolation (invariant):</b> the target company is the active one, resolved from
/// <c>ITenantContext</c> and passed in <see cref="CompanyId"/> — never from the request body. An invitation to
/// one company therefore never grants access to another; the accept flow assigns the profile scoped to exactly
/// this <see cref="CompanyId"/>.</para>
///
/// <para><b>Idempotency:</b> a resend to an e-mail that already has a pending invitation for this company
/// rehydrates that invitation (<see cref="CompanyInvitation.Reissue"/>) instead of creating a second one, so no
/// duplicate pending invitation can exist for a (company, e-mail) — also guarded by a partial unique index.</para>
/// </summary>
/// <param name="CompanyId">Active company (invitation scope), from <c>ITenantContext</c>.</param>
/// <param name="Email">Invitee e-mail; normalized by the aggregate.</param>
/// <param name="ProfileId">Lumen profile to grant on accept; must exist.</param>
/// <param name="InvitedByUserId">Coordinator issuing the invitation, from the authenticated principal.</param>
public sealed record InviteMemberCommand(
    Guid CompanyId,
    string Email,
    Guid ProfileId,
    Guid InvitedByUserId) : ICommand<InviteMemberResult>;

/// <summary>Outcome of a successful invite: the invitation id and whether it was newly created or re-sent.</summary>
/// <param name="InvitationId">Identity of the pending invitation.</param>
/// <param name="Resent">True when an existing pending invitation was re-sent; false when a new one was created.</param>
public sealed record InviteMemberResult(Guid InvitationId, bool Resent);

internal sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.InvitedByUserId).NotEmpty();
    }
}

internal sealed class InviteMemberCommandHandler : ICommandHandler<InviteMemberCommand, InviteMemberResult>
{
    private readonly ICompanyRepository _companies;
    private readonly ICompanyInvitationRepository _invitations;
    private readonly ILumenAuthorizationGateway _authorization;
    private readonly IMemberInvitationGateway _invitationGateway;
    private readonly IClock _clock;

    public InviteMemberCommandHandler(
        ICompanyRepository companies,
        ICompanyInvitationRepository invitations,
        ILumenAuthorizationGateway authorization,
        IMemberInvitationGateway invitationGateway,
        IClock clock)
    {
        _companies = companies;
        _invitations = invitations;
        _authorization = authorization;
        _invitationGateway = invitationGateway;
        _clock = clock;
    }

    public async Task<InviteMemberResult> HandleAsync(
        InviteMemberCommand request,
        CancellationToken cancellationToken = default)
    {
        // The active company must exist — guards against an invite issued with no/stale active tenant.
        Company company = await _companies.FindByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", request.CompanyId);

        // The chosen profile must exist in Lumen; a dangling profile id would grant nothing on accept.
        ProfileDto profile = await _authorization.FindProfileAsync(request.ProfileId, cancellationToken)
            ?? throw new NotFoundException("Profile", request.ProfileId);

        // Refuse inviting someone who is already a member of this company (nothing to grant, avoids confusion).
        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await IsAlreadyMemberAsync(company, normalizedEmail, cancellationToken))
            throw new ConflictException(
                $"'{request.Email}' is already a member of this company.");

        // Idempotent resend: reuse an existing pending invitation instead of creating a duplicate.
        CompanyInvitation? pending = await _invitations.FindPendingByEmailAsync(
            company.Id, normalizedEmail, cancellationToken);

        if (pending is not null)
        {
            pending.Reissue(profile.Id, _clock);
            await _invitations.UpdateAsync(pending, cancellationToken);
            return new InviteMemberResult(pending.Id, Resent: true);
        }

        var invitation = CompanyInvitation.Issue(
            company.Id, normalizedEmail, profile.Id, request.InvitedByUserId, _clock);

        await _invitations.AddAsync(invitation, cancellationToken);
        return new InviteMemberResult(invitation.Id, Resent: false);
    }

    /// <summary>
    /// Whether the invitee's e-mail already belongs to a member of this company. Memberships store the Lumen
    /// user id (not the e-mail), so this resolves the user by e-mail via the invitation gateway: a non-registered
    /// e-mail is trivially not a member; a registered user is a member only if their id is in this company's
    /// membership set. This keeps the check tenant-scoped — being a member elsewhere does not count here.
    /// </summary>
    private async Task<bool> IsAlreadyMemberAsync(
        Company company,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        Guid? existingUserId = await _invitationGateway.FindUserIdByEmailAsync(normalizedEmail, cancellationToken);
        return existingUserId is { } userId && company.IsMember(userId);
    }
}
