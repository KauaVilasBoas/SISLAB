using FluentValidation;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Application.Equipments.Commands;

/// <summary>
/// Registers a new equipment for the active company. Brand, model, storage location and calibration are
/// optional; the equipment starts <see cref="EquipmentStatus.Available"/> unless another initial status
/// is supplied. When both calibration dates are given the schedule is created; a null last-calibration
/// date leaves calibration as not applicable (n/a). The company comes from <c>ITenantContext</c> and is
/// stamped by the tenant save interceptor, never from the payload.
/// </summary>
public sealed record RegisterEquipmentCommand(
    string Name,
    string AssetTag,
    string? Brand,
    string? Model,
    Guid? StorageLocationId,
    EquipmentStatus Status,
    DateOnly? LastCalibration,
    DateOnly? NextCalibration) : ICommand<Guid>;

internal sealed class RegisterEquipmentCommandValidator : AbstractValidator<RegisterEquipmentCommand>
{
    public RegisterEquipmentCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.AssetTag).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Status).IsInEnum();

        // A next-due date only makes sense together with a last-calibration date.
        RuleFor(command => command.LastCalibration)
            .NotNull()
            .When(command => command.NextCalibration is not null)
            .WithMessage("A next calibration date requires a last calibration date.");
    }
}

internal sealed class RegisterEquipmentCommandHandler : ICommandHandler<RegisterEquipmentCommand, Guid>
{
    private readonly IEquipmentRepository _equipments;

    public RegisterEquipmentCommandHandler(IEquipmentRepository equipments) => _equipments = equipments;

    public async Task<Guid> HandleAsync(
        RegisterEquipmentCommand request,
        CancellationToken cancellationToken = default)
    {
        CalibrationSchedule? calibration = request.LastCalibration is { } last
            ? CalibrationSchedule.Create(last, request.NextCalibration)
            : null;

        Equipment equipment = Equipment.Register(
            request.Name,
            request.AssetTag,
            request.Brand,
            request.Model,
            request.StorageLocationId,
            request.Status,
            calibration);

        await _equipments.AddAsync(equipment, cancellationToken);

        return equipment.Id;
    }
}
