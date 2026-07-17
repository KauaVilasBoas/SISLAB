using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Protocols;

/// <summary>
/// Registry-backed <see cref="IExperimentProtocolResolver"/>: indexes every <see cref="IExperimentProtocol"/>
/// registered in DI by the <see cref="IExperimentProtocol.Type"/> it declares, and hands back the one matching a
/// requested type. A duplicate registration for the same type is a wiring error and fails fast at construction.
/// </summary>
internal sealed class ExperimentProtocolResolver : IExperimentProtocolResolver
{
    private readonly IReadOnlyDictionary<ExperimentType, IExperimentProtocol> _protocols;

    public ExperimentProtocolResolver(IEnumerable<IExperimentProtocol> protocols)
        => _protocols = protocols.ToDictionary(protocol => protocol.Type);

    /// <inheritdoc />
    public IExperimentProtocol Resolve(ExperimentType type)
        => _protocols.TryGetValue(type, out IExperimentProtocol? protocol)
            ? protocol
            : throw new DomainException($"No calculation protocol is registered for experiment type '{type}'.");
}
