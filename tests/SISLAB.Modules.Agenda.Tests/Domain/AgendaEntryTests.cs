using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.Modules.Agenda.Domain.Entries.Events;

namespace SISLAB.Modules.Agenda.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="AgendaEntry"/> aggregate (card [E10.1] #1): construction invariants, the
/// three edit-scope building blocks (all/only-this/this-and-following) and the domain events they raise.
/// </summary>
public sealed class AgendaEntryTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Responsible = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static AgendaEntry CreateSimple(RecurrenceRuleSpec? rule = null) => AgendaEntry.Create(
        Company, "Standup", "daily sync", Start, End,
        isAllDay: false, AgendaActivityType.Other, experimentId: null, roomId: null,
        recurrenceRule: rule, Responsible, createdAtUtc: Start);

    [Fact]
    public void Create_WithValidData_SetsFieldsAndRaisesCreatedEvent()
    {
        AgendaEntry entry = CreateSimple();

        Assert.Equal("Standup", entry.Title);
        Assert.Equal(Company, entry.CompanyId);
        Assert.False(entry.IsRecurring);
        Assert.Empty(entry.ExcludedDates);
        Assert.Single(entry.DomainEvents);
        AgendaEntryCreated created = Assert.IsType<AgendaEntryCreated>(entry.DomainEvents[0]);
        Assert.Equal(entry.Id, created.EntryId);
        Assert.False(created.IsRecurring);
    }

    [Fact]
    public void Create_TrimsTitleAndDescription()
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "  Standup  ", "  sync  ", Start, End, false,
            AgendaActivityType.Other, null, null, null, Responsible, Start);

        Assert.Equal("Standup", entry.Title);
        Assert.Equal("sync", entry.Description);
    }

    [Fact]
    public void Create_WithBlankTitle_Throws()
        => Assert.Throws<ArgumentException>(() => AgendaEntry.Create(
            Company, "   ", null, Start, End, false,
            AgendaActivityType.Other, null, null, null, Responsible, Start));

    [Fact]
    public void Create_TimedEntry_WithEndNotAfterStart_Throws()
        => Assert.Throws<ArgumentException>(() => AgendaEntry.Create(
            Company, "x", null, Start, Start, isAllDay: false,
            AgendaActivityType.Other, null, null, null, Responsible, Start));

    [Fact]
    public void Create_AllDayEntry_MayShareStartAndEnd()
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "Holiday", null, Start, Start, isAllDay: true,
            AgendaActivityType.Other, null, null, null, Responsible, Start);

        Assert.True(entry.IsAllDay);
    }

    [Fact]
    public void CancelOccurrence_OnRecurringEntry_ExcludesDateAndRaisesEvent()
    {
        AgendaEntry entry = CreateSimple(RecurrenceRuleSpec.Create("FREQ=DAILY"));
        entry.ClearDomainEvents();
        var day = new DateOnly(2026, 8, 3);

        entry.CancelOccurrence(day);

        Assert.Contains(day, entry.ExcludedDates);
        AgendaEntryOccurrenceCancelled evt =
            Assert.IsType<AgendaEntryOccurrenceCancelled>(Assert.Single(entry.DomainEvents));
        Assert.Equal(day, evt.OccurrenceDate);
    }

    [Fact]
    public void CancelOccurrence_IsIdempotent()
    {
        AgendaEntry entry = CreateSimple(RecurrenceRuleSpec.Create("FREQ=DAILY"));
        var day = new DateOnly(2026, 8, 3);

        entry.CancelOccurrence(day);
        entry.ClearDomainEvents();
        entry.CancelOccurrence(day);

        Assert.Single(entry.ExcludedDates);
        Assert.Empty(entry.DomainEvents);
    }

    [Fact]
    public void CancelOccurrence_OnNonRecurringEntry_Throws()
    {
        AgendaEntry entry = CreateSimple();
        Assert.Throws<InvalidOperationException>(() => entry.CancelOccurrence(new DateOnly(2026, 8, 3)));
    }

    [Fact]
    public void TruncateAt_RewritesUntilAndKeepsSeriesRecurring()
    {
        AgendaEntry entry = CreateSimple(RecurrenceRuleSpec.Create("FREQ=WEEKLY;BYDAY=MO"));
        var split = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

        entry.TruncateAt(split);

        Assert.True(entry.IsRecurring);
        Assert.Contains("UNTIL=", entry.RecurrenceRule!.Value);
    }

    [Fact]
    public void TruncateAt_OnNonRecurringEntry_Throws()
    {
        AgendaEntry entry = CreateSimple();
        Assert.Throws<InvalidOperationException>(() => entry.TruncateAt(Start));
    }

    [Fact]
    public void Reschedule_OverwritesFieldsAndRaisesUpdatedEvent()
    {
        AgendaEntry entry = CreateSimple();
        entry.ClearDomainEvents();
        var newStart = Start.AddDays(1);
        var newEnd = End.AddDays(1);

        entry.Reschedule("Retro", "post-mortem", newStart, newEnd, false,
            AgendaActivityType.Presentation, null, null, null);

        Assert.Equal("Retro", entry.Title);
        Assert.Equal(newStart, entry.StartDateUtc);
        Assert.Equal(AgendaActivityType.Presentation, entry.ActivityType);
        Assert.IsType<AgendaEntryUpdated>(Assert.Single(entry.DomainEvents));
    }

    [Fact]
    public void Reschedule_PreservesExistingExclusions()
    {
        AgendaEntry entry = CreateSimple(RecurrenceRuleSpec.Create("FREQ=DAILY"));
        var day = new DateOnly(2026, 8, 3);
        entry.CancelOccurrence(day);

        entry.Reschedule("Standup", null, Start, End, false, AgendaActivityType.Other, null, null,
            RecurrenceRuleSpec.Create("FREQ=DAILY"));

        Assert.Contains(day, entry.ExcludedDates);
    }

    [Fact]
    public void SetReminders_StoresRemindersAndCollapsesDuplicates()
    {
        AgendaEntry entry = CreateSimple();

        entry.SetReminders(
        [
            EntryReminder.Create(30, ReminderNotificationType.InApp),
            EntryReminder.Create(30, ReminderNotificationType.InApp), // duplicate lead time + channel
            EntryReminder.Create(60, ReminderNotificationType.InApp),
        ]);

        Assert.Equal(2, entry.Reminders.Count);
        Assert.Contains(entry.Reminders, r => r.MinutesBefore == 30);
        Assert.Contains(entry.Reminders, r => r.MinutesBefore == 60);
    }

    [Fact]
    public void SetReminders_WithEmpty_ClearsReminders()
    {
        AgendaEntry entry = CreateSimple();
        entry.SetReminders([EntryReminder.Create(15, ReminderNotificationType.InApp)]);

        entry.SetReminders([]);

        Assert.Empty(entry.Reminders);
    }

    [Fact]
    public void EntryReminder_Create_WithNonPositiveLead_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => EntryReminder.Create(0, ReminderNotificationType.InApp));

    [Fact]
    public void Create_WithReminders_StoresThem()
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "x", null, Start, End, false, AgendaActivityType.Other, null, null, null, Responsible, Start,
            reminders: [EntryReminder.Create(10, ReminderNotificationType.InApp)]);

        Assert.Single(entry.Reminders);
    }

    [Fact]
    public void Create_RoomBooking_KeepsRoomId()
    {
        var roomId = Guid.NewGuid();

        AgendaEntry entry = AgendaEntry.Create(
            Company, "Lab 1 booking", null, Start, End, false,
            AgendaActivityType.RoomBooking, experimentId: null, roomId: roomId,
            recurrenceRule: null, Responsible, Start);

        Assert.Equal(roomId, entry.RoomId);
    }

    [Fact]
    public void Create_NonRoomBooking_DropsRoomId()
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "Standup", null, Start, End, false,
            AgendaActivityType.Other, experimentId: null, roomId: Guid.NewGuid(),
            recurrenceRule: null, Responsible, Start);

        Assert.Null(entry.RoomId);
    }

    [Fact]
    public void Reschedule_AwayFromRoomBooking_ClearsRoomId()
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "Lab 1 booking", null, Start, End, false,
            AgendaActivityType.RoomBooking, experimentId: null, roomId: Guid.NewGuid(),
            recurrenceRule: null, Responsible, Start);

        entry.Reschedule("Now a talk", null, Start, End, false,
            AgendaActivityType.Presentation, experimentId: null, roomId: Guid.NewGuid(), recurrenceRule: null);

        Assert.Null(entry.RoomId);
    }

    [Fact]
    public void Create_WithColor_NormalisesToLowercase()
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "x", null, Start, End, false, AgendaActivityType.Other, null, null, null, Responsible, Start,
            color: "#D50000");

        Assert.Equal("#d50000", entry.Color);
    }

    [Fact]
    public void Create_WithoutColor_LeavesColorNull()
        => Assert.Null(CreateSimple().Color);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankColor_CollapsesToNull(string color)
    {
        AgendaEntry entry = AgendaEntry.Create(
            Company, "x", null, Start, End, false, AgendaActivityType.Other, null, null, null, Responsible, Start,
            color: color);

        Assert.Null(entry.Color);
    }

    [Theory]
    [InlineData("d50000")]     // missing '#'
    [InlineData("#d500")]      // too short
    [InlineData("#d500000")]   // too long
    [InlineData("#gggggg")]    // non-hex digits
    public void Create_WithMalformedColor_Throws(string color)
        => Assert.Throws<ArgumentException>(() => AgendaEntry.Create(
            Company, "x", null, Start, End, false, AgendaActivityType.Other, null, null, null, Responsible, Start,
            color: color));

    [Fact]
    public void Reschedule_UpdatesColor()
    {
        AgendaEntry entry = CreateSimple();

        entry.Reschedule("Standup", null, Start, End, false, AgendaActivityType.Other, null, null, null,
            color: "#33b679");

        Assert.Equal("#33b679", entry.Color);
    }

    [Fact]
    public void Reallocate_ChangesResponsibleAndRaisesUpdatedEvent()
    {
        AgendaEntry entry = CreateSimple();
        entry.ClearDomainEvents();
        var newResponsible = Guid.NewGuid();

        entry.Reallocate(newResponsible);

        Assert.Equal(newResponsible, entry.ResponsibleId);
        Assert.IsType<AgendaEntryUpdated>(Assert.Single(entry.DomainEvents));
    }
}
