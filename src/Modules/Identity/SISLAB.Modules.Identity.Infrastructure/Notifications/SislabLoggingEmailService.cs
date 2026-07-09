using Lumen.Identity.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Notifications;

/// <summary>
/// SISLAB implementation of <see cref="IEmailService"/> that does NOT send email — it only logs.
/// Used in environments without an SMTP server configured (development).
///
/// WHY THIS EXISTS: Lumen.Identity 1.0.0's <c>MailKitEmailService</c> opens a real SMTP
/// connection using <c>LumenIdentity:Smtp</c>. Without an SMTP server in dev, any email-sending
/// flow (register, forgot-password, reset-password, change-password) would fail on delivery.
/// SISLAB registers this no-op service AFTER <c>AddLumenIdentity</c> when SMTP is not configured,
/// overriding Lumen's registration. In production (with <c>LumenIdentity:Smtp:Host</c> set),
/// Lumen's <c>MailKitEmailService</c> is preserved.
///
/// The email body is still rendered by <see cref="SislabEmailTemplateRenderer"/>; this class
/// only short-circuits delivery.
/// </summary>
internal sealed class SislabLoggingEmailService : IEmailService
{
    private readonly ILogger<SislabLoggingEmailService> _logger;

    public SislabLoggingEmailService(ILogger<SislabLoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation(
            "[SMTP DISABLED] Transactional email NOT sent (dev). To: {To} | Subject: {Subject}",
            message.To,
            message.Subject);

        _logger.LogDebug("Email text body for {To}:\n{TextBody}", message.To, message.TextBody);

        return Task.CompletedTask;
    }
}
