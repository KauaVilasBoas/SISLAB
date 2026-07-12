using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Tests.Domain.Partners;

public sealed class SampleNoteTests
{
    [Fact]
    public void Create_trims_the_reference_and_status()
    {
        SampleNote sample = SampleNote.Create("  GDA-43  ", "  pendente  ");

        Assert.Equal("GDA-43", sample.Reference);
        Assert.Equal("pendente", sample.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_leaves_a_blank_status_null(string? status)
    {
        SampleNote sample = SampleNote.Create("GDA-92", status);

        Assert.Null(sample.Status);
    }

    [Fact]
    public void Create_rejects_a_blank_reference()
        => Assert.Throws<DomainException>(() => SampleNote.Create("   "));

    [Fact]
    public void ToString_renders_reference_and_status()
    {
        Assert.Equal("GDA-43 · pendente", SampleNote.Create("GDA-43", "pendente").ToString());
        Assert.Equal("GDA-43", SampleNote.Create("GDA-43").ToString());
    }

    [Fact]
    public void Two_notes_with_the_same_components_are_equal()
        => Assert.Equal(SampleNote.Create("GDA-1", "ok"), SampleNote.Create("GDA-1", "ok"));
}
