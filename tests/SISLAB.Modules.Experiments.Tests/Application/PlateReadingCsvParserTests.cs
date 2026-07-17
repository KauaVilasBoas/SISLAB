using SISLAB.Modules.Experiments.Application.Experiments;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

public sealed class PlateReadingCsvParserTests
{
    [Fact]
    public void Parse_reads_well_absorbance_pairs_and_ignores_blank_and_comment_lines()
    {
        const string csv =
            """
            # plate reader export
            well,absorbance
            A1,0.452

            B3, 1.10
            # end
            """;

        IReadOnlyList<PlateReading> readings = PlateReadingCsvParser.Parse(csv);

        Assert.Equal(2, readings.Count);
        Assert.Equal("A1", readings[0].Coordinate);
        Assert.Equal(0.452m, readings[0].Absorbance);
        Assert.Equal("B3", readings[1].Coordinate);
        Assert.Equal(1.10m, readings[1].Absorbance);
    }

    [Fact]
    public void Parse_rejects_a_malformed_line()
        => Assert.Throws<DomainException>(() => PlateReadingCsvParser.Parse("A1;0.5"));

    [Fact]
    public void Parse_rejects_a_non_numeric_absorbance()
        => Assert.Throws<DomainException>(() => PlateReadingCsvParser.Parse("A1,abc"));

    [Fact]
    public void Parse_rejects_an_empty_document()
        => Assert.Throws<DomainException>(() => PlateReadingCsvParser.Parse("   "));

    [Fact]
    public void Parse_rejects_a_document_with_only_a_header()
        => Assert.Throws<DomainException>(() => PlateReadingCsvParser.Parse("well,absorbance"));
}
