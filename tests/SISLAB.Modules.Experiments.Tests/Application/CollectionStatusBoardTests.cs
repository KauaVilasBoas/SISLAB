using SISLAB.Modules.Experiments.Application.Collection.Queries;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the SISLAB-08 collection status board's <b>derivation</b> (its core acceptance criterion): the board is not a
/// stored status but is composed by matching each planned analysis, by sample type and name, to the biobank's real
/// analyses. These pin the composition (pending/completed counts, the "done" rule, the responsible) and the read-side
/// tenant scoping, without a live database — the SQL bodies themselves are validated against PostgreSQL by the
/// integration test when Docker is available.
/// </summary>
public sealed class CollectionStatusBoardTests
{
    private static readonly Guid Sthe = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static IReadOnlyList<GetCollectionStatusBoardQueryHandler.PlannedRow> Planned(
        params (string type, string name)[] pairs)
        => pairs.Select(p => new GetCollectionStatusBoardQueryHandler.PlannedRow(p.type, p.name)).ToList();

    private static Dictionary<(string, string), GetCollectionStatusBoardQueryHandler.RealFactRow> Facts(
        params GetCollectionStatusBoardQueryHandler.RealFactRow[] rows)
    {
        var map = new Dictionary<(string, string), GetCollectionStatusBoardQueryHandler.RealFactRow>(
            (IEqualityComparer<(string, string)>)GetCollectionStatusBoardQueryHandler.FactKeyComparer);
        foreach (var row in rows)
            map[(row.SampleType, row.AnalysisName)] = row;
        return map;
    }

    [Fact]
    public void Board_derives_pending_and_completed_from_the_real_analyses()
    {
        IReadOnlyList<CollectionStatusRow> rows = GetCollectionStatusBoardQueryHandler.ComposeRows(
            Planned(("Blood", "Hemograma")),
            Facts(new GetCollectionStatusBoardQueryHandler.RealFactRow("Blood", "Hemograma", 4, 1, 3)),
            new Dictionary<string, int>(),
            new Dictionary<string, Guid> { ["Blood"] = Sthe });

        CollectionStatusRow row = Assert.Single(rows);
        Assert.Equal(4, row.CollectedSamples);
        Assert.Equal(1, row.PendingAnalyses);
        Assert.Equal(3, row.CompletedAnalyses);
        Assert.Equal(Sthe, row.ResponsibleUserId);
        Assert.False(row.IsDone);   // still one pending
    }

    [Fact]
    public void Board_marks_done_when_every_real_analysis_is_completed()
    {
        IReadOnlyList<CollectionStatusRow> rows = GetCollectionStatusBoardQueryHandler.ComposeRows(
            Planned(("Blood", "Hemograma")),
            Facts(new GetCollectionStatusBoardQueryHandler.RealFactRow("Blood", "Hemograma", 4, 0, 4)),
            new Dictionary<string, int>(),
            new Dictionary<string, Guid>());

        CollectionStatusRow row = Assert.Single(rows);
        Assert.True(row.IsDone);
        Assert.Null(row.ResponsibleUserId);   // no responsible resolved for the type
    }

    [Fact]
    public void Board_shows_a_planned_analysis_with_no_real_match_as_zeroed_and_not_done()
    {
        // Nervo → MDA is planned but nothing has been run yet; the type's collected count still surfaces.
        IReadOnlyList<CollectionStatusRow> rows = GetCollectionStatusBoardQueryHandler.ComposeRows(
            Planned(("Tissue", "MDA")),
            Facts(),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Tissue"] = 2 },
            new Dictionary<string, Guid>());

        CollectionStatusRow row = Assert.Single(rows);
        Assert.Equal(2, row.CollectedSamples);
        Assert.Equal(0, row.PendingAnalyses);
        Assert.Equal(0, row.CompletedAnalyses);
        Assert.False(row.IsDone);   // nothing completed yet
    }

    [Fact]
    public void Board_matches_type_and_name_case_insensitively()
    {
        IReadOnlyList<CollectionStatusRow> rows = GetCollectionStatusBoardQueryHandler.ComposeRows(
            Planned(("Blood", "Hemograma")),
            Facts(new GetCollectionStatusBoardQueryHandler.RealFactRow("blood", "HEMOGRAMA", 1, 0, 1)),
            new Dictionary<string, int>(),
            new Dictionary<string, Guid>());

        CollectionStatusRow row = Assert.Single(rows);
        Assert.True(row.IsDone);
        Assert.Equal(1, row.CompletedAnalyses);
    }

    [Fact]
    public void Read_side_sql_keeps_the_mandatory_company_scoping()
    {
        Assert.Contains("company_id = @CompanyId", GetCollectionStatusBoardQueryHandler.PlanSql);
        Assert.Contains("company_id = @CompanyId", GetCollectionStatusBoardQueryHandler.RealSql);
        Assert.Contains("company_id = @CompanyId", GetCollectionPlanQueryHandler.PlanSql);
    }
}
