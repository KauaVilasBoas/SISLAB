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

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListAnimalSelectionQueryHandler _selectionHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void Selection_query_takes_the_company_from_the_tenant_context_and_keeps_the_ids()
    {
        var projectId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        AnimalSelectionQueryParameters parameters = _selectionHandler.BuildParameters(
            new ListAnimalSelectionQuery(projectId, batchId) { Status = "  Included  " });

        Assert.Equal(ActiveCompany, parameters.CompanyId);
        Assert.Equal(projectId, parameters.ProjectId);
        Assert.Equal(batchId, parameters.BatchId);
        Assert.Equal("Included", parameters.Status);
    }

    [Fact]
    public void Selection_query_collapses_a_blank_status_filter_to_null()
    {
        AnimalSelectionQueryParameters parameters = _selectionHandler.BuildParameters(
            new ListAnimalSelectionQuery(Guid.NewGuid(), Guid.NewGuid()) { Status = "   " });

        Assert.Null(parameters.Status);
    }

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
    public void Assemble_nests_batches_groups_cages_and_animals_by_their_foreign_keys()
    {
        var header = new GetProjectQueryHandler.ProjectHeaderRow(
            Guid.NewGuid(), "P", "Rat", "d", "Active", CurrentDesignVersion: 2);

        Guid batchId = Guid.NewGuid();
        Guid controlId = Guid.NewGuid();
        Guid doseId = Guid.NewGuid();
        Guid cageId = Guid.NewGuid();
        Guid modelId = Guid.NewGuid();

        var batches = new[] { new GetProjectQueryHandler.BatchRow(batchId, "Leva 1", 1, "Running", modelId) };
        var groups = new[]
        {
            new GetProjectQueryHandler.GroupRow(controlId, batchId, "Controle", 0m, "mg/kg"),
            new GetProjectQueryHandler.GroupRow(doseId, batchId, "Dose 10", 10m, "mg/kg"),
        };
        var cages = new[] { new GetProjectQueryHandler.CageRow(cageId, batchId, "CX1", 4) };
        var animals = new[]
        {
            // One animal assigned to control, one still unassigned (group_id null) — both housed in the same cage.
            new GetProjectQueryHandler.AnimalRow(Guid.NewGuid(), cageId, "CX1-A1", "Male", 250m, controlId),
            new GetProjectQueryHandler.AnimalRow(Guid.NewGuid(), cageId, "CX1-A2", "Female", null, null),
        };

        ProjectDetail detail = GetProjectQueryHandler.Assemble(header, batches, groups, cages, animals);

        BatchDetail batch = Assert.Single(detail.Batches);
        Assert.Equal(modelId, batch.ExperimentalModelId);
        Assert.Equal(2, batch.Groups.Count);
        CageDetail cage = Assert.Single(batch.Cages);
        Assert.Equal(4, cage.Capacity);
        Assert.Equal(2, cage.Animals.Count);
        Assert.Equal(controlId, cage.Animals.Single(a => a.Identifier == "CX1-A1").GroupId);
        Assert.Null(cage.Animals.Single(a => a.Identifier == "CX1-A2").GroupId);
    }

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListBaselineByCageQueryHandler _baselineByCageHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListBaselineByGroupQueryHandler _baselineByGroupHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void Baseline_by_cage_query_takes_the_company_from_the_tenant_context_and_trims_the_parameter()
    {
        var projectId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        BaselineByCageQueryParameters parameters = _baselineByCageHandler.BuildParameters(
            new ListBaselineByCageQuery(projectId, batchId, "  glicemia  ") { TimepointLabel = "  basal  " });

        Assert.Equal(ActiveCompany, parameters.CompanyId);
        Assert.Equal(projectId, parameters.ProjectId);
        Assert.Equal(batchId, parameters.BatchId);
        Assert.Equal("glicemia", parameters.ParameterCode);
        Assert.Equal("basal", parameters.TimepointLabel);
    }

    [Fact]
    public void Baseline_by_cage_query_collapses_a_blank_timepoint_to_null()
    {
        BaselineByCageQueryParameters parameters = _baselineByCageHandler.BuildParameters(
            new ListBaselineByCageQuery(Guid.NewGuid(), Guid.NewGuid(), "glicemia") { TimepointLabel = "   " });

        Assert.Null(parameters.TimepointLabel);
    }

    [Fact]
    public void Baseline_by_group_query_takes_the_company_from_the_tenant_context()
    {
        BaselineByGroupQueryParameters parameters = _baselineByGroupHandler.BuildParameters(
            new ListBaselineByGroupQuery(Guid.NewGuid(), Guid.NewGuid(), "glicemia"));

        Assert.Equal(ActiveCompany, parameters.CompanyId);
        Assert.Equal("glicemia", parameters.ParameterCode);
        Assert.Null(parameters.TimepointLabel);
    }
}
