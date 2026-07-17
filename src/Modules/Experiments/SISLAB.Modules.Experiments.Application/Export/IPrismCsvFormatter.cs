namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Formats a calculated experiment's frozen snapshot JSON into a GraphPad Prism-compatible CSV (card [E11] #79).
/// One implementation per assay type, keyed by the versioned formula code it understands — resolved from a
/// registry exactly like the calculation <c>IExperimentProtocol</c>, so a new assay's export is a new
/// registration, never an edit to a switch.
/// </summary>
/// <remarks>
/// Prism accepts data pasted as CSV; the P.O. chose the pasteable CSV shape over the <c>.pzfx</c> XML. Each
/// formatter shapes the columns Prism expects for its assay: viability lays out one column per concentration with
/// one row per replicate; nitric oxide lays out the calibration curve then the samples, both as
/// absorbance / computed-value rows.
/// </remarks>
public interface IPrismCsvFormatter
{
    /// <summary>The versioned formula code whose snapshot JSON this formatter can render (e.g. <c>viability@v1</c>).</summary>
    string FormulaCode { get; }

    /// <summary>Renders the snapshot JSON as a Prism-pasteable CSV body.</summary>
    string Format(string resultJson);
}
