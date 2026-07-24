using SISLAB.Modules.Configuration.Application.ExperimentalModels;
using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Tests.Application.ExperimentalModels;

/// <summary>
/// Covers the <see cref="CreateExperimentalModelCommandHandler"/> (SISLAB-04): it maps the flat command payload
/// onto the aggregate's value objects, persists it through the repository and surfaces the domain invariants
/// (an inconsistent dose group is rejected by the domain, not silently accepted).
/// </summary>
public sealed class CreateExperimentalModelCommandHandlerTests
{
    private static CreateExperimentalModelCommand ValidCommand() =>
        new(
            "Neuropatia diabética",
            "Modelo ND.",
            new InductionProtocolInput(2, 1, 28),
            ["Basal", "Pós-indução", "28 dias"],
            ["glicemia", "peso", "rotarod"],
            [
                new StandardGroupInput("Naive", StandardGroupKind.Naive, null, null),
                new StandardGroupInput("Controle", StandardGroupKind.Control, null, null),
                new StandardGroupInput("3 g/kg", StandardGroupKind.Dose, 3m, "g/kg"),
                new StandardGroupInput("0,6 g/kg", StandardGroupKind.Dose, 0.6m, "g/kg"),
            ],
            new DilutionDefaultsInput(5m, "Óleo de soja"));

    [Fact]
    public async Task HandleAsync_persists_the_model_and_returns_its_id()
    {
        FakeExperimentalModelRepository repository = new();
        CreateExperimentalModelCommandHandler handler = new(repository);

        Guid id = await handler.HandleAsync(ValidCommand());

        ExperimentalModel saved = Assert.Single(repository.Added);
        Assert.Equal(id, saved.Id);
        Assert.Equal("Neuropatia diabética", saved.Name);
        Assert.Equal(28, saved.Induction.ReferenceDayAfterInduction);
        Assert.Equal(4, saved.Groups.Groups.Count);
        Assert.Equal("Óleo de soja", saved.DilutionDefaults.DefaultDiluent);
        Assert.Equal(5m, saved.DilutionDefaults.Ratio.MicrolitresPerGram);
    }

    [Fact]
    public async Task HandleAsync_lets_a_dose_group_without_a_dose_fail_in_the_domain()
    {
        FakeExperimentalModelRepository repository = new();
        CreateExperimentalModelCommandHandler handler = new(repository);

        CreateExperimentalModelCommand command = ValidCommand() with
        {
            Groups = [new StandardGroupInput("Curva", StandardGroupKind.Dose, null, null)],
        };

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));
        Assert.Empty(repository.Added);
    }

    private sealed class FakeExperimentalModelRepository : IExperimentalModelRepository
    {
        public List<ExperimentalModel> Added { get; } = [];

        public Task AddAsync(ExperimentalModel model, CancellationToken ct = default)
        {
            Added.Add(model);
            return Task.CompletedTask;
        }

        public Task<ExperimentalModel?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(model => model.Id == id));

        public Task UpdateAsync(ExperimentalModel model, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
