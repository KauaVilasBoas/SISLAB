using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Projects.Queries;

/// <summary>
/// Read-side query (card [E11] #73, SISLAB-03) that returns a single project's full delineation for the active
/// company: its header, its batches, and — nested per batch — the treatment groups (dose definitions) and the cages
/// (caixas) with the animals they house. Each animal carries its optional group assignment (by value). Reads
/// <c>experiments.*</c> via Dapper in one round-trip, never the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> Every SELECT keeps <c>WHERE company_id = @CompanyId</c> (child tables scope by joining
/// back to the tenant-checked project), so the read-side tenant guard is explicit. A project that does not exist
/// for the active company yields a <see cref="NotFoundException"/>.
/// </remarks>
public sealed record GetProjectQuery(Guid ProjectId) : IQuery<ProjectDetail>;

/// <summary>Full detail of a project: header + batches (each with its groups and cages).</summary>
public sealed record ProjectDetail(
    Guid Id,
    string Name,
    string Species,
    string? Description,
    string Status,
    int CurrentDesignVersion,
    IReadOnlyList<BatchDetail> Batches);

/// <summary>A batch on the project detail page, with its groups, its cages and its bound experimental model (SISLAB-04).</summary>
public sealed record BatchDetail(
    Guid Id,
    string Name,
    int DesignVersion,
    string Status,
    Guid? ExperimentalModelId,
    IReadOnlyList<GroupDetail> Groups,
    IReadOnlyList<CageDetail> Cages);

/// <summary>A dose group (treatment definition) on the project detail page.</summary>
public sealed record GroupDetail(
    Guid Id,
    string Name,
    decimal DoseAmount,
    string DoseUnit);

/// <summary>A cage (caixa) on the project detail page, with the animals it houses.</summary>
public sealed record CageDetail(
    Guid Id,
    string Name,
    int? Capacity,
    IReadOnlyList<AnimalDetail> Animals);

/// <summary>An animal housed in a cage, with its optional treatment-group assignment (SISLAB-03).</summary>
public sealed record AnimalDetail(
    Guid Id,
    string Identifier,
    string Sex,
    decimal? WeightGrams,
    Guid? GroupId);

