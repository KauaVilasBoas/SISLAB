namespace SISLAB.Modules.Inventory.Domain.StockItems;

/// <summary>
/// Physical state of a stock item's container. Relevant for reagents and drugs whose validity is
/// affected once the container is opened. Tracking it lets the laboratory distinguish a sealed,
/// long-lived stock from an opened one that must be consumed sooner.
/// </summary>
public enum ContainerState
{
    Closed,
    Open
}
