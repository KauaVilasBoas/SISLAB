namespace SISLAB.Modules.Experiments.Domain.Biobank;

/// <summary>
/// The biological material a <see cref="Sample"/> holds (card [E11] #89 — the biobank). The set covers the
/// common in vivo collections; the value is persisted by name so adding a material later is additive.
/// </summary>
public enum SampleType
{
    /// <summary>Whole blood.</summary>
    Blood = 0,

    /// <summary>Blood plasma (anticoagulated, cell-free).</summary>
    Plasma = 1,

    /// <summary>Blood serum (clotted, cell- and fibrinogen-free).</summary>
    Serum = 2,

    /// <summary>Solid tissue / organ fragment.</summary>
    Tissue = 3,

    /// <summary>Cerebrospinal fluid.</summary>
    CerebrospinalFluid = 4,

    /// <summary>Urine.</summary>
    Urine = 5,

    /// <summary>Any other material not covered above.</summary>
    Other = 6,
}
