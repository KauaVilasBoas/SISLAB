using SISLAB.Modules.Experiments.Application.Export;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Unit tests for the GraphPad Prism CSV formatters (card [E11] #79): each renders its assay's frozen snapshot
/// JSON into the pasteable table Prism expects, and the resolver routes a formula code to the right formatter.
/// </summary>
public sealed class PrismCsvFormatterTests
{
    [Fact]
    public void Viability_formatter_lays_out_one_column_per_concentration_and_one_row_per_replicate()
    {
        const string json =
            """
            {
              "formula": "viability@v1",
              "blankMean": 0.05,
              "controlMean": 1.05,
              "wells": [
                { "well": "A1", "role": "Sample", "concentrationUm": 1, "rawAbsorbance": 0.9, "viabilityPct": 85.3 },
                { "well": "B1", "role": "Sample", "concentrationUm": 1, "rawAbsorbance": 0.89, "viabilityPct": 84.9 },
                { "well": "A2", "role": "Sample", "concentrationUm": 10, "rawAbsorbance": 0.6, "viabilityPct": 61.4 }
              ]
            }
            """;

        string csv = new ViabilityPrismFormatter().Format(json);
        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Composto,1 µM,10 µM", lines[0]);
        Assert.Equal("Réplica 1,85.3,61.4", lines[1]);
        // The 10 µM group has a single replicate, so its second row cell is blank (ragged group padded).
        Assert.Equal("Réplica 2,84.9,", lines[2]);
    }

    [Fact]
    public void NitricOxide_formatter_stacks_the_curve_then_the_samples()
    {
        const string json =
            """
            {
              "formula": "nitric-oxide@v1",
              "slope": 0.02,
              "intercept": 0,
              "rSquared": 1,
              "lowConfidence": false,
              "blankBaseline": 0.05,
              "curve": [
                { "concentrationUm": 0, "absorbance": 0 },
                { "concentrationUm": 10, "absorbance": 0.2 }
              ],
              "wells": [
                { "well": "A3", "role": "Sample", "rawAbsorbance": 0.45, "concentrationUm": 20 }
              ]
            }
            """;

        string csv = new NitricOxidePrismFormatter().Format(json);
        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Concentração (µM),Absorbância,NO Calculado (µM)", lines[0]);
        Assert.Equal("0,0,—", lines[1]);
        Assert.Equal("10,0.2,—", lines[2]);
        Assert.Equal("Amostras,Absorbância,NO Calculado (µM)", lines[3]);
        Assert.Equal("A3,0.45,20", lines[4]);
    }

    [Fact]
    public void Viability_formatter_appends_a_per_condition_mean_and_sd_summary_block()
    {
        const string json =
            """
            {
              "formula": "viability@v1",
              "blankMean": 0.05,
              "controlMean": 1.05,
              "wells": [
                { "well": "A1", "role": "Sample", "sampleId": "GDA-43", "concentrationUm": 10, "rawAbsorbance": 0.9, "viabilityPct": 85 },
                { "well": "B1", "role": "Sample", "sampleId": "GDA-43", "concentrationUm": 10, "rawAbsorbance": 0.89, "viabilityPct": 84 },
                { "well": "C1", "role": "Sample", "sampleId": "GDA-43", "concentrationUm": 10, "rawAbsorbance": 0.91, "viabilityPct": 86 }
              ],
              "conditions": [
                { "sampleId": "GDA-43", "concentrationUm": 10, "replicateCount": 3, "meanViabilityPct": 85, "stdDevViabilityPct": 1, "wells": ["A1","B1","C1"] }
              ]
            }
            """;

        string csv = new ViabilityPrismFormatter().Format(json);

        Assert.Contains("Resumo por condição,Composto,Concentração (µM),N,Média (%),Desvio (%)", csv);
        Assert.Contains(",GDA-43,10,3,85,1", csv);
    }

    [Fact]
    public void NitricOxide_formatter_appends_a_per_condition_mean_and_sd_summary_block()
    {
        const string json =
            """
            {
              "formula": "nitric-oxide@v1",
              "slope": 0.02, "intercept": 0, "rSquared": 1, "lowConfidence": false, "blankBaseline": 0.05,
              "curve": [ { "concentrationUm": 0, "absorbance": 0 } ],
              "wells": [ { "well": "A3", "role": "Sample", "sampleId": "CTRL+", "rawAbsorbance": 0.45, "concentrationUm": 20 } ],
              "conditions": [
                { "sampleId": "CTRL+", "concentrationUm": null, "replicateCount": 2, "meanConcentrationUm": 20.5, "stdDevConcentrationUm": 0.7071, "wells": ["A3","B3"] }
              ]
            }
            """;

        string csv = new NitricOxidePrismFormatter().Format(json);

        Assert.Contains("Resumo por condição,N,Média NO (µM),Desvio NO (µM)", csv);
        Assert.Contains("CTRL+,2,20.5,0.7071", csv);
    }

    [Fact]
    public void Resolver_routes_each_formula_code_to_its_formatter_and_rejects_the_unknown()
    {
        var resolver = new PrismCsvFormatterResolver(new IPrismCsvFormatter[]
        {
            new ViabilityPrismFormatter(),
            new NitricOxidePrismFormatter(),
        });

        Assert.IsType<ViabilityPrismFormatter>(resolver.Resolve("viability@v1"));
        Assert.IsType<NitricOxidePrismFormatter>(resolver.Resolve("nitric-oxide@v1"));
        Assert.Throws<DomainException>(() => resolver.Resolve("unknown@v1"));
    }
}
