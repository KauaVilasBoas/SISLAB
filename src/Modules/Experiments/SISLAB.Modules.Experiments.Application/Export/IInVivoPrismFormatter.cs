namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Formats a calculated <b>in vivo</b> behavioural experiment's frozen snapshot into a GraphPad Prism-pasteable
/// CSV laid out as <b>group × timepoint</b> (card [E11] #31). Unlike the in vitro <see cref="IPrismCsvFormatter"/>,
/// which needs only the snapshot JSON, an in vivo export must also know which dose group each measured animal
/// belongs to — that mapping lives on the <c>Project</c> aggregate, so it is supplied alongside the JSON.
/// </summary>
/// <remarks>
/// One implementation per behavioural assay, keyed by the versioned formula code it understands and resolved from
/// a registry exactly like the calculation <c>IExperimentProtocol</c> and the in vitro formatters — so adding a
/// scorable assay's export is a new registration, never an edit to a switch. The Prism grouped layout is one
/// column per timepoint and, within each dose group, one replicate row per animal.
/// </remarks>
public interface IInVivoPrismFormatter
{
    /// <summary>The versioned formula code whose snapshot JSON this formatter can render (e.g. <c>von-frey-up-down@v1</c>).</summary>
    string FormulaCode { get; }

    /// <summary>
    /// Renders the snapshot JSON as a Prism-pasteable CSV body, using <paramref name="animalGroups"/> to place each
    /// animal's per-timepoint value under its dose group.
    /// </summary>
    string Format(string resultJson, IReadOnlyList<AnimalGroupAssignment> animalGroups);
}

/// <summary>
/// Maps a measured animal to the dose group it belongs to, for the group × timepoint pivot. Held by value (the
/// animal/group ids), sourced from the <c>Project</c> aggregate at export time.
/// </summary>
public sealed record AnimalGroupAssignment(
    Guid AnimalId,
    Guid GroupId,
    string GroupName,
    decimal DoseAmount,
    string DoseUnit,
    string AnimalIdentifier);
