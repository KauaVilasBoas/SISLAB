using SISLAB.Modules.Experiments.Application.Attachments.Queries;
using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.Modules.Experiments.Tests.Fakes;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Covers the tenant guard / filter normalization of the attachments read-side (SISLAB-09) without a live database. The
/// mandatory <c>WHERE company_id = @CompanyId</c> is only as safe as its parameter, so these pin that the company id
/// always comes from <see cref="SISLAB.SharedKernel.Multitenancy.ITenantContext"/> (never the request) and that the
/// animal scope and optional target filter bind as intended. The SQL body is validated against PostgreSQL by an
/// integration test when Docker is available.
/// </summary>
public sealed class AttachmentReadQueryTests
{
    private static readonly Guid ActiveCompany = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // BuildParameters does not touch the connection factory, so a null factory is never dereferenced here.
    private readonly ListAttachmentsQueryHandler _listHandler =
        new(connectionFactory: null!, new StubTenantContext(ActiveCompany));

    [Fact]
    public void List_query_takes_the_company_from_the_tenant_context_and_keeps_the_animal_scope()
    {
        Guid animalId = Guid.NewGuid();

        AttachmentsQueryParameters parameters = _listHandler.BuildParameters(new ListAttachmentsQuery(animalId));

        Assert.Equal(ActiveCompany, parameters.CompanyId);
        Assert.Equal(animalId, parameters.AnimalId);
        Assert.Null(parameters.TargetKind);
        Assert.Null(parameters.TargetId);
    }

    [Fact]
    public void List_query_binds_the_optional_target_filter_and_collapses_a_blank_kind_to_null()
    {
        Guid animalId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();

        AttachmentsQueryParameters withTarget = _listHandler.BuildParameters(
            new ListAttachmentsQuery(animalId, AttachmentTargetKind.SampleAnalysis.ToString(), targetId));
        Assert.Equal("SampleAnalysis", withTarget.TargetKind);
        Assert.Equal(targetId, withTarget.TargetId);

        AttachmentsQueryParameters blankKind = _listHandler.BuildParameters(
            new ListAttachmentsQuery(animalId, "   ", null));
        Assert.Null(blankKind.TargetKind);
    }
}
