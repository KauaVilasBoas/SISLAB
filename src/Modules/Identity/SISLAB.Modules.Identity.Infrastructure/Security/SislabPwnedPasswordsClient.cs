using System.Security.Cryptography;
using System.Text;
using Lumen.Identity.Domain.Security;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Implementação SISLAB de <see cref="IPwnedPasswordsClient"/> (verificação de senha vazada
/// via HaveIBeenPwned, com k-anonymity).
///
/// MOTIVO DO OVERRIDE: o pacote Lumen.Identity 1.0.0 registra o typed client
/// <c>AddHttpClient&lt;IPwnedPasswordsClient, PwnedPasswordsClient&gt;</c> (que configura o
/// <c>BaseAddress</c> a partir de <c>Hibp:ApiBaseUrl</c>) e, LOGO EM SEGUIDA, sobrepõe esse
/// registro com <c>AddScoped&lt;IPwnedPasswordsClient, PwnedPasswordsClient&gt;</c>. O último
/// registro vence: o <c>HttpClient</c> injetado passa a ser o default do container, SEM
/// <c>BaseAddress</c> — o que lança <c>InvalidOperationException</c> ("An invalid request URI
/// was provided") em todo <c>register</c>/<c>change-password</c>. Como a Lumen é consumida como
/// pacote externo black-box, não corrigimos o pacote: fornecemos uma implementação pública
/// própria (a interface é pública; a impl da Lumen é internal) registrada como typed client
/// corretamente configurado, sobrepondo o registro defeituoso.
///
/// Algoritmo (idêntico ao HIBP Pwned Passwords range API): SHA-1 da senha em HEX maiúsculo,
/// dividido em prefixo (5 chars) + sufixo (35 chars); <c>GET range/{prefix}</c> retorna linhas
/// <c>SUFIXO:contagem</c>; a senha está comprometida se o sufixo constar na resposta.
///
/// Fail-open: falhas de rede/HTTP NÃO bloqueiam o cadastro (retorna <c>false</c>); a verificação
/// de vazamento é defesa-em-profundidade, não a única barreira de força de senha.
/// </summary>
internal sealed class SislabPwnedPasswordsClient : IPwnedPasswordsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SislabPwnedPasswordsClient> _logger;

    public SislabPwnedPasswordsClient(HttpClient httpClient, ILogger<SislabPwnedPasswordsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsPwnedAsync(string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        (string prefix, string suffix) = ComputeSha1RangeParts(password);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync($"range/{prefix}", ct);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync(ct);
            return ContainsSuffix(body, suffix);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-open: indisponibilidade do HIBP não impede o cadastro.
            _logger.LogWarning(ex, "Verificação HaveIBeenPwned indisponível; senha não checada contra vazamentos.");
            return false;
        }
    }

    private static (string Prefix, string Suffix) ComputeSha1RangeParts(string password)
    {
        byte[] hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(password));
        string hash = Convert.ToHexString(hashBytes); // HEX maiúsculo, 40 chars
        return (hash[..5], hash[5..]);
    }

    private static bool ContainsSuffix(string rangeBody, string suffix)
    {
        // Resposta HIBP: uma linha por sufixo, no formato "SUFIXO:contagem".
        foreach (ReadOnlySpan<char> line in rangeBody.AsSpan().EnumerateLines())
        {
            int separator = line.IndexOf(':');
            ReadOnlySpan<char> lineSuffix = separator >= 0 ? line[..separator] : line;

            if (lineSuffix.Trim().Equals(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
