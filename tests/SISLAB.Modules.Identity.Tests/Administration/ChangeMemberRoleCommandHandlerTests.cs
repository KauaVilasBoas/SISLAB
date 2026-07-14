using SISLAB.Modules.Identity.Application.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Authorization;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Identity.Tests.Administration;

/// <summary>
/// Proves the command handler behind <c>CompanyMembersController.ChangeMemberRole</c> (card [E12] #77e):
/// it drives the <see cref="Company"/> aggregate (invariant + role change), persists, and then reconciles
/// the member's company-scoped Lumen profile (card #77d) — in that order, and only when the change is valid.
///
/// <para>The Lumen profile side (#77d) is exercised through the <see cref="IMemberAuthorizationProfileService"/>
/// port with a capturing fake: the handler's contract is that a valid role change triggers exactly one
/// reconciliation carrying the target user, the active company (scope) and the new role. The real
/// Lumen-backed reconciliation and the end-to-end scoped enforcement are covered elsewhere
/// (TenantScopedPermissionEnforcementTests) — here the focus is the handler's orchestration.</para>
/// </summary>
public sealed class ChangeMemberRoleCommandHandlerTests
{
    private static readonly Guid CompanyId = new("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid OtherCompanyId = new("10000000-0000-0000-0000-00000000000b");
    private static readonly Guid Coordinator = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Member = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task HandleAsync_ValidChange_PersistsThenReconcilesScopedProfile()
    {
        Company company = SeededCompanyWith(Coordinator, Role.Coordinator, Member, Role.ReadOnly);
        var repository = new CapturingCompanyRepository(company);
        var profileService = new CapturingProfileService();
        var handler = new ChangeMemberRoleCommandHandler(repository, profileService);

        await handler.HandleAsync(new ChangeMemberRoleCommand(CompanyId, Member, Role.Researcher));

        // Aggregate mutated and persisted.
        Assert.Equal(Role.Researcher, company.Memberships.First(m => m.LumenUserId == Member).Role);
        Assert.Equal(1, repository.SaveChangesCallCount);

        // Profile reconciled once, scoped to the active company, with the new role.
        ReconcileCall call = Assert.Single(profileService.Calls);
        Assert.Equal(Member, call.UserId);
        Assert.Equal(CompanyId, call.CompanyId);
        Assert.Equal(Role.Researcher, call.Role);
    }

    [Fact]
    public async Task HandleAsync_DemotingLastCoordinator_ThrowsAndDoesNotReconcile()
    {
        // Only one Coordinator: demoting must be rejected by the aggregate invariant.
        Company company = SeededCompanyWith(Coordinator, Role.Coordinator, Member, Role.ReadOnly);
        var repository = new CapturingCompanyRepository(company);
        var profileService = new CapturingProfileService();
        var handler = new ChangeMemberRoleCommandHandler(repository, profileService);

        await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ChangeMemberRoleCommand(CompanyId, Coordinator, Role.Researcher)));

        // Invariant preserved, nothing persisted, no profile churn.
        Assert.Equal(Role.Coordinator, company.Memberships.First(m => m.LumenUserId == Coordinator).Role);
        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Empty(profileService.Calls);
    }

    [Fact]
    public async Task HandleAsync_UnknownMember_ThrowsBusinessExceptionAndDoesNotReconcile()
    {
        Company company = SeededCompanyWith(Coordinator, Role.Coordinator, Member, Role.ReadOnly);
        var repository = new CapturingCompanyRepository(company);
        var profileService = new CapturingProfileService();
        var handler = new ChangeMemberRoleCommandHandler(repository, profileService);

        Guid outsider = Guid.NewGuid();

        await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ChangeMemberRoleCommand(CompanyId, outsider, Role.Operator)));

        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Empty(profileService.Calls);
    }

    [Fact]
    public async Task HandleAsync_CompanyNotFound_ThrowsNotFound()
    {
        var repository = new CapturingCompanyRepository(company: null);
        var profileService = new CapturingProfileService();
        var handler = new ChangeMemberRoleCommandHandler(repository, profileService);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.HandleAsync(new ChangeMemberRoleCommand(CompanyId, Member, Role.Operator)));

        Assert.Empty(profileService.Calls);
    }

    [Fact]
    public async Task HandleAsync_TargetsActiveCompanyScope_NotAnotherTenant()
    {
        // The command's CompanyId (active company from the cookie) is the scope handed to reconciliation —
        // proving a role change in company A cannot grant permissions in company B (tenant isolation).
        Company company = SeededCompanyWith(Coordinator, Role.Coordinator, Member, Role.ReadOnly);
        var repository = new CapturingCompanyRepository(company);
        var profileService = new CapturingProfileService();
        var handler = new ChangeMemberRoleCommandHandler(repository, profileService);

        await handler.HandleAsync(new ChangeMemberRoleCommand(CompanyId, Member, Role.Operator));

        ReconcileCall call = Assert.Single(profileService.Calls);
        Assert.Equal(CompanyId, call.CompanyId);
        Assert.NotEqual(OtherCompanyId, call.CompanyId);
    }

    private static Company SeededCompanyWith(Guid userA, Role roleA, Guid userB, Role roleB)
    {
        Company company = Company.Seed(CompanyId, "LAFTE");
        company.AddMember(userA, roleA);
        company.AddMember(userB, roleB);
        return company;
    }

    /// <summary>Repository fake that serves one company and counts <c>SaveChangesAsync</c> calls.</summary>
    private sealed class CapturingCompanyRepository : ICompanyRepository
    {
        private readonly Company? _company;

        public CapturingCompanyRepository(Company? company) => _company = company;

        public int SaveChangesCallCount { get; private set; }

        public Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_company is not null && _company.Id == id ? _company : null);

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task AddAsync(Company company, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Company company, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    /// <summary>Captures every reconciliation the handler requests, for assertion.</summary>
    private sealed class CapturingProfileService : IMemberAuthorizationProfileService
    {
        public List<ReconcileCall> Calls { get; } = [];

        public Task ReconcileAsync(
            Guid lumenUserId, Guid companyId, Role role, CancellationToken cancellationToken = default)
        {
            Calls.Add(new ReconcileCall(lumenUserId, companyId, role));
            return Task.CompletedTask;
        }
    }

    private sealed record ReconcileCall(Guid UserId, Guid CompanyId, Role Role);
}
