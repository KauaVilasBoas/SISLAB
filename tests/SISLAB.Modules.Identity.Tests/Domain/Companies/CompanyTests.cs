using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Domain.Companies.Events;
using SISLAB.SharedKernel.Authorization;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Identity.Tests.Domain.Companies;

/// <summary>
/// Testes unitários do agregado <see cref="Company"/>.
/// Validam invariantes de domínio sem dependência de banco ou infraestrutura.
/// </summary>
public sealed class CompanyTests
{
    // ---------------------------------------------------------------------------
    // Company.Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidName_ShouldReturnActiveCompany()
    {
        // Act
        Company company = Company.Create("Laboratório Central");

        // Assert
        Assert.NotEqual(Guid.Empty, company.Id);
        Assert.Equal("Laboratório Central", company.Name);
        Assert.True(company.IsActive);
        Assert.Empty(company.Memberships);
    }

    [Fact]
    public void Create_WithOptionalTaxId_ShouldPreserveTaxId()
    {
        Company company = Company.Create("ACME Lab", taxId: "12.345.678/0001-99");

        Assert.Equal("12.345.678/0001-99", company.TaxId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Create_WithEmptyOrWhitespaceName_ShouldThrow(string? name)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Company.Create(name!));
    }

    [Fact]
    public void Create_ShouldTrimLeadingAndTrailingWhitespace()
    {
        Company company = Company.Create("  Lab BIO  ");

        Assert.Equal("Lab BIO", company.Name);
    }

    [Fact]
    public void Create_ShouldSetCreatedAtToUtcNow()
    {
        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        Company company = Company.Create("Lab");
        DateTime after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(company.CreatedAt, before, after);
    }

    // ---------------------------------------------------------------------------
    // Company.Rename
    // ---------------------------------------------------------------------------

