namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Resolves the <see cref="IInVivoPrismFormatter"/> registered for a given versioned formula code (card [E11]
/// #31). The in vivo export query depends on this rather than a concrete formatter, so adding a behavioural
/// assay's group × timepoint export is a new registration — never an edit to a switch (registry, mirroring
/// <see cref="IPrismCsvFormatterResolver"/> and <c>IExperimentProtocolResolver</c>).
/// </summary>
public interface IInVivoPrismFormatterResolver
{
    /// <summary>Returns the formatter for <paramref name="formulaCode"/>, or throws when none is registered.</summary>
    IInVivoPrismFormatter Resolve(string formulaCode);
}
