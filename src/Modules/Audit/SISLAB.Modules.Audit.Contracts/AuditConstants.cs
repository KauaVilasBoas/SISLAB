namespace SISLAB.Modules.Audit.Contracts;

/// <summary>
/// Stable literals of the Audit module's public boundary (card [E9] #57): the sentinel actor recorded for
/// operations with no authenticated principal, the audited entity-type vocabulary and the CSV export
/// column headers. Centralized so writers (in other modules), the actor accessor, the export formatter and
/// their tests share one vocabulary instead of repeating magic strings.
/// </summary>
public static class AuditConstants
{
    /// <summary>Sentinel actor used for background/system operations that have no authenticated principal.</summary>
    public const string SystemActor = "system";

    /// <summary>Canonical entity-type names stamped on the <c>entity_type</c> column.</summary>
    public static class EntityTypes
    {
        public const string StockItem = "StockItem";
        public const string Equipment = "Equipment";
    }
}
