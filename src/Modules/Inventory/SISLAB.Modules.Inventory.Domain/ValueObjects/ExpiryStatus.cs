namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>Expiry classification of a stock item relative to a reference instant and warning window.</summary>
public enum ExpiryStatus
{
    Ok,
    ExpiringSoon,
    Expired
}
