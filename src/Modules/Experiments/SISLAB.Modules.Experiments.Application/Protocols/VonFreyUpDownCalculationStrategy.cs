using System.Globalization;
using System.Text.Json;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Protocols;

/// <summary>
/// The <c>von-frey-up-down@v1</c> protocol (card [E11] #88 — in vivo mechanical allodynia). It turns each animal's
/// von Frey up-down stimulus series into a 50% paw-withdrawal threshold in grams by the Dixon/Chaplan method:
/// <code>
/// 50% threshold (g) = 10^(log10(Xf) + k * δ)
/// </code>
/// where <c>Xf</c> is the last filament tested (g), <c>δ</c> is the mean step between adjacent log-filament values,
/// and <c>k</c> is the Chaplan tabular coefficient for the response pattern of the run. It returns an immutable
/// <see cref="FormulaSnapshot"/> the aggregate stores as-is.
/// </summary>
/// <remarks>
/// <para>
/// This is the in vivo analogue of the plate protocols (decision card #68 applied to in vivo): a versioned,
/// unit-tested Strategy resolved by type whose result is frozen into a snapshot rather than recomputed on read —
/// the antidote to a hand-computed Excel threshold. Each animal's raw value is its own up-down series, so the
/// snapshot carries one threshold per (animal, timepoint) measurement.
/// </para>
/// <para>
/// <b>Raw encoding.</b> A measurement's raw value is the run as <c>filament:response</c> pairs separated by commas,
/// where the filament is the force in grams and the response is <c>X</c> (paw withdrawal) or <c>O</c> (no
/// withdrawal), e.g. <c>0.4:O,0.6:X,0.4:X,0.16:O</c>. Validation fails fast with a domain error on a malformed run
/// or a run too short to score (the Chaplan k-table needs the four responses after the first up-down crossing).
/// </para>
/// </remarks>
internal sealed class VonFreyUpDownCalculationStrategy : IExperimentProtocol
{
    /// <summary>Versioned formula code stored on the snapshot.</summary>
    public const string FormulaCode = "von-frey-up-down@v1";

    /// <summary>Human-readable expression stored on the snapshot for traceability.</summary>
    public const string FormulaExpression =
        "50% threshold (g) = 10^(log10(Xf) + k * δ)  [Dixon/Chaplan up-down]";

    /// <summary>Default log-step (δ) for the standard Chaplan filament set when a run has a single filament.</summary>
    private const double DefaultLogStep = 0.224d;

    private static readonly JsonSerializerOptions ResultSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Chaplan k-table: the tabular coefficient for the pattern of the four responses after the first up-down
    /// crossing, keyed by the pattern string of O (negative) / X (positive). Values from Chaplan et al. (1994).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, double> ChaplanK = new Dictionary<string, double>
    {
        ["OOOO"] = -1.5000, ["OOOX"] = -1.1200, ["OOXO"] = -0.8500, ["OOXX"] = -0.5000,
        ["OXOO"] = -0.5500, ["OXOX"] = -0.2500, ["OXXO"] = -0.1500, ["OXXX"] = 0.2400,
        ["XOOO"] = -0.2400, ["XOOX"] = 0.1500, ["XOXO"] = 0.2500, ["XOXX"] = 0.5500,
        ["XXOO"] = 0.5000, ["XXOX"] = 0.8500, ["XXXO"] = 1.1200, ["XXXX"] = 1.5000,
    };

    /// <inheritdoc />
    public ExperimentType Type => ExperimentType.VonFrei;

    /// <inheritdoc />
    public FormulaSnapshot Calculate(Experiment experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);

        if (experiment is not VonFreiExperiment vonFrey)
            throw new DomainException($"The {FormulaCode} protocol only calculates von Frey experiments.");

        if (!vonFrey.HasMeasurements)
            throw new DomainException("The von Frey calculation requires at least one recorded timepoint.");