internal sealed class GetProjectQueryHandler
    : BaseDataAccess, IQueryHandler<GetProjectQuery, ProjectDetail>
{
    private const string HeaderSql =
        """
        SELECT
            p.id,
            p.name,
            p.species,
            p.description,
            p.status,
            p.current_design_version AS currentdesignversion
        FROM experiments.projects AS p
        WHERE p.company_id = @CompanyId
          AND p.id = @ProjectId;
        """;

    private const string BatchesSql =
        """
        SELECT
            b.id,
            b.name,
            b.design_version AS designversion,
            b.status,
            b.experimental_model_id AS experimentalmodelid
        FROM experiments.project_batches AS b
        INNER JOIN experiments.projects AS p ON p.id = b.project_id
        WHERE p.company_id = @CompanyId
          AND b.project_id = @ProjectId
        ORDER BY b.design_version ASC, b.name ASC;
        """;

    private const string GroupsSql =
        """
        SELECT
            g.id,
            g.batch_id     AS batchid,
            g.name,
            g.dose_amount  AS doseamount,
            g.dose_unit    AS doseunit
        FROM experiments.project_groups AS g
        INNER JOIN experiments.project_batches AS b ON b.id = g.batch_id
        INNER JOIN experiments.projects AS p ON p.id = b.project_id
        WHERE p.company_id = @CompanyId
          AND b.project_id = @ProjectId
        ORDER BY g.dose_amount ASC, g.name ASC;
        """;

    private const string CagesSql =
        """
        SELECT
            c.id,
            c.batch_id AS batchid,
            c.name,
            c.capacity
        FROM experiments.project_cages AS c
        INNER JOIN experiments.project_batches AS b ON b.id = c.batch_id
        INNER JOIN experiments.projects AS p ON p.id = b.project_id
        WHERE p.company_id = @CompanyId
          AND b.project_id = @ProjectId
        ORDER BY c.name ASC;
        """;

    private const string AnimalsSql =
        """
        SELECT
            a.id,
            a.cage_id      AS cageid,
            a.identifier,
            a.sex,
            a.weight_grams AS weightgrams,
            a.group_id     AS groupid
        FROM experiments.project_animals AS a
        INNER JOIN experiments.project_cages AS c ON c.id = a.cage_id
        INNER JOIN experiments.project_batches AS b ON b.id = c.batch_id
        INNER JOIN experiments.projects AS p ON p.id = b.project_id
        WHERE p.company_id = @CompanyId
          AND b.project_id = @ProjectId
        ORDER BY a.identifier ASC;
        """;

    private readonly ITenantContext _tenantContext;

    public GetProjectQueryHandler(DbConnectionFactory connectionFactory, ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<ProjectDetail> HandleAsync(GetProjectQuery request, CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        var parameters = new { CompanyId = _tenantContext.CompanyId, request.ProjectId };

        ProjectHeaderRow header = await connection.QuerySingleOrDefaultAsync<ProjectHeaderRow>(
            new CommandDefinition(HeaderSql, parameters, cancellationToken: cancellationToken))
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        IReadOnlyList<BatchRow> batchRows = (await connection.QueryAsync<BatchRow>(
            new CommandDefinition(BatchesSql, parameters, cancellationToken: cancellationToken))).AsList();

        IReadOnlyList<GroupRow> groupRows = (await connection.QueryAsync<GroupRow>(
            new CommandDefinition(GroupsSql, parameters, cancellationToken: cancellationToken))).AsList();

        IReadOnlyList<CageRow> cageRows = (await connection.QueryAsync<CageRow>(
            new CommandDefinition(CagesSql, parameters, cancellationToken: cancellationToken))).AsList();

        IReadOnlyList<AnimalRow> animalRows = (await connection.QueryAsync<AnimalRow>(
            new CommandDefinition(AnimalsSql, parameters, cancellationToken: cancellationToken))).AsList();

        return Assemble(header, batchRows, groupRows, cageRows, animalRows);
    }

    /// <summary>
    /// Stitches the five flat result sets into the nested detail tree in memory (project → batches → {groups, cages →
    /// animals}). Kept as a pure static method so the shaping is unit-testable without a live database.
    /// </summary>
    internal static ProjectDetail Assemble(
        ProjectHeaderRow header,
        IReadOnlyList<BatchRow> batchRows,
        IReadOnlyList<GroupRow> groupRows,
        IReadOnlyList<CageRow> cageRows,
        IReadOnlyList<AnimalRow> animalRows)
    {
        ILookup<Guid, AnimalDetail> animalsByCage = animalRows.ToLookup(
            a => a.CageId,
            a => new AnimalDetail(a.Id, a.Identifier, a.Sex, a.WeightGrams, a.GroupId));

        ILookup<Guid, GroupDetail> groupsByBatch = groupRows.ToLookup(
            g => g.BatchId,
            g => new GroupDetail(g.Id, g.Name, g.DoseAmount, g.DoseUnit));

        ILookup<Guid, CageDetail> cagesByBatch = cageRows.ToLookup(
            c => c.BatchId,
            c => new CageDetail(c.Id, c.Name, c.Capacity, animalsByCage[c.Id].ToList()));

        IReadOnlyList<BatchDetail> batches = batchRows
            .Select(b => new BatchDetail(
                b.Id,
                b.Name,
                b.DesignVersion,
                b.Status,
                b.ExperimentalModelId,
                groupsByBatch[b.Id].ToList(),
                cagesByBatch[b.Id].ToList()))
            .ToList();

        return new ProjectDetail(
            header.Id,
            header.Name,
            header.Species,
            header.Description,
            header.Status,
            header.CurrentDesignVersion,
            batches);
    }

    internal sealed record ProjectHeaderRow(
        Guid Id,
        string Name,
        string Species,
        string? Description,
        string Status,
        int CurrentDesignVersion);

    internal sealed record BatchRow(Guid Id, string Name, int DesignVersion, string Status, Guid? ExperimentalModelId);

    internal sealed record GroupRow(Guid Id, Guid BatchId, string Name, decimal DoseAmount, string DoseUnit);

    internal sealed record CageRow(Guid Id, Guid BatchId, string Name, int? Capacity);

    internal sealed record AnimalRow(Guid Id, Guid CageId, string Identifier, string Sex, decimal? WeightGrams, Guid? GroupId);
}
