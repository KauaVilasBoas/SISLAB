using SISLAB.Modules.Experiments.Domain.Projects;

namespace SISLAB.Modules.Experiments.Tests.Domain;

/// <summary>
/// Covers the animal-selection invariant on the Project aggregate (SISLAB-02): applying an inclusion criterion marks
/// each animal included/excluded from its latest reading and records the deciding value; a criterion whose parameter
/// is not applicable to the model is skipped (non-blocking).
/// </summary>
public sealed class AnimalSelectionTests
{
    private static readonly DateTime When = new(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc);

    // A simple threshold rule (parameter ≥ threshold) standing in for the Configuration-backed rule.
    private sealed class AtLeastRule : IInclusionRule
    {
        private readonly decimal _threshold;

        public AtLeastRule(string parameterCode, decimal threshold)
        {
            ParameterCode = parameterCode;
            _threshold = threshold;
        }

        public string ParameterCode { get; }

        public bool AppliesTo(string parameterCode)
            => string.Equals(ParameterCode, parameterCode, StringComparison.OrdinalIgnoreCase);

        public bool QualifiedBy(decimal measuredValue) => measuredValue >= _threshold;

        public string Describe(decimal measuredValue, bool qualified)
            => $"{ParameterCode} {measuredValue} vs {_threshold} — {(qualified ? "in" : "out")}";
    }

    private static (Project project, Guid batchId, Animal animal) ProjectWithOneAnimal()
    {
        Project project = Project.Create("Neuropatia diabética", "Rattus norvegicus");
        Batch batch = project.AddBatch("Leva 1");
        Group group = project.AddGroup(batch.Id, "Curva", Dose.Of(3m, "g/kg"));
        Animal animal = project.AddAnimal(batch.Id, group.Id, "M1-01", AnimalSex.Male);
        return (project, batch.Id, animal);
    }

    private static IReadOnlySet<string> Applicable(params string[] codes)
        => new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Applying_includes_an_animal_whose_reading_meets_the_threshold()
    {
        (Project project, Guid batchId, Animal animal) = ProjectWithOneAnimal();
        project.RecordPhysiologicalReading(animal.Id, "glicemia", 268m, "mg/dL", "pós-indução", "vic@lab", When);

        int decided = project.ApplyInclusionCriteria(
            batchId, [new AtLeastRule("glicemia", 250m)], Applicable("glicemia"));

        Assert.Equal(1, decided);
        Assert.NotNull(animal.Inclusion);
        Assert.Equal(AnimalInclusionStatus.Included, animal.Inclusion!.Status);
        Assert.Equal(268m, animal.Inclusion.DecidingValue);
        Assert.Equal("glicemia", animal.Inclusion.ParameterCode);
    }

    [Fact]
    public void Applying_excludes_an_animal_whose_reading_is_below_the_threshold()
    {
        (Project project, Guid batchId, Animal animal) = ProjectWithOneAnimal();
        project.RecordPhysiologicalReading(animal.Id, "glicemia", 214m, "mg/dL", "pós-indução", "vic@lab", When);

        project.ApplyInclusionCriteria(batchId, [new AtLeastRule("glicemia", 250m)], Applicable("glicemia"));

        Assert.Equal(AnimalInclusionStatus.Excluded, animal.Inclusion!.Status);
        Assert.Equal(214m, animal.Inclusion.DecidingValue);
    }

    [Fact]
    public void A_criterion_on_an_inapplicable_parameter_does_not_block_or_decide()
    {
        (Project project, Guid batchId, Animal animal) = ProjectWithOneAnimal();
        // The animal has a glicemia reading that would be excluded, but the model does not list glicemia as
        // applicable (e.g. a non-diabetic model), so the criterion is skipped — the animal stays undecided.
        project.RecordPhysiologicalReading(animal.Id, "glicemia", 100m, "mg/dL", "pós-indução", "vic@lab", When);

        int decided = project.ApplyInclusionCriteria(
            batchId, [new AtLeastRule("glicemia", 250m)], Applicable("peso", "rotarod"));

        Assert.Equal(0, decided);
        Assert.Null(animal.Inclusion);
    }

    [Fact]
    public void Applying_uses_the_latest_reading_by_instant()
    {
        (Project project, Guid batchId, Animal animal) = ProjectWithOneAnimal();
        project.RecordPhysiologicalReading(animal.Id, "glicemia", 214m, "mg/dL", "basal", "vic@lab", When);
        project.RecordPhysiologicalReading(
            animal.Id, "glicemia", 268m, "mg/dL", "pós-indução", "dai@lab", When.AddDays(28));

        project.ApplyInclusionCriteria(batchId, [new AtLeastRule("glicemia", 250m)], Applicable("glicemia"));

        Assert.Equal(AnimalInclusionStatus.Included, animal.Inclusion!.Status);
        Assert.Equal(268m, animal.Inclusion.DecidingValue);
    }

    [Fact]
    public void An_animal_without_a_reading_for_the_parameter_is_left_undecided()
    {
        (Project project, Guid batchId, Animal animal) = ProjectWithOneAnimal();

        int decided = project.ApplyInclusionCriteria(
            batchId, [new AtLeastRule("glicemia", 250m)], Applicable("glicemia"));

        Assert.Equal(0, decided);
        Assert.Null(animal.Inclusion);
    }
}
