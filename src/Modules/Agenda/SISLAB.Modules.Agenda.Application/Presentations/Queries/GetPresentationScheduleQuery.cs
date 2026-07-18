using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Presentations.Queries;

/// <summary>Returns presentations in the given date range (card [E10] #71 — semester/month schedule view).</summary>
public sealed record GetPresentationScheduleQuery(DateOnly From, DateOnly To)
    : IQuery<IReadOnlyList<PresentationListItem>>;

public sealed record PresentationListItem(
    Guid Id,
    string Type,
    string Title,
    string? Doi,
    string PresenterName,
    DateOnly ScheduledDate,
    string Status,
    bool ReminderSent,
    string? Notes);

internal sealed class GetPresentationScheduleQueryHandler
    : BaseDataAccess, IQueryHandler<GetPresentationScheduleQuery, IReadOnlyList<PresentationListItem>>
{
    private const string Sql =
        """
        SELECT
            id,
            type,
            title,
            doi,
            presenter_name  AS presentername,
            scheduled_date  AS scheduleddate,
            status,
            (reminder_sent_at IS NOT NULL) AS remindersent,
            notes
        FROM agenda.presentations
        WHERE company_id = @CompanyId
          AND scheduled_date >= @From
          AND scheduled_date <= @ToDate
        ORDER BY scheduled_date;
        """;

    private readonly ITenantContext _tenantContext;

    public GetPresentationScheduleQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<PresentationListItem>> HandleAsync(
        GetPresentationScheduleQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();
        return (await connection.QueryAsync<PresentationListItem>(
            new CommandDefinition(Sql,
                new { CompanyId = _tenantContext.CompanyId, request.From, ToDate = request.To },
                cancellationToken: cancellationToken))).AsList();
    }
}
