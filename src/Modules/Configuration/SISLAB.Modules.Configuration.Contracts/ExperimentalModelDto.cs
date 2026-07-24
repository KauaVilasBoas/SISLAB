namespace SISLAB.Modules.Configuration.Contracts;

/// <summary>
/// Public, flattened view of a tenant's experimental model / induction protocol (SISLAB-04), returned across the
/// module boundary by <see cref="ILabConfiguration"/>. It carries only primitives and Contracts-owned records —
/// never the internal <c>ExperimentalModel</c> aggregate or its value objects — so a consuming module (Experiments)
/// depends on nothing of the Configuration Domain (module isolation, section 2). The Experiments <c>Batch</c>
/// references such a model <b>by value</b> (its <see cref="Id"/>), the only thing it persists cross-module.
/// </summary>
/// <param name="Id">Model identifier — the value the Experiments <c>Batch</c> references by value.</param>
/// <param name="Name">The model's name, unique per tenant (e.g. "Neuropatia diabética").</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Induction">The induction protocol (administrations, spacing, reference day).</param>
/// <param name="Timepoints">The default timepoint labels the model measures at (e.g. basal, pós-indução, 7/15/21/28 dias).</param>
/// <param name="Parameters">The physiological/behavioural parameter codes that apply (e.g. glicemia, rotarod, peso).</param>
/// <param name="Groups">The default group design (Naive/Control/dose curve).</param>
/// <param name="DilutionDefaults">The default dilution parameters (g:µL relation + default diluent).</param>
public sealed record ExperimentalModelDto(
    Guid Id,
    string Name,
    string? Description,
    InductionProtocolDto Induction,
    IReadOnlyList<string> Timepoints,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<StandardGroupDto> Groups,
    DilutionDefaultsDto DilutionDefaults);

/// <summary>Public view of an experimental model's induction protocol.</summary>
/// <param name="Administrations">How many induction administrations the protocol prescribes.</param>
/// <param name="IntervalDays">Days between consecutive administrations.</param>
/// <param name="ReferenceDayAfterInduction">The reference day, counted after induction, the readouts anchor to.</param>
public sealed record InductionProtocolDto(int Administrations, int IntervalDays, int ReferenceDayAfterInduction);

/// <summary>Public view of one standard (default) group of an experimental model.</summary>
/// <param name="Name">The group's label (e.g. "Naive", "Controle", "3 g/kg").</param>
/// <param name="Kind">The group's kind, as a stable string code (Naive / Control / Dose).</param>
/// <param name="DoseAmount">The dose amount for a dose group; <see langword="null"/> for Naive/Control.</param>
/// <param name="DoseUnit">The dose unit for a dose group; <see langword="null"/> for Naive/Control.</param>
public sealed record StandardGroupDto(string Name, string Kind, decimal? DoseAmount, string? DoseUnit);

/// <summary>Public view of an experimental model's default dilution parameters.</summary>
/// <param name="MicrolitresPerGram">The g:µL relation as µL per gram of animal (e.g. 5 for a 1 g : 5 µL relation).</param>
/// <param name="DefaultDiluent">The default diluent/vehicle (e.g. "Óleo de soja").</param>
public sealed record DilutionDefaultsDto(decimal MicrolitresPerGram, string DefaultDiluent);
