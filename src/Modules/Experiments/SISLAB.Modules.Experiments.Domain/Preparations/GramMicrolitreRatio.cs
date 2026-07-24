using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Preparations;

/// <summary>
/// The animal-grams-to-microlitres relation used to size the final administered solution (SISLAB-01): so many µL
/// of solution per gram of animal body weight (the spreadsheet's "1 g : 5 µL" for the alkaloid/OGV base, "1 g : 10
/// µL" as the general default). A value object — immutable and compared by value — so the relation is passed as a
/// parameter, never hard-coded.
/// </summary>
/// <remarks>
/// Stored as <see cref="MicrolitresPerGram"/> (µL of solution per 1 g of animal) so the final volume is simply
/// <c>relationWeightGrams × MicrolitresPerGram</c>. The relation is a cadastrable parameter of the preparation /
/// experimental model; the current laboratory's 1:5 and the general 1:10 are just two configured values.
/// </remarks>
public sealed class GramMicrolitreRatio : ValueObject
{
    // Parameterless constructor for EF Core materialization of the owned value object.
    private GramMicrolitreRatio() { }

    private GramMicrolitreRatio(decimal microlitresPerGram) => MicrolitresPerGram = microlitresPerGram;

    /// <summary>Microlitres of final solution per gram of animal body weight (e.g. 5 for a 1 g : 5 µL relation).</summary>
    public decimal MicrolitresPerGram { get; }

    /// <summary>
    /// Builds the relation from "1 g of animal : <paramref name="microlitres"/> µL of solution". The µL value must
    /// be positive.
    /// </summary>
    public static GramMicrolitreRatio OfGramToMicrolitres(decimal microlitres)
    {
        Guard.AgainstNonPositive(microlitres, nameof(microlitres));
        return new GramMicrolitreRatio(microlitres);
    }

    /// <summary>The final solution volume (µL) for the given animal-weight basis under this relation.</summary>
    public decimal FinalVolumeMicrolitresFor(decimal relationWeightGrams)
    {
        Guard.AgainstNonPositive(relationWeightGrams, nameof(relationWeightGrams));
        return relationWeightGrams * MicrolitresPerGram;
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MicrolitresPerGram;
    }

    /// <inheritdoc />
    public override string ToString() => $"1 g : {MicrolitresPerGram} µL";
}
