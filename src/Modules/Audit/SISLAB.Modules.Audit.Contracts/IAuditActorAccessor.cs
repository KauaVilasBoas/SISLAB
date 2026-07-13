namespace SISLAB.Modules.Audit.Contracts;

/// <summary>
/// Resolves the actor ("who") recorded on an audit entry (card [E9] #57).
///
/// The actor is the authenticated user's JWT <c>sub</c> claim (a string), never taken from the request
/// payload. Background work (jobs, the Outbox dispatcher) runs with no HTTP principal, so the accessor
/// falls back to <see cref="SystemActor"/>. Exposed on the module's public boundary so the write-side
/// handlers that record audit entries (in other modules) can resolve the actor without depending on
/// ASP.NET or Identity internals.
/// </summary>
public interface IAuditActorAccessor
{
    /// <summary>Sentinel actor used for background/system operations that have no authenticated principal.</summary>
    const string SystemActor = AuditConstants.SystemActor;

    /// <summary>
    /// The current actor id — the JWT <c>sub</c> claim of the authenticated user, or
    /// <see cref="SystemActor"/> when there is no HTTP principal.
    /// </summary>
    string GetCurrentActor();
}
