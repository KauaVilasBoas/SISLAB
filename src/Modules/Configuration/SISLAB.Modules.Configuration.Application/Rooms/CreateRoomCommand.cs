using FluentValidation;
using SISLAB.Modules.Configuration.Domain.Rooms;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.Rooms;

/// <summary>
/// Creates a new room for the active company (card [E12] #76), with the "requires authorization" flag the
/// future Agenda module will consume. Write-side: it builds the aggregate through its factory and lets the
/// unit of work commit. Returns the new room id.
/// </summary>
public sealed record CreateRoomCommand(string Name, bool RequiresAuthorization) : ICommand<Guid>;

internal sealed class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomCommandValidator()
        => RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
}

internal sealed class CreateRoomCommandHandler : ICommandHandler<CreateRoomCommand, Guid>
{
    private readonly IRoomRepository _rooms;

    public CreateRoomCommandHandler(IRoomRepository rooms) => _rooms = rooms;

    public async Task<Guid> HandleAsync(
        CreateRoomCommand request,
        CancellationToken cancellationToken = default)
    {
        Room room = Room.Create(request.Name, request.RequiresAuthorization);
        await _rooms.AddAsync(room, cancellationToken);

        return room.Id;
    }
}
