using SISLAB.Modules.Agenda.Domain.Bioterium;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Bioterium.Commands;

/// <summary>
/// Generates the Mon+Thu biotério assignments for the week starting on <see cref="MondayOfWeek"/> (card [E10] #70).
/// The round-robin picks the next responsible from the provided <see cref="ResponsibleNames"/> list. Skips
/// dates that already have an assignment (idempotent — safe to re-run).
/// </summary>
public sealed record GenerateBioteriumWeekCommand(
    DateOnly MondayOfWeek,
    /// <summary>Ordered member names; the algorithm cycles through them in sequence.</summary>
    IReadOnlyList<string> ResponsibleNames) : ICommand;

internal sealed class GenerateBioteriumWeekCommandHandler : ICommandHandler<GenerateBioteriumWeekCommand>
{
    private readonly IBioteriumRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public GenerateBioteriumWeekCommandHandler(
        IBioteriumRepository repository,
        ITenantContext tenantContext,
        IClock clock)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Unit> HandleAsync(GenerateBioteriumWeekCommand command, CancellationToken cancellationToken = default)
    {
        if (command.MondayOfWeek.DayOfWeek != DayOfWeek.Monday)
            throw new ArgumentException("MondayOfWeek must be a Monday.", nameof(command.MondayOfWeek));

        if (command.ResponsibleNames.Count == 0)
            throw new ArgumentException("At least one responsible name is required.", nameof(command.ResponsibleNames));

        DateTime now = _clock.UtcNow;
        DateOnly thursday = command.MondayOfWeek.AddDays(3);

        // Round-robin index: use the ISO week number mod member count so consecutive calls are stable.
        int isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(command.MondayOfWeek.ToDateTime(TimeOnly.MinValue));
        int mondayIndex = isoWeek % command.ResponsibleNames.Count;
        int thursdayIndex = (isoWeek + 1) % command.ResponsibleNames.Count;

        // Skip dates that already have an assignment (idempotency).
        if (!await _repository.ExistsForDateAsync(command.MondayOfWeek, cancellationToken))
            _repository.Add(BioteriumAssignment.Create(
                _tenantContext.CompanyId,
                command.MondayOfWeek,
                command.ResponsibleNames[mondayIndex],
                now));

        if (!await _repository.ExistsForDateAsync(thursday, cancellationToken))
            _repository.Add(BioteriumAssignment.Create(
                _tenantContext.CompanyId,
                thursday,
                command.ResponsibleNames[thursdayIndex],
                now));

        return Unit.Value;
    }
}
