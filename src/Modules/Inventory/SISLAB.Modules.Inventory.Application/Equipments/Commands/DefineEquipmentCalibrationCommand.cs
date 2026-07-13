using FluentValidation;
using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Equipments.Commands;

/// <summary>
/// Defines or replaces an equipment's calibration schedule (last calibration + optional next-due date).
/// Passing a null last-calibration date clears the schedule, marking calibration as not applicable (n/a)
/// for equipment that does not require it (e.g. a vortex). The derived "overdue" state is computed on
/// read from the schedule and a clock; it is never stored.
/// </summary>
public sealed record DefineEquipmentCalibrationCommand(
    Guid EquipmentId,
    DateOnly? LastCalibration,
    DateOnly? NextCalibration) : ICommand;

internal sealed class DefineEquipmentCalibrationCommandValidator
    : AbstractValidator<DefineEquipmentCalibrationCommand>
{
    public DefineEquipmentCalibrationCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();

        // Clearing the schedule (null last) also clears any next-due date.
        RuleFor(command => command.LastCalibration)
            .NotNull()
            .When(command => command.NextCalibration is not null)
            .WithMessage("A next calibration date requires a last calibration date.");
    }
}

internal sealed class DefineEquipmentCalibrationCommandHandler
    : ICommandHandler<DefineEquipmentCalibrationCommand>
{
    private readonly IEquipmentRepository _equipments;
    private readonly InventoryAuditRecorder _audit;

    public DefineEquipmentCalibrationCommandHandler(
        IEquipmentRepository equipments,
        InventoryAuditRecorder audit)
    {
        _equipments = equipments;
        _audit = audit;
    }

    public async Task<Unit> HandleAsync(
        DefineEquipmentCalibrationCommand request,
        CancellationToken cancellationToken = default)
    {
        Equipment equipment = await _equipments.FindByIdAsync(request.EquipmentId, cancellationToken)
            ?? throw new NotFoundException("Equipment", request.EquipmentId);

        CalibrationSchedule? calibration = request.LastCalibration is { } last
            ? CalibrationSchedule.Create(last, request.NextCalibration)
            : null;

        equipment.DefineCalibration(calibration);

        await _equipments.UpdateAsync(equipment, cancellationToken);

        // Equipment interventions are always audited (card #57).
        await _audit.RecordEquipmentAsync(
            equipment.CompanyId,
            equipment.Id,
            InventoryAuditActions.EquipmentCalibration,
            new
            {
                request.LastCalibration,
                request.NextCalibration
            },
            cancellationToken);

        return Unit.Value;
    }
}
