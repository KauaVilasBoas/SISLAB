using Lumen.Identity.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Notifications;

/// <summary>
/// Implementação SISLAB de <see cref="IEmailService"/> que NÃO envia e-mail: apenas registra
/// a mensagem no log. Usada em ambientes sem SMTP configurado (desenvolvimento).
///
/// MOTIVO: o serviço de e-mail do pacote Lumen.Identity 1.0.0 (<c>MailKitEmailService</c>)
/// abre conexão SMTP real usando <c>LumenIdentity:Smtp</c>. Em desenvolvimento não há servidor
/// SMTP disponível; sem um substituto, todo fluxo que dispara e-mail (register, forgot/reset,
/// change-password) falharia na entrega. Registramos este serviço no-op DEPOIS de
/// <c>AddLumenIdentity</c> apenas quando o SMTP não está configurado — vencendo o registro da
/// Lumen na resolução única de <see cref="IEmailService"/>. Em produção (com
/// <c>LumenIdentity:Smtp:Host</c> definido) mantemos o <c>MailKitEmailService</c> da Lumen.
///
/// O corpo do e-mail continua sendo gerado pelo renderer SISLAB
/// (<see cref="SislabEmailTemplateRenderer"/>); aqui apenas curto-circuitamos a entrega.
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
            "[SMTP DESABILITADO] E-mail transacional NÃO enviado (dev). Destinatário: {To} | Assunto: {Subject}",
            message.To,
            message.Subject);

        _logger.LogDebug("Corpo (texto) do e-mail para {To}:\n{TextBody}", message.To, message.TextBody);

        return Task.CompletedTask;
    }
}
