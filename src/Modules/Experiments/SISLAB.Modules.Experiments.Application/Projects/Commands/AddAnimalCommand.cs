using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Houses an animal in a cage of a batch (card [E11] #73, SISLAB-03). The <paramref name="Identifier"/> (ear tag /
/// cage code) is kept unique across the whole project by the aggregate, so a timepoint launch can name the animal
/// unambiguously. <paramref name="WeightGrams"/> is an optional baseline body weight. The treatment
/// <paramref name="GroupId"/> is <b>optional</b>: omit it for the pre-randomization flow (the animal is measured basal
/// before being assigned a group), or supply it to assign the group at entry (the classic flow). Returns the new
/// animal id.
/// </summary>
public sealed record AddAnimalCommand(
    Guid ProjectId,
    Guid BatchId,
    Guid CageId,
    string Identifier,
    AnimalSex Sex,
    decimal? WeightGrams,
    Guid? GroupId = null) : ICommand<Guid>;

internal sealed class AddAnimalCommandValidator : AbstractValidator<AddAnimalCommand>
{
    public AddAnimalCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.CageId).NotEmpty();
        RuleFor(command => command.Identifier).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Sex).IsInEnum();
        RuleFor(command => command.WeightGrams).GreaterThanOrEqualTo(0).When(c => c.WeightGrams.HasValue);
        RuleFor(command => command.GroupId).NotEqual(Guid.Empty).When(c => c.GroupId.HasValue);
    }
}

internal sealed class AddAnimalCommandHandler : ICommandHandler<AddAnimalCommand, Guid>
{
    private readonly IProjectRepository _projects;

    public AddAnimalCommandHandler(IProjectRepository projects) => _projects = projects;

    public async Task<Guid> HandleAsync(AddAnimalCommand request, CancellationToken cancellationToken = default)
    {
        Project project = await _projects.FindByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project '{request.ProjectId}' was not found.");

        Animal animal = project.AddAnimalToCage(
            request.BatchId,
            request.CageId,
            request.Identifier,
            request.Sex,
            request.WeightGrams,
            request.GroupId);

        await _projects.UpdateAsync(project, cancellationToken);

        return animal.Id;
    }
}
