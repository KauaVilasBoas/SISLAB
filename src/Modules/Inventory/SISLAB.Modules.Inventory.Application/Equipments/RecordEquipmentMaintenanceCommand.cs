using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Equipments;

/// <summary>
/// Logs a maintenance event (date, type, optional note) into an equipment's append-only history. The
/// "responsible" of the maintenance is intentionally not captured here: the "who" is audit-trail data
/// owned by card [E9] #57, consistent with the aggregate's design note.
/// </summary>
public sealed record RecordEquipmentMaintenanceCommand(
    Guid EquipmentId,
    DateOnly Date,
    MaintenanceType Type,
    string? Notes) : ICommand;

internal sealed class RecordEquipmentMaintenanceCommandValidator
    : AbstractValidator<RecordEquipmentMaintenanceCommand>
{
    public RecordEquipmentMaintenanceCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();
        RuleFor(command => command.Date).NotEmpty();
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Notes).MaximumLength(1000);
    }
}

internal sealed class RecordEquipmentMaintenanceCommandHandler
    : ICommandHandler<RecordEquipmentMaintenanceCommand>
{
    private readonly IEquipmentRepository _equipments;

    public RecordEquipmentMaintenanceCommandHandler(IEquipmentRepository equipments)
        => _equipments = equipments;

    public async Task<Unit> HandleAsync(
        RecordEquipmentMaintenanceCommand request,
        CancellationToken cancellationToken = default)
    {
        Equipment equipment = await _equipments.FindByIdAsync(request.EquipmentId, cancellationToken)
            ?? throw new NotFoundException("Equipment", request.EquipmentId);

        equipment.RecordMaintenance(
            MaintenanceRecord.Create(request.Date, request.Type, request.Notes));

        await _equipments.UpdateAsync(equipment, cancellationToken);

        return Unit.Value;
    }
}
