using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Equipments.Commands;

/// <summary>
/// Moves an equipment to a new operational status (in use / available / under maintenance / inactive).
/// The transition policy is enforced by the aggregate: an unsupported move is rejected as a domain
/// error; moving to the current status is a no-op.
/// </summary>
public sealed record ChangeEquipmentStatusCommand(
    Guid EquipmentId,
    EquipmentStatus Status) : ICommand;

internal sealed class ChangeEquipmentStatusCommandValidator : AbstractValidator<ChangeEquipmentStatusCommand>
{
    public ChangeEquipmentStatusCommandValidator()
    {
        RuleFor(command => command.EquipmentId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
    }
}

internal sealed class ChangeEquipmentStatusCommandHandler : ICommandHandler<ChangeEquipmentStatusCommand>
{
    private readonly IEquipmentRepository _equipments;

    public ChangeEquipmentStatusCommandHandler(IEquipmentRepository equipments) => _equipments = equipments;

    public async Task<Unit> HandleAsync(
        ChangeEquipmentStatusCommand request,
        CancellationToken cancellationToken = default)
    {
        Equipment equipment = await _equipments.FindByIdAsync(request.EquipmentId, cancellationToken)
            ?? throw new NotFoundException("Equipment", request.EquipmentId);

        equipment.ChangeStatus(request.Status);

        await _equipments.UpdateAsync(equipment, cancellationToken);

        return Unit.Value;
    }
}
