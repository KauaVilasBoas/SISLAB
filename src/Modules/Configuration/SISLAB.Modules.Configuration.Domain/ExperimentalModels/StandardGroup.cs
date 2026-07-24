using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// One default group of an experimental model's design (SISLAB-04): a named arm with a role
/// (<see cref="StandardGroupKind"/>) and, for a dose arm, the dose amount and unit. An immutable value object
/// owning the "a dose group has a positive dose; Naive/Control never carry a dose" invariant.
/// </summary>
/// <remarks>
/// The concrete groups are cadastered per model — the current lab's Naive, Controle, 3 g/kg and 0,6 g/kg are just
/// one configured curve, never code constants. The dose is kept as amount + free-typed unit (e.g. "g/kg"), so a
/// model can express any dose-response curve without the unit being fixed in code.
/// </remarks>
public sealed class StandardGroup : ValueObject
{
    private const int MaxNameLength = 120;
    private const int MaxUnitLength = 20;

    private StandardGroup(string name, StandardGroupKind kind, decimal? doseAmount, string? doseUnit)
    {
        Name = name;
        Kind = kind;
        DoseAmount = doseAmount;
        DoseUnit = doseUnit;
    }

    /// <summary>The group's display name (e.g. "Naive", "Controle", "3 g/kg"), unique within the model.</summary>
    public string Name { get; }

    /// <summary>The role the group plays (Naive / Control / Dose).</summary>
    public StandardGroupKind Kind { get; }

    /// <summary>The dose amount for a <see cref="StandardGroupKind.Dose"/> group; <see langword="null"/> otherwise.</summary>
    public decimal? DoseAmount { get; }

    /// <summary>The dose unit for a <see cref="StandardGroupKind.Dose"/> group (e.g. "g/kg"); <see langword="null"/> otherwise.</summary>
    public string? DoseUnit { get; }

    /// <summary>Creates a non-dosed group (Naive or Control): a validated name and no dose.</summary>
    public static StandardGroup NonDosed(string name, StandardGroupKind kind)
    {
        if (kind == StandardGroupKind.Dose)
            throw new DomainException("A dose group must be created with a dose amount and unit.");

        return new StandardGroup(NormalizeName(name), kind, doseAmount: null, doseUnit: null);
    }

    /// <summary>Creates a dose group on the response curve: a validated name plus a positive dose amount and unit.</summary>
    public static StandardGroup Dosed(string name, decimal doseAmount, string doseUnit)
    {
        Guard.AgainstNonPositive(doseAmount, nameof(doseAmount));
        Guard.AgainstNullOrWhiteSpace(doseUnit, nameof(doseUnit));

        string unit = doseUnit.Trim();
        Guard.AgainstMaxLength(unit, MaxUnitLength, nameof(doseUnit));

        return new StandardGroup(NormalizeName(name), StandardGroupKind.Dose, doseAmount, unit);
    }

    private static string NormalizeName(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmed = name.Trim();
        Guard.AgainstMaxLength(trimmed, MaxNameLength, nameof(name));
        return trimmed;
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name.ToLowerInvariant();
        yield return Kind;
        yield return DoseAmount;
        yield return DoseUnit?.ToLowerInvariant();
    }
}
