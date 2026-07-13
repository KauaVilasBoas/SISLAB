using FluentValidation;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;
using UnitAggregate = SISLAB.Modules.Configuration.Domain.Units.Unit;
using IUnitRepository = SISLAB.Modules.Configuration.Domain.Units.IUnitRepository;

namespace SISLAB.Modules.Configuration.Application.Units;

/// <summary>
/// Creates a new unit of measure/consumption for the active company (card [E12] #76). Write-side: it guards
/// the symbol's uniqueness within the tenant, builds the aggregate through its factory and lets the unit of
/// work commit. Returns the new unit id.
/// </summary>
public sealed record CreateUnitCommand(string Symbol, string Name) : ICommand<Guid>;

internal sealed class CreateUnitCommandValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitCommandValidator()
    {
        RuleFor(command => command.Symbol).NotEmpty().MaximumLength(20);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(80);
    }
}

internal sealed class CreateUnitCommandHandler : ICommandHandler<CreateUnitCommand, Guid>
{
    private readonly IUnitRepository _units;

    public CreateUnitCommandHandler(IUnitRepository units) => _units = units;

    public async Task<Guid> HandleAsync(
        CreateUnitCommand request,
        CancellationToken cancellationToken = default)
    {
        UnitAggregate? existing = await _units.FindBySymbolAsync(request.Symbol, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A unit with symbol '{request.Symbol.Trim()}' already exists.");

        UnitAggregate unit = UnitAggregate.Create(request.Symbol, request.Name);
        await _units.AddAsync(unit, cancellationToken);

        return unit.Id;
    }
}
