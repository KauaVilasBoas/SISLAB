using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Enrols an animal into a group of a batch (card [E11] #73). The <paramref name="Identifier"/> (ear tag / cage
/// code) is kept unique across the whole project by the aggregate, so a timepoint launch can name the animal
/// unambiguously. <paramref name="WeightGrams"/> is an optional baseline body weight. Returns the new animal id.
/// </summary>
public sealed record AddAnimalCommand(
    Guid ProjectId,
    Guid BatchId,
    Guid GroupId,
    string Identifier,
    AnimalSex Sex,
    decimal? WeightGrams) : ICommand<Guid>;

internal sealed class AddAnimalCommandValidator : AbstractValidator<AddAnimalCommand>
{
    public AddAnimalCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.Identifier).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Sex).IsInEnum();
        RuleFor(command => command.WeightGrams).GreaterThanOrEqualTo(0).When(c => c.WeightGrams.HasValue);
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

        Animal animal = project.AddAnimal(
            request.BatchId,
            request.GroupId,
            request.Identifier,
            request.Sex,
            request.WeightGrams);

        await _projects.UpdateAsync(project, cancellationToken);

        return animal.Id;
    }
}
