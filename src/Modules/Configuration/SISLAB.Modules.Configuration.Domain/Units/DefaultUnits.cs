using System.Security.Cryptography;
using System.Text;

namespace SISLAB.Modules.Configuration.Domain.Units;

/// <summary>
/// The base catalogue of units a laboratory starts with (card [E12] #76). Seeded per company at tenant
/// provisioning, each at a deterministic id so re-seeding is idempotent. A lab is free to add, rename or
/// stop using any of them afterwards.
/// </summary>
public static class DefaultUnits
{
    /// <summary>A single entry of the default unit catalogue: symbol + display name.</summary>
    public sealed record Default(string Symbol, string Name);

    /// <summary>The base units covering the common laboratory magnitudes (volume, mass, count/packaging).</summary>
    public static readonly IReadOnlyList<Default> Catalogue =
    [
        new("mL", "Mililitro"),
        new("L", "Litro"),
        new("g", "Grama"),
        new("mg", "Miligrama"),
        new("kg", "Quilograma"),
        new("un", "Unidade"),
        new("cx", "Caixa")
    ];

    /// <summary>
    /// Materializes the default catalogue as seeded <see cref="Unit"/> aggregates for
    /// <paramref name="companyId"/>, each at its deterministic id so re-seeding never duplicates a unit.
    /// </summary>
    public static IReadOnlyList<Unit> ForCompany(Guid companyId)
        => Catalogue
            .Select(entry => Unit.Seed(DeterministicId(companyId, entry.Symbol), entry.Symbol, entry.Name))
            .ToList();

    /// <summary>
    /// Derives a stable id for a default unit from <paramref name="companyId"/> and the unit
    /// <paramref name="symbol"/> — the same pair always yields the same id, keeping the seed idempotent. MD5
    /// here is a non-cryptographic id derivation, not a security primitive.
    /// </summary>
    public static Guid DeterministicId(Guid companyId, string symbol)
    {
        byte[] payload = companyId
            .ToByteArray()
            .Concat(Encoding.UTF8.GetBytes("unit:" + symbol))
            .ToArray();

        byte[] hash = MD5.HashData(payload);
        return new Guid(hash);
    }
}
