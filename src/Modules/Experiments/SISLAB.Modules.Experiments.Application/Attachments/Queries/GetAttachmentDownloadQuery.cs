using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Attachments.Queries;

/// <summary>
/// Read-side query (SISLAB-09) that resolves the storage descriptor of a single attachment so the controller can stream
/// its bytes back through the <c>IFileStorage</c> port. It returns only what a download needs — the opaque storage key,
/// the content type and the download file name — never the bytes themselves (those flow through the storage port, not
/// the database).
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the request,
/// and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — an attachment of another tenant simply does not resolve
/// (surfaced as a <see cref="NotFoundException"/>), so the download endpoint cannot leak cross-tenant evidence.
/// </remarks>
public sealed record GetAttachmentDownloadQuery(Guid AttachmentId) : IQuery<AttachmentDownload>;

/// <summary>The storage descriptor a download needs: the opaque key plus how to present the streamed bytes.</summary>
public sealed record AttachmentDownload(string StorageKey, string ContentType, string FileName);

internal sealed class GetAttachmentDownloadQueryHandler
    : BaseDataAccess, IQueryHandler<GetAttachmentDownloadQuery, AttachmentDownload>
{
    private const string Sql =
        """
        SELECT
            a.storage_key  AS storagekey,
            a.content_type AS contenttype,
            a.file_name    AS filename
        FROM experiments.attachments AS a
        WHERE a.company_id = @CompanyId
          AND a.id = @AttachmentId;
        """;

    private readonly ITenantContext _tenantContext;

    public GetAttachmentDownloadQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<AttachmentDownload> HandleAsync(
        GetAttachmentDownloadQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        AttachmentDownload? download = await connection.QuerySingleOrDefaultAsync<AttachmentDownload>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId, request.AttachmentId },
                cancellationToken: cancellationToken));

        return download
            ?? throw new NotFoundException($"Attachment '{request.AttachmentId}' was not found.");
    }
}
