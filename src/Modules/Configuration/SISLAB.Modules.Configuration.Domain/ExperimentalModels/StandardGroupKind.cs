namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// The role a default group plays in an experimental model's design (SISLAB-04). It classifies the cadastered
/// standard groups so downstream design (SISLAB-02/03) knows which arms are untreated, which is the vehicle
/// control and which are the dose-response curve.
/// </summary>
/// <remarks>
/// This is a stable, structural classification (not lab data): the current lab's Naive/Controle/3 g·kg⁻¹/0,6 g·kg⁻¹
/// map onto <see cref="Naive"/>, <see cref="Control"/> and two <see cref="Dose"/> groups. Only a <see cref="Dose"/>
/// group carries a dose amount; Naive and Control never do.
/// </remarks>
public enum StandardGroupKind
{
    /// <summary>Untreated, non-induced group (Naive): no induction, no treatment, no dose.</summary>
    Naive = 1,

    /// <summary>Induced group receiving vehicle only (Controle): no dose amount.</summary>
    Control = 2,

    /// <summary>Treated group on the dose-response curve: carries a dose amount and unit.</summary>
    Dose = 3,
}
