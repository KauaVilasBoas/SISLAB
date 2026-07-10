using SISLAB.Modules.Identity.Application.Administration;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Tests.Administration;

/// <summary>
/// Proves the read handler that backs <c>CompanyMembersController.ListMembers</c>: the controller
/// no longer touches the repository — this handler owns that call and maps the aggregate to DTOs.
/// </summary>
public sealed class ListCompanyMembersQueryHandlerTests
{
    private static readonly Guid CompanyId = new("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid UserA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task HandleAsync_WhenCompanyExists_ReturnsFlattenedMembers()
    {
        Company company = Company.Seed(CompanyId, "LAFTE");
        company.AddMember(UserA);
        company.AddMember(UserB);

        var handler = new ListCompanyMembersQueryHandler(new CompanyRepositoryStub(company));

        ListCompanyMembersResult result =
            await handler.HandleAsync(new ListCompanyMembersQuery(CompanyId));

        Assert.True(result.CompanyExists);
        Assert.Equal(2, result.Members.Count);
        Assert.Contains(result.Members, m => m.UserId == UserA);
        Assert.Contains(result.Members, m => m.UserId == UserB);
    }

    [Fact]
    public async Task HandleAsync_WhenCompanyMissing_ReportsNotFound()
    {
        var handler = new ListCompanyMembersQueryHandler(new CompanyRepositoryStub(company: null));

        ListCompanyMembersResult result =
            await handler.HandleAsync(new ListCompanyMembersQuery(CompanyId));

        Assert.False(result.CompanyExists);
        Assert.Empty(result.Members);
    }
}
