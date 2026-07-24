using FluentValidation;
using SISLAB.Modules.Configuration.Domain.ExperimentalModels;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.ExperimentalModels;

/// <summary>
/// Creates a per-tenant experimental model (SISLAB-04, sub-task 1): the reusable induction-protocol template a lab
/// cadasters once and later binds batches to. Write-side: it maps the flat command payload onto the aggregate's
/// value objects (each owning its own invariant) and lets the unit of work commit. Returns the new model id.
/// </summary>
/// <remarks>
/// Nothing here is lab-specific — the induction counts, timepoints, applicable parameters, standard groups and the
/// g:µL relation + diluent are all supplied by the caller and validated by the domain value objects, never
/// hard-coded. The FluentValidation layer only guards the shallow shape (required fields, lengths); the deep
/// invariants (min ≤ interval, unique group names, dose group has a dose) stay in the domain.
/// </remarks>
public sealed record CreateExperimentalModelCommand(
    string Name,
    string? Description,
    InductionProtocolInput Induction,
    IReadOnlyList<string> Timepoints,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<StandardGroupInput> Groups,
    DilutionDefaultsInput DilutionDefaults) : ICommand<Guid>;

/// <summary>Flat input for the induction protocol (administrations, spacing, reference day).</summary>
public sealed record InductionProtocolInput(int Administrations, int IntervalDays, int ReferenceDayAfterInduction);

/// <summary>Flat input for one standard group: name, role and (for a dose arm) the dose amount + unit.</summary>
public sealed record StandardGroupInput(string Name, StandardGroupKind Kind, decimal? DoseAmount, string? DoseUnit);

/// <summary>Flat input for the default dilution parameters (g:µL relation + default diluent).</summary>
public sealed record DilutionDefaultsInput(decimal MicrolitresPerGram, string DefaultDiluent);

internal sealed class CreateExperimentalModelCommandValidator : AbstractValidator<CreateExperimentalModelCommand>
{
    public CreateExperimentalModelCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Description).MaximumLength(500);

        RuleFor(command => command.Induction).NotNull();
        RuleFor(command => command.Induction.Administrations)
            .GreaterThanOrEqualTo(1)
            .When(command => command.Induction is not null);

        RuleFor(command => command.Timepoints)
            .NotNull()
            .Must(timepoints => timepoints is { Count: > 0 })
            .WithMessage("An experimental model must define at least one timepoint.");

        RuleFor(command => command.Groups)
            .NotNull()
            .Must(groups => groups is { Count: > 0 })
            .WithMessage("An experimental model must define at least one standard group.");

        RuleFor(command => command.DilutionDefaults).NotNull();
        RuleFor(command => command.DilutionDefaults.MicrolitresPerGram)
            .GreaterThan(0)
            .When(command => command.DilutionDefaults is not null);
        RuleFor(command => command.DilutionDefaults.DefaultDiluent)
            .NotEmpty()
            .MaximumLength(120)
            .When(command => command.DilutionDefaults is not null);
    }
}

internal sealed class CreateExperimentalModelCommandHandler : ICommandHandler<CreateExperimentalModelCommand, Guid>
{
    private readonly IExperimentalModelRepository _models;

    public CreateExperimentalModelCommandHandler(IExperimentalModelRepository models) => _models = models;

    public async Task<Guid> HandleAsync(
        CreateExperimentalModelCommand request,
        CancellationToken cancellationToken = default)
    {
        ExperimentalModel model = ExperimentalModel.Create(
            request.Name,
            request.Description,
            InductionProtocol.Of(
                request.Induction.Administrations,
                request.Induction.IntervalDays,
                request.Induction.ReferenceDayAfterInduction),
            StandardTimepoints.From(request.Timepoints),
            ApplicableParameters.From(request.Parameters),
            StandardGroups.From(request.Groups.Select(ToDomainGroup)),
            DilutionDefaults.Of(
                request.DilutionDefaults.MicrolitresPerGram,
                request.DilutionDefaults.DefaultDiluent));

        await _models.AddAsync(model, cancellationToken);

        return model.Id;
    }

    private static StandardGroup ToDomainGroup(StandardGroupInput input)
        => input.Kind == StandardGroupKind.Dose
            ? StandardGroup.Dosed(input.Name, input.DoseAmount ?? 0m, input.DoseUnit ?? string.Empty)
            : StandardGroup.NonDosed(input.Name, input.Kind);
}
