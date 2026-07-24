using SISLAB.Modules.Configuration.Application.InclusionCriteria;
using SISLAB.Modules.Configuration.Domain.InclusionCriteria;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Application.InclusionCriteria;

/// <summary>
/// Covers the <see cref="CreateInclusionCriterionCommandHandler"/> (SISLAB-02): it maps the flat payload onto the
/// aggregate and rejects a duplicate parameter (one criterion per parameter per tenant).
/// </summary>
public sealed class CreateInclusionCriterionCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_persists_the_criterion_and_returns_its_id()
    {
        FakeInclusionCriterionRepository repository = new();
        CreateInclusionCriterionCommandHandler handler = new(repository);

        Guid id = await handler.HandleAsync(new CreateInclusionCriterionCommand(
            "glicemia", ComparisonOperator.GreaterThanOrEqual, 250m, "mg/dL"));

        InclusionCriterion saved = Assert.Single(repository.Added);
        Assert.Equal(id, saved.Id);
        Assert.Equal("glicemia", saved.ParameterCode);
        Assert.True(saved.Includes(268m));
    }

    [Fact]
    public async Task HandleAsync_rejects_a_duplicate_parameter()
    {
        FakeInclusionCriterionRepository repository = new() { ExistingParameter = "glicemia" };
        CreateInclusionCriterionCommandHandler handler = new(repository);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new CreateInclusionCriterionCommand("glicemia", ComparisonOperator.GreaterThanOrEqual, 250m, "mg/dL")));

        Assert.Empty(repository.Added);
    }

    private sealed class FakeInclusionCriterionRepository : IInclusionCriterionRepository
    {
        public List<InclusionCriterion> Added { get; } = [];

        public string? ExistingParameter { get; init; }

        public Task<bool> ParameterExistsAsync(string parameterCode, CancellationToken ct = default)
            => Task.FromResult(string.Equals(ExistingParameter, parameterCode.Trim(), StringComparison.OrdinalIgnoreCase));

        public Task AddAsync(InclusionCriterion criterion, CancellationToken ct = default)
        {
            Added.Add(criterion);
            return Task.CompletedTask;
        }

        public Task<InclusionCriterion?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(criterion => criterion.Id == id));

        public Task UpdateAsync(InclusionCriterion criterion, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
