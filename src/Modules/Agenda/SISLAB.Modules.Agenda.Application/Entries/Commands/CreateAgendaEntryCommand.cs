using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Agenda.Application.Entries.Commands;

/// <summary>
/// Creates a new calendar entry (card [E10.2] #2). <see cref="ResponsibleId"/> is resolved by the controller
/// from the authenticated principal and passed in — never taken from the client body — so an entry is always
/// owned by a real member of the active company (defense-in-depth, section 7). <see cref="RecurrenceRule"/> is
/// the raw RRULE string; the handler validates it through <see cref="RecurrenceRuleSpec"/>.
/// </summary>
public sealed record CreateAgendaEntryCommand(
    string Title,
    string? Description,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsAllDay,
    AgendaActivityType ActivityType,
    Guid? ExperimentId,
    string? RecurrenceRule,
    Guid ResponsibleId) : ICommand<Guid>;

internal sealed class CreateAgendaEntryCommandHandler : ICommandHandler<CreateAgendaEntryCommand, Guid>
{
    private readonly IAgendaEntryRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public CreateAgendaEntryCommandHandler(
        IAgendaEntryRepository repository,
        ITenantContext tenantContext,
        IClock clock)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public Task<Guid> HandleAsync(CreateAgendaEntryCommand command, CancellationToken cancellationToken = default)
    {
        RecurrenceRuleSpec? recurrence = RecurrenceRuleSpec.CreateOptional(command.RecurrenceRule);

        AgendaEntry entry = AgendaEntry.Create(
            _tenantContext.CompanyId,
            command.Title,
            command.Description,
            command.StartDateUtc,
            command.EndDateUtc,
            command.IsAllDay,
            command.ActivityType,
            command.ExperimentId,
            recurrence,
            command.ResponsibleId,
            _clock.UtcNow);

        _repository.Add(entry);
        return Task.FromResult(entry.Id);
    }
}
