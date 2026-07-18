using SISLAB.Modules.Agenda.Domain.Rooms;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Rooms.Commands;

public sealed record RegisterRoomCommand(
    string Name,
    int Capacity,
    RoomType Type) : ICommand<Guid>;

internal sealed class RegisterRoomCommandHandler : ICommandHandler<RegisterRoomCommand, Guid>
{
    private readonly IRoomRepository _rooms;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public RegisterRoomCommandHandler(
        IRoomRepository rooms,
        ITenantContext tenantContext,
        IClock clock)
    {
        _rooms = rooms;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public Task<Guid> HandleAsync(RegisterRoomCommand command, CancellationToken cancellationToken = default)
    {
        Room room = Room.Register(
            _tenantContext.CompanyId,
            command.Name,
            command.Capacity,
            command.Type,
            _clock.UtcNow);

        _rooms.Add(room);
        return Task.FromResult(room.Id);
    }
}