        IReadOnlyList<AnimalThreshold> thresholds = vonFrey.Measurements
            .OrderBy(measurement => measurement.TimepointLabel)
            .ThenBy(measurement => measurement.AnimalId)
            .Select(measurement => new AnimalThreshold(
                measurement.AnimalId,
                measurement.TimepointLabel,
                Math.Round(ComputeThreshold(measurement.RawValue), 4)))
            .ToList();

        var payload = new VonFreyResultPayload(FormulaCode, thresholds);
        string resultJson = JsonSerializer.Serialize(payload, ResultSerializerOptions);

        return FormulaSnapshot.Create(FormulaCode, FormulaExpression, DateTime.UtcNow, resultJson);
    }

    /// <summary>
    /// Computes the 50% withdrawal threshold (g) for a single up-down run. Exposed internally so the Dixon/Chaplan
    /// maths is unit-testable in isolation from the aggregate and the snapshot plumbing.
    /// </summary>
    internal static double ComputeThreshold(string rawValue)
    {
        IReadOnlyList<(double Filament, bool Withdrawal)> run = ParseRun(rawValue);

        double lastFilament = run[^1].Filament;

        double logStep = ComputeLogStep(run);

        string pattern = ResponsePattern(run);
        double k = ChaplanK.TryGetValue(pattern, out double value)
            ? value
            : throw new DomainException(
                $"von Frey run '{rawValue}' does not resolve to a scorable Chaplan pattern.");

        double logThreshold = Math.Log10(lastFilament) + (k * logStep);
        return Math.Pow(10, logThreshold);
    }

    private static IReadOnlyList<(double Filament, bool Withdrawal)> ParseRun(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new DomainException("von Frey run is empty.");

        var run = new List<(double Filament, bool Withdrawal)>();

        foreach (string token in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = token.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new DomainException($"von Frey step '{token}' must be 'filament:response'.");

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double filament)
                || filament <= 0)
                throw new DomainException($"von Frey filament '{parts[0]}' must be a positive number of grams.");

            bool withdrawal = parts[1].Equals("X", StringComparison.OrdinalIgnoreCase);
            bool noWithdrawal = parts[1].Equals("O", StringComparison.OrdinalIgnoreCase);
            if (!withdrawal && !noWithdrawal)
                throw new DomainException($"von Frey response '{parts[1]}' must be 'X' (withdrawal) or 'O' (none).");

            run.Add((filament, withdrawal));
        }

        if (run.Count < 5)
            throw new DomainException(
                "A von Frey up-down run needs at least five stimuli (one crossing plus four scored responses).");

        return run;
    }

    private static double ComputeLogStep(IReadOnlyList<(double Filament, bool Withdrawal)> run)
    {
        List<double> logs = run.Select(step => Math.Log10(step.Filament)).ToList();

        List<double> deltas = new();
        for (int i = 1; i < logs.Count; i++)
        {
            double delta = Math.Abs(logs[i] - logs[i - 1]);
            if (delta > 0)
                deltas.Add(delta);
        }

        return deltas.Count == 0 ? DefaultLogStep : deltas.Average();
    }

    /// <summary>The last four responses of the run as the O/X pattern the Chaplan k-table is keyed by.</summary>
    private static string ResponsePattern(IReadOnlyList<(double Filament, bool Withdrawal)> run)
        => string.Concat(run
            .TakeLast(4)
            .Select(step => step.Withdrawal ? 'X' : 'O'));

    /// <summary>Serialized result payload (the snapshot's frozen JSON). Web-cased to match the API shape.</summary>
    private sealed record VonFreyResultPayload(string Formula, IReadOnlyList<AnimalThreshold> Thresholds);

    /// <summary>Per-(animal, timepoint) 50% threshold line inside the result payload.</summary>
    private sealed record AnimalThreshold(Guid AnimalId, string Timepoint, double ThresholdGrams);
}
