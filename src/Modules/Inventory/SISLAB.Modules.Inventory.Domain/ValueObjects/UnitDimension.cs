namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>
/// Physical dimension a <see cref="UnitOfMeasure"/> belongs to. Only quantities sharing the
/// same dimension can be added or subtracted without a conversion table, which the MVP does not provide.
/// </summary>
public enum UnitDimension
{
    Mass,
    Volume,
    Discrete
}
