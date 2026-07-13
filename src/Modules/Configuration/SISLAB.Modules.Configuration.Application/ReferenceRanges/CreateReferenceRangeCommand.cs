using FluentValidation;
using SISLAB.Modules.Configuration.Domain.ReferenceRanges;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.ReferenceRanges;

/// <summary>
/// Creates a reference range for the active company (card [E12] #76): the healthy interval of an analyte,
/// scoped by species/strain. Write-side: it builds the aggregate through its factory (which owns the
/// "min ≤ max, at least one bound" invariant via <c>RangeBounds</c>) and lets the unit of work commit.
/// Returns the new range id.
/// </summary>
public sealed record CreateReferenceRangeCommand(
    string Analyte,
    string Species,
    decimal? Minimum,
    decimal? Maximum,
    string? Unit) : ICommand<Guid>;

internal sealed class CreateReferenceRangeCommandValidator : AbstractValidator<CreateReferenceRangeCommand>
{
    public CreateReferenceRangeCommandValidator()
    {
        RuleFor(command => command.Analyte).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Species).NotEmpty().MaximumLength(120);
        RuleFor(command => command)
            .Must(command => command.Minimum is not null || command.Maximum is not null)
            .WithMessage("A reference range must define at least a minimum or a maximum.");
    }
}

internal sealed class CreateReferenceRangeCommandHandler : ICommandHandler<CreateReferenceRangeCommand, Guid>
{
    private readonly IReferenceRangeRepository _ranges;

    public CreateReferenceRangeCommandHandler(IReferenceRangeRepository ranges) => _ranges = ranges;

    public async Task<Guid> HandleAsync(
        CreateReferenceRangeCommand request,
        CancellationToken cancellationToken = default)
    {
        ReferenceRange range = ReferenceRange.Create(
            request.Analyte,
            request.Species,
            request.Minimum,
            request.Maximum,
            request.Unit);

        await _ranges.AddAsync(range, cancellationToken);

        return range.Id;
    }
}
