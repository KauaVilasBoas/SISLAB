using SISLAB.Modules.Configuration.Application.CollectionRoles;
using SISLAB.Modules.Configuration.Domain.CollectionRoles;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Application.CollectionRoles;

/// <summary>
/// Covers the <see cref="CreateCollectionRoleCommandHandler"/> (SISLAB-08): it maps the flat payload onto the aggregate
/// and rejects a duplicate name (one role per name per tenant).
/// </summary>
public sealed class CreateCollectionRoleCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_persists_the_role_and_returns_its_id()
    {
        FakeCollectionRoleRepository repository = new();
        CreateCollectionRoleCommandHandler handler = new(repository);

        Guid id = await handler.HandleAsync(new CreateCollectionRoleCommand("Decapitação", null));

        CollectionRole saved = Assert.Single(repository.Added);
        Assert.Equal(id, saved.Id);
        Assert.Equal("Decapitação", saved.Name);
    }

    [Fact]
    public async Task HandleAsync_rejects_a_duplicate_name()
    {
        FakeCollectionRoleRepository repository = new() { ExistingName = "Sangue" };
        CreateCollectionRoleCommandHandler handler = new(repository);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new CreateCollectionRoleCommand("Sangue", null)));

        Assert.Empty(repository.Added);
    }

    private sealed class FakeCollectionRoleRepository : ICollectionRoleRepository
    {
        public List<CollectionRole> Added { get; } = [];

        public string? ExistingName { get; init; }

        public Task<bool> NameExistsAsync(string name, CancellationToken ct = default)
            => Task.FromResult(string.Equals(ExistingName, name.Trim(), StringComparison.OrdinalIgnoreCase));

        public Task AddAsync(CollectionRole role, CancellationToken ct = default)
        {
            Added.Add(role);
            return Task.CompletedTask;
        }

        public Task<CollectionRole?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(role => role.Id == id));

        public Task UpdateAsync(CollectionRole role, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
