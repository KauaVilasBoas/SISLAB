using System.Security.Cryptography;
using System.Text;

namespace SISLAB.Modules.Configuration.Domain.ItemCategories;

/// <summary>
/// The canonical catalogue of item categories every laboratory starts with (card [E12] #76). These are the
/// nine values that used to be the closed <c>StockItemCategory</c> enum in the Inventory domain; here they
/// become the tenant's default, editable <see cref="ItemCategory"/> rows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Single source of truth for two consumers.</b> The tenant seeder (default provisioning) and the
/// enum→category data backfill (Inventory migration) both need "the base categories, per company, with a
/// stable id". This type is that single source: <see cref="ForCompany"/> materializes the catalogue for a
/// company, and <see cref="DeterministicId"/> yields the same id for the same <c>(company, code)</c> pair on
/// every run — which is what makes the seed and the backfill idempotent and reproducible.
/// </para>
/// <para>
/// <b>Code = the legacy enum name.</b> Each default carries the exact legacy enum name as its immutable
/// <c>Code</c>. The backfill matches the old <c>stock_items.category</c> string (an enum name such as
/// "Solvent") to the seeded category with the same code, then rewrites the column to that category's id.
/// </para>
/// </remarks>
public static class DefaultItemCategories
{
    /// <summary>A single entry of the default catalogue: the legacy enum code, a display name and the controlled flag.</summary>
    public sealed record Default(string Code, string Name, bool IsControlled);

    /// <summary>
    /// The nine legacy categories, in enum order. Controlled flags mirror the legacy semantics: the two
    /// "Controlled*" categories are controlled; the rest are not (the per-item controlled flag remains
    /// independent — E3 #21).
    /// </summary>
    public static readonly IReadOnlyList<Default> Catalogue =
    [
        new("Reagent", "Reagente", false),
        new("Solvent", "Solvente", false),
        new("Kit", "Kit", false),
        new("Drug", "Fármaco", false),
        new("Disposable", "Descartável", false),
        new("Supply", "Insumo", false),
        new("ControlledAnesthetic", "Anestésico Controlado", true),
        new("ControlledOpioid", "Opioide Controlado", true),
        new("TestCompound", "Composto de Teste", false)
    ];

    /// <summary>
    /// Materializes the default catalogue as seeded <see cref="ItemCategory"/> aggregates for
    /// <paramref name="companyId"/>, each at its deterministic id so re-seeding is idempotent.
    /// </summary>
    public static IReadOnlyList<ItemCategory> ForCompany(Guid companyId)
        => Catalogue
            .Select(entry => ItemCategory.Seed(
                DeterministicId(companyId, entry.Code),
                entry.Name,
                aliases: null,
                entry.IsControlled))
            .ToList();

    /// <summary>Namespace prefix keeping category ids disjoint from other config defaults sharing the derivation.</summary>
    internal const string IdNamespace = "sislab:item-category:";

    /// <summary>
    /// Derives a stable, collision-resistant id for a default category from the <paramref name="companyId"/>
    /// and the category <paramref name="code"/>. Deterministic by construction, so the same pair always yields
    /// the same id — the property the idempotent seed and the SQL backfill both rely on.
    /// </summary>
    /// <remarks>
    /// The hash is taken over the <b>canonical, big-endian UUID text</b> of the company id (not the
    /// mixed-endian <c>Guid.ToByteArray()</c> bytes), so the exact same id is reproducible in PostgreSQL with
    /// <c>md5(lower(company_id::text) || 'sislab:item-category:' || code)</c> — this is what makes the C# seed
    /// and the SQL backfill agree. MD5 here is a non-cryptographic id derivation, not a security primitive.
    /// </remarks>
    public static Guid DeterministicId(Guid companyId, string code)
        => DeterministicGuid.From(IdNamespace + companyId.ToString("D").ToLowerInvariant(), code);
}
