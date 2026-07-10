using System.Reflection;
using Lumen.Identity.Domain.Notifications;

namespace SISLAB.Modules.Identity.Infrastructure.Notifications;

/// <summary>
/// SISLAB implementation of <see cref="IEmailTemplateRenderer"/>: renders transactional email
/// bodies (account confirmation, password reset, password changed) from HTML/text templates
/// with SISLAB branding.
///
/// WHY THIS EXISTS: Lumen.Identity 1.0.0's <c>EmailTemplateRenderer</c> resolves templates as
/// embedded resources in the Lumen assembly itself
/// (prefix <c>Lumen.Identity.Infrastructure.Notifications.Templates.Email.</c>).
/// Those resources do NOT exist in the published package — any email flow (notably
/// <c>register</c>, which sends the confirmation) throws
/// <c>InvalidOperationException("Email template '...' was not found as an embedded resource.")</c>
/// and returns HTTP 500. Because Lumen is consumed as an external NuGet black box, SISLAB
/// does not patch the package: it provides its own renderer with product-branded templates
/// and registers it AFTER <c>AddLumenIdentity</c>, winning the <see cref="IEmailTemplateRenderer"/>
/// singleton override.
///
/// Contract (verified by reflection against Lumen.Identity 1.0.0):
/// - <c>Render(templateName, placeholders)</c> returns <c>(string HtmlBody, string TextBody)</c>.
///   The email subject is set by Lumen's calling service, not by the renderer.
/// - Placeholder syntax in templates: <c>{{Key}}</c> (case-sensitive).
/// - Template names used by Lumen: <c>EmailConfirmation</c> (placeholders: <c>UserName</c>,
///   <c>ConfirmationUrl</c>), <c>PasswordReset</c> (<c>UserName</c>, <c>ResetUrl</c>),
///   <c>PasswordChanged</c> (<c>UserName</c>).
///
/// Resolution convention: each template is a pair of embedded resources in this assembly at
/// <c>Notifications/Templates/{templateName}.html</c> and <c>.txt</c>.
/// The <c>.txt</c> is optional (falls back to an empty string).
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
                    $"SISLAB email template '{fileName}' not found as an embedded resource " +
                    $"('{resourceName}'). Make sure the file is marked as <EmbeddedResource> in the .csproj.");
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
