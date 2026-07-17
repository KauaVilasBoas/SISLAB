namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>
/// Resolves the <see cref="IPrismCsvFormatter"/> registered for a given versioned formula code. The export query
/// depends on this rather than a concrete formatter, so adding an assay's export is a new registration — never an
/// edit to a switch (registry, mirroring <c>IExperimentProtocolResolver</c>).
/// </summary>
public interface IPrismCsvFormatterResolver
{
    /// <summary>Returns the formatter for <paramref name="formulaCode"/>, or throws when none is registered.</summary>
    IPrismCsvFormatter Resolve(string formulaCode);
}
