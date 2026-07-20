using SISLAB.Modules.Agenda.Domain.Entries;

namespace SISLAB.Modules.Agenda.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="RecurrenceRuleSpec"/> value object (card [E10.1] #1): structural validation,
/// normalization, the <c>RRULE:</c> prefix strip, <c>UNTIL</c> rewriting and structural equality.
/// </summary>
public sealed class RecurrenceRuleSpecTests
{
    [Theory]
    [InlineData("FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;BYDAY=MO,WE,FR")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=15")]
    public void Create_WithValidRule_Succeeds(string rrule)
    {
        RecurrenceRuleSpec spec = RecurrenceRuleSpec.Create(rrule);
        Assert.Equal(rrule.ToUpperInvariant(), spec.Value);
    }

    [Fact]
    public void Create_NormalizesToUpperCaseAndTrims()
    {
        RecurrenceRuleSpec spec = RecurrenceRuleSpec.Create("  freq=daily  ");
        Assert.Equal("FREQ=DAILY", spec.Value);
    }

    [Fact]
    public void Create_StripsRrulePrefix()
    {
        RecurrenceRuleSpec spec = RecurrenceRuleSpec.Create("RRULE:FREQ=WEEKLY");
        Assert.Equal("FREQ=WEEKLY", spec.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("BYDAY=MO")]              // no FREQ
    [InlineData("FREQ=FORTNIGHTLY")]      // unsupported frequency
    [InlineData("this is not a rule")]    // malformed shape
    public void Create_WithInvalidRule_Throws(string rrule)
        => Assert.Throws<ArgumentException>(() => RecurrenceRuleSpec.Create(rrule));

    [Fact]
    public void CreateOptional_WithBlank_ReturnsNull()
        => Assert.Null(RecurrenceRuleSpec.CreateOptional("  "));

    [Fact]
    public void CreateOptional_WithValue_ReturnsSpec()
        => Assert.NotNull(RecurrenceRuleSpec.CreateOptional("FREQ=DAILY"));

    [Fact]
    public void WithUntil_AddsUntilPart()
    {
        RecurrenceRuleSpec spec = RecurrenceRuleSpec.Create("FREQ=DAILY");
        RecurrenceRuleSpec truncated = spec.WithUntil(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains("UNTIL=20260930T000000Z", truncated.Value);
        Assert.StartsWith("FREQ=DAILY", truncated.Value);
    }

    [Fact]
    public void WithUntil_ReplacesExistingUntilAndDropsCount()
    {
        RecurrenceRuleSpec spec = RecurrenceRuleSpec.Create("FREQ=DAILY;COUNT=10;UNTIL=20260101T000000Z");
        RecurrenceRuleSpec truncated = spec.WithUntil(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc));

        Assert.DoesNotContain("COUNT=", truncated.Value);
        // Exactly one UNTIL part remains (splitting on "UNTIL=" yields 2 segments for a single occurrence).
        Assert.Equal(2, truncated.Value.Split("UNTIL=").Length);
        Assert.Contains("UNTIL=20260930T000000Z", truncated.Value);
    }

    [Fact]
    public void Equals_IsStructural()
    {
        RecurrenceRuleSpec a = RecurrenceRuleSpec.Create("FREQ=DAILY");
        RecurrenceRuleSpec b = RecurrenceRuleSpec.Create("freq=daily");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }
}
