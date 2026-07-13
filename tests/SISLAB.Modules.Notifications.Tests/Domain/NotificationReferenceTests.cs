using SISLAB.Modules.Notifications.Domain.Notifications;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Notifications.Tests.Domain;

public sealed class NotificationReferenceTests
{
    [Fact]
    public void To_normalizes_the_target_type_to_a_lowercased_trimmed_slug()
    {
        Guid id = Guid.NewGuid();

        NotificationReference reference = NotificationReference.To("  Stock_Item  ", id);

        Assert.Equal("stock_item", reference.TargetType);
        Assert.Equal(id, reference.TargetId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void To_rejects_a_blank_target_type(string targetType)
        => Assert.Throws<DomainException>(() => NotificationReference.To(targetType, Guid.NewGuid()));

    [Fact]
    public void To_rejects_an_empty_target_id()
        => Assert.Throws<DomainException>(() => NotificationReference.To("equipment", Guid.Empty));

    [Fact]
    public void References_with_the_same_components_are_structurally_equal()
    {
        Guid id = Guid.NewGuid();

        NotificationReference a = NotificationReference.To("equipment", id);
        NotificationReference b = NotificationReference.To("EQUIPMENT", id);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void References_pointing_at_different_targets_are_not_equal()
    {
        NotificationReference a = NotificationReference.To("stock_item", Guid.NewGuid());
        NotificationReference b = NotificationReference.To("stock_item", Guid.NewGuid());

        Assert.NotEqual(a, b);
    }
}
