using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Notifications.Tests.Domain;

public sealed class DedupeKeyTests
{
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void For_composes_the_canonical_key_including_type_reference_company_and_bucket()
    {
        Guid target = Guid.Parse("22222222-2222-2222-2222-222222222222");
        NotificationReference reference = NotificationReference.To("stock_item", target);

        DedupeKey key = DedupeKey.For(Company, NotificationType.Expiry, reference, "2026-07");

        Assert.Equal(
            $"expiry:stock_item:{target}:{Company}:2026-07",
            key.Value);
    }

    [Fact]
    public void For_produces_the_same_key_for_the_same_alert_in_the_same_cycle()
    {
        NotificationReference reference = NotificationReference.To("equipment", Guid.NewGuid());

        DedupeKey first = DedupeKey.For(Company, NotificationType.Calibration, reference, "window-30");
        DedupeKey second = DedupeKey.For(Company, NotificationType.Calibration, reference, "window-30");

        Assert.Equal(first, second);
    }

    [Fact]
    public void For_produces_a_different_key_in_a_later_cycle_so_the_alert_can_re_fire()
    {
        NotificationReference reference = NotificationReference.To("stock_item", Guid.NewGuid());

        DedupeKey july = DedupeKey.For(Company, NotificationType.Expiry, reference, "2026-07");
        DedupeKey august = DedupeKey.For(Company, NotificationType.Expiry, reference, "2026-08");

        Assert.NotEqual(july, august);
    }

    [Fact]
    public void FromValue_normalizes_to_lowercase_and_trims()
    {
        DedupeKey key = DedupeKey.FromValue("  Expiry:Stock_Item:X  ");

        Assert.Equal("expiry:stock_item:x", key.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromValue_rejects_a_blank_key(string value)
        => Assert.Throws<DomainException>(() => DedupeKey.FromValue(value));

    [Fact]
    public void FromValue_rejects_a_key_over_the_maximum_length()
        => Assert.Throws<DomainException>(() => DedupeKey.FromValue(new string('a', 201)));
}
