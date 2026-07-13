using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Audit.Application.AuditRead;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Audit.Application;

/// <summary>
/// HTTP boundary for the audit trail of the <b>active company</b> (card [E9] #57): a paginated listing and
/// a CSV export, both filterable by entity type, action and an inclusive occurred-at date window. The
/// controller only dispatches CQRS queries through <see cref="IMediator"/> and maps the result; it never
/// touches Dapper or the writer, and never maps errors — those bubble up to the exception-handling middleware.
/// </summary>
/// <remarks>
/// Tenant-scoped: every query runs against the active company resolved from the httpOnly cookie
/// (<c>ITenantContext</c> + read-side <c>WHERE company_id</c>), never from the request. Requires authentication.
/// </remarks>
[Route("api/audit")]
[Authorize]
public sealed class AuditController : SislabControllerBase
{
    private readonly IMediator _mediator;

    public AuditController(IMediator mediator) => _mediator = mediator;

    /// <summary>Lists the company's audit entries, newest first, with optional filters.</summary>
    [HttpGet("entries")]
    [ProducesResponseType(typeof(ApiResult<PagedResult<AuditEntryListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken ct)
    {
        PagedResult<AuditEntryListItem> result = await _mediator.SendAsync(
            new ListAuditEntriesQuery
            {
                EntityType = entityType,
                Action = action,
                From = from,
                To = to,
                Page = page,
                PageSize = pageSize
            },
            ct);

        return Ok(new ApiResult<PagedResult<AuditEntryListItem>>(true, "Audit entries listed.", result));
    }

    /// <summary>Exports the filtered audit trail (no pagination) as a CSV attachment.</summary>
    [HttpGet("entries/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Export(
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        IReadOnlyList<AuditEntryListItem> entries = await _mediator.SendAsync(
            new ExportAuditEntriesQuery
            {
                EntityType = entityType,
                Action = action,
                From = from,
                To = to
            },
            ct);

        string csv = AuditCsvFormatter.ToCsv(entries);
        byte[] bytes = Encoding.UTF8.GetBytes(csv);
        string fileName =
            $"audit-{DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv";

        return File(bytes, HttpConstants.ContentTypes.Csv, fileName);
    }
}
