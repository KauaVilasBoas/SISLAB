using SISLAB.Modules.Experiments.Domain.Experiments;

namespace SISLAB.Modules.Experiments.Application.Protocols;

/// <summary>
/// Resolves the <see cref="IExperimentProtocol"/> registered for a given <see cref="ExperimentType"/>. The
/// calculation command depends on this rather than a concrete strategy, so adding an assay type is a new
/// registration — never an edit to a switch or to the handler (Strategy + registry, decision card #68).
/// </summary>
public interface IExperimentProtocolResolver
{
    /// <summary>Returns the protocol for <paramref name="type"/>, or throws when none is registered.</summary>
    IExperimentProtocol Resolve(ExperimentType type);
}
