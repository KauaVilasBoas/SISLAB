using Microsoft.Extensions.Configuration;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Experiments.Application.Experiments.Queries;
using SISLAB.Modules.Experiments.Tests.Fakes;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Unit tests for the experiments list-filter parameter building (card [E11]): the tenant guard (company from
/// <see cref="SISLAB.SharedKernel.Multitenancy.ITenantContext"/>, never the request), the multi-select status
/// filter (DP-6), and the multi-select responsible filter — all assertable without a live database.
/// </summary>
public sealed class ListExperimentsQueryParametersTests
{
    private static readonly Guid Company = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    [Fact]
    public void No_filters_yields_the_tenant_company_and_no_status_or_responsible_filter()
    {
        ExperimentsListQueryParameters parameters = BuildHandler().BuildParameters(new ListExperimentsQuery());

        Assert.Equal(Company, parameters.CompanyId);
        Assert.False(parameters.HasStatusFilter);
        Assert.Null(parameters.Statuses);
        Assert.False(parameters.HasResponsibleFilter);
        Assert.Null(parameters.ResponsibleUserIds);
    }

    [Fact]
    public void Status_multi_select_is_trimmed_deduped_and_flagged()
    {
        ExperimentsListQueryParameters parameters = BuildHandler().BuildParameters(new ListExperimentsQuery
        {
            Statuses = [" InProgress ", "InProgress", "AwaitingAnalysis", "  "],
        });

        Assert.True(parameters.HasStatusFilter);
        Assert.Equal(new[] { "InProgress", "AwaitingAnalysis" }, parameters.Statuses);
    }

    [Fact]
    public void Empty_status_list_collapses_to_no_filter()
    {
        ExperimentsListQueryParameters parameters = BuildHandler().BuildParameters(new ListExperimentsQuery
        {
            Statuses = ["   ", ""],
        });

        Assert.False(parameters.HasStatusFilter);
        Assert.Null(parameters.Statuses);
    }

    [Fact]
    public void Responsible_multi_select_drops_empties_dedupes_and_flags()
    {
        ExperimentsListQueryParameters parameters = BuildHandler().BuildParameters(new ListExperimentsQuery
        {
            ResponsibleUserIds = [UserA, UserA, Guid.Empty, UserB],
        });

        Assert.True(parameters.HasResponsibleFilter);
        Assert.Equal(new[] { UserA, UserB }, parameters.ResponsibleUserIds);
    }

    [Fact]
    public void Company_always_comes_from_the_tenant_context()
    {
        ExperimentsListQueryParameters parameters = BuildHandler().BuildParameters(new ListExperimentsQuery
        {
            Statuses = ["Draft"],
            ResponsibleUserIds = [UserA],
        });

        Assert.Equal(Company, parameters.CompanyId);
    }

    private static ListExperimentsQueryHandler BuildHandler()
        => new(BuildConnectionFactory(), new StubTenantContext(Company));

    // The factory is never asked to open a connection here — only BuildParameters is exercised — so a
    // placeholder connection string satisfies the constructor.
    private static DbConnectionFactory BuildConnectionFactory()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SislabDb"] = "Host=localhost;Database=sislab;Username=u;Password=p",
            })
            .Build();

        return new DbConnectionFactory(configuration);
    }
}
