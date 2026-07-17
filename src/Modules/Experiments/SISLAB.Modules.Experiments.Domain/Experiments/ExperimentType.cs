namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// Discriminator of an <see cref="Experiment"/> — the kind of assay it models. Persisted via EF Core TPH
/// as the <c>type</c> column of <c>experiments.experiments</c>, and used at runtime to resolve the matching
/// calculation strategy (<c>IExperimentProtocol</c>) from DI (decision card #68: herança para estado do
/// tipo, Strategy para o comportamento/cálculo).
/// </summary>
/// <remarks>
/// The in vitro plate assays ship here: <see cref="ViabilidadeCelular"/> (MTT, % cell viability) and
/// <see cref="NitricOxide"/> (Griess, NO concentration). The remaining in vivo subtypes mapped in the discovery
/// (VonFrei, TailFlick, RotaRod, Hemograma, ...) join this enum — and gain their own subtype + protocol — later.
/// </remarks>
public enum ExperimentType
{
    /// <summary>In vitro cell-viability assay on an 8×12 plate (MTT / 570-600 nm).</summary>
    ViabilidadeCelular = 1,

    /// <summary>In vitro nitric-oxide assay on an 8×12 plate (Griess reaction, nitrite calibration curve).</summary>
    NitricOxide = 2,
}
