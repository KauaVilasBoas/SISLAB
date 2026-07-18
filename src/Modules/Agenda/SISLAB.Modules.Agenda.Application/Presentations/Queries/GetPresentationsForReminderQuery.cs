using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Application.Presentations.Queries;

/// <summary>
/// Returns presentations that are scheduled within <paramref name="WithinDays"/> days of today and have
/// not yet had a reminder sent (card [E6] #83). Used by <c>PresentationReminderJob</c> to determine which
/// presentations need the 15-day advance notification. Tenant-scoped.
/// </summary>
public sealed record GetPresentationsForReminderQuery(DateOnly Today, int WithinDays)
    : IQuery<IReadOnlyList<PresentationReminderItem>>;

public sealed record PresentationReminderItem(
    Guid Id,
    string Title,
    string PresenterName,
    DateOnly ScheduledDate);

internal sealed class GetPresentationsForReminderQueryHandler
    : BaseDataAccess, IQueryHandler<GetPresentationsForReminderQuery, IReadOnlyList<PresentationReminderItem>>
{
    private const string Sql =
        """
        SELECT id, title, presenter_name AS presentername, scheduled_date AS scheduleddate
        FROM agenda.presentations
        WHERE company_id = @CompanyId
          AND status = 'Scheduled'
          AND reminder_sent_at IS NULL
          AND scheduled_date >= @Today
          AND scheduled_date <= @Threshold
        ORDER BY scheduled_date;
        """;

    private readonly ITenantContext _tenantContext;

    public GetPresentationsForReminderQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory) => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<PresentationReminderItem>> HandleAsync(
        GetPresentationsForReminderQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();
        DateOnly threshold = request.Today.AddDays(request.WithinDays);
        return (await connection.QueryAsync<PresentationReminderItem>(
            new CommandDefinition(Sql,
                new { CompanyId = _tenantContext.CompanyId, request.Today, Threshold = threshold },
                cancellationToken: cancellationToken))).AsList();
    }
}
