using SISLAB.Modules.Identity.Application.Onboarding;
using SISLAB.Modules.Identity.Contracts.Onboarding;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Domain.Companies.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Identity.Tests.Onboarding;

/// <summary>
/// Proves the self-service signup handler (card [E12] #75a): the happy path provisions the coordinator user,
/// creates the tenant bound to it (founding membership + <see cref="CompanyCreated"/>) and grants
/// company-scoped access; duplicate company name or coordinator e-mail short-circuit with a conflict before
/// anything is created.
/// </summary>
public sealed class SignupCompanyCommandHandlerTests
{
    private static readonly Guid CoordinatorId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static SignupCompanyCommand ValidCommand() => new(
        CompanyName: "New Lab",
        TaxId: "00000000000191",
        CoordinatorEmail: "coord@newlab.test",
        CoordinatorUsername: "coord",
        CoordinatorPassword: "Str0ng-Password!");

    [Fact]
    public async Task HandleAsync_HappyPath_CreatesCompanyCoordinatorAndGrantsAccess()
    {
        var companies = new FakeCompanyRepository();
        var onboarding = new FakeOnboardingGateway(coordinatorId: CoordinatorId);
        var handler = new SignupCompanyCommandHandler(companies, onboarding);

        SignupCompanyResult result = await handler.HandleAsync(ValidCommand());

        // Coordinator user was created and its id flows into the result.
        Assert.Equal(CoordinatorId, result.CoordinatorUserId);
        Assert.Equal("coord@newlab.test", onboarding.CreatedEmail);

        // The tenant was added, bound to the coordinator as founding member.
        Company added = Assert.Single(companies.Added);
        Assert.Equal(result.CompanyId, added.Id);
        Assert.Equal("New Lab", added.Name);
        Assert.True(added.IsMember(CoordinatorId));

        // CompanyCreated was raised by the aggregate.
        IDomainEvent domainEvent = Assert.Single(added.DomainEvents);
        CompanyCreated created = Assert.IsType<CompanyCreated>(domainEvent);
        Assert.Equal(added.Id, created.CompanyId);
        Assert.Equal(CoordinatorId, created.CoordinatorUserId);

        // Coordinator access was granted scoped to the new company.
        Assert.Equal((CoordinatorId, added.Id), onboarding.Granted);
    }

    [Fact]
    public async Task HandleAsync_WhenCompanyNameTaken_ThrowsConflict_AndCreatesNothing()
    {
        var companies = new FakeCompanyRepository { NameExists = true };
        var onboarding = new FakeOnboardingGateway(coordinatorId: CoordinatorId);
        var handler = new SignupCompanyCommandHandler(companies, onboarding);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(ValidCommand()));

        Assert.Empty(companies.Added);
        Assert.Null(onboarding.CreatedEmail); // no coordinator created
        Assert.Null(onboarding.Granted);
    }

    [Fact]
    public async Task HandleAsync_WhenCoordinatorEmailTaken_ThrowsConflict_AndCreatesNoTenant()
    {
        var companies = new FakeCompanyRepository();
        var onboarding = new FakeOnboardingGateway(coordinatorId: CoordinatorId) { EmailExists = true };
        var handler = new SignupCompanyCommandHandler(companies, onboarding);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(ValidCommand()));

        Assert.Empty(companies.Added);
        Assert.Null(onboarding.CreatedEmail);
        Assert.Null(onboarding.Granted);
    }

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        public bool NameExists { get; init; }
        public List<Company> Added { get; } = [];

        public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(NameExists);

        public Task AddAsync(Company company, CancellationToken ct = default)
        {
            Added.Add(company);
            return Task.CompletedTask;
        }

        public Task<Company?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Company>> ListActiveAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Company>> ListForMemberAsync(Guid lumenUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> IsActiveMemberAsync(Guid companyId, Guid lumenUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Company company, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeOnboardingGateway : ICompanyOnboardingGateway
    {
        private readonly Guid _coordinatorId;

        public FakeOnboardingGateway(Guid coordinatorId) => _coordinatorId = coordinatorId;

        public bool EmailExists { get; init; }
        public string? CreatedEmail { get; private set; }
        public (Guid UserId, Guid CompanyId)? Granted { get; private set; }

        public Task<bool> CoordinatorEmailExistsAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(EmailExists);

        public Task<Guid> CreateCoordinatorAsync(
            string email, string username, string password, CancellationToken cancellationToken = default)
        {
            CreatedEmail = email;
            return Task.FromResult(_coordinatorId);
        }

        public Task GrantCoordinatorAccessAsync(
            Guid coordinatorUserId, Guid companyId, CancellationToken cancellationToken = default)
        {
            Granted = (coordinatorUserId, companyId);
            return Task.CompletedTask;
        }
    }
}
