using SISLAB.Modules.Experiments.Application.Export;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Unit tests for the in vivo GraphPad Prism CSV export (card [E11] #31): the von Frey formatter pivots the frozen
/// per-(animal, timepoint) thresholds into a group × timepoint grouped table using the Project's animal→group
/// mapping, and the resolver routes a formula code to the right formatter.
/// </summary>
public sealed class InVivoPrismFormatterTests
{
    private static readonly Guid A1 = Guid.NewGuid();
    private static readonly Guid A2 = Guid.NewGuid();
    private static readonly Guid A3 = Guid.NewGuid();
    private static readonly Guid ControlGroup = Guid.NewGuid();
    private static readonly Guid DoseGroup = Guid.NewGuid();

    private static string Snapshot() =>
        $$"""
        {
          "formula": "von-frey-up-down@v1",
          "thresholds": [
            { "animalId": "{{A1}}", "timepoint": "Baseline", "thresholdGrams": 8.0 },
            { "animalId": "{{A1}}", "timepoint": "T30",      "thresholdGrams": 2.5 },
            { "animalId": "{{A2}}", "timepoint": "Baseline", "thresholdGrams": 7.5 },
            { "animalId": "{{A2}}", "timepoint": "T30",      "thresholdGrams": 2.1 },
            { "animalId": "{{A3}}", "timepoint": "Baseline", "thresholdGrams": 8.2 },
            { "animalId": "{{A3}}", "timepoint": "T30",      "thresholdGrams": 6.9 }
          ]
        }
        """;

    private static IReadOnlyList<AnimalGroupAssignment> Groups() =>
    [
        // A1, A2 in the vehicle group; A3 in the 10 mg/kg group.
        new AnimalGroupAssignment(A1, ControlGroup, "Controle", 0m, "mg/kg", "M1-01"),
        new AnimalGroupAssignment(A2, ControlGroup, "Controle", 0m, "mg/kg", "M1-02"),
        new AnimalGroupAssignment(A3, DoseGroup, "Dose 10", 10m, "mg/kg", "M1-03"),
    ];

    [Fact]
    public void VonFrey_in_vivo_formatter_lays_out_group_rows_by_ascending_dose_with_timepoint_columns()
    {
        string csv = new VonFreyInVivoPrismFormatter().Format(Snapshot(), Groups());
        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header: Grupo + timepoint columns ordered by label (Baseline before T30).
        Assert.Equal("Grupo,Baseline,T30", lines[0]);
        // Vehicle group first (dose 0): two replicate rows, header only on the first.
        Assert.Equal("Controle (0 mg/kg),8,2.5", lines[1]);
        Assert.Equal(",7.5,2.1", lines[2]);
        // Then the 10 mg/kg group: one replicate row.
        Assert.Equal("Dose 10 (10 mg/kg),8.2,6.9", lines[3]);
    }

    [Fact]
    public void VonFrey_in_vivo_formatter_buckets_an_unmapped_animal_into_sem_grupo()
    {
        string csv = new VonFreyInVivoPrismFormatter()
            .Format(Snapshot(), new[] { Groups()[0], Groups()[1] }); // A3 has no assignment

        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // The unmapped A3 collapses into a trailing "Sem grupo" bucket rather than being dropped.
        Assert.Contains(lines, line => line.StartsWith("Sem grupo,", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolver_routes_the_formula_code_to_its_formatter_and_rejects_the_unknown()
    {
        var resolver = new InVivoPrismFormatterResolver(new IInVivoPrismFormatter[]
        {
            new VonFreyInVivoPrismFormatter(),
        });

        Assert.IsType<VonFreyInVivoPrismFormatter>(resolver.Resolve("von-frey-up-down@v1"));
        Assert.Throws<DomainException>(() => resolver.Resolve("unknown@v1"));
    }
}
