using SISLAB.Modules.Inventory.Application.PartnerRead;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Inventory.Tests.Application.PartnerRead;

/// <summary>
/// Covers the tenant guard and the filter normalization of the E4 #28 partner read-side query handlers, without a
/// live database: the Dapper parameter set they build is asserted directly. The mandatory
/// <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so these tests pin that the company id
/// always comes from <see cref="ITenantContext"/> (never the request), that the type filter is carried as its
/// enum name and that a blank search collapses to null. The SQL body itself is validated against PostgreSQL by the
/// tenant-isolation integration test (when Docker is available).
/// </summary>
public sealed class PartnerReadQueryParametersTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListPartnersQueryHandler _listHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    private readonly GetPartnerDetailQueryHandler _detailHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void List_query_takes_the_company_from_the_tenant_context()
    {
        PartnerListQueryParameters parameters = _listHandler.BuildParameters(new ListPartnersQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void List_query_hides_inactive_partners_by_default()
    {
        PartnerListQueryParameters parameters = _listHandler.BuildParameters(new ListPartnersQuery());

        Assert.False(parameters.IncludeInactive);
    }

    [Fact]
    public void List_query_passes_the_include_inactive_flag_through()
    {
        PartnerListQueryParameters parameters =
            _listHandler.BuildParameters(new ListPartnersQuery { IncludeInactive = true });

        Assert.True(parameters.IncludeInactive);
    }

    [Fact]
    public void List_query_leaves_the_type_filter_null_by_default()
    {
        PartnerListQueryParameters parameters = _listHandler.BuildParameters(new ListPartnersQuery());

        Assert.Null(parameters.Type);
    }

    [Theory]
    [InlineData(PartnerType.Supplier)]
    [InlineData(PartnerType.Client)]
    [InlineData(PartnerType.Both)]
    public void List_query_maps_the_type_filter_to_its_enum_name(PartnerType type)
    {
        PartnerListQueryParameters parameters =
            _listHandler.BuildParameters(new ListPartnersQuery { Type = type });

        Assert.Equal(type.ToString(), parameters.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void List_query_collapses_a_blank_search_to_null(string? blank)
    {
        PartnerListQueryParameters parameters =
            _listHandler.BuildParameters(new ListPartnersQuery { Search = blank });

        Assert.Null(parameters.Search);
    }

    [Fact]
    public void List_query_trims_a_populated_search()
    {
        PartnerListQueryParameters parameters =
            _listHandler.BuildParameters(new ListPartnersQuery { Search = "  Merck  " });

        Assert.Equal("Merck", parameters.Search);
    }

    [Fact]
    public void List_query_maps_pagination_bounds()
    {
        PartnerListQueryParameters parameters =
            _listHandler.BuildParameters(new ListPartnersQuery { Page = 2, PageSize = 15 });

        Assert.Equal(16, parameters.FirstResult); // (2-1)*15 + 1
        Assert.Equal(30, parameters.LastResult);  // 2*15
    }

    [Fact]
    public void Detail_query_takes_the_company_from_the_tenant_context()
    {
        PartnerDetailQueryParameters parameters =
            _detailHandler.BuildParameters(new GetPartnerDetailQuery(Guid.NewGuid()));

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Detail_query_passes_the_requested_id_through()
    {
        Guid partnerId = Guid.NewGuid();

        PartnerDetailQueryParameters parameters =
            _detailHandler.BuildParameters(new GetPartnerDetailQuery(partnerId));

        Assert.Equal(partnerId, parameters.PartnerId);
    }
}
