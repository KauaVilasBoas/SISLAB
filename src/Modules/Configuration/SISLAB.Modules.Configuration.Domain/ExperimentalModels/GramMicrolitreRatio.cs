using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// The animal-grams-to-microlitres relation an experimental model uses by default to size the final administered
/// solution (SISLAB-04, ties into SISLAB-01): so many µL of solution per gram of animal body weight — the
/// spreadsheet's "1 g : 5 µL" for the alkaloid/OGV base, "1 g : 10 µL" as the general default. An immutable value
/// object compared by value, so the relation is stored as a cadastrable parameter of the model, never hard-coded.
/// </summary>
/// <remarks>
/// This concept is intentionally <b>replicated</b> in the Configuration domain rather than reused from the
/// Experiments domain: module isolation forbids Configuration.Domain from referencing Experiments.Domain. Any
/// cross-module exchange happens later through <c>Configuration.Contracts</c> (a flattened DTO), not by sharing the
/// aggregate's internal value objects.
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

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MicrolitresPerGram;
    }

    /// <inheritdoc />
    public override string ToString() => $"1 g : {MicrolitresPerGram} µL";
}
