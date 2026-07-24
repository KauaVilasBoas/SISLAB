using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// The default preparation parameters an experimental model carries (SISLAB-04): the g:µL relation that sizes the
/// administered solution and the default diluent/vehicle. Together they seed the in vivo preparation calculator
/// (SISLAB-01) so an operator preparing a dose group for this model starts from the lab's cadastered defaults
/// instead of retyping them.
/// </summary>
/// <remarks>
/// An immutable value object. The current lab's "1 g : 5 µL" and "óleo de soja" are just one cadastered instance —
/// nothing here is a code constant. The diluent is a free-typed name (the concrete substance is lab data), kept as
/// a normalized non-blank string.
/// </remarks>
public sealed class DilutionDefaults : ValueObject
{
    private const int MaxDiluentLength = 120;

    // Parameterless constructor for EF Core materialization (the Ratio navigation is set by EF, not via ctor).
    private DilutionDefaults() { }

    private DilutionDefaults(GramMicrolitreRatio ratio, string defaultDiluent)
    {
        Ratio = ratio;
        DefaultDiluent = defaultDiluent;
    }

    /// <summary>The default animal-grams-to-µL relation used to size the final solution volume.</summary>
    public GramMicrolitreRatio Ratio { get; private set; } = default!;

    /// <summary>The default diluent/vehicle name (e.g. "Óleo de soja"). Lab data, never a code constant.</summary>
    public string DefaultDiluent { get; private set; } = default!;

    /// <summary>Builds the defaults from a positive g:µL relation and a non-blank diluent name.</summary>
    public static DilutionDefaults Of(decimal microlitresPerGram, string defaultDiluent)
    {
        GramMicrolitreRatio ratio = GramMicrolitreRatio.OfGramToMicrolitres(microlitresPerGram);

        Guard.AgainstNullOrWhiteSpace(defaultDiluent, nameof(defaultDiluent));
        string trimmed = defaultDiluent.Trim();
        Guard.AgainstMaxLength(trimmed, MaxDiluentLength, nameof(defaultDiluent));

        return new DilutionDefaults(ratio, trimmed);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Ratio;
        yield return DefaultDiluent;
    }
}
