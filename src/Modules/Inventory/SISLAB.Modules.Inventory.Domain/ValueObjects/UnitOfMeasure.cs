using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>
/// Validated unit of measure used to qualify a <see cref="Quantity"/>. Each unit carries the
/// physical <see cref="UnitDimension"/> it belongs to, so the domain can reject arithmetic between
/// incompatible units (for example mass versus volume) without a conversion table.
/// </summary>
/// <remarks>
/// The MVP does not convert between units of the same dimension (mg to g, mL to L). Two quantities
/// are only additive when their units are structurally equal. This is a deliberate decision recorded
/// on card [E3] #22 to avoid a premature conversion table.
/// </remarks>
public sealed class UnitOfMeasure : ValueObject
{
    public static readonly UnitOfMeasure Gram = new("g", UnitDimension.Mass);
    public static readonly UnitOfMeasure Milligram = new("mg", UnitDimension.Mass);
    public static readonly UnitOfMeasure Milliliter = new("mL", UnitDimension.Volume);
    public static readonly UnitOfMeasure Liter = new("L", UnitDimension.Volume);
    public static readonly UnitOfMeasure Unit = new("unidade", UnitDimension.Discrete);
    public static readonly UnitOfMeasure Ampoule = new("ampola", UnitDimension.Discrete);
    public static readonly UnitOfMeasure Box = new("caixa", UnitDimension.Discrete);
    public static readonly UnitOfMeasure Package = new("pacote", UnitDimension.Discrete);
    public static readonly UnitOfMeasure Kit = new("kit", UnitDimension.Discrete);

    private static readonly IReadOnlyDictionary<string, UnitOfMeasure> KnownUnits =
        new[] { Gram, Milligram, Milliliter, Liter, Unit, Ampoule, Box, Package, Kit }
            .ToDictionary(unit => unit.Symbol, StringComparer.OrdinalIgnoreCase);

    private UnitOfMeasure(string symbol, UnitDimension dimension)
    {
        Symbol = symbol;
        Dimension = dimension;
    }

    public string Symbol { get; }

    public UnitDimension Dimension { get; }

    /// <summary>Resolves a unit from its symbol (case-insensitive), rejecting unknown units.</summary>
    public static UnitOfMeasure FromSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Unit of measure symbol cannot be null or empty.");

        if (!KnownUnits.TryGetValue(symbol.Trim(), out UnitOfMeasure? unit))
            throw new DomainException($"Unknown unit of measure: '{symbol}'.");

        return unit;
    }

    /// <summary>True when both units belong to the same physical dimension and are therefore additive.</summary>
    public bool IsCompatibleWith(UnitOfMeasure other) => Equals(other);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Symbol;
    }

    public override string ToString() => Symbol;
}
