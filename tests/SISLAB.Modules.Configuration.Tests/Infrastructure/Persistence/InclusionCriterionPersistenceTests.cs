using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Configuration.Domain.InclusionCriteria;
using SISLAB.Modules.Configuration.Infrastructure.Persistence;

namespace SISLAB.Modules.Configuration.Tests.Infrastructure.Persistence;

/// <summary>
/// Round-trips an <see cref="InclusionCriterion"/> through the real <see cref="ConfigurationDbContext"/> on the EF
/// Core InMemory provider (SISLAB-02): proves the owned <see cref="InclusionThreshold"/> (operator stored by name +
/// threshold) reconstitutes the aggregate's value object intact — the mapping's risk point — without a live Postgres.
/// </summary>
/// <remarks>
/// Full Postgres validation of the enum-as-string column and the unique index is a later Dockerized smoke, signalled
/// in the delivery report.
/// </remarks>
public sealed class InclusionCriterionPersistenceTests
{
    [Fact]
    public async Task Criterion_round_trips_its_threshold_through_the_db_context()
    {
        DbContextOptions<ConfigurationDbContext> options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        InclusionCriterion criterion = InclusionCriterion.Create(
            "glicemia", ComparisonOperator.GreaterThanOrEqual, 250m, "mg/dL");
        Guid id = criterion.Id;

        await using (ConfigurationDbContext write = new(options))
        {
            write.InclusionCriteria.Add(criterion);
            await write.SaveChangesAsync();
        }

        await using ConfigurationDbContext read = new(options);
        InclusionCriterion? loaded = await read.InclusionCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        Assert.NotNull(loaded);
        Assert.Equal("glicemia", loaded!.ParameterCode);
        Assert.Equal("mg/dL", loaded.Unit);
        Assert.Equal(ComparisonOperator.GreaterThanOrEqual, loaded.Threshold.Operator);
        Assert.Equal(250m, loaded.Threshold.Value);
        Assert.True(loaded.Includes(268m));
        Assert.False(loaded.Includes(214m));
    }
}
