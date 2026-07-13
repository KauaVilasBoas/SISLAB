using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.Units;

/// <summary>
/// A per-tenant unit of measure/consumption (card [E12] #76): the lab declares which units it works in
/// (e.g. "mL", "g", "unidade", "caixa") instead of relying on a fixed list. Each unit has a short
/// <see cref="Symbol"/> (its identity within the tenant) and a human-readable <see cref="Name"/>.
/// </summary>
/// <remarks>
/// This is the declarative catalogue of units a lab uses; the Inventory <c>UnitOfMeasure</c> value object
/// (which carries conversion/compatibility semantics) is a separate, richer concept and is intentionally not
/// merged here — Configuration owns the transversal "which units exist for this tenant" list, nothing more.
/// </remarks>
public sealed class Unit : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxSymbolLength = 20;
    private const int MaxNameLength = 80;

    // Parameterless constructor for EF Core materialization.
    private Unit() : base(Guid.Empty) { }

    private Unit(Guid id, string symbol, string name) : base(id)
    {
        Symbol = symbol;
        Name = name;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>Short symbol identifying the unit within the tenant (e.g. "mL"); unique per company.</summary>
    public string Symbol { get; private set; } = default!;

    /// <summary>Human-readable name (e.g. "Mililitro").</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Creates a new tenant unit with a validated symbol and name.</summary>
    public static Unit Create(string symbol, string name)
        => new(Guid.NewGuid(), NormalizeSymbol(symbol), NormalizeName(name));

    /// <summary>Rehydrates a unit at a deterministic id — used by the idempotent tenant seeder.</summary>
    internal static Unit Seed(Guid id, string symbol, string name)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        return new Unit(id, NormalizeSymbol(symbol), NormalizeName(name));
    }

    /// <summary>Renames the unit (its symbol identity is unchanged).</summary>
    public void Rename(string name) => Name = NormalizeName(name);

    private static string NormalizeSymbol(string symbol)
    {
        Guard.AgainstNullOrWhiteSpace(symbol, nameof(symbol));
        string trimmed = symbol.Trim();
        Guard.AgainstMaxLength(trimmed, MaxSymbolLength, nameof(symbol));
        return trimmed;
    }

    private static string NormalizeName(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmed = name.Trim();
        Guard.AgainstMaxLength(trimmed, MaxNameLength, nameof(name));
        return trimmed;
    }
}
