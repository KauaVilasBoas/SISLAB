using SISLAB.Modules.Experiments.Domain.Experiments;

namespace SISLAB.Modules.Experiments.Application.Protocols;

/// <summary>
/// Strategy that owns the versioned calculation of one experiment <see cref="ExperimentType"/> (decision
/// card #68 — the calculation is a Strategy resolved by type, never baked into the domain). An implementation
/// validates the experiment's measurements, applies its versioned formula and returns an immutable
/// <see cref="FormulaSnapshot"/> the aggregate then stores as-is.
/// </summary>
/// <remarks>
/// Keeping the formula out of the aggregate is what makes it versionable and testable in isolation — a new
/// formula version is a new strategy registration, never a change to the domain model. Each implementation
/// declares the <see cref="ExperimentType"/> it handles; the application resolves the matching one from DI by
/// type, so adding an assay type never edits a switch.
/// </remarks>
public interface IExperimentProtocol
{
    /// <summary>The experiment type this protocol calculates.</summary>
    ExperimentType Type { get; }

    /// <summary>
    /// Validates the experiment's measurements and computes its result, returning an immutable snapshot
    /// (versioned formula code, expression, instant and frozen JSON result). Throws a domain error when the
    /// experiment is not ready to be calculated.
    /// </summary>
    FormulaSnapshot Calculate(Experiment experiment);
}
