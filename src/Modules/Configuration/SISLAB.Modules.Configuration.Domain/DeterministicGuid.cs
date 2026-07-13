using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SISLAB.Modules.Configuration.Domain;

/// <summary>
/// Derives a stable <see cref="Guid"/> from text, in a way that is <b>reproducible byte-for-byte in
/// PostgreSQL</b>. Used to give the per-tenant configuration defaults (categories, units, expiry policy)
/// deterministic ids so the C# seeder and the SQL data backfill agree on the exact same id for the same
/// input — the property that makes both idempotent and the enum→category migration reversible.
/// </summary>
/// <remarks>
/// The id is <c>md5(namespace || value)</c> interpreted as a UUID in <b>canonical (big-endian) text order</b>
/// — i.e. the 32 hex characters of the MD5 laid out as <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>. This is
/// exactly what PostgreSQL produces with <c>md5(namespace || value)::uuid</c>, so the SQL backfill can
/// recompute the same ids without shipping a lookup table. The mixed-endian <see cref="Guid(byte[])"/>
/// constructor is deliberately avoided (it would reorder the first three fields and diverge from Postgres).
/// MD5 here is a non-cryptographic id derivation, not a security primitive.
/// </remarks>
public static class DeterministicGuid
{
    /// <summary>
    /// Returns the canonical-order UUID derived from <c>md5(namespacePrefix + value)</c> — equal to the
    /// PostgreSQL expression <c>md5(namespacePrefix || value)::uuid</c>.
    /// </summary>
    public static Guid From(string namespacePrefix, string value)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(namespacePrefix + value));
        string hex = Convert.ToHexString(hash).ToLowerInvariant();

        string canonical =
            $"{hex[..8]}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20, 12)}";

        return Guid.ParseExact(canonical, "D");
    }

    /// <summary>The lowercase canonical UUID text of <paramref name="companyId"/>, matching PostgreSQL <c>company_id::text</c>.</summary>
    public static string CompanyToken(Guid companyId)
        => companyId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
}
