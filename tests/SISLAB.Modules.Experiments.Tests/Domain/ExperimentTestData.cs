using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;

namespace SISLAB.Modules.Experiments.Tests.Domain;

/// <summary>Builders for viability experiments / plate designs used across the domain and handler tests.</summary>
internal static class ExperimentTestData
{
    public static ViabilidadeCelularExperiment NewExperiment(string title = "MTT run")
        => ViabilidadeCelularExperiment.Create(title, description: null, createdBy: "tester@lab", createdAtUtc: DateTime.UtcNow);

    public static NitricOxideExperiment NewNitricOxideExperiment(string title = "Griess run")
        => NitricOxideExperiment.Create(title, description: null, createdBy: "tester@lab", createdAtUtc: DateTime.UtcNow);

    public static Well MakeWell(
        char row,
        int column,
        WellRole role,
        decimal? absorbance = null,
        decimal? concentrationUm = null,
        string? sampleId = null)
    {
        Well well = Well.Create(row, column, role, concentrationUm, sampleId);
        if (absorbance.HasValue)
            well.RecordAbsorbance(absorbance.Value);
        return well;
    }

    /// <summary>
    /// A minimal, fully-read plate: one blank (0.05), two controls (mean 1.05) and one sample (0.55). Under
    /// viability@v1 the sample computes to (0.55 - 0.05) / (1.05 - 0.05) * 100 = 50.00%.
    /// </summary>
    public static IReadOnlyList<Well> FullyReadPlate() =>
    [
        MakeWell('A', 1, WellRole.Blank, absorbance: 0.05m),
        MakeWell('B', 1, WellRole.Control, absorbance: 1.00m),
        MakeWell('C', 1, WellRole.Control, absorbance: 1.10m),
        MakeWell('D', 1, WellRole.Sample, absorbance: 0.55m, concentrationUm: 10m),
    ];
}
