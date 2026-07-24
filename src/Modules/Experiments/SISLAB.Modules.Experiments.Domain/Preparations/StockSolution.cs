using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The stock (mother) solution of a compound for an in vitro plate (SISLAB-05). It is defined two ways, exactly as
/// the spreadsheet does: for a compound with a known molar mass, by molarity (<c>V = m × M / MM</c>); for a compound
/// without one, by a plain mass concentration in mg/mL. A value object so a stock is reproducible and compared by
/// value.
/// </summary>
/// <remarks>
/// Both routes expose a single downstream quantity — the stock <see cref="ConcentrationMicromolar"/> (µM) — which the
/// serial dilution then steps down. When the compound has no molar mass the µM concentration is not defined, so the
/// dilution works from the mass concentration instead; callers read <see cref="HasMolarMass"/> to know which is
/// available. Nothing here is laboratory-specific: molar mass, mass, target molarity and mg/mL are all inputs.
/// </remarks>
public sealed class StockSolution : ValueObject
{
    private StockSolution(
        decimal massMilligrams,
        decimal? molarMassGramsPerMole,
        decimal? concentrationMicromolar,
        decimal concentrationMilligramsPerMillilitre,
        decimal volumeMillilitres)
    {
        MassMilligrams = massMilligrams;
        MolarMassGramsPerMole = molarMassGramsPerMole;
        ConcentrationMicromolar = concentrationMicromolar;
        ConcentrationMilligramsPerMillilitre = concentrationMilligramsPerMillilitre;
        VolumeMillilitres = volumeMillilitres;
    }

    /// <summary>Mass of compound weighed for the stock (mg).</summary>
    public decimal MassMilligrams { get; }

    /// <summary>Molar mass of the compound (g/mol); null when the compound has none (mg/mL route).</summary>
    public decimal? MolarMassGramsPerMole { get; }

    /// <summary>Stock concentration in µM; null on the mg/mL route (no molar mass).</summary>
    public decimal? ConcentrationMicromolar { get; }

    /// <summary>Stock concentration in mg/mL — always available, and the only concentration on the mg/mL route.</summary>
    public decimal ConcentrationMilligramsPerMillilitre { get; }

    /// <summary>Solvent volume the stock is made up to (mL).</summary>
    public decimal VolumeMillilitres { get; }

    /// <summary>True when the stock has a molar mass, so a µM concentration is defined.</summary>
    public bool HasMolarMass => MolarMassGramsPerMole is not null;

    /// <summary>
    /// Builds a stock by molarity: <c>V(mL) = m(mmol) / M(mol/L)</c>, i.e. the volume of solvent that yields the
    /// target molarity from the weighed mass, given the molar mass. All four inputs are positive.
    /// </summary>
    public static StockSolution FromMolarMass(
        decimal massMilligrams,
        decimal molarMassGramsPerMole,
        decimal targetMolarityMicromolar)
    {
        Guard.AgainstNonPositive(massMilligrams, nameof(massMilligrams));
        Guard.AgainstNonPositive(molarMassGramsPerMole, nameof(molarMassGramsPerMole));
        Guard.AgainstNonPositive(targetMolarityMicromolar, nameof(targetMolarityMicromolar));

        // moles = mass(g) / MM(g/mol); volume(L) = moles / molarity(mol/L). Kept in consistent units:
        // molarity(µM) = µmol/L; moles(µmol) = mass(mg) / MM(g/mol) * 1000; volume(L) = µmol / (µM).
        decimal micromoles = massMilligrams / molarMassGramsPerMole * 1000m;
        decimal volumeLitres = micromoles / targetMolarityMicromolar;
        decimal volumeMillilitres = volumeLitres * 1000m;
        decimal concentrationMgPerMl = massMilligrams / volumeMillilitres;

        return new StockSolution(
            massMilligrams,
            molarMassGramsPerMole,
            targetMolarityMicromolar,
            concentrationMgPerMl,
            volumeMillilitres);
    }

    /// <summary>
    /// Builds a stock for a compound without a molar mass, straight from a mass concentration (mg/mL) and the
    /// volume made up. No µM concentration is defined — the dilution steps the mg/mL value down instead.
    /// </summary>
    public static StockSolution FromMassConcentration(
        decimal concentrationMilligramsPerMillilitre,
        decimal volumeMillilitres)
    {
        Guard.AgainstNonPositive(concentrationMilligramsPerMillilitre, nameof(concentrationMilligramsPerMillilitre));
        Guard.AgainstNonPositive(volumeMillilitres, nameof(volumeMillilitres));

        decimal mass = concentrationMilligramsPerMillilitre * volumeMillilitres;

        return new StockSolution(
            mass,
            molarMassGramsPerMole: null,
            concentrationMicromolar: null,
            concentrationMilligramsPerMillilitre,
            volumeMillilitres);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MassMilligrams;
        yield return MolarMassGramsPerMole;
        yield return ConcentrationMicromolar;
        yield return ConcentrationMilligramsPerMillilitre;
        yield return VolumeMillilitres;
    }
}
