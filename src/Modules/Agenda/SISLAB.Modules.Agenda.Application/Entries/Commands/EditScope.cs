namespace SISLAB.Modules.Agenda.Application.Entries.Commands;

/// <summary>
/// The Google-Calendar edit scope chosen when updating an occurrence of a (possibly recurring) entry
/// (card [E10.2] #2). The <see cref="UpdateAgendaEntryCommandHandler"/> composes the aggregate's primitive
/// operations differently per scope. For a non-recurring entry every scope collapses to a direct update.
/// </summary>
public enum EditScope
{
    /// <summary>Edit only the targeted occurrence: exclude its date from the series and create a detached one-off.</summary>
    OnlyThis = 1,

    /// <summary>Edit this occurrence and all later ones: truncate the original series and start a new one at the split.</summary>
    ThisAndFollowing = 2,

    /// <summary>Edit the whole series in place.</summary>
    AllOccurrences = 3,
}
