using FluentValidation;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StorageLocations.Commands;

/// <summary>
/// Corrects the descriptive metadata of an existing storage location (card [E7] #112): its
/// <paramref name="Name"/> and free-text <paramref name="Description"/>, plus — for a refrigerated location —
/// its target <see cref="TemperatureRange"/>. The location <b>type is intentionally not editable</b>: it is
/// fixed at creation because it drives the storage rules (a controlled box vs. a general shelf) and the
/// movement history already recorded against it; changing it would silently reclassify past stock. Correcting a
/// wrong type is a deactivate-and-recreate flow, not an edit.
/// </summary>
/// <remarks>
/// Passing a null/blank description clears it; passing null temperature bounds clears the range. Because the
/// type is immutable here, supplying a temperature range for a non-refrigerated location is rejected by the
/// aggregate's <see cref="StorageLocation.DefineTemperatureRange"/> guard — the same invariant the create
/// command relies on. An unknown location id (or one belonging to another company, which the tenant-scoped
/// repository cannot see) is a <see cref="NotFoundException"/>.
/// </remarks>
public sealed record UpdateStorageLocationCommand(
    Guid StorageLocationId,
    string Name,
    string? Description,
    decimal? TemperatureMinCelsius,
    decimal? TemperatureMaxCelsius) : ICommand;

internal sealed class UpdateStorageLocationCommandValidator
    : AbstractValidator<UpdateStorageLocationCommand>
{
    public UpdateStorageLocationCommandValidator()
    {
        RuleFor(command => command.StorageLocationId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);

        When(command => command.TemperatureMinCelsius.HasValue || command.TemperatureMaxCelsius.HasValue, () =>
        {
            RuleFor(command => command.TemperatureMinCelsius).NotNull();
            RuleFor(command => command.TemperatureMaxCelsius).NotNull();
        });
    }
}

internal sealed class UpdateStorageLocationCommandHandler : ICommandHandler<UpdateStorageLocationCommand>
{
    private readonly IStorageLocationRepository _storageLocations;

    public UpdateStorageLocationCommandHandler(IStorageLocationRepository storageLocations)
        => _storageLocations = storageLocations;

    public async Task<Unit> HandleAsync(
        UpdateStorageLocationCommand request,
        CancellationToken cancellationToken = default)
    {
        StorageLocation location = await _storageLocations.FindByIdAsync(request.StorageLocationId, cancellationToken)
            ?? throw new NotFoundException("StorageLocation", request.StorageLocationId);

        location.Rename(request.Name);
        location.DescribeAs(request.Description);
        location.DefineTemperatureRange(BuildTemperatureRange(request));

        await _storageLocations.UpdateAsync(location, cancellationToken);

        return Unit.Value;
    }

    private static TemperatureRange? BuildTemperatureRange(UpdateStorageLocationCommand request)
        => request.TemperatureMinCelsius.HasValue && request.TemperatureMaxCelsius.HasValue
            ? TemperatureRange.Between(request.TemperatureMinCelsius.Value, request.TemperatureMaxCelsius.Value)
            : null;
}
