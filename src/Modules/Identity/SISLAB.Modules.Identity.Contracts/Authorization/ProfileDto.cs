namespace SISLAB.Modules.Identity.Contracts.Authorization;

/// <summary>
/// A Lumen authorization profile as listed by the management UI (card [E12] #103). A profile is a named set
/// of permissions that can be assigned to users; SISLAB creates and edits profiles but never touches the
/// permission catalogue itself.
/// </summary>
/// <param name="Id">The Lumen profile id.</param>
/// <param name="Name">The profile name, unique among active profiles.</param>
/// <param name="Description">Free-text description; may be empty.</param>
/// <param name="IsSystem">
/// True for Lumen's built-in profiles (e.g. <c>Administrator</c>), whose permissions are managed
/// automatically and cannot be overwritten through the SISLAB API.
/// </param>
public sealed record ProfileDto(
    Guid Id,
    string Name,
    string Description,
    bool IsSystem);
