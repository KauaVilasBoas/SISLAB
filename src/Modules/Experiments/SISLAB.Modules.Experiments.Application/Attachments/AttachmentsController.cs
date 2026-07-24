using Lumen.Authorization.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SISLAB.Infrastructure.AspNetCore;
using SISLAB.Modules.Experiments.Application.Attachments.Commands;
using SISLAB.Modules.Experiments.Application.Attachments.Queries;
using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.SharedKernel.Http;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Storage;

namespace SISLAB.Modules.Experiments.Application.Attachments;

/// <summary>
/// HTTP boundary for evidence attachments (SISLAB-09): uploading a photo/PDF of a hemogram laudo or external-reader
/// result to an animal's reading/analysis, listing an animal's evidence, and streaming a file back. The controller
/// adapts the ASP.NET multipart <see cref="IFormFile"/> into the infra-agnostic command/port (a plain
/// <see cref="Stream"/>) and dispatches CQRS requests through <see cref="IMediator"/>; it never touches repositories,
/// the DbContext or Dapper directly, and it resolves file bytes only through the <see cref="IFileStorage"/> port.
/// </summary>
/// <remarks>
/// Tenant-scoped: every request runs against the active company resolved from the httpOnly cookie (write side stamps
/// <c>company_id</c>; the reads keep the mandatory <c>WHERE company_id = @CompanyId</c>), never from the request body.
/// The upload is gated by Lumen's <c>[RequirePermission]</c>; the reads are page-level <c>[Authorize]</c>.
/// </remarks>
[Route("api/attachments")]
[Authorize]
public sealed class AttachmentsController : SislabControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorage _fileStorage;

    public AttachmentsController(IMediator mediator, IFileStorage fileStorage)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
    }

    /// <summary>Lists the active company's evidence for an animal, optionally narrowed to one reading/analysis.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<AttachmentListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] Guid animalId,
        [FromQuery] AttachmentTargetKind? targetKind = null,
        [FromQuery] Guid? targetId = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<AttachmentListItem> items = await _mediator.SendAsync(
            new ListAttachmentsQuery(animalId, targetKind?.ToString(), targetId), ct);

        return Ok(new ApiResult<IReadOnlyList<AttachmentListItem>>(true, "Attachments listed.", items));
    }

    /// <summary>Attaches an uploaded file to an animal's reading/analysis. Returns the new attachment id.</summary>
    [HttpPost]
    [RequirePermission]
    [ProducesResponseType(typeof(ApiResult<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Attach([FromForm] AttachEvidenceRequest body, CancellationToken ct)
    {
        IFormFile file = body.File;

        await using Stream content = file.OpenReadStream();

        Guid id = await _mediator.SendAsync(
            new AttachEvidenceCommand(
                body.AnimalId,
                body.TargetKind,
                body.OwnerId,
                body.TargetId,
                content,
                file.FileName,
                file.ContentType,
                file.Length,
                body.Origin),
            ct);

        return Ok(new ApiResult<Guid>(true, "Evidence attached.", id));
    }

    /// <summary>Streams an attachment's file bytes back to the client, resolved through the storage port by key.</summary>
    [HttpGet("{attachmentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid attachmentId, CancellationToken ct)
    {
        AttachmentDownload download = await _mediator.SendAsync(new GetAttachmentDownloadQuery(attachmentId), ct);

        Stream stream = await _fileStorage.OpenReadAsync(StoredFileKey.Of(download.StorageKey), ct);

        return File(stream, download.ContentType, download.FileName);
    }
}

/// <summary>
/// Multipart request body to attach evidence. The company comes from the session, never the payload; the target
/// descriptor (kind + owner + target ids) proves the anexo↔animal↔análise link the handler validates.
/// </summary>
public sealed class AttachEvidenceRequest
{
    /// <summary>The study animal the evidence belongs to.</summary>
    public Guid AnimalId { get; init; }

    /// <summary>Whether the evidence documents a sample analysis or an experiment reading.</summary>
    public AttachmentTargetKind TargetKind { get; init; }

    /// <summary>The owning aggregate id (biobank sample id or behavioural experiment id) that proves the link.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>The reading/analysis id the evidence documents.</summary>
    public Guid TargetId { get; init; }

    /// <summary>Free-text provenance label (e.g. "Fiocruz", "Leitora externa"). Optional; never hardcoded.</summary>
    public string? Origin { get; init; }

    /// <summary>The uploaded evidence file (photo/PDF).</summary>
    public IFormFile File { get; init; } = default!;
}
