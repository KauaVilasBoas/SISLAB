using SISLAB.Modules.Experiments.Domain.Scheduling;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Experiments.Tests.Domain.Scheduling;

/// <summary>
/// Covers the <see cref="ResponsibleRoster"/> rotation (SISLAB-10): the configurable "Vic e Dai" alternation and its
/// generalisations. The people and the cadence are inputs, so the tests assert the rule (round-robin by shift), not
/// any lab-specific roster.
/// </summary>
public sealed class ResponsibleRosterTests
{
    private static readonly Guid Vic = Guid.NewGuid();
    private static readonly Guid Dai = Guid.NewGuid();
    private static readonly Guid Ana = Guid.NewGuid();

    [Fact]
    public void Two_responsibles_one_day_per_shift_alternate_day_on_day_off()
    {
        ResponsibleRoster roster = ResponsibleRoster.Of(new[] { Vic, Dai });

        // Vic, Dai, Vic, Dai, … — the spreadsheet's "dia sim, dia não".
        Assert.Equal(Vic, roster.ResponsibleForDay(0));
        Assert.Equal(Dai, roster.ResponsibleForDay(1));
        Assert.Equal(Vic, roster.ResponsibleForDay(2));
        Assert.Equal(Dai, roster.ResponsibleForDay(3));
    }

    [Fact]
    public void A_wider_shift_keeps_one_responsible_for_consecutive_days_before_rotating()
    {
        ResponsibleRoster roster = ResponsibleRoster.Of(new[] { Vic, Dai }, daysPerShift: 2);

        // Vic covers days 0-1, Dai covers days 2-3, then it wraps back to Vic.
        Assert.Equal(Vic, roster.ResponsibleForDay(0));
        Assert.Equal(Vic, roster.ResponsibleForDay(1));
        Assert.Equal(Dai, roster.ResponsibleForDay(2));
        Assert.Equal(Dai, roster.ResponsibleForDay(3));
        Assert.Equal(Vic, roster.ResponsibleForDay(4));
    }

    [Fact]
    public void A_three_person_roster_cycles_round_robin()
    {
        ResponsibleRoster roster = ResponsibleRoster.Of(new[] { Vic, Dai, Ana });

        Assert.Equal(Vic, roster.ResponsibleForDay(0));
        Assert.Equal(Dai, roster.ResponsibleForDay(1));
        Assert.Equal(Ana, roster.ResponsibleForDay(2));
        Assert.Equal(Vic, roster.ResponsibleForDay(3));
    }

    [Fact]
    public void An_empty_roster_is_rejected()
        => Assert.Throws<DomainException>(() => ResponsibleRoster.Of(Array.Empty<Guid>()));

    [Fact]
    public void An_empty_responsible_id_is_rejected()
        => Assert.Throws<DomainException>(() => ResponsibleRoster.Of(new[] { Vic, Guid.Empty }));

    [Fact]
    public void A_non_positive_shift_is_rejected()
        => Assert.Throws<DomainException>(() => ResponsibleRoster.Of(new[] { Vic }, daysPerShift: 0));

    [Fact]
    public void A_negative_day_index_is_rejected()
    {
        ResponsibleRoster roster = ResponsibleRoster.Of(new[] { Vic });
        Assert.Throws<ArgumentOutOfRangeException>(() => roster.ResponsibleForDay(-1));
    }
}
