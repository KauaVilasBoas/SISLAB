using SISLAB.Modules.Audit.Contracts;
using SISLAB.Modules.Experiments.Application.Protocols;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>Fixed actor accessor for handler tests — returns a stable identity without an HTTP principal.</summary>
internal sealed class FakeActorAccessor : IAuditActorAccessor
{
    private readonly string _actor;

    public FakeActorAccessor(string actor = "tester@lab") => _actor = actor;

    public string GetCurrentActor() => _actor;
}

/// <summary>Fixed clock for deterministic timestamps in tests.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }
}

/// <summary>
/// Real registry resolver wrapping the real strategies — so the calculate handler tests exercise the actual
/// Strategy resolution + formula, not a stub.
/// </summary>
internal static class TestProtocols
{
    public static IExperimentProtocolResolver Viability()
        => new ExperimentProtocolResolver(new IExperimentProtocol[] { new ViabilityCalculationStrategy() });

    /// <summary>Resolver holding every registered protocol, as the module wires it up.</summary>
    public static IExperimentProtocolResolver All()
        => new ExperimentProtocolResolver(new IExperimentProtocol[]
        {
            new ViabilityCalculationStrategy(),
            new NitricOxideCalculationStrategy(),
        });
}
