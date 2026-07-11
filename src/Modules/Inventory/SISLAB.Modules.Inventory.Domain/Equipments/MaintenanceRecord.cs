using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Domain.Equipments;

/// <summary>
/// A single servicing event logged against an <see cref="Equipment"/>: the date it happened, its
/// <see cref="MaintenanceType"/> and an optional free-text note. Maintenance records are an append-only
/// history owned by the equipment aggregate; they are never mutated once logged.
/// </summary>
/// <remarks>
/// Responsible-party decision (card [E3] #27, option A): a maintenance record deliberately does <b>not</b>
/// carry a "responsible user". <i>Who</i> performed/logged the maintenance is audit-trail data owned by
/// the cross-cutting audit card ([E9] #57), exactly as the stock-entry aggregate already left the "who"
/// to the audit trail on card [E3] #24. Modelling a <c>responsibleUserId</c> here would (a) contradict
/// that #24 precedent and (b) break module isolation, since the Inventory module has no access to the
/// logged-in user id — <c>IUserIdAccessor</c> is a Lumen concern living only in the Identity module, and
/// <c>ITenantContext</c> exposes only the <c>CompanyId</c>. The audit trail (#57) will attribute the "who"
/// uniformly across every write, without leaking identity into this domain.
/// </remarks>
public sealed class MaintenanceRecord : ValueObject
{
    private const int MaxNotesLength = 1000;

    private MaintenanceRecord(DateOnly date, MaintenanceType type, string? notes)
    {
        Date = date;
        Type = type;
        Notes = notes;
    }

    /// <summary>Date the maintenance was performed.</summary>
    public DateOnly Date { get; }

    /// <summary>Nature of the maintenance (preventive/corrective/calibration).</summary>
    public MaintenanceType Type { get; }

    /// <summary>Optional free-text description of what was done; <see langword="null"/> when omitted.</summary>
    public string? Notes { get; }

    /// <summary>Creates a maintenance record from its date, type and an optional note.</summary>
    public static MaintenanceRecord Create(DateOnly date, MaintenanceType type, string? notes = null)
    {
        string? normalizedNotes = NormalizeNotes(notes);

        return new MaintenanceRecord(date, type, normalizedNotes);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Date;
        yield return Type;
        yield return Notes;
    }

    public override string ToString() =>
        Notes is null ? $"{Date:yyyy-MM-dd} · {Type}" : $"{Date:yyyy-MM-dd} · {Type} · {Notes}";

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        string trimmed = notes.Trim();
        if (trimmed.Length > MaxNotesLength)
            throw new DomainException(
                $"Maintenance notes exceed the maximum length of {MaxNotesLength} characters.");

        return trimmed;
    }
}
