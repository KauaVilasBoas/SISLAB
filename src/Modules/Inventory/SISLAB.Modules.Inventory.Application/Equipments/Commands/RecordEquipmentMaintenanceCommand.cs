using FluentValidation;
using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Equipments.Commands;

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
    private readonly InventoryAuditRecorder _audit;

    public RecordEquipmentMaintenanceCommandHandler(
        IEquipmentRepository equipments,
        InventoryAuditRecorder audit)
    {
        _equipments = equipments;
        _audit = audit;
    }

    public async Task<Unit> HandleAsync(
        RecordEquipmentMaintenanceCommand request,
        CancellationToken cancellationToken = default)
    {
        Equipment equipment = await _equipments.FindByIdAsync(request.EquipmentId, cancellationToken)
            ?? throw new NotFoundException("Equipment", request.EquipmentId);

        equipment.RecordMaintenance(
            MaintenanceRecord.Create(request.Date, request.Type, request.Notes));

        await _equipments.UpdateAsync(equipment, cancellationToken);

        // Equipment interventions are always audited (card #57).
        await _audit.RecordEquipmentAsync(
            equipment.CompanyId,
            equipment.Id,
            InventoryAuditActions.EquipmentMaintenance,
            new
            {
                request.Date,
                Type = request.Type.ToString(),
                request.Notes
            },
            cancellationToken);

        return Unit.Value;
    }
}
