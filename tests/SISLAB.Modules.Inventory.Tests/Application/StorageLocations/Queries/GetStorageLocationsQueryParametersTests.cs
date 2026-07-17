using SISLAB.Modules.Inventory.Application.StorageLocations.Queries;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Inventory.Tests.Application.StorageLocations.Queries;

/// <summary>
/// Covers the tenant guard of the flat storage-location management query (card [E7] #112) without a live
/// database: the Dapper parameter set it builds is asserted directly. The mandatory
/// <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so this pins that the company id
/// always comes from <c>ITenantContext</c>, never the request. The SQL body itself is validated against
/// PostgreSQL by a smoke test.
/// </summary>
public sealed class GetStorageLocationsQueryParametersTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // BuildParameters never touches the connection factory, so a null factory is never dereferenced here.
    private readonly GetStorageLocationsQueryHandler _handler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void Takes_the_company_from_the_tenant_context()
    {
        StorageLocationsQueryParameters parameters = _handler.BuildParameters();

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }
}
