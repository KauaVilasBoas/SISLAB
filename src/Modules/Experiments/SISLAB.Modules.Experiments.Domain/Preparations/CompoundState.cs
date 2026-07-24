namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// Physical state of the compound being prepared for in vivo administration (SISLAB-01). It decides how the
/// compound mass turns into the final solution: a <see cref="Liquid"/> occupies a real volume (obtained from its
/// density) that is <b>subtracted</b> from the final solution volume to size the diluent; a <see cref="Powder"/>
/// dissolves without displacing a measurable volume, so nothing is subtracted.
/// </summary>
/// <remarks>
/// The state is an <b>input per compound</b>, never a code constant — the same formula serves a powdered alkaloid
/// and a liquid oil. It is the switch behind the acceptance rule "liquid applies density and subtracts; powder does
/// not; control (vehicle only) does not".
/// </remarks>
public enum CompoundState
{
    /// <summary>Solid compound: dissolves into the diluent without displacing a subtractable volume.</summary>
    Powder = 1,

    /// <summary>Liquid compound: occupies a density-derived volume that is subtracted from the final solution.</summary>
    Liquid = 2,
}
