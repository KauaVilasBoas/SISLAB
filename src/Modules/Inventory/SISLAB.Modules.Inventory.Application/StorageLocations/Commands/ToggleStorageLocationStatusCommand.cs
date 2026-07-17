using FluentValidation;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StorageLocations.Commands;

/// <summary>
/// Puts a storage location in or out of service (card [E7] #112). A deactivated location is preserved for the
/// traceability of the movements already recorded against it — it is never deleted — but can no longer receive
/// stock (the aggregate's <see cref="StorageLocation.EnsureCanStore"/> guard rejects an inactive destination on
/// transfer/entry). Setting <paramref name="IsActive"/> to the location's current state is a no-op: the
/// aggregate's <see cref="StorageLocation.Deactivate"/>/<see cref="StorageLocation.Reactivate"/> are idempotent.
/// </summary>
/// <remarks>
/// A single command with a target state (rather than two separate activate/deactivate commands) mirrors the
/// PATCH <c>/status</c> endpoint the UI toggle calls and keeps the write-side symmetric with the aggregate's
/// idempotent pair. An unknown id (or one belonging to another company) is a <see cref="NotFoundException"/>.
/// </remarks>
public sealed record ToggleStorageLocationStatusCommand(
    Guid StorageLocationId,
    bool IsActive) : ICommand;

internal sealed class ToggleStorageLocationStatusCommandValidator
    : AbstractValidator<ToggleStorageLocationStatusCommand>
{
    public ToggleStorageLocationStatusCommandValidator()
        => RuleFor(command => command.StorageLocationId).NotEmpty();
}

internal sealed class ToggleStorageLocationStatusCommandHandler
    : ICommandHandler<ToggleStorageLocationStatusCommand>
{
    private readonly IStorageLocationRepository _storageLocations;

    public ToggleStorageLocationStatusCommandHandler(IStorageLocationRepository storageLocations)
        => _storageLocations = storageLocations;

    public async Task<Unit> HandleAsync(
        ToggleStorageLocationStatusCommand request,
        CancellationToken cancellationToken = default)
    {
        StorageLocation location = await _storageLocations.FindByIdAsync(request.StorageLocationId, cancellationToken)
            ?? throw new NotFoundException("StorageLocation", request.StorageLocationId);

        if (request.IsActive)
            location.Reactivate();
        else
            location.Deactivate();

        await _storageLocations.UpdateAsync(location, cancellationToken);

        return Unit.Value;
    }
}
