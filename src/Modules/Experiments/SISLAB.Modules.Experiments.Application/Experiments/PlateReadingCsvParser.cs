using System.Globalization;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Application.Experiments;

/// <summary>
/// Parses the canonical plate-reading CSV the operator pastes from the plate reader: two columns
/// <c>well,absorbance</c> (e.g. <c>A1,0.452</c>), one reading per line. Blank lines and comment lines (those
/// starting with <c>#</c>) are ignored, and a leading <c>well,absorbance</c> header line is tolerated.
/// </summary>
/// <remarks>
/// This is a focused application-layer collaborator (Single Responsibility): it turns raw CSV text into a list
/// of (coordinate, absorbance) pairs and fails fast with a domain error on a malformed line, so the command
/// handler stays about orchestration. The absorbance is parsed with the invariant culture (a dot decimal
/// separator), matching the reader's export.
/// </remarks>
internal static class PlateReadingCsvParser
{
    public static IReadOnlyList<PlateReading> Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            throw new DomainException("The plate reading CSV is empty.");

        var readings = new List<PlateReading>();

        string[] lines = csvContent.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length != 2)
                throw new DomainException(
                    $"Malformed plate reading line '{line}'. Expected 'well,absorbance' (e.g. 'A1,0.452').");

            string coordinate = parts[0].Trim();

            // Tolerate a header line ("well,absorbance") without treating it as data.
            if (coordinate.Equals("well", StringComparison.OrdinalIgnoreCase))
                continue;

            if (coordinate.Length == 0)
                throw new DomainException($"Malformed plate reading line '{line}': the well coordinate is empty.");

            if (!decimal.TryParse(parts[1].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal absorbance))
                throw new DomainException(
                    $"Malformed plate reading line '{line}': '{parts[1].Trim()}' is not a valid absorbance.");

            readings.Add(new PlateReading(coordinate, absorbance));
        }

        if (readings.Count == 0)
            throw new DomainException("The plate reading CSV contains no readings.");

        return readings;
    }
}

/// <summary>A single parsed reading: the well coordinate (e.g. "A1") and its raw absorbance.</summary>
internal sealed record PlateReading(string Coordinate, decimal Absorbance);
