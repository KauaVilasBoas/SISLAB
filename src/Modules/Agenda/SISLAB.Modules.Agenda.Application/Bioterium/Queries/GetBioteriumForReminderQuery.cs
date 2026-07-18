using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Bioterium.Queries;

/// <summary>
/// Returns pending biotério assignments in the given date range (card [E6] #83). Used by the
/// <c>BioteriumReminderJob</c> to notify each week's responsible on the Monday of the assignment week.
/// </summary>
public sealed record GetBioteriumForReminderQuery(DateOnly From, DateOnly To)
    : IQuery<IReadOnlyList<BioteriumReminderItem>>;

public sealed record BioteriumReminderItem(
    Guid Id,
    DateOnly AssignmentDate,
    string ResponsibleName);

internal sealed class GetBioteriumForReminderQueryHandler
    : BaseDataAccess, IQueryHandler<GetBioteriumForReminderQuery, IReadOnlyList<BioteriumReminderItem>>
{
    private const string Sql =
        """
        SELECT id, assignment_date AS assignmentdate, responsible_name AS responsiblename
        FROM agenda.bioterium_assignments
        WHERE company_id = @CompanyId
          AND assignment_date >= @From
          AND assignment_date <= @ToDate
          AND status = 'Pending'
        ORDER BY assignment_date;
        """;

    private readonly ITenantContext _tenantContext;

    public GetBioteriumForReminderQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<BioteriumReminderItem>> HandleAsync(
        GetBioteriumForReminderQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();
        return (await connection.QueryAsync<BioteriumReminderItem>(
            new CommandDefinition(Sql,
                new { CompanyId = _tenantContext.CompanyId, request.From, ToDate = request.To },
                cancellationToken: cancellationToken))).AsList();
    }
}
