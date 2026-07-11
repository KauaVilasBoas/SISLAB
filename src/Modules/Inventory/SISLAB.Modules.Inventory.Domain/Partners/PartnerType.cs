namespace SISLAB.Modules.Inventory.Domain.Partners;

/// <summary>
/// The role a <see cref="Partner"/> plays for the laboratory, taken from the real prototype (screen
/// "Parceiros" #48). It drives whether the partner may be recorded as the <b>origin</b> of a stock
/// entry: only a partner that supplies (<see cref="Supplier"/> or <see cref="Both"/>) can.
/// </summary>
public enum PartnerType
{
    /// <summary>Supplies inputs/reagents/analyses to the lab ("Fornecedor"). May be an entry origin.</summary>
    Supplier,

    /// <summary>Receives from or collaborates with the lab ("Parceiro/Cliente"). Not an entry origin.</summary>
    Client,

    /// <summary>Both supplies and collaborates ("Ambos"). May be an entry origin.</summary>
    Both
}
