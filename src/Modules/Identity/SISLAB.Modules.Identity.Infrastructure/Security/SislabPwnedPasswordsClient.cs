using System.Security.Cryptography;
using System.Text;
using Lumen.Identity.Domain.Security;
using Microsoft.Extensions.Logging;

namespace SISLAB.Modules.Identity.Infrastructure.Security;

/// <summary>
/// SISLAB implementation of <see cref="IPwnedPasswordsClient"/> (HaveIBeenPwned k-anonymity check).
///
/// WHY THIS EXISTS: Lumen.Identity 1.0.0 registers a typed HttpClient
/// <c>AddHttpClient&lt;IPwnedPasswordsClient, PwnedPasswordsClient&gt;</c> (configured with
/// BaseAddress from <c>Hibp:ApiBaseUrl</c>) and then IMMEDIATELY overrides it with
/// <c>AddScoped&lt;IPwnedPasswordsClient, PwnedPasswordsClient&gt;</c>. The last registration
/// wins: the injected HttpClient is the container default, with no BaseAddress — causing
/// <c>InvalidOperationException</c> ("An invalid request URI was provided") on every
/// register/change-password call. Because Lumen is a black-box NuGet, SISLAB registers
/// its own correctly configured typed client after AddLumenIdentity, winning the override.
/// The interface is public; Lumen's implementation is internal.
///
/// Algorithm (HIBP Pwned Passwords range API): SHA-1 of the password in uppercase hex,
/// split into prefix (5 chars) + suffix (35 chars); <c>GET range/{prefix}</c> returns lines
/// <c>SUFFIX:count</c>; the password is compromised if the suffix appears in the response.
///
/// Fail-open: network/HTTP failures do NOT block registration (returns <c>false</c>).
/// The HIBP check is defense-in-depth, not the only password strength barrier.
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
            // Fail-open: HIBP unavailability does not block registration.
            _logger.LogWarning(ex, "HaveIBeenPwned check unavailable; password not verified against breaches.");
            return false;
        }
    }

    private static (string Prefix, string Suffix) ComputeSha1RangeParts(string password)
    {
        byte[] hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(password));
        string hash = Convert.ToHexString(hashBytes); // uppercase hex, 40 chars
        return (hash[..5], hash[5..]);
    }

    private static bool ContainsSuffix(string rangeBody, string suffix)
    {
        // HIBP response: one line per suffix, format "SUFFIX:count".
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
