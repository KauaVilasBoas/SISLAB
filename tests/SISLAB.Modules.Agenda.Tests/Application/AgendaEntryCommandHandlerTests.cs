using SISLAB.Modules.Agenda.Application.Entries.Commands;
using SISLAB.Modules.Agenda.Application.Entries.Conflicts;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.Modules.Agenda.Tests.Fakes;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Agenda.Tests.Application;

/// <summary>
/// Handler tests for the E10.2 write-side (card #2): create, the three Google-Calendar edit scopes, occurrence
/// cancellation and series deletion — asserting the aggregate operations the handlers orchestrate.
/// </summary>
public sealed class AgendaEntryCommandHandlerTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Responsible = Guid.NewGuid();
    private static readonly FixedClock Clock = new(new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
    private static readonly StubTenantContext Tenant = new(Company);
    private static readonly StubConflictChecker NoConflicts = new();
    private static readonly DateTime Start = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static CreateAgendaEntryCommand NewCreate(string? rrule = null) => new(
        "Standup", "sync", Start, End, IsAllDay: false, AgendaActivityType.Other,
        ExperimentId: null, RoomId: null, RecurrenceRule: rrule, Responsible);

    private static AgendaEntry SeedRecurring(FakeAgendaEntryRepository repo)
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "Standup", "sync", Start, End, false, AgendaActivityType.Other,
            null, null, RecurrenceRuleSpec.Create("FREQ=WEEKLY;BYDAY=FR"), Responsible, Start);
        repo.Seed(entry);
        return entry;
    }

    [Fact]
    public async Task Create_persists_entry_scoped_to_active_company()
    {
        var repo = new FakeAgendaEntryRepository();
        var handler = new CreateAgendaEntryCommandHandler(repo, Tenant, Clock, NoConflicts);

        Guid id = (await handler.HandleAsync(NewCreate("FREQ=DAILY"))).EntryId;

        AgendaEntry created = Assert.Single(repo.Added);
        Assert.Equal(id, created.Id);
        Assert.Equal(Company, created.CompanyId);
        Assert.True(created.IsRecurring);
        Assert.Equal(Responsible, created.ResponsibleId);
    }

    [Fact]
    public async Task Create_with_invalid_rrule_throws()
    {
        var handler = new CreateAgendaEntryCommandHandler(new FakeAgendaEntryRepository(), Tenant, Clock, NoConflicts);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(NewCreate("FREQ=NOPE")));
    }

    [Fact]
    public async Task Create_propagates_advisory_conflict_warnings_without_blocking()
    {
        var repo = new FakeAgendaEntryRepository();
        var conflicts = new StubConflictChecker(
            AgendaConflictWarnings.Person, AgendaConflictWarnings.Room);
        var handler = new CreateAgendaEntryCommandHandler(repo, Tenant, Clock, conflicts);

        AgendaEntryMutationResult result = await handler.HandleAsync(NewCreate());

        Assert.Single(repo.Added); // the write still succeeded — warnings never block
        Assert.Contains(AgendaConflictWarnings.Person, result.Warnings);
        Assert.Contains(AgendaConflictWarnings.Room, result.Warnings);
    }

    [Fact]
    public async Task Update_excludes_the_edited_entry_from_the_conflict_check()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry entry = SeedRecurring(repo);
        var conflicts = new StubConflictChecker();
        var handler = new UpdateAgendaEntryCommandHandler(repo, Tenant, Clock, conflicts);

        AgendaEntryMutationResult result = await handler.HandleAsync(new UpdateAgendaEntryCommand(
            entry.Id, EditScope.AllOccurrences, OccurrenceDate: null,
            "Retro", null, Start, End, false, AgendaActivityType.Other, null, null, "FREQ=WEEKLY;BYDAY=FR"));

        // A series must never conflict with itself: the id under check is excluded from the candidate set.
        Assert.Equal(result.EntryId, conflicts.LastExcludeEntryId);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Update_all_occurrences_edits_series_in_place()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry entry = SeedRecurring(repo);
        var handler = new UpdateAgendaEntryCommandHandler(repo, Tenant, Clock, NoConflicts);

        Guid resultId = (await handler.HandleAsync(new UpdateAgendaEntryCommand(
            entry.Id, EditScope.AllOccurrences, OccurrenceDate: null,
            "Retro", null, Start, End, false, AgendaActivityType.Presentation, null, null, "FREQ=WEEKLY;BYDAY=FR"))).EntryId;

        Assert.Equal(entry.Id, resultId);
        Assert.Equal("Retro", entry.Title);
        Assert.Empty(repo.Added); // no new entry created for an in-place edit
    }

    [Fact]
    public async Task Update_only_this_excludes_date_and_creates_detached_oneoff()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry entry = SeedRecurring(repo);
        var handler = new UpdateAgendaEntryCommandHandler(repo, Tenant, Clock, NoConflicts);
        var occurrence = new DateOnly(2026, 8, 7);

        Guid resultId = (await handler.HandleAsync(new UpdateAgendaEntryCommand(
            entry.Id, EditScope.OnlyThis, occurrence,
            "Moved", null, Start.AddHours(2), End.AddHours(2), false, AgendaActivityType.Other, null, null, null))).EntryId;

        Assert.Contains(occurrence, entry.ExcludedDates);
        AgendaEntry detached = Assert.Single(repo.Added);
        Assert.Equal(resultId, detached.Id);
        Assert.False(detached.IsRecurring);
        Assert.Equal("Moved", detached.Title);
    }

    [Fact]
    public async Task Update_this_and_following_truncates_original_and_forks_new_series()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry entry = SeedRecurring(repo);
        var handler = new UpdateAgendaEntryCommandHandler(repo, Tenant, Clock, NoConflicts);
        var occurrence = new DateOnly(2026, 8, 21);

        Guid resultId = (await handler.HandleAsync(new UpdateAgendaEntryCommand(
            entry.Id, EditScope.ThisAndFollowing, occurrence,
            "New series", null, Start, End, false, AgendaActivityType.Other, null, null, "FREQ=WEEKLY;BYDAY=FR"))).EntryId;

        Assert.Contains("UNTIL=", entry.RecurrenceRule!.Value); // original truncated
        AgendaEntry forked = Assert.Single(repo.Added);
        Assert.Equal(resultId, forked.Id);
        Assert.True(forked.IsRecurring);
        Assert.Equal("New series", forked.Title);
    }

    [Fact]
    public async Task Update_this_and_following_without_occurrence_date_throws()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry entry = SeedRecurring(repo);
        var handler = new UpdateAgendaEntryCommandHandler(repo, Tenant, Clock, NoConflicts);

        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateAgendaEntryCommand(
            entry.Id, EditScope.ThisAndFollowing, OccurrenceDate: null,
            "x", null, Start, End, false, AgendaActivityType.Other, null, null, "FREQ=WEEKLY")));
    }

    [Fact]
    public async Task Update_missing_entry_throws_not_found()
    {
        var handler = new UpdateAgendaEntryCommandHandler(new FakeAgendaEntryRepository(), Tenant, Clock, NoConflicts);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new UpdateAgendaEntryCommand(
            Guid.NewGuid(), EditScope.AllOccurrences, null,
            "x", null, Start, End, false, AgendaActivityType.Other, null, null, null)));
    }

    [Fact]
    public async Task CancelOccurrence_adds_exclusion()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry entry = SeedRecurring(repo);
        var handler = new CancelAgendaOccurrenceCommandHandler(repo);
        var day = new DateOnly(2026, 8, 14);

        await handler.HandleAsync(new CancelAgendaOccurrenceCommand(entry.Id, day));

        Assert.Contains(day, entry.ExcludedDates);
    }

    [Fact]
    public async Task CancelOccurrence_on_oneoff_throws_business_exception()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry oneOff = AgendaEntry.Create(
            Company, "x", null, Start, End, false, AgendaActivityType.Other, null, null, null, Responsible, Start);
        repo.Seed(oneOff);
        var handler = new CancelAgendaOccurrenceCommandHandler(repo);

        await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new CancelAgendaOccurrenceCommand(oneOff.Id, new DateOnly(2026, 8, 14))));
    }

    [Fact]
    public async Task Delete_removes_series()
    {
        var repo = new FakeAgendaEntryRepository();
        AgendaEntry entry = SeedRecurring(repo);
        var handler = new DeleteAgendaEntryCommandHandler(repo);

        await handler.HandleAsync(new DeleteAgendaEntryCommand(entry.Id));

        Assert.Single(repo.Removed);
        Assert.Same(entry, repo.Removed[0]);
    }

    [Fact]
    public async Task Delete_missing_entry_throws_not_found()
    {
        var handler = new DeleteAgendaEntryCommandHandler(new FakeAgendaEntryRepository());
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new DeleteAgendaEntryCommand(Guid.NewGuid())));
    }
}
