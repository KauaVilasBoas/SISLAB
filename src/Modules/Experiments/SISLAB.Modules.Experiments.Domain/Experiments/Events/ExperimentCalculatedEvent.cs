using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Experiments.Events;

/// <summary>
/// Raised when an experiment's versioned calculation has run and produced its result snapshot (decision
/// card #68 — the calculation moment). It is translated to a public integration event and published via the
/// Outbox so the Inventory module can correlate reagent consumption to a real, calculated experiment
/// (card #109). The <see cref="ExperimentId"/> crosses the boundary <b>by value</b> (no FK/navigation).
/// </summary>
public sealed record ExperimentCalculatedEvent(
    Guid CompanyId,
    Guid ExperimentId,
    ExperimentType Type,
    string FormulaName,
    DateTime AppliedAtUtc) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
