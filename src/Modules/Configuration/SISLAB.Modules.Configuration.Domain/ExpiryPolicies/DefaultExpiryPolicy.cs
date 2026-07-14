using System.Security.Cryptography;
using System.Text;

namespace SISLAB.Modules.Configuration.Domain.ExpiryPolicies;

/// <summary>
/// The default expiry policy a laboratory starts with (card [E12] #76): the singleton
/// <see cref="ExpiryPolicy"/> with the sensible 30-day warning window, seeded per company at tenant
/// provisioning. Kept alongside the aggregate so the default value and the deterministic id (which makes the
/// seed idempotent) live with the domain rule, not in the provisioner.
/// </summary>
public static class DefaultExpiryPolicy
{
    /// <summary>Stable code the deterministic id is derived from (a company has a single expiry policy).</summary>
    private const string PolicyCode = "expiry-policy";

    /// <summary>
    /// Materializes the default expiry policy for <paramref name="companyId"/> at its deterministic id, so a
    /// re-run of the seed never creates a second policy for the same company.
    /// </summary>
    public static ExpiryPolicy ForCompany(Guid companyId)
        => ExpiryPolicy.Seed(DeterministicId(companyId), ExpiryPolicy.DefaultWarningWindowDays);

    /// <summary>
    /// Derives the stable id of a company's singleton expiry policy. MD5 here is a non-cryptographic id
    /// derivation (a namespaced hash), not a security primitive.
    /// </summary>
    public static Guid DeterministicId(Guid companyId)
    {
        byte[] payload = companyId
            .ToByteArray()
            .Concat(Encoding.UTF8.GetBytes(PolicyCode))
            .ToArray();

        byte[] hash = MD5.HashData(payload);
        return new Guid(hash);
    }
}
