using Lumen.Identity.Domain.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SISLAB.Modules.Identity.Contracts.Events;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Infrastructure.Notifications;

/// <summary>
/// Reacts to the module's own <see cref="MemberInvitedIntegrationEvent"/> to send the invitation e-mail (card
/// [E12] #75c). It is the single consumer of that fact — same bounded context, so it stays in Infrastructure.
///
/// <para><b>Eventual + retried (Outbox).</b> The event arrives from the <c>tenancy</c> Outbox via
/// <see cref="IEventBus"/>, published by the background Outbox dispatcher AFTER the invite transaction has
/// committed. Because delivery runs off the invite transaction, a mail failure never rolls back (or blocks) the
/// invitation: it simply propagates so the Outbox dispatcher leaves the message unprocessed and retries it on
/// the next tick (at-least-once). Sending the same invitation e-mail twice is harmless.</para>
///
/// <para>It renders the branded <c>MemberInvitation</c> template (the same renderer the auth e-mails use) and
/// sends it via Lumen's <see cref="IEmailService"/> (real SMTP in production; a logging no-op in dev). The
/// accept link embeds the raw token from the event; the company name is resolved from the tenancy store.</para>
/// </summary>
internal sealed class SendInvitationEmailOnMemberInvitedHandler
    : IIntegrationEventHandler<MemberInvitedIntegrationEvent>
{
    private const string TemplateName = "MemberInvitation";
    private const string EmailSubject = "Você foi convidado para o SISLAB";
    private const string DefaultBaseUrl = "http://localhost:5000";

    private readonly ICompanyRepository _companies;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendInvitationEmailOnMemberInvitedHandler> _logger;

    public SendInvitationEmailOnMemberInvitedHandler(
        ICompanyRepository companies,
        IEmailTemplateRenderer renderer,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<SendInvitationEmailOnMemberInvitedHandler> logger)
    {
        _companies = companies;
        _renderer = renderer;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(
        MemberInvitedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        Company? company = await _companies.FindByIdAsync(integrationEvent.CompanyId, cancellationToken);
        string companyName = company?.Name ?? "sua empresa";

        string acceptUrl = BuildAcceptUrl(integrationEvent.RawToken);

        var placeholders = new Dictionary<string, string>
        {
            ["CompanyName"] = companyName,
            ["AcceptUrl"] = acceptUrl,
        };

        (string htmlBody, string textBody) = _renderer.Render(TemplateName, placeholders);

        _logger.LogInformation(
            "Sending member-invitation e-mail to {Email} for company {CompanyId} ('{CompanyName}').",
            integrationEvent.Email, integrationEvent.CompanyId, companyName);

        // Let a delivery failure propagate: the Outbox dispatcher must NOT mark the message processed, so it is
        // retried on the next tick. Re-sending the invitation e-mail is idempotent from the recipient's view.
        await _emailService.SendAsync(
            new EmailMessage(integrationEvent.Email, EmailSubject, htmlBody, textBody),
            cancellationToken);
    }

    /// <summary>
    /// Builds the accept link the invitee follows. The SPA owns the <c>/invitations/accept</c> route; the raw
    /// token travels in the query string. Base URL comes from configuration (<c>LumenIdentity:App:BaseUrl</c>,
    /// the same key the auth e-mails use), never hardcoded.
    /// </summary>
    private string BuildAcceptUrl(string rawToken)
    {
        string? baseUrl = _configuration["LumenIdentity:App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = DefaultBaseUrl;

        return $"{baseUrl.TrimEnd('/')}/invitations/accept?token={Uri.EscapeDataString(rawToken)}";
    }
}
