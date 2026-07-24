using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Attachments.Queries;

/// <summary>
/// Read-side query (SISLAB-09) that lists the active company's evidence attachments for an animal, newest first,
/// optionally narrowed to a single reading/analysis (<see cref="TargetKind"/> + <see cref="TargetId"/>). It reads
/// <c>experiments.attachments</c> via Dapper — never the write DbContext — and projects the flat
/// <see cref="AttachmentListItem"/> the evidence gallery needs (its metadata; the bytes are fetched separately through
/// the storage port by key).
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the request,
/// and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global query filter, so the
/// tenant guard is explicit (defense-in-depth, section 7). The animal is always scoped; the target pair is optional.
/// </remarks>
public sealed record ListAttachmentsQuery(Guid AnimalId, string? TargetKind = null, Guid? TargetId = null)
    : IQuery<IReadOnlyList<AttachmentListItem>>;

/// <summary>Flat read row for one evidence attachment. Never leaks the aggregate or its value objects.</summary>
public sealed record AttachmentListItem(
    Guid Id,
    Guid AnimalId,
    string TargetKind,
    Guid TargetId,
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Origin,
    string UploadedBy,
    DateTime UploadedAtUtc);

internal sealed class ListAttachmentsQueryHandler
    : BaseDataAccess, IQueryHandler<ListAttachmentsQuery, IReadOnlyList<AttachmentListItem>>
{
    // Active company's attachments for one animal, newest first, optionally narrowed to a single reading/analysis.
    // company_id keeps the mandatory read-side tenant scoping (the EF global filter does not cover Dapper).
    private const string Sql =
        """
        SELECT
            a.id,
            a.animal_id       AS animalid,
            a.target_kind     AS targetkind,
            a.target_id       AS targetid,
            a.storage_key     AS storagekey,
            a.file_name       AS filename,
            a.content_type    AS contenttype,
            a.size_bytes      AS sizebytes,
            a.origin,
            a.uploaded_by     AS uploadedby,
            a.uploaded_at_utc AS uploadedatutc
        FROM experiments.attachments AS a
        WHERE a.company_id = @CompanyId
          AND a.animal_id = @AnimalId
          AND (@TargetKind IS NULL OR a.target_kind = @TargetKind)
          AND (@TargetId IS NULL OR a.target_id = @TargetId)
        ORDER BY a.uploaded_at_utc DESC, a.id DESC;
        """;

    private readonly ITenantContext _tenantContext;

    public ListAttachmentsQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<AttachmentListItem>> HandleAsync(
        ListAttachmentsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        AttachmentsQueryParameters parameters = BuildParameters(request);

        return (await connection.QueryAsync<AttachmentListItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();
    }

    /// <summary>
    /// Materializes the Dapper parameter set. The company id always comes from <see cref="ITenantContext"/> (never the
    /// request), and a blank target-kind filter collapses to null. Extracted so the tenant guard and filter
    /// normalization are unit-testable without a live database.
    /// </summary>
    internal AttachmentsQueryParameters BuildParameters(ListAttachmentsQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        AnimalId: request.AnimalId,
        TargetKind: string.IsNullOrWhiteSpace(request.TargetKind) ? null : request.TargetKind.Trim(),
        TargetId: request.TargetId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListAttachmentsQuery"/>. Property names match the <c>@Parameter</c>
/// tokens exactly (Dapper binds by name). Exposed to the module's tests so the tenant guard and filter normalization can
/// be asserted without a live database.
/// </summary>
internal sealed record AttachmentsQueryParameters(
    Guid CompanyId,
    Guid AnimalId,
    string? TargetKind,
    Guid? TargetId);