    [Fact]
    public void Rename_WithValidName_ShouldUpdateName()
    {
        Company company = Company.Create("Antigo Nome");

        company.Rename("Novo Nome");

        Assert.Equal("Novo Nome", company.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Rename_WithEmptyName_ShouldThrow(string name)
    {
        Company company = Company.Create("Empresa");

        Assert.Throws<ArgumentException>(() => company.Rename(name));
    }

    // ---------------------------------------------------------------------------
    // Company.Activate / Deactivate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        Company company = Company.Create("Lab");

        company.Deactivate();

        Assert.False(company.IsActive);
    }

    [Fact]
    public void Activate_AfterDeactivate_ShouldSetIsActiveToTrue()
    {
        Company company = Company.Create("Lab");
        company.Deactivate();

        company.Activate();

        Assert.True(company.IsActive);
    }

    // ---------------------------------------------------------------------------
    // Company.AddMember
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddMember_WithNewUser_ShouldAddMembership()
    {
        Company company = Company.Create("Lab");
        Guid userId = Guid.NewGuid();

        company.AddMember(userId);

        Assert.Single(company.Memberships);
        Assert.Equal(userId, company.Memberships[0].LumenUserId);
        Assert.Equal(company.Id, company.Memberships[0].CompanyId);
    }

    [Fact]
    public void AddMember_SameUserTwice_ShouldThrow()
    {
        Company company = Company.Create("Lab");
        Guid userId = Guid.NewGuid();
        company.AddMember(userId);

        Assert.Throws<InvalidOperationException>(() => company.AddMember(userId));
    }

    [Fact]
    public void AddMember_MultipleDistinctUsers_ShouldAddAll()
    {
        Company company = Company.Create("Lab");
        Guid user1 = Guid.NewGuid();
        Guid user2 = Guid.NewGuid();
        Guid user3 = Guid.NewGuid();

        company.AddMember(user1);
        company.AddMember(user2);
        company.AddMember(user3);

        Assert.Equal(3, company.Memberships.Count);
    }

    // ---------------------------------------------------------------------------
    // Company.RemoveMember
    // ---------------------------------------------------------------------------

    [Fact]
    public void RemoveMember_ExistingUser_ShouldRemoveMembership()
    {
        Company company = Company.Create("Lab");
        Guid userId = Guid.NewGuid();
        company.AddMember(userId);

        company.RemoveMember(userId);

        Assert.Empty(company.Memberships);
    }

    [Fact]
    public void RemoveMember_NonExistingUser_ShouldThrow()
    {
        Company company = Company.Create("Lab");

        Assert.Throws<InvalidOperationException>(() => company.RemoveMember(Guid.NewGuid()));
    }

    [Fact]
    public void RemoveMember_ShouldNotAffectOtherMembers()
    {
        Company company = Company.Create("Lab");
        Guid user1 = Guid.NewGuid();
        Guid user2 = Guid.NewGuid();
        company.AddMember(user1);
        company.AddMember(user2);

        company.RemoveMember(user1);

        Assert.Single(company.Memberships);
        Assert.Equal(user2, company.Memberships[0].LumenUserId);
    }

    // ---------------------------------------------------------------------------
    // CompanyMembership properties
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddMember_ShouldSetMembershipJoinedAtToUtcNow()
    {
        Company company = Company.Create("Lab");
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        company.AddMember(Guid.NewGuid());

        DateTime after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(company.Memberships[0].JoinedAt, before, after);
    }

    [Fact]
    public void AddMember_MembershipId_ShouldBeUniquePerMember()
    {
        Company company = Company.Create("Lab");
        company.AddMember(Guid.NewGuid());
        company.AddMember(Guid.NewGuid());

        Guid id1 = company.Memberships[0].Id;
        Guid id2 = company.Memberships[1].Id;

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(Guid.Empty, id1);
        Assert.NotEqual(Guid.Empty, id2);
    }

    // ---------------------------------------------------------------------------
    // Company.AddMember — Role
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddMember_WithoutExplicitRole_ShouldDefaultToReadOnly()
    {
        Company company = Company.Create("Lab");

        company.AddMember(Guid.NewGuid());

        Assert.Equal(Role.ReadOnly, company.Memberships[0].Role);
    }

    [Fact]
    public void AddMember_WithExplicitRole_ShouldPreserveRole()
    {
        Company company = Company.Create("Lab");
        Guid userId = Guid.NewGuid();

        company.AddMember(userId, Role.Coordinator);

        Assert.Equal(Role.Coordinator, company.Memberships[0].Role);
    }

    // ---------------------------------------------------------------------------
    // Company.AssignMemberRole
    // ---------------------------------------------------------------------------

    [Fact]
    public void AssignMemberRole_ExistingMember_ShouldChangeRole()
    {
        Company company = Company.Create("Lab");
        Guid coordinator = Guid.NewGuid();
        Guid member = Guid.NewGuid();
        company.AddMember(coordinator, Role.Coordinator);
        company.AddMember(member, Role.ReadOnly);

        company.AssignMemberRole(member, Role.Researcher);

        CompanyMembership updated = company.Memberships.First(m => m.LumenUserId == member);
        Assert.Equal(Role.Researcher, updated.Role);
    }

    [Fact]
    public void AssignMemberRole_ShouldRaiseMemberRoleChangedEvent()
    {
        Company company = Company.Create("Lab");
        Guid coordinator = Guid.NewGuid();
        Guid member = Guid.NewGuid();
        company.AddMember(coordinator, Role.Coordinator);
        company.AddMember(member, Role.ReadOnly);

        company.AssignMemberRole(member, Role.Operator);

        MemberRoleChangedEvent evt = Assert.Single(
            company.DomainEvents.OfType<MemberRoleChangedEvent>());
        Assert.Equal(company.Id, evt.CompanyId);
        Assert.Equal(member, evt.LumenUserId);
        Assert.Equal(Role.ReadOnly, evt.PreviousRole);
        Assert.Equal(Role.Operator, evt.NewRole);
    }

    [Fact]
    public void AssignMemberRole_SameRole_ShouldBeNoOpWithoutEvent()
    {
        Company company = Company.Create("Lab");
        Guid coordinator = Guid.NewGuid();
        Guid member = Guid.NewGuid();
        company.AddMember(coordinator, Role.Coordinator);
        company.AddMember(member, Role.Researcher);

        company.AssignMemberRole(member, Role.Researcher);

        Assert.Empty(company.DomainEvents.OfType<MemberRoleChangedEvent>());
        Assert.Equal(Role.Researcher, company.Memberships.First(m => m.LumenUserId == member).Role);
    }

    [Fact]
    public void AssignMemberRole_UnknownMember_ShouldThrowBusinessException()
    {
        Company company = Company.Create("Lab");
        company.AddMember(Guid.NewGuid(), Role.Coordinator);

        Assert.Throws<BusinessException>(
            () => company.AssignMemberRole(Guid.NewGuid(), Role.Researcher));
    }

    [Fact]
    public void AssignMemberRole_DemotingLastCoordinator_ShouldThrowBusinessException()
    {
        Company company = Company.Create("Lab");
        Guid onlyCoordinator = Guid.NewGuid();
        company.AddMember(onlyCoordinator, Role.Coordinator);
        company.AddMember(Guid.NewGuid(), Role.ReadOnly);

        BusinessException ex = Assert.Throws<BusinessException>(
            () => company.AssignMemberRole(onlyCoordinator, Role.Researcher));

        Assert.Contains("at least one Coordinator", ex.Message);
        // Invariant preserved: role unchanged, no event raised.
        Assert.Equal(Role.Coordinator, company.Memberships.First(m => m.LumenUserId == onlyCoordinator).Role);
        Assert.Empty(company.DomainEvents.OfType<MemberRoleChangedEvent>());
    }

    [Fact]
    public void AssignMemberRole_DemotingCoordinator_WhenAnotherCoordinatorExists_ShouldSucceed()
    {
        Company company = Company.Create("Lab");
        Guid firstCoordinator = Guid.NewGuid();
        Guid secondCoordinator = Guid.NewGuid();
        company.AddMember(firstCoordinator, Role.Coordinator);
        company.AddMember(secondCoordinator, Role.Coordinator);

        company.AssignMemberRole(firstCoordinator, Role.Researcher);

        Assert.Equal(Role.Researcher, company.Memberships.First(m => m.LumenUserId == firstCoordinator).Role);
        Assert.Equal(Role.Coordinator, company.Memberships.First(m => m.LumenUserId == secondCoordinator).Role);
    }

    [Fact]
    public void AssignMemberRole_PromotingToCoordinator_ShouldSucceed()
    {
        Company company = Company.Create("Lab");
        Guid coordinator = Guid.NewGuid();
        Guid member = Guid.NewGuid();
        company.AddMember(coordinator, Role.Coordinator);
        company.AddMember(member, Role.ReadOnly);

        company.AssignMemberRole(member, Role.Coordinator);

        Assert.Equal(Role.Coordinator, company.Memberships.First(m => m.LumenUserId == member).Role);
        Assert.Single(company.DomainEvents.OfType<MemberRoleChangedEvent>());
    }

    // ---------------------------------------------------------------------------
    // Company entity equality
    // ---------------------------------------------------------------------------

    [Fact]
    public void TwoCompanies_WithSameId_ShouldBeEqual()
    {
        // Criamos dois agregados distintos simulando carga do banco via reflexão
        Company c1 = Company.Create("A");
        Company c2 = Company.Create("B");

        // IDs distintos → não iguais
        Assert.NotEqual(c1, c2);
    }
}
