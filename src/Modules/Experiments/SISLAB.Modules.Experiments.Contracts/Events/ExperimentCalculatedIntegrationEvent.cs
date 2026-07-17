using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Contracts.Events;

/// <summary>
/// Public, flattened contract published when an experiment's versioned calculation produced its result
/// snapshot (decision card #68). Published via the Outbox so other bounded contexts can react without
/// reaching into the Experiments internals — notably the Inventory module, which correlates reagent
/// consumption to a real, calculated experiment for its cost report (card #109). The <see cref="ExperimentId"/>
/// crosses <b>by value</b> — there is no FK/navigation between modules.
/// </summary>
public sealed record ExperimentCalculatedIntegrationEvent : IIntegrationEvent
{
    public ExperimentCalculatedIntegrationEvent(
        Guid eventId,
        DateTime occurredOnUtc,
        Guid companyId,
        Guid experimentId,
        string experimentType,
        string formulaName,
        DateTime appliedAtUtc)
    {
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        CompanyId = companyId;
        ExperimentId = experimentId;
        ExperimentType = experimentType;
        FormulaName = formulaName;
        AppliedAtUtc = appliedAtUtc;
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }

    /// <inheritdoc />
    public string EventType => nameof(ExperimentCalculatedIntegrationEvent);

    public Guid CompanyId { get; }

    /// <summary>The calculated experiment, held by value (no cross-module FK/navigation).</summary>
    public Guid ExperimentId { get; }

    /// <summary>Experiment type name (e.g. "ViabilidadeCelular").</summary>
    public string ExperimentType { get; }

    /// <summary>Versioned formula code applied (e.g. "viability@v1").</summary>
    public string FormulaName { get; }

    /// <summary>Instant (UTC) the formula was applied.</summary>
    public DateTime AppliedAtUtc { get; }
}
