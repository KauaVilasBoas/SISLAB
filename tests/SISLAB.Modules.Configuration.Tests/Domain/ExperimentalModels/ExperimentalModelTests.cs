using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Tests.Domain.ExperimentalModels;

/// <summary>
/// Covers the <see cref="ExperimentalModel"/> aggregate (SISLAB-04): it is tenant-scoped, composes the validated
/// value objects, normalizes its name/description and exposes behaviour methods that swap each value object.
/// </summary>
public sealed class ExperimentalModelTests
{
    private static ExperimentalModel NewModel() =>
        ExperimentalModel.Create(
            "Neuropatia diabética",
            "Modelo ND com curva de dose.",
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

    [Fact]
    public void Create_composes_the_model_from_its_value_objects()
    {
        ExperimentalModel model = NewModel();

        Assert.Equal("Neuropatia diabética", model.Name);
        Assert.Equal(28, model.Induction.ReferenceDayAfterInduction);
        Assert.Equal(4, model.Groups.Groups.Count);
        Assert.Equal(5m, model.DilutionDefaults.Ratio.MicrolitresPerGram);
        Assert.Equal("Óleo de soja", model.DilutionDefaults.DefaultDiluent);
        Assert.True(model.Parameters.Applies("glicemia"));
    }

    [Fact]
    public void ExperimentalModel_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(NewModel());
    }

    [Fact]
    public void Create_trims_the_name_and_leaves_a_blank_description_null()
    {
        ExperimentalModel model = ExperimentalModel.Create(
            "  Ligadura de nervo ciático  ",
            "   ",
            InductionProtocol.Of(1, 0, 14),
            StandardTimepoints.From(["Basal"]),
            ApplicableParameters.None,
            StandardGroups.From([StandardGroup.NonDosed("Sham", StandardGroupKind.Control)]),
            DilutionDefaults.Of(10m, "Salina"));

        Assert.Equal("Ligadura de nervo ciático", model.Name);
        Assert.Null(model.Description);
    }

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() => ExperimentalModel.Create(
            "   ",
            null,
            InductionProtocol.Of(1, 0, 14),
            StandardTimepoints.From(["Basal"]),
            ApplicableParameters.None,
            StandardGroups.From([StandardGroup.NonDosed("Sham", StandardGroupKind.Control)]),
            DilutionDefaults.Of(10m, "Salina")));
    }

    [Fact]
    public void Rename_changes_the_name_keeping_identity()
    {
        ExperimentalModel model = NewModel();
        Guid id = model.Id;

        model.Rename("  ND crônico  ");

        Assert.Equal("ND crônico", model.Name);
        Assert.Equal(id, model.Id);
    }

    [Fact]
    public void Change_methods_swap_the_value_objects()
    {
        ExperimentalModel model = NewModel();

        model.ChangeInduction(InductionProtocol.Of(3, 2, 30));
        model.ChangeTimepoints(StandardTimepoints.From(["Basal", "30 dias"]));
        model.ChangeParameters(ApplicableParameters.From(["peso"]));
        model.ChangeGroups(StandardGroups.From([StandardGroup.NonDosed("Naive", StandardGroupKind.Naive)]));
        model.ChangeDilutionDefaults(DilutionDefaults.Of(8m, "PBS"));
        model.ChangeDescription(null);

        Assert.Equal(3, model.Induction.Administrations);
        Assert.Equal(2, model.Timepoints.Labels.Count);
        Assert.Equal(["peso"], model.Parameters.Codes);
        Assert.Single(model.Groups.Groups);
        Assert.Equal(8m, model.DilutionDefaults.Ratio.MicrolitresPerGram);
        Assert.Null(model.Description);
    }
}
