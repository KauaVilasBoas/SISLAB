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
/// Anonymous read that previews an invitation by its raw token (card [E12] #75c): resolves the company name,
/// invitee e-mail, profile name, expiry and whether it can still be accepted, so the SPA can render the accept
/// screen before the invitee commits.
///
/// <para><b>Why a query handler (not a Dapper read):</b> the preview joins data across two stores — SISLAB's
/// <c>tenancy</c> tables (invitation + company) and Lumen's profile/identity stores (profile display name and
/// whether the e-mail already has an account). A single Dapper statement cannot span the anti-corruption
/// boundary to Lumen, so this orchestrates the invitation/company repositories and the Lumen gateways instead.
/// The lookup is by token hash — no active tenant exists at preview time, and the token itself is the secret.</para>
/// </summary>
/// <param name="Token">The raw token from the invitation e-mail link.</param>
public sealed record PreviewInvitationQuery(string Token) : IQuery<InvitationPreviewDto>;

internal sealed class PreviewInvitationQueryValidator : AbstractValidator<PreviewInvitationQuery>
{
    public PreviewInvitationQueryValidator()
        => RuleFor(query => query.Token).NotEmpty();
}

internal sealed class PreviewInvitationQueryHandler
    : IQueryHandler<PreviewInvitationQuery, InvitationPreviewDto>
{
    private readonly ICompanyInvitationRepository _invitations;
    private readonly ICompanyRepository _companies;
    private readonly ILumenAuthorizationGateway _authorization;
    private readonly IMemberInvitationGateway _invitationGateway;
    private readonly IClock _clock;

    public PreviewInvitationQueryHandler(
        ICompanyInvitationRepository invitations,
        ICompanyRepository companies,
        ILumenAuthorizationGateway authorization,
        IMemberInvitationGateway invitationGateway,
        IClock clock)
    {
        _invitations = invitations;
        _companies = companies;
        _authorization = authorization;
        _invitationGateway = invitationGateway;
        _clock = clock;
    }

    public async Task<InvitationPreviewDto> HandleAsync(
        PreviewInvitationQuery request,
        CancellationToken cancellationToken = default)
    {
        string tokenHash = InvitationToken.FromRawToken(request.Token).TokenHash;

        CompanyInvitation invitation = await _invitations.FindByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new NotFoundException("This invitation link is invalid.");

        Company company = await _companies.FindByIdAsync(invitation.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", invitation.CompanyId);

        ProfileDto? profile = await _authorization.FindProfileAsync(invitation.ProfileId, cancellationToken);
        Guid? existingUserId = await _invitationGateway.FindUserIdByEmailAsync(invitation.Email, cancellationToken);

        return new InvitationPreviewDto(
            Email: invitation.Email,
            CompanyName: company.Name,
            ProfileName: profile?.Name ?? "—",
            ExpiresAt: invitation.ExpiresAt,
            Status: invitation.Status.ToString(),
            Acceptable: invitation.IsAcceptable(_clock.UtcNow),
            RequiresAccountCreation: existingUserId is null);
    }
}
