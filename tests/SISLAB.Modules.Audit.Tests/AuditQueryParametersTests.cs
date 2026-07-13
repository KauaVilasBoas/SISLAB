using Microsoft.Extensions.Configuration;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Audit.Application.AuditRead;

namespace SISLAB.Modules.Audit.Tests;

/// <summary>
/// Unit tests for the audit read-side parameter building (card [E9] #57): the tenant guard (company comes
/// from <see cref="ITenantContext"/>, never the request), the optional filters, and the inclusive date
/// window semantics — all assertable without a live database.
/// </summary>
public sealed class AuditQueryParametersTests
{
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void List_takes_the_company_from_the_tenant_context_not_the_request()
    {
        ListAuditEntriesQueryHandler handler = BuildListHandler(Company);

        AuditEntriesQueryParameters parameters = handler.BuildParameters(new ListAuditEntriesQuery());

        Assert.Equal(Company, parameters.CompanyId);
    }

    [Fact]
    public void List_passes_pagination_bounds_from_the_query()
    {
        ListAuditEntriesQueryHandler handler = BuildListHandler(Company);

        var query = new ListAuditEntriesQuery { Page = 3, PageSize = 25 };
        AuditEntriesQueryParameters parameters = handler.BuildParameters(query);

        Assert.Equal(query.FirstResult, parameters.FirstResult);
        Assert.Equal(query.LastResult, parameters.LastResult);
    }

    [Fact]
    public void List_normalizes_blank_filters_to_null()
    {
        ListAuditEntriesQueryHandler handler = BuildListHandler(Company);

        var query = new ListAuditEntriesQuery { EntityType = "  ", Action = "" };
        AuditEntriesQueryParameters parameters = handler.BuildParameters(query);

        Assert.Null(parameters.EntityType);
        Assert.Null(parameters.Action);
    }

    [Fact]
    public void List_trims_provided_filters()
    {
        ListAuditEntriesQueryHandler handler = BuildListHandler(Company);

        var query = new ListAuditEntriesQuery { EntityType = " StockItem ", Action = " consumption " };
        AuditEntriesQueryParameters parameters = handler.BuildParameters(query);

        Assert.Equal("StockItem", parameters.EntityType);
        Assert.Equal("consumption", parameters.Action);
    }

    [Fact]
    public void List_makes_the_To_bound_exclusive_upper_by_adding_one_day()
    {
        ListAuditEntriesQueryHandler handler = BuildListHandler(Company);

        var query = new ListAuditEntriesQuery
        {
            From = new DateOnly(2026, 7, 1),
            To = new DateOnly(2026, 7, 31)
        };
        AuditEntriesQueryParameters parameters = handler.BuildParameters(query);

        // The lower bound is inclusive at the start of the From day; the upper bound is the day AFTER To,
        // so the whole To day is included via a half-open range (occurred_at_utc < @ToExclusive).
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), parameters.From);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), parameters.ToExclusive);
    }

    [Fact]
    public void List_leaves_the_date_window_null_when_not_provided()
    {
        ListAuditEntriesQueryHandler handler = BuildListHandler(Company);

        AuditEntriesQueryParameters parameters = handler.BuildParameters(new ListAuditEntriesQuery());

        Assert.Null(parameters.From);
        Assert.Null(parameters.ToExclusive);
    }

    [Fact]
    public void Export_takes_the_company_from_the_tenant_context()
    {
        ExportAuditEntriesQueryHandler handler = BuildExportHandler(Company);

        AuditEntriesQueryParameters parameters = handler.BuildParameters(new ExportAuditEntriesQuery
        {
            To = new DateOnly(2026, 7, 31)
        });

        Assert.Equal(Company, parameters.CompanyId);
        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), parameters.ToExclusive);
    }

    private static ListAuditEntriesQueryHandler BuildListHandler(Guid company) =>
        new(BuildConnectionFactory(), new StubTenantContext(company));

    private static ExportAuditEntriesQueryHandler BuildExportHandler(Guid company) =>
        new(BuildConnectionFactory(), new StubTenantContext(company));

    // The factory is never asked to open a connection in these tests — only BuildParameters is exercised —
    // so a placeholder connection string is enough to satisfy the constructor.
    private static DbConnectionFactory BuildConnectionFactory()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = "Host=localhost;Database=sislab;Username=u;Password=p"
            })
            .Build();

        return new DbConnectionFactory(configuration);
    }
}
