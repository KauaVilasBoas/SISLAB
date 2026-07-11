using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Equipments;

/// <summary>
/// Updates an equipment's identification data (name, asset tag, brand, model and storage location).
/// Passing a null/blank brand or model clears it; passing a null storage location clears it. Does not
/// change the operational status, the calibration schedule nor the maintenance history; those have their
/// own operations.
/// </summary>
public sealed record UpdateEquipmentCommand(
    Guid EquipmentId,
    string Name,
    string AssetTag,
    string? Brand,
    string? Model,
    Guid? StorageLocationId) : ICommand;

internal sealed class UpdateEquipmentCommandValidator : AbstractValidator<UpdateEquipmentCommand>
{
    public UpdateEquipmentCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.AssetTag).NotEmpty().MaximumLength(60);
    }
}

internal sealed class UpdateEquipmentCommandHandler : ICommandHandler<UpdateEquipmentCommand>
{
    private readonly IEquipmentRepository _equipments;

    public UpdateEquipmentCommandHandler(IEquipmentRepository equipments) => _equipments = equipments;

    public async Task<Unit> HandleAsync(
        UpdateEquipmentCommand request,
        CancellationToken cancellationToken = default)
    {
        Equipment equipment = await _equipments.FindByIdAsync(request.EquipmentId, cancellationToken)
            ?? throw new NotFoundException("Equipment", request.EquipmentId);

        equipment.Rename(request.Name);
        equipment.ReassignAssetTag(request.AssetTag);
        equipment.DescribeModel(request.Brand, request.Model);
        equipment.RelocateTo(request.StorageLocationId);

        await _equipments.UpdateAsync(equipment, cancellationToken);

        return Unit.Value;
    }
}
