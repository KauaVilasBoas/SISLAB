using System.Reflection;
using Lumen.Identity.Domain.Notifications;

namespace SISLAB.Modules.Identity.Infrastructure.Notifications;

/// <summary>
/// Implementação SISLAB de <see cref="IEmailTemplateRenderer"/>: renderiza o corpo dos
/// e-mails transacionais (confirmação de conta, redefinição de senha, aviso de senha
/// alterada) a partir de templates HTML/texto com a marca SISLAB.
///
/// MOTIVO DO OVERRIDE: o renderer do pacote Lumen.Identity 1.0.0
/// (<c>EmailTemplateRenderer</c>) resolve os templates como <b>recursos embedados no
/// assembly da própria Lumen</b> (prefixo
/// <c>Lumen.Identity.Infrastructure.Notifications.Templates.Email.</c>). Esses recursos NÃO
/// existem no pacote publicado, então todo fluxo que dispara e-mail (notadamente o
/// <c>register</c>, que envia a confirmação) lança
/// <c>InvalidOperationException("Email template '...' was not found as an embedded
/// resource.")</c> e retorna HTTP 500. Como a Lumen é consumida como pacote externo
/// black-box, não corrigimos o pacote: fornecemos o renderer do SISLAB — os templates são
/// nossos, com a identidade visual do produto — e o registramos DEPOIS de
/// <c>AddLumenIdentity</c>, vencendo o registro defeituoso na resolução única de
/// <see cref="IEmailTemplateRenderer"/>.
///
/// Contrato (confirmado por reflexão no assembly da Lumen 1.0.0):
/// <list type="bullet">
///   <item><c>Render(templateName, placeholders)</c> retorna a tupla
///   <c>(string HtmlBody, string TextBody)</c> — o <b>assunto</b> é definido pelo serviço
///   chamador da Lumen, não pelo renderer.</item>
///   <item>Placeholders no template usam a sintaxe <c>{{Chave}}</c> (case-sensitive), a
///   mesma do renderer original.</item>
///   <item>Nomes de template usados pela Lumen: <c>EmailConfirmation</c> (placeholders
///   <c>UserName</c>, <c>ConfirmationUrl</c>), <c>PasswordReset</c> (<c>UserName</c>,
///   <c>ResetUrl</c>) e <c>PasswordChanged</c> (<c>UserName</c>).</item>
/// </list>
///
/// Convenção de resolução: cada template é um par de recursos embedados neste assembly em
/// <c>Notifications/Templates/{templateName}.html</c> e <c>.txt</c>. O <c>.txt</c> é
/// opcional (fallback para string vazia).
/// </summary>
internal sealed class SislabEmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string ResourceNamespace =
        "SISLAB.Modules.Identity.Infrastructure.Notifications.Templates";

    private static readonly Assembly TemplateAssembly =
        typeof(SislabEmailTemplateRenderer).Assembly;

    public (string HtmlBody, string TextBody) Render(
        string templateName,
        IReadOnlyDictionary<string, string> placeholders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(placeholders);

        string html = ApplyPlaceholders(
            LoadTemplate($"{templateName}.html", required: true)!,
            placeholders);

        string? textTemplate = LoadTemplate($"{templateName}.txt", required: false);
        string text = textTemplate is null
            ? string.Empty
            : ApplyPlaceholders(textTemplate, placeholders);

        return (html, text);
    }

    private static string? LoadTemplate(string fileName, bool required)
    {
        string resourceName = $"{ResourceNamespace}.{fileName}";
        using Stream? stream = TemplateAssembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            if (required)
            {
                throw new InvalidOperationException(
                    $"Template de e-mail SISLAB '{fileName}' não encontrado como recurso " +
                    $"embedado ('{resourceName}'). Verifique se o arquivo está marcado como " +
                    "<EmbeddedResource> no .csproj.");
            }

            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ApplyPlaceholders(
        string template,
        IReadOnlyDictionary<string, string> placeholders)
    {
        string result = template;
        foreach (KeyValuePair<string, string> placeholder in placeholders)
            result = result.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value, StringComparison.Ordinal);

        return result;
    }
}
