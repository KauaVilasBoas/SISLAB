using SISLAB.Modules.Experiments.Application.Projects.Queries;
using SISLAB.Modules.Experiments.Tests.Fakes;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the tenant guard / filter normalization of the in vivo project read-side (card [E11] #73) and the pure
/// in-memory shaping of the detail tree — both without a live database. The mandatory
/// <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so these pin that the company id always
/// comes from <see cref="SISLAB.SharedKernel.Multitenancy.ITenantContext"/> (never the request). The SQL bodies are
/// validated against PostgreSQL by the tenant-isolation integration test when Docker is available.
/// </summary>
public sealed class ProjectReadQueryTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListProjectsQueryHandler _listHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void List_query_takes_the_company_from_the_tenant_context()
    {
        ProjectsListQueryParameters parameters = _listHandler.BuildParameters(new ListProjectsQuery());

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void List_query_collapses_a_blank_status_filter_to_null()
    {
        ProjectsListQueryParameters parameters =
            _listHandler.BuildParameters(new ListProjectsQuery { Status = "   " });

        Assert.Null(parameters.Status);
    }

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListPhysiologicalReadingsQueryHandler _readingsHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void Readings_query_takes_the_company_from_the_tenant_context()
    {
        PhysiologicalReadingsQueryParameters parameters =
            _readingsHandler.BuildParameters(new ListPhysiologicalReadingsQuery(Guid.NewGuid()));

        Assert.Equal(ActiveCompany, parameters.CompanyId);
    }

    [Fact]
    public void Readings_query_collapses_a_blank_parameter_filter_to_null()
    {
        var projectId = Guid.NewGuid();

        PhysiologicalReadingsQueryParameters parameters = _readingsHandler.BuildParameters(
            new ListPhysiologicalReadingsQuery(projectId) { ParameterCode = "   " });

        Assert.Null(parameters.ParameterCode);
        Assert.Equal(projectId, parameters.ProjectId);
    }

    [Fact]
    public void Assemble_nests_batches_groups_and_animals_by_their_foreign_keys()
    {
        var header = new GetProjectQueryHandler.ProjectHeaderRow(
            Guid.NewGuid(), "P", "Rat", "d", "Active", CurrentDesignVersion: 2);

        Guid batchId = Guid.NewGuid();
        Guid controlId = Guid.NewGuid();
        Guid doseId = Guid.NewGuid();
        Guid modelId = Guid.NewGuid();

        var batches = new[] { new GetProjectQueryHandler.BatchRow(batchId, "Leva 1", 1, "Running", modelId) };
        var groups = new[]
        {
            new GetProjectQueryHandler.GroupRow(controlId, batchId, "Controle", 0m, "mg/kg"),
            new GetProjectQueryHandler.GroupRow(doseId, batchId, "Dose 10", 10m, "mg/kg"),
        };
        var animals = new[]
        {
            new GetProjectQueryHandler.AnimalRow(Guid.NewGuid(), controlId, "M1-01", "Male", 250m),
            new GetProjectQueryHandler.AnimalRow(Guid.NewGuid(), doseId, "M1-02", "Female", null),
        };

        ProjectDetail detail = GetProjectQueryHandler.Assemble(header, batches, groups, animals);

        BatchDetail batch = Assert.Single(detail.Batches);
        Assert.Equal(modelId, batch.ExperimentalModelId);
        Assert.Equal(2, batch.Groups.Count);
        Assert.Equal("M1-01", Assert.Single(batch.Groups.Single(g => g.Id == controlId).Animals).Identifier);
        Assert.Equal("M1-02", Assert.Single(batch.Groups.Single(g => g.Id == doseId).Animals).Identifier);
    }
}
