using SISLAB.Modules.Experiments.Application.Experiments.Commands;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Plates;
using SISLAB.Modules.Experiments.Tests.Domain;
using SISLAB.Modules.Experiments.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Application;

/// <summary>
/// Application tests for the responsibility write path (card [E11]): assigning the lead/step responsibles (with
/// the cross-module membership guard) and the responsibility-based edit authorization enforced by the existing
/// write commands (403 for a non-responsible).
/// </summary>
public sealed class ExperimentResponsibilityCommandTests
{
    private static readonly FixedClock Clock = new(new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc));
    private static readonly FakeActorAccessor Actor = new("alice@lab");
    private static readonly Guid Company = Guid.Parse("dddddddd-0000-0000-0000-000000000009");
    private static readonly Guid Lead = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Outsider = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    [Fact]
    public async Task AssignResponsible_sets_the_lead_when_the_target_is_a_company_member()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new AssignExperimentResponsibleCommandHandler(
            experiments, new FakeCompanyMembershipQuery(Lead), new StubTenantContext(Company));

        await handler.HandleAsync(new AssignExperimentResponsibleCommand(experiment.Id, Lead));

        Assert.Equal(Lead, experiment.ResponsibleUserId);
        Assert.Same(experiment, experiments.LastUpdated);
    }

    [Fact]
    public async Task AssignResponsible_rejects_a_user_who_is_not_a_company_member()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        var experiments = new FakeExperimentRepository().Seed(experiment);
        // Membership fake allows only the lead — the outsider is not a member.
        var handler = new AssignExperimentResponsibleCommandHandler(
            experiments, new FakeCompanyMembershipQuery(Lead), new StubTenantContext(Company));

        await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new AssignExperimentResponsibleCommand(experiment.Id, Outsider)));
    }

    [Fact]
    public async Task AssignStepResponsible_adds_the_member_to_the_step()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        Guid step = experiment.Steps[0].Id;
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new AssignStepResponsibleCommandHandler(
            experiments, new FakeCompanyMembershipQuery(Lead), new StubTenantContext(Company));

        await handler.HandleAsync(new AssignStepResponsibleCommand(experiment.Id, step, Lead));

        Assert.True(experiment.Steps[0].IsResponsible(Lead));
    }

    [Fact]
    public async Task RemoveStepResponsible_removes_the_user()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        Guid step = experiment.Steps[0].Id;
        experiment.AssignStepResponsible(step, Lead);
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new RemoveStepResponsibleCommandHandler(experiments);

        await handler.HandleAsync(new RemoveStepResponsibleCommand(experiment.Id, step, Lead));

        Assert.False(experiment.Steps[0].IsResponsible(Lead));
    }

    [Fact]
    public async Task A_write_command_is_forbidden_for_a_non_responsible_once_responsibility_is_set()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.AssignResponsible(Lead); // gate now active; the current user is the outsider
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new DesignPlateCommandHandler(
            experiments, Actor, new FakeCurrentUserContext(Outsider), Clock);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(
            new DesignPlateCommand(experiment.Id,
            [
                new PlateWellDefinition('A', 1, WellRole.Blank, null, null),
            ])));
    }

    [Fact]
    public async Task The_lead_may_run_a_write_command()
    {
        ViabilidadeCelularExperiment experiment = ExperimentTestData.NewExperiment();
        experiment.AssignResponsible(Lead);
        var experiments = new FakeExperimentRepository().Seed(experiment);
        var handler = new DesignPlateCommandHandler(
            experiments, Actor, new FakeCurrentUserContext(Lead), Clock);

        await handler.HandleAsync(new DesignPlateCommand(experiment.Id,
        [
            new PlateWellDefinition('A', 1, WellRole.Blank, null, null),
        ]));

        Assert.True(experiment.Plate.IsDesigned);
    }
}
