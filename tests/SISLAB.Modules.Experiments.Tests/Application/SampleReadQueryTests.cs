using SISLAB.Modules.Experiments.Application.Biobank.Queries;
using SISLAB.Modules.Experiments.Tests.Fakes;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the tenant guard / filter normalization of the biobank read-side (card [E11] #89) without a live
/// database. The mandatory <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so these pin
/// that the company id always comes from <see cref="SISLAB.SharedKernel.Multitenancy.ITenantContext"/> (never the
/// request). The SQL bodies (including the derived balance) are validated against PostgreSQL by the
/// tenant-isolation integration test when Docker is available.
/// </summary>
public sealed class SampleReadQueryTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListSamplesQueryHandler _listHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void List_query_takes_the_company_from_the_tenant_context()
    {
        SamplesListQueryParameters parameters = _listHandler.BuildParameters(new ListSamplesQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void List_query_collapses_a_blank_type_filter_to_null_and_keeps_the_project_filter()
    {
        Guid projectId = Guid.NewGuid();

        SamplesListQueryParameters parameters = _listHandler.BuildParameters(
            new ListSamplesQuery { Type = "   ", ProjectId = projectId });

        Assert.Null(parameters.Type);
        Assert.Equal(projectId, parameters.ProjectId);
    }
}
