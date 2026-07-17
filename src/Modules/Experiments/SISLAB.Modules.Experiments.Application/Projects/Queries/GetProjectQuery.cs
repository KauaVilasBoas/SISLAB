using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Application.Projects.Queries;

/// <summary>
/// Read-side query (card [E11] #73) that returns a single project's full delineation for the active company: its
/// header, its batches, and — nested per batch — the groups and their animals. Reads <c>experiments.*</c> via
/// Dapper in one round-trip (four result sets), never the write DbContext.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> Every SELECT keeps <c>WHERE company_id = @CompanyId</c> (child tables scope by joining
/// back to the tenant-checked project), so the read-side tenant guard is explicit. A project that does not exist
/// for the active company yields a <see cref="NotFoundException"/>.
/// </remarks>
public sealed record GetProjectQuery(Guid ProjectId) : IQuery<ProjectDetail>;

/// <summary>Full detail of a project: header + batches (each with its groups and animals).</summary>
public sealed record ProjectDetail(
    Guid Id,
    string Name,
    string Species,
    string? Description,
    string Status,
    int CurrentDesignVersion,
    IReadOnlyList<BatchDetail> Batches);

/// <summary>A batch on the project detail page, with its groups.</summary>
public sealed record BatchDetail(
    Guid Id,
    string Name,
    int DesignVersion,
    string Status,
    IReadOnlyList<GroupDetail> Groups);

/// <summary>A dose group on the project detail page, with its animals.</summary>
public sealed record GroupDetail(
    Guid Id,
    string Name,
    decimal DoseAmount,
    string DoseUnit,
    IReadOnlyList<AnimalDetail> Animals);

/// <summary>An enrolled animal on the project detail page.</summary>
public sealed record AnimalDetail(
    Guid Id,
    string Identifier,
    string Sex,
    decimal? WeightGrams);

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
            b.status
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

    private const string AnimalsSql =
        """
        SELECT
            a.id,
            a.group_id     AS groupid,
            a.identifier,
            a.sex,
            a.weight_grams AS weightgrams
        FROM experiments.project_animals AS a
        INNER JOIN experiments.project_groups AS g ON g.id = a.group_id
        INNER JOIN experiments.project_batches AS b ON b.id = g.batch_id
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

        IReadOnlyList<AnimalRow> animalRows = (await connection.QueryAsync<AnimalRow>(
            new CommandDefinition(AnimalsSql, parameters, cancellationToken: cancellationToken))).AsList();

        return Assemble(header, batchRows, groupRows, animalRows);
    }

    /// <summary>
    /// Stitches the four flat result sets into the nested detail tree in memory (project → batches → groups →
    /// animals). Kept as a pure static method so the shaping is unit-testable without a live database.
    /// </summary>
    internal static ProjectDetail Assemble(
        ProjectHeaderRow header,
        IReadOnlyList<BatchRow> batchRows,
        IReadOnlyList<GroupRow> groupRows,
        IReadOnlyList<AnimalRow> animalRows)
    {
        ILookup<Guid, AnimalDetail> animalsByGroup = animalRows.ToLookup(
            a => a.GroupId,
            a => new AnimalDetail(a.Id, a.Identifier, a.Sex, a.WeightGrams));

        ILookup<Guid, GroupDetail> groupsByBatch = groupRows.ToLookup(
            g => g.BatchId,
            g => new GroupDetail(
                g.Id,
                g.Name,
                g.DoseAmount,
                g.DoseUnit,
                animalsByGroup[g.Id].ToList()));

        IReadOnlyList<BatchDetail> batches = batchRows
            .Select(b => new BatchDetail(
                b.Id,
                b.Name,
                b.DesignVersion,
                b.Status,
                groupsByBatch[b.Id].ToList()))
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

    internal sealed record BatchRow(Guid Id, string Name, int DesignVersion, string Status);

    internal sealed record GroupRow(Guid Id, Guid BatchId, string Name, decimal DoseAmount, string DoseUnit);

    internal sealed record AnimalRow(Guid Id, Guid GroupId, string Identifier, string Sex, decimal? WeightGrams);
}
