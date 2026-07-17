namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// Lifecycle state of a <see cref="Project"/> — the in vivo experimental design (card [E11] #73). A project is
/// drafted while its design (batches, groups, animals) is laid out, becomes <see cref="Active"/> once at least
/// one batch is running, and is <see cref="Closed"/> when the study ends. The design is versioned per batch, not
/// per project, so a project stays editable across its life.
/// </summary>
public enum ProjectStatus
{
    /// <summary>Being designed — the experimental delineation is still editable. Initial state.</summary>
    Draft = 0,

    /// <summary>At least one batch is running; the study is under way.</summary>
    Active = 1,

    /// <summary>The study has ended; kept for the record.</summary>
    Closed = 2,
}
