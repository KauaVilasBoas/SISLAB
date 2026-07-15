using System.Security.Cryptography;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Identity.Domain.Invitations;

/// <summary>
/// Value object for a member-invitation secret (card [E12] #75c). It pairs the raw, single-use token that
/// travels in the invitation e-mail with the deterministic SHA-256 hash that is the only thing persisted.
///
/// <para><b>Why hash at rest:</b> the token is a bearer credential — whoever presents it accepts the
/// invitation without authenticating. Storing only its hash means a database leak never yields a usable
/// token (the raw value is unrecoverable), and lookups on accept compare hashes, never the secret. This
/// mirrors how password-reset/confirmation tokens are handled.</para>
///
/// <para>Immutable with structural equality (two tokens are equal iff their hashes match), so it composes
/// cleanly into the <see cref="CompanyInvitation"/> aggregate and is trivially testable.</para>
/// </summary>
public sealed class InvitationToken : ValueObject
{
    /// <summary>Bytes of entropy in a freshly generated token — 256 bits, well beyond guessing range.</summary>
    private const int TokenByteLength = 32;

    private InvitationToken(string tokenHash) => TokenHash = tokenHash;

    /// <summary>The SHA-256 hash (lowercase hex) of the raw token — the only value persisted.</summary>
    public string TokenHash { get; }

    /// <summary>
    /// Generates a brand-new invitation secret: a cryptographically random raw token (URL-safe base64) and
    /// its hash. The raw value is returned once here so it can be placed in the outgoing e-mail; it is never
    /// stored — only the returned <see cref="InvitationToken"/> (the hash) is kept on the aggregate.
    /// </summary>
    /// <returns>The raw token to send by e-mail, paired with the hash-bearing value object to persist.</returns>
    public static (string RawToken, InvitationToken Token) Generate()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        string rawToken = ToUrlSafeBase64(bytes);

        return (rawToken, FromRawToken(rawToken));
    }

    /// <summary>
    /// Reconstructs the value object from a raw token by hashing it — used on accept to compare a presented
    /// token against the persisted hash without ever storing the raw value.
    /// </summary>
    public static InvitationToken FromRawToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        return new InvitationToken(Hash(rawToken));
    }

    /// <summary>Rehydrates the value object from a stored hash (EF materialization / repository).</summary>
    public static InvitationToken FromHash(string tokenHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new InvitationToken(tokenHash);
    }

    /// <summary>Whether the given raw token hashes to this token — the accept-time secret comparison.</summary>
    public bool Matches(string rawToken) =>
        !string.IsNullOrWhiteSpace(rawToken)
        && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(TokenHash),
            System.Text.Encoding.ASCII.GetBytes(Hash(rawToken)));

    private static string Hash(string rawToken)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToUrlSafeBase64(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TokenHash;
    }
}
