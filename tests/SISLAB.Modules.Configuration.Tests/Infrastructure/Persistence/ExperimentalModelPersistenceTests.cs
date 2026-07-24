using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Tests.Infrastructure.Persistence;

/// <summary>
/// Round-trips an <see cref="ExperimentalModel"/> through the real <see cref="ConfigurationDbContext"/> on the EF
/// Core InMemory provider (SISLAB-04): proves the jsonb value converters (timepoints/parameters/groups) and the
/// owned singles (induction protocol, dilution defaults with its nested g:µL ratio) serialize and reconstitute the
/// aggregate's value objects intact — the highest-risk part of the mapping — without needing a live Postgres.
/// </summary>
/// <remarks>
/// The context is built through the design-time constructor path (no tenant services), so the global query filter
/// is off and the model is read back by id across the (unstamped) company. Full Postgres validation of the jsonb
/// column type and the unique index is a later step (a Dockerized smoke), signalled in the delivery report.
/// </remarks>
public sealed class ExperimentalModelPersistenceTests
{
    [Fact]
    public async Task Model_round_trips_all_value_objects_through_the_db_context()
    {
        DbContextOptions<ConfigurationDbContext> options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ExperimentalModel model = ExperimentalModel.Create(
            "Neuropatia diabética",
            "Modelo ND.",
            InductionProtocol.Of(2, 1, 28),
            StandardTimepoints.From(["Basal", "Pós-indução", "28 dias"]),
            ApplicableParameters.From(["glicemia", "peso", "rotarod"]),
            StandardGroups.From(
            [
                StandardGroup.NonDosed("Naive", StandardGroupKind.Naive),
                StandardGroup.NonDosed("Controle", StandardGroupKind.Control),
                StandardGroup.Dosed("3 g/kg", 3m, "g/kg"),
                StandardGroup.Dosed("0,6 g/kg", 0.6m, "g/kg"),
            ]),
            DilutionDefaults.Of(5m, "Óleo de soja"));
        Guid id = model.Id;

        await using (ConfigurationDbContext write = new(options))
        {
            write.ExperimentalModels.Add(model);
            await write.SaveChangesAsync();
        }

        await using ConfigurationDbContext read = new(options);
        ExperimentalModel? loaded = await read.ExperimentalModels
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        Assert.NotNull(loaded);
        Assert.Equal("Neuropatia diabética", loaded!.Name);
        Assert.Equal("Modelo ND.", loaded.Description);

        Assert.Equal(2, loaded.Induction.Administrations);
        Assert.Equal(1, loaded.Induction.IntervalDays);
        Assert.Equal(28, loaded.Induction.ReferenceDayAfterInduction);

        Assert.Equal(["Basal", "Pós-indução", "28 dias"], loaded.Timepoints.Labels);
        Assert.Equal(["glicemia", "peso", "rotarod"], loaded.Parameters.Codes);

        Assert.Equal(4, loaded.Groups.Groups.Count);
        StandardGroup doseGroup = loaded.Groups.Groups.Single(g => g.Name == "3 g/kg");
        Assert.Equal(StandardGroupKind.Dose, doseGroup.Kind);
        Assert.Equal(3m, doseGroup.DoseAmount);
        Assert.Equal("g/kg", doseGroup.DoseUnit);

        Assert.Equal(5m, loaded.DilutionDefaults.Ratio.MicrolitresPerGram);
        Assert.Equal("Óleo de soja", loaded.DilutionDefaults.DefaultDiluent);
    }
}
