using SISLAB.Modules.Experiments.Application.Pendencies.Queries;
using SISLAB.Modules.Experiments.Tests.Fakes;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the tenant guard of the pendencies read-side (card [E11] #90) and the pure in-memory shaping of the
/// per-kind counts — both without a live database. The mandatory <c>WHERE company_id = @CompanyId</c> is only as
/// safe as its parameter, so this pins that the company id always comes from
/// <see cref="SISLAB.SharedKernel.Multitenancy.ITenantContext"/> (never the request). The SQL union is validated
/// against PostgreSQL by the tenant-isolation integration test when Docker is available.
/// </summary>
public sealed class PendenciesQueryTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Query_takes_the_company_from_the_tenant_context()
    {
        // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
        var handler = new GetPendenciesQueryHandler(
            connectionFactory: null!, new StubTenantContext(ActiveCompany));

        PendenciesQueryParameters parameters = handler.BuildParameters();

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Assemble_counts_the_items_per_kind()
    {
        var now = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc);
        var items = new[]
        {
            new PendencyItem("AwaitingCalculation", Guid.NewGuid(), "Von Frey", "Aguardando cálculo", now),
            new PendencyItem("PendingStep", Guid.NewGuid(), "MTT", "Reader import", now),
            new PendencyItem("PendingStep", Guid.NewGuid(), "MTT", "Viability calc", now),
            new PendencyItem("SampleAwaitingAnalysis", Guid.NewGuid(), "S-001", "Amostra…", now),
        };

        PendenciesResult result = GetPendenciesQueryHandler.Assemble(items);

        Assert.Equal(4, result.Items.Count);
        Assert.Equal(1, result.AwaitingCalculationCount);
        Assert.Equal(2, result.PendingStepCount);
        Assert.Equal(1, result.SampleAwaitingAnalysisCount);
    }

    [Fact]
    public void Assemble_of_an_empty_panel_reports_zero_everywhere()
    {
        PendenciesResult result = GetPendenciesQueryHandler.Assemble([]);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.AwaitingCalculationCount);
        Assert.Equal(0, result.PendingStepCount);
        Assert.Equal(0, result.SampleAwaitingAnalysisCount);
    }
}
