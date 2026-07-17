using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Inventory.Application.StorageLocations.Commands;
using SISLAB.Modules.Inventory.Application.StorageLocations.Queries;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StorageLocations;

/// <summary>
/// HTTP boundary for the storage locations of the <b>active company</b> (card [E7] #112): the "Gerenciar locais"
/// management screen. It groups the flat management listing (read side) and the CRUD write side — registration,
/// the conservative metadata update (never the type, which is fixed at creation) and the active/inactive toggle
/// that preserves the movement history. The controller only dispatches CQRS requests through
/// <see cref="IMediator"/> and maps the successful result to the uniform <see cref="ApiResult"/>/
/// <see cref="ApiResult{T}"/> envelope; it never touches repositories, the DbContext or Dapper, and never maps
/// errors — those bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// The master-detail left-column summary (item/expired counts, critical flag) lives on the
/// <c>StockController</c> at <c>storage-locations/summary</c> because it belongs to the item-browsing read side;
/// this controller owns the gestão CRUD. Tenant-scoped: every request runs against the active company resolved
/// from the httpOnly cookie (EF Core global query filter + <c>ITenantContext</c>; the read side keeps the
/// mandatory <c>WHERE company_id = @CompanyId</c>), never from the request body.
/// </remarks>
[Route("api/inventory/storage-locations")]
[Authorize]
public sealed class StorageLocationsController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public StorageLocationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lists the active company's storage locations for the management screen — every location (active or not)
    /// with its editable metadata and derived item count, ordered by name. Only <c>[Authorize]</c>, mirroring
    /// the other page-level read-side listings.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<StorageLocationListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListStorageLocations(CancellationToken ct)
    {
        IReadOnlyList<StorageLocationListItem> locations = await _mediator.SendAsync(
            new GetStorageLocationsQuery(),
            ct);

        return Ok(new ApiResult<IReadOnlyList<StorageLocationListItem>>(true, "Storage locations retrieved.", locations));
    }

    /// <summary>
    /// Registers a new storage location for the active company: name (required), type (fixed at creation) and an
    /// optional description; a temperature range is accepted only for a refrigerated location. Returns the new id.
    /// </summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterStorageLocationRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new RegisterStorageLocationCommand(
                body.Name,
                body.Type,
                body.Description,
                body.TemperatureMinCelsius,
                body.TemperatureMaxCelsius),
            ct);

        return Ok(new ApiResult<Guid>(true, "Storage location registered.", id));
    }

    /// <summary>
    /// Corrects a storage location's metadata — name, description and (for a refrigerated location) the target
    /// temperature range. The type is intentionally not editable. An unknown location is a 404.
    /// </summary>
    [HttpPut("{storageLocationId:guid}")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid storageLocationId,
        [FromBody] UpdateStorageLocationRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new UpdateStorageLocationCommand(
                storageLocationId,
                body.Name,
                body.Description,
                body.TemperatureMinCelsius,
                body.TemperatureMaxCelsius),
            ct);

        return Ok(new ApiResult(true, "Storage location updated."));
    }

    /// <summary>
    /// Activates or deactivates a storage location, preserving its movement history. A deactivated location can
    /// no longer receive stock. Idempotent. An unknown location is a 404.
    /// </summary>
    [HttpPatch("{storageLocationId:guid}/status")]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        Guid storageLocationId,
        [FromBody] ChangeStorageLocationStatusRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new ToggleStorageLocationStatusCommand(storageLocationId, body.IsActive),
            ct);

        return Ok(new ApiResult(true, body.IsActive ? "Storage location reactivated." : "Storage location deactivated."));
    }
}

/// <summary>
/// Request body to register a storage location; the company comes from the session, never the payload. The
/// temperature bounds are accepted only for a refrigerated location and must travel together.
/// </summary>
public sealed record RegisterStorageLocationRequest(
    string Name,
    StorageLocationType Type,
    string? Description,
    decimal? TemperatureMinCelsius,
    decimal? TemperatureMaxCelsius);

/// <summary>
/// Request body to correct a storage location's metadata. The type is intentionally absent — it is fixed at
/// creation. A null/blank description clears it; null temperature bounds clear the range.
/// </summary>
public sealed record UpdateStorageLocationRequest(
    string Name,
    string? Description,
    decimal? TemperatureMinCelsius,
    decimal? TemperatureMaxCelsius);

/// <summary>Request body to toggle a storage location's active status.</summary>
public sealed record ChangeStorageLocationStatusRequest(bool IsActive);
