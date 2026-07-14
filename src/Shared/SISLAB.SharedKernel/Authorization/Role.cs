namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Business role a member holds within a company (tenant) in SISLAB's multi-lab platform (E12).
///
/// <para>This is the SISLAB-owned RBAC concept: it is a property of a member's link to a company
/// (<c>CompanyMembership</c>) and drives which Lumen authorization Profile the user is assigned,
/// scoped to that company. It lives in the SharedKernel — not in the Identity Domain — because it
/// is a cross-cutting platform primitive consumed by the Identity Domain (the aggregate invariant),
/// by the module's public Contracts (the Role→permissions map, the role management DTO) and by the
/// Application/Infrastructure (the Role→Lumen-Profile translation). Placing it in the Domain would
/// prevent the Contracts boundary — which must never reference a module's Domain — from using it.</para>
///
/// <para>The five roles are ordered from most to least privileged for readability only; the concrete
/// permissions each role grants are defined by the Role→permissions map, not by this ordinal.</para>
/// </summary>
public enum Role
{
    /// <summary>
    /// Lab coordinator. Full control over the company, including managing members and their roles.
    /// A company must always retain at least one active Coordinator (domain invariant).
    /// </summary>
    Coordinator = 0,

    /// <summary>
    /// Researcher. Operates the lab (registers stock movements, equipment, partners) but does not
    /// manage members or roles.
    /// </summary>
    Researcher = 1,

    /// <summary>
    /// Module manager. Write access delegated to the module(s) under their responsibility, without
    /// company-wide administration.
    /// </summary>
    ModuleManager = 2,

    /// <summary>
    /// Operator. Performs day-to-day inventory operations (entries, consumption, counts) with a
    /// narrower write surface than a Researcher.
    /// </summary>
    Operator = 3,

    /// <summary>
    /// Read-only. May view (GET) the company's data but cannot perform any write operation.
    /// </summary>
    ReadOnly = 4
}
