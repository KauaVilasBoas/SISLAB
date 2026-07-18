using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Bioterium.Queries;

/// <summary>
/// Returns the biotério assignments in the given date range (card [E10] #70), ordered by date.
/// Used to render the weekly/monthly schedule view.
/// </summary>
public sealed record GetBioteriumScheduleQuery(DateOnly From, DateOnly To)
    : IQuery<IReadOnlyList<BioteriumAssignmentItem>>;

public sealed record BioteriumAssignmentItem(
    Guid Id,
    DateOnly AssignmentDate,
    string ResponsibleName,
    string Status,
    string? SwappedFromName,
    string? SwapReason,
    string? Notes);

internal sealed class GetBioteriumScheduleQueryHandler
    : BaseDataAccess, IQueryHandler<GetBioteriumScheduleQuery, IReadOnlyList<BioteriumAssignmentItem>>
{
    private const string Sql =
        """
        SELECT
            id,
            assignment_date  AS assignmentdate,
            responsible_name AS responsiblename,
            status,
            swapped_from_name AS swappedfromname,
            swap_reason      AS swapreason,
            notes
        FROM agenda.bioterium_assignments
        WHERE company_id = @CompanyId
          AND assignment_date >= @From
          AND assignment_date <= @To
        ORDER BY assignment_date;
        """;

    private readonly ITenantContext _tenantContext;

    public GetBioteriumScheduleQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<BioteriumAssignmentItem>> HandleAsync(
        GetBioteriumScheduleQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();
        return (await connection.QueryAsync<BioteriumAssignmentItem>(
            new CommandDefinition(Sql,
                new { CompanyId = _tenantContext.CompanyId, request.From, request.To },
                cancellationToken: cancellationToken))).AsList();
    }
}
