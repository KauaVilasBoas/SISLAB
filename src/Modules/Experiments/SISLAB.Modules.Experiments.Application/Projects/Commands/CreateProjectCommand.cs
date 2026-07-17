using FluentValidation;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Experiments.Application.Projects.Commands;

/// <summary>
/// Creates a new in vivo experimental design (card [E11] #73, decision F1) for the active company: its
/// <paramref name="Name"/> (required), the animal <paramref name="Species"/> under study and an optional
/// <paramref name="Description"/>. Returns the new project id. The project starts in Draft at design version 1;
/// batches, groups and animals are added by the follow-up design commands.
/// </summary>
/// <remarks>
/// The company is never in the payload — it is stamped by the write-side tenant machinery on <c>SaveChanges</c>.
/// The aggregate owns trimming/guards and raises its creation event, so the handler only builds it and adds it to
/// the write set.
/// </remarks>
public sealed record CreateProjectCommand(
    string Name,
    string Species,
    string? Description) : ICommand<Guid>;

internal sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Species).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(2000);
    }
}

internal sealed class CreateProjectCommandHandler : ICommandHandler<CreateProjectCommand, Guid>
{
    private readonly IProjectRepository _projects;

    public CreateProjectCommandHandler(IProjectRepository projects) => _projects = projects;

    public async Task<Guid> HandleAsync(CreateProjectCommand request, CancellationToken cancellationToken = default)
    {
        Project project = Project.Create(request.Name, request.Species, request.Description);

        await _projects.AddAsync(project, cancellationToken);

        return project.Id;
    }
}
