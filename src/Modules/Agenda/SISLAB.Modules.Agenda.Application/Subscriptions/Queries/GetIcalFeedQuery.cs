using System.Data;
using System.Text.Json;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Agenda.Application.Subscriptions.Queries;

/// <summary>
/// Builds the iCal (RFC 5545) feed for a subscription token (card [E10.10]). This is the public, session-less
/// read path an external calendar client polls, so it is <b>not</b> backed by <c>ITenantContext</c>: the token
/// itself carries the tenant. The handler resolves the token to its (company, user), then every agenda entry of
/// that user in that company becomes one <c>VEVENT</c>. Recurring entries keep their <c>RRULE</c>/<c>EXDATE</c>
/// verbatim — the client expands the series, we never materialise occurrences here.
/// </summary>
public sealed record GetIcalFeedQuery(Guid Token) : IQuery<IcalFeedResult>;

/// <summary>
/// The rendered feed, or <see cref="Found"/> = <see langword="false"/> when the token matches no subscription
/// (so the endpoint can answer 404 without leaking whether a token ever existed).
/// </summary>
public sealed record IcalFeedResult(bool Found, string Content)
{
    public static readonly IcalFeedResult NotFound = new(false, string.Empty);
}

internal sealed class GetIcalFeedQueryHandler
    : BaseDataAccess, IQueryHandler<GetIcalFeedQuery, IcalFeedResult>
{
    // Resolve the token to its (company, user). One row at most — token has a unique index.
    private const string SubscriptionSql =
        """
        SELECT s.company_id AS companyid, s.user_id AS userid
        FROM agenda.ical_subscriptions s
        WHERE s.token = @Token;
        """;

    // The subscriber's own entries in that company. Tenant-scoped explicitly (@CompanyId) since Dapper does not
    // see the EF global filter, and the public request has no tenant context to fall back on.
    private const string EntriesSql =
        """
        SELECT
            e.id              AS id,
            e.title           AS title,
            e.description     AS description,
            e.start_date_utc  AS startdateutc,
            e.end_date_utc    AS enddateutc,
            e.is_all_day      AS isallday,
            e.recurrence_rule AS recurrencerule,
            e.excluded_dates  AS excludeddatesjson
        FROM agenda.agenda_entries e
        WHERE e.company_id = @CompanyId
          AND e.responsible_id = @UserId
        ORDER BY e.start_date_utc;
        """;

    private readonly IcalFeedBuilder _feedBuilder;

    public GetIcalFeedQueryHandler(DbConnectionFactory connectionFactory, IcalFeedBuilder feedBuilder)
        : base(connectionFactory)
    {
        _feedBuilder = feedBuilder;
    }

    public async Task<IcalFeedResult> HandleAsync(
        GetIcalFeedQuery request, CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        SubscriptionRow? subscription = await connection.QuerySingleOrDefaultAsync<SubscriptionRow>(
            new CommandDefinition(SubscriptionSql, new { request.Token }, cancellationToken: cancellationToken));

        if (subscription is null)
            return IcalFeedResult.NotFound;

        IEnumerable<EntryRow> rows = await connection.QueryAsync<EntryRow>(
            new CommandDefinition(
                EntriesSql,
                new { subscription.CompanyId, subscription.UserId },
                cancellationToken: cancellationToken));

        IReadOnlyList<IcalEntry> entries = rows.Select(ToIcalEntry).ToList();
        string content = _feedBuilder.Build(entries);

        return new IcalFeedResult(true, content);
    }

    private static IcalEntry ToIcalEntry(EntryRow row) => new(
        row.Id,
        row.Title,
        row.Description,
        row.StartDateUtc,
        row.EndDateUtc,
        row.IsAllDay,
        row.RecurrenceRule,
        DeserializeExcludedDates(row.ExcludedDatesJson));

    private static IReadOnlyList<DateOnly> DeserializeExcludedDates(string? json)
        => string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<DateOnly>>(json) ?? [];

    private sealed record SubscriptionRow(Guid CompanyId, Guid UserId);

    private sealed record EntryRow(
        Guid Id,
        string Title,
        string? Description,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        bool IsAllDay,
        string? RecurrenceRule,
        string? ExcludedDatesJson);
}

/// <summary>A single agenda entry projected for iCal serialization (card [E10.10]).</summary>
public sealed record IcalEntry(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsAllDay,
    string? RecurrenceRule,
    IReadOnlyList<DateOnly> ExcludedDates);
