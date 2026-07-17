using FluentValidation;
using SISLAB.Modules.Inventory.Domain.StorageLocations;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.StorageLocations.Commands;

/// <summary>
/// Registers a new storage location for the active company (card [E7] #112): its <paramref name="Name"/>
/// (required), <paramref name="Type"/> (fixed at creation — see the update command, which deliberately
/// cannot change it) and an optional free-text <paramref name="Description"/>. A target temperature range
/// (<paramref name="TemperatureMinCelsius"/>/<paramref name="TemperatureMaxCelsius"/>) may only be supplied
/// for a <see cref="StorageLocationType.Refrigerated"/> location; the aggregate rejects it for any other
/// type. Returns the new location id.
/// </summary>
/// <remarks>
/// The company is never part of the payload: it is stamped by the write-side global query filter / tenant
/// context on <c>SaveChanges</c>, exactly like every other Inventory command. The aggregate owns the
/// name/description trimming, the max-length guards and the temperature-range-matches-type invariant, so the
/// handler only translates the primitive temperature bounds into a <see cref="TemperatureRange"/> value object
/// and delegates to <see cref="StorageLocation.Register"/>.
/// </remarks>
public sealed record RegisterStorageLocationCommand(
    string Name,
    StorageLocationType Type,
    string? Description,
    decimal? TemperatureMinCelsius,
    decimal? TemperatureMaxCelsius) : ICommand<Guid>;

internal sealed class RegisterStorageLocationCommandValidator
    : AbstractValidator<RegisterStorageLocationCommand>
{
    public RegisterStorageLocationCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Description).MaximumLength(500);

        // Both temperature bounds travel together: either none or both. The bounds ordering and the
        // "only a refrigerated location may declare a range" invariant are enforced by the aggregate.
        When(command => command.TemperatureMinCelsius.HasValue || command.TemperatureMaxCelsius.HasValue, () =>
        {
            RuleFor(command => command.TemperatureMinCelsius).NotNull();
            RuleFor(command => command.TemperatureMaxCelsius).NotNull();
        });
    }
}

internal sealed class RegisterStorageLocationCommandHandler
    : ICommandHandler<RegisterStorageLocationCommand, Guid>
{
    private readonly IStorageLocationRepository _storageLocations;

    public RegisterStorageLocationCommandHandler(IStorageLocationRepository storageLocations)
        => _storageLocations = storageLocations;

    public async Task<Guid> HandleAsync(
        RegisterStorageLocationCommand request,
        CancellationToken cancellationToken = default)
    {
        TemperatureRange? temperatureRange = BuildTemperatureRange(request);

        StorageLocation location = StorageLocation.Register(
            request.Name,
            request.Type,
            request.Description,
            temperatureRange);

        await _storageLocations.AddAsync(location, cancellationToken);

        return location.Id;
    }

    /// <summary>
    /// Translates the optional primitive bounds into a <see cref="TemperatureRange"/> value object. The
    /// validator guarantees the bounds arrive together; the value object guards the ordering and the
    /// aggregate guards the "refrigerated only" rule.
    /// </summary>
    private static TemperatureRange? BuildTemperatureRange(RegisterStorageLocationCommand request)
        => request.TemperatureMinCelsius.HasValue && request.TemperatureMaxCelsius.HasValue
            ? TemperatureRange.Between(request.TemperatureMinCelsius.Value, request.TemperatureMaxCelsius.Value)
            : null;
}
