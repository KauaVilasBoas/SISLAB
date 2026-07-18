using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Biobank.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain;

public sealed class SampleTests
{
    private static readonly DateTime When = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Company = Guid.NewGuid();

    private static Sample NewSample(decimal collected = 2.0m, string unit = "mL")
        => Sample.Collect(
            companyId: Company,
            code: "S-001",
            type: SampleType.Plasma,
            projectId: Guid.NewGuid(),
            batchId: Guid.NewGuid(),
            animalId: Guid.NewGuid(),
            sourceExperimentId: Guid.NewGuid(),
            collectedQuantity: SampleAmount.Of(collected, unit),
            collectedBy: "tech@lab",
            collectedAtUtc: When);

    [Fact]
    public void Collect_starts_with_full_balance_and_raises_collected()
    {
        Sample sample = NewSample(collected: 2.0m);

        Assert.Equal(2.0m, sample.RemainingQuantity.Value);
        Assert.Equal(0m, sample.ConsumedQuantity.Value);
        Assert.False(sample.IsDepleted);
        Assert.Empty(sample.Analyses);

        SampleCollectedEvent collected =
            Assert.IsType<SampleCollectedEvent>(Assert.Single(sample.DomainEvents));
        Assert.Equal(Company, collected.CompanyId);
        Assert.Equal(sample.CompanyId, collected.CompanyId);
    }

    [Fact]
    public void Collect_rejects_a_zero_quantity()
    {
        Assert.Throws<DomainException>(() => Sample.Collect(
            Company, "S-002", SampleType.Blood, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SampleAmount.Of(0m, "mL"), "tech@lab", When));
    }

    [Fact]
    public void Analyse_consumes_from_the_derived_balance()
    {
        Sample sample = NewSample(collected: 2.0m);

        sample.Analyse("ELISA TNF-α", SampleAmount.Of(0.5m, "mL"), "tech@lab", When);

        Assert.Equal(0.5m, sample.ConsumedQuantity.Value);
        Assert.Equal(1.5m, sample.RemainingQuantity.Value);
        Assert.Single(sample.Analyses);
        Assert.Equal(AnalysisStatus.Pending, sample.Analyses[0].Status);
    }

    [Fact]
    public void Multiple_analyses_sum_into_the_balance_until_depleted()
    {
        Sample sample = NewSample(collected: 1.0m);

        sample.Analyse("A1", SampleAmount.Of(0.4m, "mL"), "tech@lab", When);
        sample.Analyse("A2", SampleAmount.Of(0.6m, "mL"), "tech@lab", When);

        Assert.Equal(1.0m, sample.ConsumedQuantity.Value);
        Assert.Equal(0m, sample.RemainingQuantity.Value);
        Assert.True(sample.IsDepleted);
    }

    [Fact]
    public void Analyse_rejects_over_consumption_of_the_balance()
    {
        Sample sample = NewSample(collected: 1.0m);
        sample.Analyse("A1", SampleAmount.Of(0.8m, "mL"), "tech@lab", When);

        ConflictException ex = Assert.Throws<ConflictException>(
            () => sample.Analyse("A2", SampleAmount.Of(0.5m, "mL"), "tech@lab", When));

        Assert.Contains("only", ex.Message);
        // The rejected analysis must not have been recorded — the balance is unchanged.
        Assert.Single(sample.Analyses);
        Assert.Equal(0.2m, sample.RemainingQuantity.Value);
    }

    [Fact]
    public void Analyse_rejects_a_unit_that_differs_from_the_sample_unit()
    {
        Sample sample = NewSample(collected: 2.0m, unit: "mL");

        Assert.Throws<DomainException>(
            () => sample.Analyse("A1", SampleAmount.Of(0.5m, "µL"), "tech@lab", When));
    }

    [Fact]
    public void RecordAnalysisResult_completes_the_pending_analysis()
    {
        Sample sample = NewSample();
        Analysis analysis = sample.Analyse("A1", SampleAmount.Of(0.5m, "mL"), "tech@lab", When);

        sample.RecordAnalysisResult(analysis.Id, "42.7 pg/mL");

        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Equal("42.7 pg/mL", analysis.Result);
    }

    [Fact]
    public void RecordAnalysisResult_rejects_an_unknown_analysis()
    {
        Sample sample = NewSample();

        Assert.Throws<NotFoundException>(() => sample.RecordAnalysisResult(Guid.NewGuid(), "x"));
    }

    [Fact]
    public void DefineConservationRange_sets_the_range()
    {
        Sample sample = NewSample();

        sample.DefineConservationRange(TemperatureRange.Between(-80m, -20m));

        Assert.NotNull(sample.ConservationRange);
        Assert.Equal(-80m, sample.ConservationRange!.MinimumCelsius);
    }
}
