using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Inventory.Application.Equipments.Commands;
using SISLAB.Modules.Inventory.Application.Equipments.Queries;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Equipments;

/// <summary>
/// HTTP boundary for the laboratory equipment of the <b>active company</b>. It groups both sides of the
/// "Equipamentos" screen (#48) behind a single controller: the read side (paginated listing filterable by
/// calibration status, storage location and free-text search, plus the per-equipment detail) and the write
/// side (registration, update, status transitions, calibration scheduling and the incremental logging of
/// maintenance events). The controller only dispatches CQRS requests through <see cref="IMediator"/> and
/// maps the successful result to the uniform <see cref="ApiResult"/>/<see cref="ApiResult{T}"/> envelope; it
/// never touches repositories, the DbContext or Dapper. A missing detail is translated into a
/// <see cref="NotFoundException"/> (404) here to keep the query pure; every other error bubbles up to the
/// exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie
/// (EF Core global query filter + <c>ITenantContext</c>; on the read side the query handlers keep the
/// mandatory <c>WHERE company_id = @CompanyId</c>), never from the request body.
/// </remarks>
[Route("api/inventory/equipment")]
[Authorize]
public sealed class EquipmentController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public EquipmentController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lists the active company's equipment for the equipment table, optionally filtered by calibration status,
    /// storage location and free-text search, ordered by name and paginated. Inactive (retired) equipment is
    /// hidden unless <paramref name="includeInactive"/> is set.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<PagedResult<EquipmentListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListEquipment(
        [FromQuery] CalibrationStatus? status,
        [FromQuery] Guid? storageLocationId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<EquipmentListItem> result = await _mediator.SendAsync(
            new ListEquipmentQuery
            {
                Status = status,
                StorageLocationId = storageLocationId,
                Search = search,
                IncludeInactive = includeInactive,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<EquipmentListItem>>(true, "Equipment retrieved.", result));
    }

    /// <summary>Returns the detail of a single equipment of the active company, or 404 when it does not exist.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<EquipmentDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEquipmentDetail(Guid id, CancellationToken ct)
    {
        EquipmentDetail detail = await _mediator.SendAsync(new GetEquipmentDetailQuery(id), ct)
            ?? throw new NotFoundException("Equipment", id);

        return Ok(new ApiResult<EquipmentDetail>(true, "Equipment retrieved.", detail));
    }

    /// <summary>Registers a new equipment.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterEquipmentRequest body,
        CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new RegisterEquipmentCommand(
                body.Name,
                body.AssetTag,
                body.Brand,
                body.Model,
                body.StorageLocationId,
                body.Status,
                body.LastCalibration,
                body.NextCalibration),
            ct);

        return Ok(new ApiResult<Guid>(true, "Equipment registered.", id));
    }

    /// <summary>Updates an equipment's identification data.</summary>
    [HttpPut("{equipmentId:guid}")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid equipmentId,
        [FromBody] UpdateEquipmentRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new UpdateEquipmentCommand(
                equipmentId,
                body.Name,
                body.AssetTag,
                body.Brand,
                body.Model,
                body.StorageLocationId),
            ct);

        return Ok(new ApiResult(true, "Equipment updated."));
    }

    /// <summary>Moves the equipment to a new operational status.</summary>
    [HttpPost("{equipmentId:guid}/status")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        Guid equipmentId,
        [FromBody] ChangeEquipmentStatusRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(new ChangeEquipmentStatusCommand(equipmentId, body.Status), ct);

        return Ok(new ApiResult(true, "Equipment status changed."));
    }

    /// <summary>Defines or clears the equipment's calibration schedule.</summary>
    [HttpPut("{equipmentId:guid}/calibration")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DefineCalibration(
        Guid equipmentId,
        [FromBody] DefineEquipmentCalibrationRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new DefineEquipmentCalibrationCommand(
                equipmentId,
                body.LastCalibration,
                body.NextCalibration),
            ct);

        return Ok(new ApiResult(true, "Calibration schedule updated."));
    }

    /// <summary>Logs a maintenance event against the equipment.</summary>
    [HttpPost("{equipmentId:guid}/maintenances")]
    [ProducesResponseType(typeof(ApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordMaintenance(
        Guid equipmentId,
        [FromBody] RecordEquipmentMaintenanceRequest body,
        CancellationToken ct)
    {
        await _mediator.SendAsync(
            new RecordEquipmentMaintenanceCommand(
                equipmentId,
                body.Date,
                body.Type,
                body.Notes),
            ct);

        return Ok(new ApiResult(true, "Maintenance recorded."));
    }
}

/// <summary>Request body to register an equipment; the company comes from the session, never the payload.</summary>
public sealed record RegisterEquipmentRequest(
    string Name,
    string AssetTag,
    string? Brand,
    string? Model,
    Guid? StorageLocationId,
    EquipmentStatus Status,
    DateOnly? LastCalibration,
    DateOnly? NextCalibration);

/// <summary>Request body to update an equipment's identification data.</summary>
public sealed record UpdateEquipmentRequest(
    string Name,
    string AssetTag,
    string? Brand,
    string? Model,
    Guid? StorageLocationId);

/// <summary>Request body to move an equipment to a new operational status.</summary>
public sealed record ChangeEquipmentStatusRequest(EquipmentStatus Status);

/// <summary>Request body to define/clear an equipment's calibration schedule.</summary>
public sealed record DefineEquipmentCalibrationRequest(
    DateOnly? LastCalibration,
    DateOnly? NextCalibration);

/// <summary>Request body to log a maintenance event.</summary>
public sealed record RecordEquipmentMaintenanceRequest(
    DateOnly Date,
    MaintenanceType Type,
    string? Notes);
