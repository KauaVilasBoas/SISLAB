using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Configuration.Application.ItemCategories;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application;

/// <summary>
/// HTTP boundary for the active company's item categories (card [E12] #76): listing and creating the
/// dynamic, per-tenant categories that replaced the retired <c>StockItemCategory</c> enum. The controller
/// only dispatches CQRS requests through <see cref="IMediator"/> and maps the result to the uniform
/// <see cref="ApiResult"/> envelope; it never touches repositories, the DbContext or Dapper.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie, never from
/// the request body. RBAC permissions are layered on in card #77.
/// </remarks>
[Route("api/configuration/item-categories")]
[Authorize]
public sealed class ItemCategoryController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public ItemCategoryController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the active company's item categories, ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<ItemCategoryListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        IReadOnlyList<ItemCategoryListItem> categories =
            await _mediator.SendAsync(new ListItemCategoriesQuery(), ct);

        return Ok(new ApiResult<IReadOnlyList<ItemCategoryListItem>>(true, "Item categories listed.", categories));
    }

    /// <summary>Creates a new item category for the active company.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateItemCategoryRequest body, CancellationToken ct)
    {
        Guid id = await _mediator.SendAsync(
            new CreateItemCategoryCommand(body.Name, body.Aliases, body.IsControlled), ct);

        return Ok(new ApiResult<Guid>(true, "Item category created.", id));
    }
}

/// <summary>Request body for creating an item category.</summary>
public sealed record CreateItemCategoryRequest(
    string Name,
    IReadOnlyList<string>? Aliases,
    bool IsControlled);
