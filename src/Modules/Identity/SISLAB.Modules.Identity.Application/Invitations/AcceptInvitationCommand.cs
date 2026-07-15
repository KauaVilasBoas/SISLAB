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
/// Accepts a member invitation by its raw token (card [E12] #75c): the invitee joins the invited company with
/// the invited profile. Anonymous — the caller has no session yet; the token <b>is</b> the credential, verified
/// by hash against the stored invitation.
///
/// <para><b>Fork 1 (link vs. create):</b> if the invitation's e-mail already belongs to a Lumen user, that
/// account is reused — no password is required (the request password is ignored). Only a brand-new invitee
/// (no existing user) supplies a username/password to create an account, mirroring signup. Either way the user
/// then gets a <see cref="CompanyMembership"/> in the invited company plus the invited profile scoped to it,
/// honouring the N:N "one user, many companies" model (§7).</para>
///
/// <para><b>Idempotency:</b> a second accept of the same (already accepted) invitation is a no-op success — it
/// re-resolves the joined user and returns, never adding a duplicate membership or re-granting the profile.
/// Membership addition is itself guarded by <see cref="Company.IsMember"/>. Expiry is lazy: accepting past the
/// window flips the invitation to <c>Expired</c> and is rejected (HTTP 422).</para>
///
/// <para><b>Cross-store orchestration:</b> like signup, the handler coordinates Lumen's identity/authorization
/// stores and SISLAB's tenancy store, which share no transaction. It provisions the user (Lumen) first, then
/// adds the membership (SISLAB, committed by the module's <c>TransactionBehavior</c>), then grants the profile
/// (Lumen). The profile grant is idempotent, so a re-run after a partial failure is safe.</para>
/// </summary>
/// <param name="Token">The raw accept token from the invitation e-mail link.</param>
/// <param name="Username">Display/user name for a NEW invitee account; ignored when the account already exists.</param>
/// <param name="Password">Password for a NEW invitee account; ignored when the account already exists.</param>
public sealed record AcceptInvitationCommand(
    string Token,
    string? Username,
    string? Password) : ICommand<AcceptInvitationResult>;

/// <summary>Outcome of a successful accept: what the invitee joined and whether a new account was created.</summary>
/// <param name="CompanyId">Company the invitee joined.</param>
/// <param name="UserId">Lumen user id the membership/profile were granted to.</param>
/// <param name="AccountCreated">True when a new Lumen account was created; false when an existing one was linked.</param>
public sealed record AcceptInvitationResult(Guid CompanyId, Guid UserId, bool AccountCreated);

internal sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(command => command.Token).NotEmpty();
        // Username/Password are only required when creating a new account — that path is validated in the
        // handler once we know whether the e-mail already has a Lumen user (we cannot know it here).
    }
}

internal sealed class AcceptInvitationCommandHandler
    : ICommandHandler<AcceptInvitationCommand, AcceptInvitationResult>
{
    private readonly ICompanyInvitationRepository _invitations;
    private readonly ICompanyRepository _companies;
    private readonly IMemberInvitationGateway _invitationGateway;
    private readonly ILumenAuthorizationGateway _authorization;
    private readonly IClock _clock;

    public AcceptInvitationCommandHandler(
        ICompanyInvitationRepository invitations,
        ICompanyRepository companies,
        IMemberInvitationGateway invitationGateway,
        ILumenAuthorizationGateway authorization,
        IClock clock)
    {
        _invitations = invitations;
        _companies = companies;
        _invitationGateway = invitationGateway;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<AcceptInvitationResult> HandleAsync(
        AcceptInvitationCommand request,
        CancellationToken cancellationToken = default)
    {
        CompanyInvitation invitation = await ResolveByTokenAsync(request.Token, cancellationToken);

        // Idempotent double-accept: an already-accepted invitation resolves to the same joined user without
        // adding a duplicate membership or re-granting the profile.
        if (invitation.Status == InvitationStatus.Accepted)
            return await BuildIdempotentResultAsync(invitation, cancellationToken);

        // 1. Resolve the invitee's Lumen account (Fork 1: link existing, else create new).
        (Guid userId, bool accountCreated) = await ResolveInviteeUserAsync(invitation, request, cancellationToken);

        // 2. Transition the invitation (guards pending + not-expired; lazy-expires past the window).
        invitation.Accept(_clock);
        await _invitations.UpdateAsync(invitation, cancellationToken);

        // 3. Add the membership in the invited company (idempotent — never duplicates an existing member).
        Company company = await _companies.FindByIdAsync(invitation.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", invitation.CompanyId);

        if (!company.IsMember(userId))
        {
            company.AddMember(userId);
            await _companies.UpdateAsync(company, cancellationToken);
        }

        // 4. Grant the invited profile scoped to THIS company only (idempotent). Isolation: the scope is the
        //    invitation's company, so accepting never grants access to any other tenant.
        await _authorization.AssignProfileAsync(userId, invitation.ProfileId, company.Id, cancellationToken);

        return new AcceptInvitationResult(company.Id, userId, accountCreated);
    }

    private async Task<CompanyInvitation> ResolveByTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        string tokenHash = InvitationToken.FromRawToken(rawToken).TokenHash;

        return await _invitations.FindByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new NotFoundException("This invitation link is invalid.");
    }

    /// <summary>
    /// Links the existing Lumen account for the invitation's e-mail, or creates a new one when none exists
    /// (Fork 1). Creating an account requires username/password on the request; linking ignores them.
    /// </summary>
    private async Task<(Guid UserId, bool AccountCreated)> ResolveInviteeUserAsync(
        CompanyInvitation invitation,
        AcceptInvitationCommand request,
        CancellationToken cancellationToken)
    {
        Guid? existing = await _invitationGateway.FindUserIdByEmailAsync(invitation.Email, cancellationToken);
        if (existing is { } existingUserId)
            return (existingUserId, AccountCreated: false);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new BusinessException(
                "This e-mail has no account yet. A username and password are required to accept the invitation.");

        Guid newUserId = await _invitationGateway.CreateInvitedUserAsync(
            invitation.Email, request.Username, request.Password, cancellationToken);

        return (newUserId, AccountCreated: true);
    }

    /// <summary>
    /// Builds the success result for a re-accept of an already-accepted invitation, resolving the joined user by
    /// the invitation's e-mail so the response is stable across retries without mutating anything.
    /// </summary>
    private async Task<AcceptInvitationResult> BuildIdempotentResultAsync(
        CompanyInvitation invitation,
        CancellationToken cancellationToken)
    {
        Guid? userId = await _invitationGateway.FindUserIdByEmailAsync(invitation.Email, cancellationToken);
        return new AcceptInvitationResult(
            invitation.CompanyId,
            userId ?? Guid.Empty,
            AccountCreated: false);
    }
}
