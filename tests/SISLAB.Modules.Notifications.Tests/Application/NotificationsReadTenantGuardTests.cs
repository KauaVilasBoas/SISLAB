using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Notifications.Application.NotificationsRead;
using Microsoft.Extensions.Configuration;

namespace SISLAB.Modules.Notifications.Tests.Application;

/// <summary>
/// Guards that the read-side Dapper handlers of the bell take the company from <see cref="ITenantContext"/>
/// (never the request) — the read side has no EF global query filter, so the tenant scoping must be explicit
/// (defense-in-depth, section 7). Asserted through each handler's <c>BuildParameters</c> without a live
/// database, complementing the static SQL guard.
/// </summary>
public sealed class NotificationsReadTenantGuardTests
{
    private static readonly Guid Company = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // A connection factory is required by the base ctor but never opened (BuildParameters does not touch it).
    private static readonly DbConnectionFactory UnusedFactory =
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = "Host=localhost;Database=unused;Username=u;Password=p"
            })
            .Build());

    [Fact]
    public void List_query_takes_the_company_from_the_tenant_context_and_carries_the_unread_filter()
    {
        var handler = new ListNotificationsQueryHandler(UnusedFactory, new StubTenantContext(Company));

        ListNotificationsQueryParameters parameters =
            handler.BuildParameters(new ListNotificationsQuery { UnreadOnly = true, Page = 2, PageSize = 10 });

        Assert.Equal(Company, parameters.CompanyId);
        Assert.True(parameters.UnreadOnly);
        Assert.Equal(11, parameters.FirstResult);
        Assert.Equal(20, parameters.LastResult);
    }

    [Fact]
    public void Count_query_takes_the_company_from_the_tenant_context()
    {
        var handler = new CountUnreadNotificationsQueryHandler(UnusedFactory, new StubTenantContext(Company));

        CountUnreadNotificationsQueryParameters parameters = handler.BuildParameters();

        Assert.Equal(Company, parameters.CompanyId);
    }

    [Fact]
    public void List_query_sql_scopes_to_the_active_company()
        => AssertContainsTenantPredicate(GetSqlConstant(typeof(ListNotificationsQueryHandler)));

    [Fact]
    public void Count_query_sql_scopes_to_the_active_company()
        => AssertContainsTenantPredicate(GetSqlConstant(typeof(CountUnreadNotificationsQueryHandler)));

    private static void AssertContainsTenantPredicate(string sql)
        => Assert.Contains("company_id = @CompanyId", sql, StringComparison.OrdinalIgnoreCase);

    private static string GetSqlConstant(Type handler)
    {
        System.Reflection.FieldInfo field = handler.GetField(
            "Sql",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)field.GetValue(null)!;
    }
}
